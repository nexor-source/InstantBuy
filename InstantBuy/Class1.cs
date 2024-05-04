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
        private const string modVersion = "0.0.1";

        private readonly Harmony harmony = new Harmony(modGUID);

        public ConfigEntry<float> offset;

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

            Logger = base.Logger;
            harmony.PatchAll();
            Logger.LogInfo("InstantBuy 0.0.1 loaded.");

            
        }

        /// <summary>
        /// 瞬间生成购买物品 （客机仍然会保留买物品的数量）
        /// </summary>
        [HarmonyPatch(typeof(Terminal))]
        internal class Terminal_Patch
        {
            [HarmonyPatch("SyncGroupCreditsClientRpc")]
            [HarmonyPrefix]
            public static void prefix(Terminal __instance, int newGroupCredits, ref int numItemsInShip)
            {
                numItemsInShip = 0;
            }

            [HarmonyPatch("SyncGroupCreditsClientRpc")]
            [HarmonyPostfix]
            public static void postfix(Terminal __instance, int newGroupCredits, int numItemsInShip)
            {
                NetworkManager networkManager = StartOfRound.Instance.localPlayerController.NetworkManager;
                if (!networkManager.IsServer)
                {
                    // InstantBuy.Logger.LogInfo("你不是server，已退出");
                    return;
                }

                // InstantBuy.Logger.LogInfo("触发同步金钱函数，新购入物品数量为" + numItemsInShip);
                List<int> boughtItems = __instance.orderedItemsFromTerminal;
                for (int i = 0; i < boughtItems.Count; i++)
                {
                    GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.buyableItemsList[boughtItems[i]].spawnPrefab,
                        new Vector3(2f + UnityEngine.Random.Range(-InstantBuy.Instance.offset.Value, InstantBuy.Instance.offset.Value), 0.3f, -14f + UnityEngine.Random.Range(-InstantBuy.Instance.offset.Value, InstantBuy.Instance.offset.Value)), Quaternion.identity, StartOfRound.Instance.propsContainer);
                    gameObject.GetComponent<GrabbableObject>().fallTime = 0f;
                    gameObject.GetComponent<NetworkObject>().Spawn(false);
                    // InstantBuy.Logger.LogInfo("已完成实例化: " + gameObject.GetComponent<GrabbableObject>().itemProperties.itemName);
                }


                /*ItemDropship x = UnityEngine.Object.FindObjectOfType<ItemDropship>();
                x.ShipLeaveClientRpc();*/


                __instance.ClearBoughtItems();
                // InstantBuy.Logger.LogInfo("正常退出");
            }
        }
    }
}
