using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ShowDamageNumber
{
    public class DamageNumber
    {
        //public static GameObject normObj;
        public static GameObject glowingObj;
        public static Font defaultFont;
        public static Transform parentTrans;
        public static Color[] presetColors;

        public static int fadeBeginFrame = 20; // 开始变淡的帧数
        public static float fadePerFrame = 0.025f; // 每帧变淡
        public static int destoryFrame = 60; // 数字持续帧数
        public static float xSpeedPerFrame = 0.4f; // x方向位移速度
        public static float yA = -0.03f; // 以下两项为y坐标位移参数
        public static float y0Frame = 40;
        public static float ShdResistNumDropSpeed = 1.2f; // 护盾减伤的下落速度
        public static int onhitFrame = 15; // 刚击中时，数字是双倍大小，在这段时间内逐渐变为正常大小
        public static int basicMidFontSize2160p = 60;
        public static int basicBigFontSize2160p = 80;
        public static int basicSmallFontSize2160p = 44;
        public static int basicSmallFontLocalDmgThreshold = 10; // 伤害倍率科技为100%时，对地伤害低于此值的数字为小数字，高于则为中或大数字（取决于下面的）
        public static int basicBigFontLocalDmgThreshold = 200; // 伤害倍率科技为100%时，对地伤害高于此值的数字为大数字
        public static int basicSmallFontSpaceDmgThreshold = 100; // 同上，但是是太空伤害数字大小判据
        public static int basicBigFontSpaceDmgThreshold = 2000;

        // 以为运行时自动计算的数值
        public static int curMidFontSize;
        public static int curBigFontSize;
        public static int curSmallFontSize;
        public static float dotSizeIncPerDamage;
        public static int curSmallFontLocalDmgThreshold; // 实际伤害数字大小的判据，会考虑所有伤害科技加成中最高的那个，然后以加成比例的90%乘在基础判据上
        public static int curBigFontLocalDmgThreshold;
        public static int curSmallFontSpaceDmgThreshold;
        public static int curBigFontSpaceDmgThreshold;


        public static void Init()
        {
            Transform inGameTrans = GameObject.Find("UI Root/Overlay Canvas/In Game").transform;
            GameObject damageTextsObj = new GameObject("DamageTexts");
            damageTextsObj.transform.parent = inGameTrans;
            damageTextsObj.transform.SetAsFirstSibling();
            damageTextsObj.transform.localScale = Vector3.one;
            damageTextsObj.transform.localPosition = Vector3.zero;
            damageTextsObj.AddComponent<RectTransform>();
            parentTrans = damageTextsObj.transform;
            defaultFont = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Blueprint Browser/inspector-group/Scroll View/Viewport/Content/group-1/label").GetComponent<Text>().font;

            Text oriGlowingText = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Replicator Window/queue-group/label").GetComponent<Text>();

            glowingObj = new GameObject("dmg");
            glowingObj.AddComponent<Text>();
            glowingObj.GetComponent<Text>().text = "0";
            glowingObj.GetComponent<Text>().raycastTarget = false;
            glowingObj.GetComponent<Text>().color = new Color(0.732f, 0.45f, 0f, 1);
            glowingObj.GetComponent<Text>().font = Resources.Load<Font>("ui/fonts/sairasb");
            glowingObj.GetComponent<Text>().material = oriGlowingText.material;
            glowingObj.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            glowingObj.AddComponent<Outline>();
            glowingObj.GetComponent<Outline>().effectColor = Color.black;
            glowingObj.AddComponent<Shadow>();
            glowingObj.GetComponent<Shadow>().effectColor = Color.black;

            float actualUILayoutHeight = DSPGame.globalOption.uiLayoutHeight;
            if (DSPGame.globalOption.uiLayoutHeight <= 0)
                actualUILayoutHeight = UICanvasScalerHandler.GetSuggestUILayoutHeight(DSPGame.globalOption.resolution.height);
            curMidFontSize = (int)Math.Round(actualUILayoutHeight / 2160 * basicMidFontSize2160p);
            curBigFontSize = (int)Math.Round(actualUILayoutHeight / 2160 * basicBigFontSize2160p);
            curSmallFontSize = (int)Math.Round(actualUILayoutHeight / 2160 * basicSmallFontSize2160p);
            glowingObj.GetComponent<Text>().fontSize = curMidFontSize;

            glowingObj.transform.localScale = Vector3.one;
            glowingObj.transform.SetParent(DamageNumber.parentTrans, false);
            glowingObj.GetComponent<RectTransform>().anchorMax = Vector3.zero;
            glowingObj.GetComponent<RectTransform>().anchorMin = Vector3.zero;
            glowingObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            glowingObj.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            glowingObj.SetActive(false);

            presetColors = new Color[10];
            presetColors[(int)EColorMode.Auto] = new Color(0.732f, 0.45f, 0f, 1);
            presetColors[(int)EColorMode.CustomFixed] = new Color(0.732f, 0.45f, 0f, 1);
            presetColors[(int)EColorMode.PresetWhite] = new Color(0.65f, 0.65f, 0.65f, 1);
            presetColors[(int)EColorMode.PresetRed] = new Color(0.75f, 0.15f, 0f, 1f);
            presetColors[(int)EColorMode.PresetGold] = new Color(0.732f, 0.45f, 0f, 1);
            presetColors[(int)EColorMode.PresetPurple] = new Color(0.502f, 0f, 0.872f, 1);
            presetColors[(int)EColorMode.PresetGray] = new Color(0.45f, 0.45f, 0.45f, 1);
            presetColors[(int)EColorMode.PresetGreen] = new Color(0.2f, 0.8f, 0.2f, 0.917f);
            presetColors[(int)EColorMode.PresetBlue] = new Color(0.2f, 0.55f, 0.72f, 1f);
            //presetColors[(int)EColorMode.PresetBlue] = new Color(0.2f, 0.55f, 0.72f, 1f);
        }

        public static void UpdateGameHistoryData()
        {
            if(GameMain.history != null)
            {
                float maxScale = Math.Max(GameMain.history.energyDamageScale, GameMain.history.blastDamageScale);
                maxScale = Math.Max(maxScale, GameMain.history.kineticDamageScale);
                maxScale = 1 + (maxScale - 1) * 0.9f;
                curSmallFontLocalDmgThreshold = (int)Math.Round(basicSmallFontLocalDmgThreshold * maxScale);
                curBigFontLocalDmgThreshold = (int)Math.Round(basicBigFontLocalDmgThreshold * maxScale);
                curSmallFontSpaceDmgThreshold = (int)Math.Round(basicSmallFontSpaceDmgThreshold * maxScale);
                curBigFontSpaceDmgThreshold = (int)Math.Round(basicBigFontSpaceDmgThreshold * maxScale);
                dotSizeIncPerDamage = (curBigFontSize - curSmallFontSize) * 1.0f / (curBigFontLocalDmgThreshold);
            }
        }

        private int index;
        public float damage;
        public EDmgType type;
        public ESizeMode sizeMode;
        public EColorMode colorMode;
        public string prefix;
        public string suffix;

        public bool isGroundTarget;
        public int localEnemyId;

        public int time;
        public GameObject obj;
        public Text text;
        public float x;
        public float y;
        public int autoSize;
        public int xDirection;

        public DamageNumber(int index, float damage, Vector3 pos, bool isGroundTarget, EDmgType dmgType = EDmgType.Normal, ESizeMode sizeMode = ESizeMode.Auto, EColorMode colorMode = EColorMode.Auto)
        {
            this.index = index;
            this.damage = damage;
            this.type = dmgType;
            this.sizeMode = sizeMode;
            this.colorMode = colorMode;
            Vector2 uiPos;
            obj = GameObject.Instantiate(glowingObj);
            obj.transform.SetParent(parentTrans, false);
            obj.transform.localScale = type == EDmgType.Dot || type == EDmgType.DotEnding ? new Vector3(0.5f, 0.5f, 0.5f) : Vector3.one;
            API.WorldPointIntoScreen(pos, parentTrans as RectTransform, out uiPos);
            if (dmgType == EDmgType.ShieldRisist)
            {
                x = 0;
                y = 0;
            }
            else
            {
                x = uiPos.x + (float)(Utils.randSeed.Value.NextDouble() - 0.5) * curBigFontSize / 2 + curBigFontSize;
                y = uiPos.y + (float)(Utils.randSeed.Value.NextDouble() - 0.5) * curBigFontSize / 2 + 1.5f * curBigFontSize;
            }
            obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            xDirection = Utils.randSeed.Value.NextDouble() > 0.5 ? 1 : -1;
            text = obj.GetComponent<Text>();
            if (type == EDmgType.ShieldRisist)
            {
                prefix = "- ";
            }
            else if (type == EDmgType.Dot)
            {
                suffix = "!";
            }
            text.text = prefix + ((int)damage).ToString() + suffix;
            obj.SetActive(true);

            this.isGroundTarget = isGroundTarget;

            AutoSize();
            AutoColor();
            //Update();
        }

        public void SetTargetData(ref SkillTargetLocal target)
        {
            localEnemyId = target.id;
        }

        public void AddDotDamage(float damage)
        {
            this.damage += damage;
            text.text = ((int)this.damage).ToString();
            time = 0;
            AutoSize();
            AutoColor();
        }

        public void Update()
        {
            if(obj == null)
            {
                OnNullObj();
            }

            if (time > destoryFrame)
            {
                Remove();
                return;
            }

            // dot伤害跟随目标位置，dot伤害最终阶段不移动。暴击伤害不跳动，也不跟随。其他伤害文本会以初始击中位置为标准进行一次跳动。
            if(type == EDmgType.ShieldRisist)
            {
                obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y - time * ShdResistNumDropSpeed);
                if(time > onhitFrame) // 护盾减伤提前开始变淡
                    text.color = new Color(text.color.r, text.color.g, text.color.b, text.color.a - fadePerFrame);
                AutoScale();
                time++;
                return;
            }
            else if (type != EDmgType.Dot && type != EDmgType.DotEnding && type != EDmgType.Crit)
            {
                obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(x + time * xSpeedPerFrame * xDirection, y + yA * time * (time - y0Frame));
            }
            else if (type == EDmgType.Dot)
            {
                if (GameMain.localPlanet != null && DamageNumberController.main.lastPlanetId == GameMain.localPlanet.id)
                {
                    PlanetFactory factory = GameMain.galaxy.PlanetById(DamageNumberController.main.lastPlanetId)?.factory;
                    if (factory != null && localEnemyId >0 && localEnemyId < factory.enemyCursor)
                    {
                        ref EnemyData ptr2 = ref factory.enemyPool[localEnemyId];
                        if (ptr2.id > 0)
                        {
                            VectorLF3 pos = ptr2.pos;
                            Quaternion rot = ptr2.rot;
                            pos = GameMain.spaceSector.skillSystem.sector.GetRelativePose(factory.planet.astroId, pos, rot).position;
                            Vector2 uiPos;
                            API.WorldPointIntoScreen(pos, parentTrans as RectTransform, out uiPos);
                            obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(uiPos.x + curBigFontSize, uiPos.y + curBigFontSize); // 初始获得的位置偏左下，不知道为啥
                        }
                        else // 说明单位寄了
                        {
                            type = EDmgType.DotEnding;
                            time = (int)(fadeBeginFrame * 0.6f);
                            if(DamageNumberController.main.dotArray.ContainsKey(this.localEnemyId))
                                DamageNumberController.main.dotArray.Remove(this.localEnemyId); // 从Dot列表中移除                            
                        }
                    }
                }
                else
                {
                    Remove();
                    return;
                }
            }

            if(time > fadeBeginFrame)
            {
                text.color = new Color(text.color.r, text.color.g, text.color.b, text.color.a - fadePerFrame);
            }
            if(type == EDmgType.Dot)
            {
                AutoSize();
                AutoColor();
            }
            AutoScale();
            time++;
        }

        public void Remove()
        {
            if(type == EDmgType.Dot && DamageNumberController.main.dotArray.ContainsKey(this.localEnemyId))
                DamageNumberController.main.dotArray.Remove(this.localEnemyId);
            if(obj != null)
                GameObject.Destroy(obj);
            if (index >= 0 && index < DamageNumberController.main.cursor)
            {
                DamageNumberController.main.activeArray[index] = null;
                while (DamageNumberController.main.recycleCursor >= DamageNumberController.main.recycleArray.Length)
                {
                    int[] oldArray = DamageNumberController.main.recycleArray;
                    DamageNumberController.main.recycleArray = new int[oldArray.Length * 2];
                    Array.Copy(oldArray, DamageNumberController.main.recycleArray, oldArray.Length);
                }
                DamageNumberController.main.recycleArray[DamageNumberController.main.recycleCursor] = index;
                DamageNumberController.main.recycleCursor++;
            }
            
        }

        public void OnNullObj()
        {
            Remove();
        }

        public void AutoSize()
        {
            if(type== EDmgType.Dot)
            {
                int size = (int)(curSmallFontSize + damage * dotSizeIncPerDamage);
                text.fontSize = size > 1.5f * curBigFontSize ? (int)(1.5f * curBigFontSize) : size;
            }
            else if(sizeMode == ESizeMode.Auto)
            {
                if(isGroundTarget)
                {
                    if (damage < curSmallFontLocalDmgThreshold)
                        text.fontSize = curSmallFontSize;
                    else if (damage < curBigFontLocalDmgThreshold)
                        text.fontSize = curMidFontSize;
                    else 
                        text.fontSize = curBigFontSize;
                }
                else
                {
                    if (damage < curSmallFontSpaceDmgThreshold)
                        text.fontSize = curSmallFontSize;
                    else if (damage < curBigFontSpaceDmgThreshold)
                        text.fontSize = curMidFontSize;
                    else
                        text.fontSize = curBigFontSize;
                }
            }
        }

        public void AutoScale()
        {
            if (type != EDmgType.Dot && time <= onhitFrame)
            {
                float scale = 1 - (time * 0.5f / onhitFrame);
                obj.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        public void AutoColor()
        {
            if (type == EDmgType.Dot)
            {
                if (damage < curSmallFontLocalDmgThreshold)
                {
                    float ratio = damage / curSmallFontLocalDmgThreshold;
                    Color c0 = presetColors[(int)EColorMode.PresetWhite];
                    Color c1 = presetColors[(int)EColorMode.Auto];
                    text.color = new Color(c0.r *(1- ratio) + c1.r * ratio, c0.g * (1 - ratio) + c1.g * ratio, c0.b * (1 - ratio) + c1.b * ratio, c0.a * (1 - ratio) + c1.a * ratio);
                }
                else if (damage < curBigFontLocalDmgThreshold)
                {

                    float ratio = (damage - curSmallFontLocalDmgThreshold) / (curBigFontLocalDmgThreshold - curSmallFontLocalDmgThreshold);
                    Color c0 = presetColors[(int)EColorMode.Auto];
                    Color c1 = presetColors[(int)EColorMode.PresetRed];
                    text.color = new Color(c0.r * (1 - ratio) + c1.r * ratio, c0.g * (1 - ratio) + c1.g * ratio, c0.b * (1 - ratio) + c1.b * ratio, c0.a * (1 - ratio) + c1.a * ratio);
                }
                else
                {
                    text.color = presetColors[(int)EColorMode.PresetRed];
                }
            }
            else if (type == EDmgType.ShieldRisist)
            {
                text.color = presetColors[(int)EColorMode.PresetBlue];
            }
            else if (colorMode == EColorMode.Auto)
            {
                if (isGroundTarget)
                {
                    if (damage < curSmallFontLocalDmgThreshold)
                        text.color = presetColors[(int)EColorMode.PresetWhite];
                    else if (damage < curBigFontLocalDmgThreshold)
                        text.color = presetColors[(int)EColorMode.Auto];
                    else
                        text.color = presetColors[(int)EColorMode.PresetRed];
                }
                else
                {
                    if (damage < curSmallFontSpaceDmgThreshold)
                        text.color = presetColors[(int)EColorMode.PresetWhite];
                    else if (damage < curBigFontSpaceDmgThreshold)
                        text.color = presetColors[(int)EColorMode.Auto];
                    else
                        text.color = presetColors[(int)EColorMode.PresetRed];
                }
            }
        }
    }

    public enum EDmgType
    {
        Normal = 0,
        Dot = 1,
        DotEnding = 2,
        Crit = 3,
        Heal = 4,
        ShieldRisist = 5,
        Custom = 99,
    }

    public enum ESizeMode
    {
        Auto = 0,
        CustomFixed = 1,
        Small = 2,
        Middle = 3,
        Big = 4,
    }

    public enum EColorMode
    {
        Auto = 0,
        CustomFixed = 1,
        PresetWhite = 2,
        PresetRed = 3,
        PresetGold = 4,
        PresetPurple = 5,
        PresetGray = 6,
        PresetGreen = 7,
        PresetBlue = 8,
    }
}
