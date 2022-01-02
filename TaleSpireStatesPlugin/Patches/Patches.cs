using BepInEx;
using HarmonyLib;

using System;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace LordAshes
{
    public partial class StatesPlugin : BaseUnityPlugin
    {
        [HarmonyPatch(typeof(CreatureManager), "DeleteCreature")]
        public static class Patches
        {
            public static bool Prefix(CreatureGuid creatureGuid, UniqueCreatureGuid uniqueId)
            {
                GameObject go = GameObject.Find("Effect:" + creatureGuid + ".StatesBlock");
                if(go!=null)
                {
                    Debug.Log("States Plugin: DeleteCreature Patch: Destroying 'Effect:" + creatureGuid + ".StatesBlock'");
                    GameObject.Destroy(go);
                }
                return true;
            }
        }
    }
}
