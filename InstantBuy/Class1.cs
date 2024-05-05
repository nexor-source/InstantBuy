using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

using System;
using System.Security.Cryptography;
using Unity.Netcode;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;


namespace InstantBuy
{
    /// <summary>
    /// 插件加载
    /// </summary>
    [BepInPlugin(modGUID, modName, modVersion)]
    public class InstantBuy : BaseUnityPlugin
    {
        private const string modGUID = "nexor.InstantBuy";
        private const string modName = "InstantBuy";
        private const string modVersion = "0.0.4";

        private readonly Harmony harmony = new Harmony(modGUID);

        public ConfigEntry<float> offset;
        public ConfigEntry<string> ignored_item;

        public static InstantBuy Instance;
        public static BepInEx.Logging.ManualLogSource Logger;


        // 在插件启动时会直接调用Awake()方法
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            offset = Config.Bind<float>("InstantBuy Config",
                                        "offset 偏移",
                                        0.2f,
                                        "Controls the offset of where purchased items are generated 控制购买物品生成位置的偏移");

            ignored_item = Config.Bind<string>("InstantBuy Config",
                                        "ignored_item 不会触发该mod的物品名单",
                                        "-1,",
                                        "Numbers are separated by commas, e.g. -1,0,1,2    -1 is used as a placeholder, please go to the mod introduction page in the ThunderStore to check which number corresponds to which item. " +
                                        "数字使用逗号隔开，如-1,0,1,2    -1是用来占位的，具体哪个数字对应哪个物品请到雷电商城的mod介绍页查看");

            Logger = base.Logger;
            harmony.PatchAll();
            Logger.LogInfo("InstantBuy " + modVersion + " loaded.");

            
        }

        /// <summary>
        /// 瞬间生成购买物品 （客机仍然会保留买物品的数量）
        /// </summary>
        [HarmonyPatch(typeof(Terminal))]
        internal class Terminal_Patch
        {
            private static List<int> instantItems;


            [HarmonyPatch("SyncGroupCreditsClientRpc")]
            [HarmonyPrefix]
            public static void Prefix(Terminal __instance, int newGroupCredits, ref int numItemsInShip)
            {
                NetworkManager networkManager = StartOfRound.Instance.localPlayerController.NetworkManager;
                if (!networkManager.IsServer)
                {
                    // InstantBuy.Logger.LogInfo("你不是server，已退出");
                    return;
                }

                List<int> boughtItems = __instance.orderedItemsFromTerminal;
                // 使用逗号分隔
                List<int>  ignoredItem_list = InstantBuy.Instance.ignored_item.Value.Trim(',').Split(',').Select(int.Parse).ToList();
                // 将boughtItems分为两个列表，一个是在ignoredItems中出现的，一类则不是
                instantItems = new List<int>();
                instantItems = boughtItems.Where(item => !ignoredItem_list.Contains(item)).ToList();
                
                
                numItemsInShip = __instance.orderedItemsFromTerminal.Count - instantItems.Count;

                // Logger.LogInfo("钱 瞬间购买列表物品数:" + instantItems.Count);
            }

            [HarmonyPatch("SyncGroupCreditsClientRpc")]
            [HarmonyPostfix]
            public static void Postfix(Terminal __instance, int newGroupCredits, int numItemsInShip)
            {
                NetworkManager networkManager = StartOfRound.Instance.localPlayerController.NetworkManager;
                if (!networkManager.IsServer)
                {
                    // InstantBuy.Logger.LogInfo("你不是server，已退出");
                    return;
                }

                List<int> boughtItems = __instance.orderedItemsFromTerminal;
                instantItems = new List<int>();
                List<int> ignoredItem_list;

                if (string.IsNullOrEmpty(InstantBuy.Instance.ignored_item.Value)) 
                {
                    ignoredItem_list = new List<int>();
                }
                else
                {
                    ignoredItem_list = InstantBuy.Instance.ignored_item.Value.Trim(',').Split(',').Select(int.Parse).ToList();
                }
                instantItems = boughtItems.Where(item => !ignoredItem_list.Contains(item)).ToList();

                // InstantBuy.Logger.LogInfo("触发同步金钱函数，新购入物品数量为" + numItemsInShip);
                // Logger.LogInfo("瞬间购买列表物品数:" + instantItems.Count);

                Vector3 spawn_pos = StartOfRound.Instance.insideShipPositions[10].position;
                spawn_pos.z += 1.5f;

                foreach (int itemIndex in instantItems)
                {
                    GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.buyableItemsList[itemIndex].spawnPrefab,
                        new Vector3(spawn_pos.x + UnityEngine.Random.Range(-InstantBuy.Instance.offset.Value, InstantBuy.Instance.offset.Value), spawn_pos.y, 
                        spawn_pos.z + UnityEngine.Random.Range(-InstantBuy.Instance.offset.Value, InstantBuy.Instance.offset.Value)), Quaternion.identity, StartOfRound.Instance.propsContainer);
                    gameObject.GetComponent<GrabbableObject>().fallTime = 0f;
                    gameObject.GetComponent<GrabbableObject>().isInShipRoom = true;
                    gameObject.GetComponent<GrabbableObject>().transform.parent = GameObject.Find("/Environment/HangarShip").transform;
                    gameObject.GetComponent<NetworkObject>().Spawn(false);
                    // InstantBuy.Logger.LogInfo("已完成实例化: " + gameObject.GetComponent<GrabbableObject>().itemProperties.itemName);
                }

                __instance.orderedItemsFromTerminal = boughtItems.Where(item => ignoredItem_list.Contains(item)).ToList();
                // InstantBuy.Logger.LogInfo("正常退出");
            }
        }

    }
}
