using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ShowDamageNumber
{
    public static class API
    {
        public static Color defaultColor = new Color(0.75f, 0.45f, 0.30f, 1f);

        public static DamageNumber ShowDamage(float damage, ref SkillTarget target, EDmgType dmgType = EDmgType.Normal, ESizeMode sizeMode = ESizeMode.Auto, EColorMode colorMode = EColorMode.Auto)
        {
            if (!ShowDamageNumberPlugin.ShowDamage.Value)
                return null;
            SkillSystem skillSystem = GameMain.spaceSector.skillSystem;
            SpaceSector sector = GameMain.spaceSector;
            bool show = false;
            VectorLF3 pos = VectorLF3.zero;
            Quaternion rot = Quaternion.identity;
            //GameCamera.instance.main;
            int astroId = target.astroId;
            if (target.type == ETargetType.Player)
            {
                pos = GameMain.mainPlayer.uPosition;
                rot = GameMain.mainPlayer.uRotation;
                show = true;
            }
            if (astroId > 1000000)
            {
                if (target.type == ETargetType.Enemy && ShowDamageNumberPlugin.SpaceEnemyOnHitDamage.Value)
                {
                    EnemyDFHiveSystem enemyDFHiveSystem = GameMain.spaceSector.dfHivesByAstro[astroId - 1000000];
                    if(enemyDFHiveSystem == null || GameMain.localStar == null)
                        return null;
                    if(enemyDFHiveSystem.starData.id != GameMain.localStar.id) // 异星系伤害不显示
                        return null;
                    ref EnemyData enemyData = ref sector.enemyPool[target.id];
                    pos = enemyData.pos;
                    rot = enemyData.rot;
                    show = true;
                }
                else if (target.type == ETargetType.Craft && ShowDamageNumberPlugin.AllyOnHitDamage.Value)
                {
                    ref CraftData craftData = ref sector.craftPool[target.id];
                    pos = craftData.pos;
                    rot = craftData.rot;
                    show = true;
                }
            }
            else if(astroId > 100 && astroId <= 204899 && astroId % 100 > 0)
            {
                
            }
            else if (astroId % 100 == 0 && target.type == ETargetType.Craft && ShowDamageNumberPlugin.AllyOnHitDamage.Value)
            {
                ref CraftData craftData = ref sector.craftPool[target.id];
                pos = craftData.pos;
                rot = craftData.rot;
                show = true;
            }

            if (show)
            {
                pos = skillSystem.sector.GetRelativePose(target.astroId, pos, rot).position;
                return TryAddNewDamageNumber(damage, pos, false, dmgType, sizeMode, colorMode);
            }
            return null;

        }

        public static DamageNumber ShowDamageGround(float damage, ref SkillTargetLocal target, PlanetFactory factory, EDmgType dmgType = EDmgType.Normal, ESizeMode sizeMode = ESizeMode.Auto, EColorMode colorMode = EColorMode.Auto)
        {
            if (!ShowDamageNumberPlugin.ShowDamage.Value)
                return null;
            bool show = false;
            VectorLF3 pos = VectorLF3.zero;
            Quaternion rot = Quaternion.identity;
            if (target.type == ETargetType.Enemy && ShowDamageNumberPlugin.GoundEnemyOnHitDamage.Value)
            {
                ref EnemyData ptr2 = ref factory.enemyPool[target.id];
                pos = ptr2.pos;
                rot = ptr2.rot;
                show = true;
            }
            else if (target.type == ETargetType.Craft && ShowDamageNumberPlugin.AllyOnHitDamage.Value)
            {
                ref CraftData ptr6 = ref factory.craftPool[target.id];
                pos = ptr6.pos;
                rot = ptr6.rot;
                show = true;
            }
            else if (target.type == ETargetType.None && ShowDamageNumberPlugin.AllyOnHitDamage.Value)
            {
                ref EntityData ptr9 = ref factory.entityPool[target.id];
                pos = ptr9.pos;
                rot = ptr9.rot;
                show = true;
            }
            else if (target.type == ETargetType.Vegetable && ShowDamageNumberPlugin.VegetableOnHitDamage.Value)
            {
                ref VegeData ptr14 = ref factory.vegePool[target.id];
                pos = ptr14.pos;
                rot = ptr14.rot;
                show = true;
            }
            else if (target.type == ETargetType.Vein && ShowDamageNumberPlugin.VegetableOnHitDamage.Value)
            {
                ref VeinData ptr16 = ref factory.veinPool[target.id];
                pos = ptr16.pos;
                rot = Quaternion.identity;
                show = true;
            }
            else if(target.type == ETargetType.Player && target.id == 1)
            {
                pos = GameMain.mainPlayer.position;
                rot = Quaternion.identity;
                show = true;
            }

            if (show)
            {
                pos = GameMain.spaceSector.skillSystem.sector.GetRelativePose(factory.planet.astroId, pos, rot).position;
                return TryAddNewDamageNumber(damage, pos, true, dmgType, sizeMode, colorMode, target.id);
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="damage"></param>
        /// <param name="pos"></param>
        /// <param name="isGroundTarget"></param>
        /// <param name="dmgType"></param>
        /// <param name="sizeMode"></param>
        /// <param name="colorMode"></param>
        /// <param name="targetId">只有在Dot伤害时才需要传递</param>
        public static DamageNumber TryAddNewDamageNumber(float damage, Vector3 pos, bool isGroundTarget, EDmgType dmgType, ESizeMode sizeMode, EColorMode colorMode, int targetId = -1)
        {
            if (!ShowDamageNumberPlugin.ShowDamage.Value)
                return null;
            lock (DamageNumberController.main)
            {                
                if (dmgType != EDmgType.Dot)
                {
                    int index = ArrangeNewNormalIndex();
                    if (index < 0)
                        return null;
                    DamageNumber dn = new DamageNumber(index, damage, pos, isGroundTarget, dmgType, sizeMode, colorMode);
                    DamageNumberController.main.activeArray[index] = dn;
                    return dn;
                }
                else if (dmgType == EDmgType.Dot && targetId > 0)
                {
                    if (DamageNumberController.main.dotArray.ContainsKey(targetId) && DamageNumberController.main.dotArray[targetId] != null)
                    {
                        DamageNumberController.main.dotArray[targetId].AddDotDamage(damage);
                    }
                    else if (DamageNumberController.main.dotArray.Count < 150)
                    {
                        int index = ArrangeNewNormalIndex();
                        if (index < 0)
                            return null;
                        DamageNumber dn = new DamageNumber(index, damage, pos, isGroundTarget, dmgType, sizeMode, colorMode);
                        //dn.localEnemyId = targetId;
                        DamageNumberController.main.activeArray[index] = dn;
                        DamageNumberController.main.dotArray[targetId] = dn;
                        return dn;
                    }

                }
            }
            return null;
        }

        public static DamageNumber TryAddPlayerShieldResistNumber(float damage)
        {
            if (!ShowDamageNumberPlugin.ShowDamage.Value)
                return null;
            if(!ShowDamageNumberPlugin.IcarusShieldOnHitDamage.Value)
                return null;
            int index = ArrangeNewNormalIndex();
            if (index < 0)
                return null;
            DamageNumber dn = new DamageNumber(index, damage, Vector3.zero, false, EDmgType.ShieldRisist, ESizeMode.Auto, EColorMode.Auto);
            DamageNumberController.main.activeArray[index] = dn;
            return dn;
        }

        public static bool WorldPointIntoScreen(Vector3 worldPoint, RectTransform rect, out Vector2 rectPoint)
        {
            Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, UIRoot.instance.overlayCanvas.worldCamera, out rectPoint);
            return true;
        }

        public static int ArrangeNewNormalIndex()
        {
            int index = -1;
            if (DamageNumberController.main.recycleCursor > 0)
            {
                index = DamageNumberController.main.recycleArray[DamageNumberController.main.recycleCursor - 1];
                Interlocked.Add(ref DamageNumberController.main.recycleCursor, -1);
            }
            else
            {
                if (DamageNumberController.main.cursor >= DamageNumberController.main.activeArray.Length && DamageNumberController.main.activeArray.Length >= ShowDamageNumberPlugin.MaxNumberCount.Value) // 不允许过多
                    return -1;

                while (DamageNumberController.main.cursor >= DamageNumberController.main.activeArray.Length)
                {
                    DamageNumber[] old = DamageNumberController.main.activeArray;
                    DamageNumberController.main.activeArray = new DamageNumber[old.Length * 2];
                    Array.Copy(old, DamageNumberController.main.activeArray, old.Length);
                }
                index = DamageNumberController.main.cursor;
                Interlocked.Add(ref DamageNumberController.main.cursor, 1);
            }
            return index;
        }
    }
}
