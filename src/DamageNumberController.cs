using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace ShowDamageNumber
{
    public static class DamageNumberController
    {
        public static DamageNumberDatas main;
        public static List<int> listForClearNull;
        public static Image ImgShowDamage;
        public static Sprite checkBoxCheckedIcon;
        public static Sprite checkBoxUncheckedIcon;

        public static long lastNFrameShdDmgTotal; // 用于控制每n帧最多显示一次护盾受伤
        public const int showShdDmgOncePerNFrame = 6;

        public static void Init()
        {
            main = new DamageNumberDatas();
            listForClearNull = new List<int>();
            lastNFrameShdDmgTotal = 0;

            checkBoxCheckedIcon = Resources.Load<Sprite>("ui/textures/sprites/icons/checkbox-on");
            checkBoxUncheckedIcon = Resources.Load<Sprite>("ui/textures/sprites/icons/checkbox-off");
            // 初始化开关伤害显示的UI
            GameObject parentObj = GameObject.Find("UI Root/Overlay Canvas/In Game/Z Screen/fleet-group/fleet-panel");
            GameObject oriCheckBox = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Dyson Sphere Editor/Dyson Editor Control Panel/hierarchy/layers/display-group/display-toggle-3/checkbox-back-structures");
            if (oriCheckBox != null && parentObj != null)
            {
                GameObject checkButtonObj = GameObject.Instantiate(oriCheckBox);
                checkButtonObj.transform.SetParent(parentObj.transform, false);
                checkButtonObj.transform.localScale = Vector3.one;
                checkButtonObj.name = "show-damage";
                GameObject.DestroyImmediate(checkButtonObj.GetComponent<Image>());
                checkButtonObj.AddComponent<Image>();
                checkButtonObj.GetComponent<Image>().sprite = Resources.Load<Sprite>("ui/textures/sprites/sci-fi/sci-fi-button-32-4k");
                checkButtonObj.GetComponent<Button>().onClick.RemoveAllListeners();
                checkButtonObj.GetComponent<Button>().onClick.AddListener(() => { ShowHideDamageNumberOnChange(); });
                checkButtonObj.transform.Find("text").GetComponent<Text>().color = Color.white;
                checkButtonObj.GetComponent<RectTransform>().sizeDelta = new Vector3(140, 22, 0);
                checkButtonObj.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
                checkButtonObj.GetComponent<RectTransform>().anchorMin = new Vector2(1, 1);
                checkButtonObj.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
                checkButtonObj.GetComponent<RectTransform>().anchoredPosition3D = new Vector3(-230, 45, 0);
                checkButtonObj.GetComponent<UIButton>().tips.tipTitle = "";
                checkButtonObj.GetComponent<UIButton>().tips.corner = 1;
                checkButtonObj.GetComponent<UIButton>().tips.delay = 0.1f;
                checkButtonObj.GetComponent<UIButton>().tips.width = 300;
                checkButtonObj.GetComponent<UIButton>().transitions[0].target = checkButtonObj.GetComponent<Image>();
                checkButtonObj.GetComponent<UIButton>().transitions[0].normalColor = new Color(1, 1, 1, 0);
                checkButtonObj.GetComponent<UIButton>().transitions[0].mouseoverColor = new Color(1, 1, 1, 0.0392f);
                checkButtonObj.GetComponent<UIButton>().transitions[0].pressedColor = new Color(1, 1, 1, 0.0314f);
                checkButtonObj.transform.Find("text").GetComponent<Localizer>().stringKey = "Show Damage Number";
                checkButtonObj.AddComponent<UIBlockZone>();

                GameObject checkboxIconObj = new GameObject();
                checkboxIconObj.name = "icon";
                checkboxIconObj.transform.SetParent(checkButtonObj.transform, false);
                checkboxIconObj.AddComponent<Image>();
                checkboxIconObj.GetComponent<Image>().sprite = checkBoxCheckedIcon;
                ImgShowDamage = checkboxIconObj.GetComponent<Image>();
                checkboxIconObj.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0.5f);
                checkboxIconObj.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.5f);
                checkboxIconObj.GetComponent<RectTransform>().pivot = new Vector2(0, 0.5f);
                checkboxIconObj.GetComponent<RectTransform>().anchoredPosition3D = new Vector3(0, 0, 0);
                checkboxIconObj.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 20);
                RefreshUI();
            }
        }

        public static void RefreshDataWhenLoad()
        {
            ClearAllNumbers();
            main.lastPlanetId = -1;
        }

        public static void Update()
        {
            //if(GameMain.instance != null)
            //    GameTickUpdate(GameMain.instance.timei);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), "GameTick")]
        public static void GameTickUpdate(long time)
        {
            if (ShowDamageNumberPlugin.ShowDamage.Value)
            {
                CheckPlayerViewStatus();

                if(time % 60 == 50)
                {
                    DamageNumber.UpdateGameHistoryData();
                }
                if(lastNFrameShdDmgTotal > 0 && Utils.randSeed.Value.NextDouble() * showShdDmgOncePerNFrame <= 1)
                {
                    API.TryAddPlayerShieldResistNumber(lastNFrameShdDmgTotal);
                    lastNFrameShdDmgTotal = 0;
                }


                for (int i = 0; i < main.cursor; i++)
                {
                    if (main.activeArray[i] != null && main.activeArray[i].index >= 0)
                        main.activeArray[i].Update();
                }
                //listForClearNull.Clear();
                //foreach (var pair in main.dotArray)
                //{
                //    if (pair.Value != null)
                //        pair.Value.Update();
                //    else
                //        listForClearNull.Add(pair.Key);
                //}
                //// 清理unll项
                //foreach (var key in listForClearNull)
                //{
                //    if (main.dotArray.ContainsKey(key))
                //        main.dotArray.Remove(key);
                //}
            }
        }

        // 刷新数据，which is used for 根据玩家所处的星球/太空，以及摄像机模式，决定哪些数据显示，哪些不显示。
        // 并且处理localPlanet变更时的数据清理
        public static void CheckPlayerViewStatus()
        {
            int curPlanetId = GameMain.localPlanet != null ? GameMain.localPlanet.id : -1;
            if(curPlanetId != main.lastPlanetId)
            {
                ClearAllNumbers();
                main.lastPlanetId = curPlanetId;
            }
        }


        public static void ClearAllNumbers()
        {
            // 清理所有伤害数字
            //for (int i = 0; i < main.cursor; i++)
            //{
            //    if (main.activeArray[i] != null && main.activeArray[i].index >= 0)
            //        main.activeArray[i].Remove();
            //}

            //main.dotArray.Clear();
            //main.cursor = 0;
            //main.recycleCursor = 0;
            //lastNFrameShdDmgTotal = 0;
            ForceRemoveAllNumbers();
        }

        public static void ForceRemoveAllNumbers()
        {
            int i = 0;
            while (i < DamageNumber.parentTrans.childCount)
            {
                if (DamageNumber.parentTrans.GetChild(i).gameObject.name != "dmg")
                {
                    GameObject.DestroyImmediate(DamageNumber.parentTrans.GetChild(i).gameObject);
                }
                else
                {
                    i++;
                }
            }
            int length = main.activeArray.Length;
            for (int j = 0; j < length; j++)
            {
                main.activeArray[j] = null;
            }
            main.dotArray.Clear();
            main.cursor = 0;
            main.recycleCursor = 0;
            lastNFrameShdDmgTotal = 0;
        }

        public static void ShowHideDamageNumberOnChange()
        {
            if(ShowDamageNumberPlugin.ShowDamage.Value)
            {
                ForceRemoveAllNumbers();
            }
            ShowDamageNumberPlugin.ShowDamage.Value = !ShowDamageNumberPlugin.ShowDamage.Value;
            ShowDamageNumberPlugin.ShowDamage.ConfigFile.Save();
            RefreshUI();
        }

        public static void RefreshUI()
        {
            if (ShowDamageNumberPlugin.ShowDamage.Value)
            {
                ImgShowDamage.sprite = checkBoxCheckedIcon;
            }
            else
            {
                ImgShowDamage.sprite = checkBoxUncheckedIcon;
            }
        }
    }

    public class DamageNumberDatas
    {

        public DamageNumber[] activeArray;
        public int cursor;
        public int[] recycleArray;
        public int recycleCursor;
        public Dictionary<int, DamageNumber> dotArray;
        public int lastPlanetId;

        public DamageNumberDatas()
        {
            activeArray = new DamageNumber[10];
            cursor = 0;
            recycleArray = new int[10];
            recycleCursor = 0;
            dotArray = new Dictionary<int, DamageNumber>();
            lastPlanetId = -1;
        }
    }
}
