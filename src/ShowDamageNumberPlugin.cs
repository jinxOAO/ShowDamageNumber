using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace ShowDamageNumber
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class ShowDamageNumberPlugin : BaseUnityPlugin
    {

        public const string NAME = "ShowDamageNumber";
        public const string GUID = "com.GniMaerd.ShowDamageNumber";
        public const string VERSION = "1.0.2";

        public static ConfigEntry<bool> ShowDamage;
        public static ConfigEntry<bool> LocalHideFar;
        public static ConfigEntry<int> MaxNumberCount;
        public static ConfigEntry<bool> GoundEnemyOnHitDamage;
        public static ConfigEntry<bool> SpaceEnemyOnHitDamage;
        public static ConfigEntry<bool> IcarusShieldOnHitDamage;
        public static ConfigEntry<bool> AllyOnHitDamage;
        public static ConfigEntry<bool> VegetableOnHitDamage;

        public static ConfigEntry<int> SmallTextSize;
        public static ConfigEntry<int> MiddleTextSize;
        public static ConfigEntry<int> BigTextSize;

        public void Awake()
        {
            ShowDamage = Config.Bind<bool>("config", "ShowDamage", true, "Whether show the damage number. 是否显示伤害数字。");
            LocalHideFar = Config.Bind<bool>("config", "Local_HideFarDamageNumber", true, "Hide the Damage Number far from player when on a planet. 在行星上时，隐藏距离玩家较远的伤害数字。");
            MaxNumberCount = Config.Bind<int>("config", "MaxNumberCount", 150, "Limit the maximum number of damage numbers that can be displayed simultaneously. Excessive values may significantly reduce performance in high-frequency damage scenarios. 限制可同时显示的伤害数字的最大数量。过高的同屏限制可能会在高频伤害场景严重降低性能。");

            GoundEnemyOnHitDamage = Config.Bind<bool>("config", "GoundEnemyOnHitDamage", true, "Whether show the damage number when the ground dark fog is hit. 是否显示地面黑雾的受击伤害数字。");
            SpaceEnemyOnHitDamage = Config.Bind<bool>("config", "SpaceEnemyOnHitDamage", true, "Whether show the damage number when the space dark fog is hit. 是否显示太空黑雾的受击伤害数字。");
            IcarusShieldOnHitDamage = Config.Bind<bool>("config", "IcarusShieldOnHitDamage", false, "Whether show the damage number when the Icarus' shield is hit. 是否显示伊卡洛斯护盾的受击伤害数字。");
            AllyOnHitDamage = Config.Bind<bool>("config", "AllyOnHitDamage", false, "Whether show the damage number when the ally buildings or fleets are hit. 是否显示友方建筑或舰队的受击伤害数字。");
            VegetableOnHitDamage = Config.Bind<bool>("config", "VegetableOnHitDamage", true, "Whether show the damage number when the ground sundries are hit. 是否显示地面杂物的受击伤害数字。");

            SmallTextSize = Config.Bind<int>("config", "SmallTextSize", 44, "Text size for minor damage. 较小伤害的文本大小。");
            MiddleTextSize = Config.Bind<int>("config", "MiddleTextSize", 60, "Text size for normal damage. 普通伤害的文本大小。");
            BigTextSize = Config.Bind<int>("config", "BigTextSize", 90, "Text size for major damage and critical strike damage. 较大伤害和暴击伤害的文本大小。");

            DamageNumber.basicSmallFontSize2160p = SmallTextSize.Value;
            DamageNumber.basicMidFontSize2160p = MiddleTextSize.Value;
            DamageNumber.basicBigFontSize2160p = BigTextSize.Value;

            Harmony.CreateAndPatchAll(typeof(ShowDamageNumberPlugin));
            Harmony.CreateAndPatchAll(typeof(OnDamagePatcher));
            Harmony.CreateAndPatchAll(typeof(DamageNumberController));
        }

        public void Start()
        {
            DamageNumberController.Init();
            DamageNumber.Init();
        }

        public void Update()
        {
            DamageNumberController.Update();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameSave), "LoadCurrentGame")]
        public void OnLoadGame()
        {
            DamageNumberController.RefreshDataWhenLoad();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), "NewGame")]
        public void OnNewGame()
        {
            DamageNumberController.RefreshDataWhenLoad();
        }

    }
}
