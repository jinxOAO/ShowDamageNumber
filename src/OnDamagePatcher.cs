using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ShowDamageNumber
{
    public class OnDamagePatcher
    {
        public const int dotInterval = 20;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillSystem), "DamageObject")]
        public static void OnDamageObject(ref SkillSystem __instance, int damage, int slice, ref SkillTarget target)
        {
            if (!ShowDamageNumberPlugin.ShowDamage.Value)
                return;
            if (GameMain.localPlanet != null && target.type != ETargetType.Player) // 在星球上时不显示太空受击伤害
                return;
            int astroId = target.astroId;
            if(astroId > 1000000)
            {

            }
            else if (astroId > 100 && astroId <= 204899 && astroId % 100 > 0)
            {
                return; // DamageGroundObject处理
            }
            float dmgf = (float)Math.Round(damage / 100f);
            API.ShowDamage(dmgf, ref target);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillSystem), "DamageGroundObjectByLocalCaster")]
        public static void OnDamageGroundObjectByLocalCaster(ref SkillSystem __instance, PlanetFactory factory, int damage, int slice, ref SkillTargetLocal target)
        {
            if (!ShowDamageNumberPlugin.ShowDamage.Value)
                return;
            if (target.id <= 0)
                return;
            if (GameMain.localPlanet == null)
                return;
            else if (GameMain.localPlanet.id != factory.planetId) // 与玩家不在同一个星球上，不显示
                return;

            float dmgf = (float)Math.Round(damage / 100f);
            if (slice == 10 && target.type == ETargetType.Enemy) // 激光塔攻击
            {
                DamageNumber dn = API.ShowDamageGround(dmgf, ref target, factory, EDmgType.Dot);
                if(dn != null)
                    dn.SetTargetData(ref target);
            }
            else
            {
                API.ShowDamageGround(dmgf, ref target, factory);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillSystem), "DamageGroundObjectByRemoteCaster")]
        public static void OnDamageGroundObjectByRemoteCaster(ref SkillSystem __instance, PlanetFactory factory, int damage, int slice, ref SkillTargetLocal target)
        {
            if (!ShowDamageNumberPlugin.ShowDamage.Value)
                return;
            if (target.id <= 0)
                return;
            if (GameMain.localPlanet == null)
                return;
            else if (GameMain.localPlanet.id != factory.planetId) // 与玩家不在同一个星球上，不显示
                return;

            float dmgf = (float)Math.Round(damage / 100f);
            API.ShowDamageGround(dmgf, ref target, factory);
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(SkillSystem), "MechaEnergyShieldResist", new Type[] { typeof(SkillTarget), typeof(int) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref })]
        public static bool OnSpaceDamageMechaShield(int damage)
        {
            int dmgf = (int)Math.Round(damage / 100f);
            Interlocked.Add(ref DamageNumberController.lastNFrameShdDmgTotal, dmgf);
            return true;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(SkillSystem), "MechaEnergyShieldResist", new Type[] { typeof(SkillTargetLocal), typeof(int), typeof(int) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref })]
        public static bool OnLocalDamageMechaShield(int damage)
        {
            int dmgf = (int)Math.Round(damage / 100f);
            Interlocked.Add(ref DamageNumberController.lastNFrameShdDmgTotal, dmgf);
            return true;
        }
    }
}
