using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShowDamageNumber
{
    /// <summary>
    /// 将所有伤害数字合并进极少量的批次网格来渲染，替代“每个数字一个 Text+Outline+Shadow GameObject”。
    /// 关键：必须用 Graphic 子类，UGUI 才会把该渲染器正规接入 Overlay Canvas（正交、按 canvas 坐标绘制）；
    /// 纯手工 CanvasRenderer 不会被 Canvas 接管，会被场景透视相机当成世界渲染器绘制，导致文字被拉伸汇聚成“随镜头移动的中心”。
    /// 每帧只有池里这几个 Graphic 需要重建网格，绘制调用极少，可同屏渲染上千个数字而不掉帧。
    /// 字形度量用与游戏本体一致的方式(Font.RequestCharactersInTexture + GetCharacterInfo)手动铺设，材质复用游戏本体 UI 文字的发光材质。
    /// </summary>
    public class BatchedDamageRenderer : MonoBehaviour
    {
        // 单个字形 quad：顶点位置(相对数字左中锚点) 与 uv
        private struct Glyph
        {
            public Vector3 p0, p1, p2, p3;
            public Vector2 uv0, uv1, uv2, uv3;
        }

        private class IconTemplate
        {
            public Vector2 uv0, uv1, uv2, uv3;
        }

        // 单个 Graphic 的顶点上限，留出余量以避免超出 16 位索引(65535)
        private const int MaxVertsPerGraphic = 60000;
        // 字形模板缓存上限：Dot 伤害字号连续会不断产生新键，需封顶避免缓存无限增长(内存/重建开销)
        private const int MaxGlyphCache = 8192;

        private readonly Dictionary<KeyValuePair<int, string>, List<Glyph>> _glyphCache
            = new Dictionary<KeyValuePair<int, string>, List<Glyph>>();

        private Material _glowMat;
        private Font _font;
        private Texture _fontTex;
        private Sprite _iconSprite;
        private Texture _iconTex;
        private IconTemplate _iconTpl;
        private Texture _lastFontTex;
        private bool _textureChanged;

        // 本帧可见剔除范围（canvas 单位，相对屏幕中心）
        private float _cullX;
        private float _cullY;

        // 每个数字的文字副本数：6=阴影+4描边+本体(默认)；3=阴影+1描边+本体(高负载降级)。本帧统一，保证同批一致。
        private int _copies = 6;

        private readonly List<BatchedDamageGraphic> _textPool = new List<BatchedDamageGraphic>();
        private readonly List<BatchedDamageGraphic> _iconPool = new List<BatchedDamageGraphic>();
        private RectTransform _root;

        // Outline / Shadow 偏移（与旧组件默认 effectDistance 一致）
        private static readonly Vector2 OutlineDist = new Vector2(1f, -1f);
        private static readonly Vector2 ShadowDist = new Vector2(1f, -1f);
        private static readonly Vector2 IconOutlineDist = new Vector2(2f, -2f);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        public static BatchedDamageRenderer instance;

        public static BatchedDamageRenderer Create(RectTransform parent, Material glowMat, Font font, Sprite iconSprite)
        {
            var go = new GameObject("DamageBatch");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var r = go.AddComponent<BatchedDamageRenderer>();
            r._root = rt;
            r._glowMat = glowMat;
            r._font = font;
            r._iconSprite = iconSprite;
            // 监听动态字体图集重建（与游戏 MarkerUIRenderer 同一机制）：重建后所有缓存字形 UV 失效，需清缓存重铺
            Font.textureRebuilt += r.HandleFontRebuild;
            instance = r;
            return r;
        }

        private void HandleFontRebuild(Font f)
        {
            if (f == _font)
            {
                _glyphCache.Clear();
                _textureChanged = true;
            }
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= HandleFontRebuild;
        }

        private BatchedDamageGraphic GetGraphic(List<BatchedDamageGraphic> pool, int index, Texture tex)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject(pool == _textPool ? "TextBatch" + pool.Count : "IconBatch" + pool.Count);
                go.transform.SetParent(_root, false);
                var g = go.AddComponent<BatchedDamageGraphic>();
                g.rectTransform.anchorMin = Vector2.zero;
                g.rectTransform.anchorMax = Vector2.one;
                g.rectTransform.pivot = Vector2.zero;
                g.rectTransform.offsetMin = Vector2.zero;
                g.rectTransform.offsetMax = Vector2.zero;
                g.color = Color.white;
                g.raycastTarget = false;
                g.material = _glowMat;
                pool.Add(g);
            }
            var graphic = pool[index];
            graphic.batchTexture = tex;
            graphic.enabled = true;
            return graphic;
        }

        public void Rebuild(IReadOnlyList<DamageNumber> list, int count)
        {
            // 可见范围（canvas 单位，anchorPos 相对屏幕中心）：屏幕外的数字不生成网格——它们本来就看不见，
            // 但旧逻辑仍把它们(含 6 份描边/阴影副本)全铺进网格，白白消耗 CPU 与顶点。这是进一步优化的最大头。
            ComputeViewBounds();

            // 预热：先把本帧所有【可见】且需要的字符串一次性请求进图集，触发其中可能的图集扩容/重建。
            // 这样后续读取 GetCharacterInfo 时图集已稳定，避免“边读边扩容”导致同帧内早期字形 UV 失效(碎裂拼接)。
            int visibleCount = 0;
            for (int i = 0; i < count; i++)
            {
                var d = list[i];
                if (d == null || !d.active || IsOffScreen(d))
                    continue;
                if (string.IsNullOrEmpty(d.displayText) || d.fontSize <= 0)
                    continue;
                visibleCount++;
                _font.RequestCharactersInTexture(d.displayText, d.fontSize, FontStyle.Normal);
            }

            UpdateFontTexture();
            if (_textureChanged)
            {
                _glyphCache.Clear(); // 事件可能在预热期间又触发了一次，确保用最新图集重铺
                _textureChanged = false;
            }

            // 高负载自动降级：可见数字超阈值时把描边 4 份→ 1 份（副本 6→3），顶点与构建 CPU 直接减半；普通战斗不变
            int threshold = ShowDamageNumberPlugin.DegradeOutlineThreshold.Value;
            _copies = (threshold > 0 && visibleCount > threshold) ? 3 : 6;

            int textGI = 0, iconGI = 0;
            var curText = GetGraphic(_textPool, 0, _fontTex);
            curText.Begin();
            BatchedDamageGraphic curIcon = null;

            for (int i = 0; i < count; i++)
            {
                var d = list[i];
                if (d == null || !d.active || IsOffScreen(d))
                    continue;

                var glyphs = GetGlyphs(d.displayText, d.fontSize);

                // 该数字需要的文字顶点数（副本数 × 每字 4 顶点），整份不跨 Graphic 以保证同一数字的绘制顺序
                int needVerts = (glyphs != null ? glyphs.Count : 0) * 4 * _copies;
                if (needVerts > 0 && curText.Count + needVerts > MaxVertsPerGraphic)
                {
                    curText.End();
                    textGI++;
                    curText = GetGraphic(_textPool, textGI, _fontTex);
                    curText.Begin();
                }

                if (glyphs != null)
                    EmitNumber(curText, d, glyphs);

                if (d.showIcon && GetIconTemplate() != null)
                {
                    int iconNeed = 4 * 5;
                    if (curIcon == null)
                    {
                        curIcon = GetGraphic(_iconPool, iconGI, _iconTex);
                        curIcon.Begin();
                    }
                    else if (curIcon.Count + iconNeed > MaxVertsPerGraphic)
                    {
                        curIcon.End();
                        iconGI++;
                        curIcon = GetGraphic(_iconPool, iconGI, _iconTex);
                        curIcon.Begin();
                    }
                    EmitIcon(curIcon, d);
                }
            }

            curText.End();
            if (curIcon != null) curIcon.End();

            // 关闭未用到的池项
            for (int k = textGI + 1; k < _textPool.Count; k++) _textPool[k].enabled = false;
            int iconUsed = curIcon != null ? iconGI + 1 : 0;
            for (int k = iconUsed; k < _iconPool.Count; k++) _iconPool[k].enabled = false;
        }

        private void ComputeViewBounds()
        {
            // canvas 单位：屏幕高度 == uiLayoutHeight(参考分辨率高度，matchHeight)，宽度按屏幕宽高比换算
            float lh = UICanvasScalerHandler.uiLayoutHeight;
            if (lh <= 0)
                lh = UICanvasScalerHandler.GetSuggestUILayoutHeight(Screen.height);
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
            float margin = DamageNumber.curBigFontSize * 4f; // 预留数字上飘/右移与字号余量，确保不会误剔可见数字
            _cullX = lh * 0.5f * aspect + margin;
            _cullY = lh * 0.5f + margin;
        }

        private bool IsOffScreen(DamageNumber d)
        {
            if (d.behindCam) return true; // 目标在相机背后，投影坐标会镜像到错误位置，直接不渲染
            var p = d.anchorPos;
            return p.x < -_cullX || p.x > _cullX || p.y < -_cullY || p.y > _cullY;
        }

        private void UpdateFontTexture()
        {
            if (_font != null && _font.material != null)
                _fontTex = _font.material.mainTexture;
            if (_iconTex == null && _iconSprite != null)
                _iconTex = _iconSprite.texture;

            // 动态字体图集扩容/重建会使缓存字形 UV 失效：纹理实例变化则清缓存，下一帧重铺
            if (_fontTex != null && _lastFontTex != null && _fontTex != _lastFontTex)
                _glyphCache.Clear();
            if (_fontTex != null)
                _lastFontTex = _fontTex;
        }

        private void EmitNumber(BatchedDamageGraphic g, DamageNumber d, List<Glyph> glyphs)
        {
            float s = d.scale;
            Color32 col = d.color;
            Color32 black = new Color32(0, 0, 0, col.a); // 描边/阴影与本体同步淡出，避免淡出时残留一个不透明的黑数字
            Vector2 pivot = d.anchorPos;

            // 顺序与旧实现一致：先 Shadow，再 Outline，最后 Base（Base 覆盖在上层）
            EmitGlyphSet(g, glyphs, pivot, ShadowDist * s, black, s);
            EmitGlyphSet(g, glyphs, pivot, new Vector2(OutlineDist.x, OutlineDist.y) * s, black, s);
            if (_copies >= 6)
            {
                // 完整 4 向描边
                EmitGlyphSet(g, glyphs, pivot, new Vector2(-OutlineDist.x, -OutlineDist.y) * s, black, s);
                EmitGlyphSet(g, glyphs, pivot, new Vector2(OutlineDist.x, -OutlineDist.y) * s, black, s);
                EmitGlyphSet(g, glyphs, pivot, new Vector2(-OutlineDist.x, OutlineDist.y) * s, black, s);
            }
            EmitGlyphSet(g, glyphs, pivot, Vector2.zero, col, s);
        }

        private void EmitGlyphSet(BatchedDamageGraphic g, List<Glyph> glyphs, Vector2 pivot, Vector2 off, Color32 col, float scale)
        {
            Vector3 basePos = pivot + off;
            for (int k = 0; k < glyphs.Count; k++)
            {
                var gl = glyphs[k];
                g.Quad(
                    basePos + Scaled(gl.p0, scale), col, gl.uv0,
                    basePos + Scaled(gl.p1, scale), col, gl.uv1,
                    basePos + Scaled(gl.p2, scale), col, gl.uv2,
                    basePos + Scaled(gl.p3, scale), col, gl.uv3);
            }
        }

        private static Vector3 Scaled(Vector3 p, float s) => new Vector3(p.x * s, p.y * s, 0f);

        private void EmitIcon(BatchedDamageGraphic g, DamageNumber d)
        {
            var tpl = _iconTpl;
            float s = d.scale;
            Color32 col = d.iconColor;
            Color32 black = new Color32(0, 0, 0, col.a); // 图标描边/阴影与本体同步淡出
            Vector2 pivot = d.anchorPos;
            Vector2 center = pivot + d.iconCenter * s;
            float hs = d.iconSize * 0.5f * s;
            Vector2 half = new Vector2(hs, hs);

            EmitIconQuad(g, center, half, tpl, IconOutlineDist * s, black);
            EmitIconQuad(g, center, half, tpl, -IconOutlineDist * s, black);
            EmitIconQuad(g, center, half, tpl, new Vector2(IconOutlineDist.x, -IconOutlineDist.y) * s, black);
            EmitIconQuad(g, center, half, tpl, new Vector2(-IconOutlineDist.x, IconOutlineDist.y) * s, black);
            EmitIconQuad(g, center, half, tpl, Vector2.zero, col);
        }

        private void EmitIconQuad(BatchedDamageGraphic g, Vector2 center, Vector2 half, IconTemplate tpl, Vector2 off, Color32 col)
        {
            Vector2 c = center + off;
            g.Quad(
                c + new Vector2(-half.x, -half.y), col, tpl.uv0,
                c + new Vector2(-half.x, half.y), col, tpl.uv1,
                c + new Vector2(half.x, half.y), col, tpl.uv2,
                c + new Vector2(half.x, -half.y), col, tpl.uv3);
        }

        private IconTemplate GetIconTemplate()
        {
            if (_iconTpl != null) return _iconTpl;
            if (_iconSprite == null) return null;
            var rc = _iconSprite.rect;
            var tex = _iconSprite.texture;
            float w = tex.width, h = tex.height;
            _iconTpl = new IconTemplate
            {
                uv0 = new Vector2(rc.xMin / w, rc.yMin / h),
                uv1 = new Vector2(rc.xMin / w, (rc.yMin + rc.height) / h),
                uv2 = new Vector2((rc.xMin + rc.width) / w, (rc.yMin + rc.height) / h),
                uv3 = new Vector2((rc.xMin + rc.width) / w, rc.yMin / h),
            };
            return _iconTpl;
        }

        // 手动用 CharacterInfo 铺设字形 quad（位置相对左中锚点，MiddleLeft 对齐）
        private List<Glyph> GetGlyphs(string text, int fontSize)
        {
            if (string.IsNullOrEmpty(text) || fontSize <= 0)
                return null;
            var key = new KeyValuePair<int, string>(fontSize, text);
            if (_glyphCache.TryGetValue(key, out var cached))
                return cached;

            _font.RequestCharactersInTexture(text, fontSize, FontStyle.Normal);

            float ascent = _font.ascent;
            float lineHeight = _font.lineHeight;
            int fsize = _font.fontSize > 0 ? _font.fontSize : fontSize;
            float baseline = (int)(((lineHeight - ascent) / 2f - ascent * 0.5f) / fsize * fontSize + 0.4995f);

            var list = new List<Glyph>(text.Length);
            float pen = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r') continue;
                if (c == '\n') c = ' ';
                CharacterInfo ci;
                if (!_font.GetCharacterInfo(c, out ci, fontSize, FontStyle.Normal))
                {
                    pen += fontSize * 0.5f;
                    continue;
                }
                float x0 = pen + ci.minX;
                float x1 = pen + ci.maxX;
                float y0 = baseline + ci.minY;
                float y1 = baseline + ci.maxY;

                list.Add(new Glyph
                {
                    p0 = new Vector3(x0, y0, 0f), // 左下
                    p1 = new Vector3(x1, y0, 0f), // 右下
                    p2 = new Vector3(x1, y1, 0f), // 右上
                    p3 = new Vector3(x0, y1, 0f), // 左上
                    uv0 = ci.uvBottomLeft,
                    uv1 = ci.uvBottomRight,
                    uv2 = ci.uvTopRight,
                    uv3 = ci.uvTopLeft,
                });
                pen += ci.advance;
            }

            _glyphCache[key] = list;
            // 封顶：Dot 连续字号会持续产生新键，超过上限则整体清空(下帧会重建，数字总量本就有限)
            if (_glyphCache.Count > MaxGlyphCache)
                _glyphCache.Clear();
            return list;
        }

        /// <summary>
        /// 承载一批字形/图标 quad 顶点的 Graphic。作为 Graphic，UGUI 会把它正规接入 Canvas 并用正交投影绘制。
        /// 顶点以“每 4 个一组”存储，OnPopulateMesh 里用 AddUIVertexQuad 生成正确的三角形。
        /// </summary>
        public class BatchedDamageGraphic : Graphic
        {
            public Texture batchTexture;
            private readonly List<UIVertex> _verts = new List<UIVertex>(1024);
            private readonly UIVertex[] _quad = new UIVertex[4];

            public int Count => _verts.Count;

            public override Texture mainTexture => batchTexture != null ? batchTexture : base.mainTexture;

            private Texture _appliedTex;

            public void Begin() { _verts.Clear(); }

            public void Quad(Vector3 p0, Color32 c0, Vector2 t0,
                             Vector3 p1, Color32 c1, Vector2 t1,
                             Vector3 p2, Color32 c2, Vector2 t2,
                             Vector3 p3, Color32 c3, Vector2 t3)
            {
                _verts.Add(Make(p0, c0, t0));
                _verts.Add(Make(p1, c1, t1));
                _verts.Add(Make(p2, c2, t2));
                _verts.Add(Make(p3, c3, t3));
            }

            private static UIVertex Make(Vector3 p, Color32 c, Vector2 uv)
            {
                return new UIVertex { position = p, color = c, uv0 = uv };
            }

            public void End()
            {
                SetVerticesDirty();
                // 仅当绑定的纹理实例发生变化才重建材质批次（材质/图集引用变化时才需要），避免每帧 SetMaterialDirty 的额外开销
                if (batchTexture != _appliedTex)
                {
                    _appliedTex = batchTexture;
                    SetMaterialDirty();
                }
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                for (int i = 0; i + 3 < _verts.Count; i += 4)
                {
                    _quad[0] = _verts[i];
                    _quad[1] = _verts[i + 1];
                    _quad[2] = _verts[i + 2];
                    _quad[3] = _verts[i + 3];
                    vh.AddUIVertexQuad(_quad);
                }
            }
        }
    }
}
