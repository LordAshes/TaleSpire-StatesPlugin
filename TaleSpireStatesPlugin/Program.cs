using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using BepInEx;
using Bounce.Unmanaged;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Newtonsoft.Json;

namespace LordAshes
{
    [BepInPlugin(Guid, Name, Version)]
    public class StatesPlugin : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "States Plug-In";
        public const string Guid = "org.lordashes.plugins.states";
        public const string Version = "1.1.0.0";

        // Configuration
        private ConfigEntry<KeyboardShortcut> triggerKey { get; set; }
        private ConfigEntry<UnityEngine.Color> baseColor { get; set; }

        // Content directory
        private string dir = UnityEngine.Application.dataPath.Substring(0, UnityEngine.Application.dataPath.LastIndexOf("/")) + "/TaleSpire_CustomData/";

        // Holds current creature states
        private Dictionary<CreatureGuid, string> creatureStates = new Dictionary<CreatureGuid, string>();

        // Colorized keywords
        private Dictionary<string, string> colorizations = new Dictionary<string, string>();

        /// <summary>
        /// Function for initializing plugin
        /// This function is called once by TaleSpire
        /// </summary>
        void Awake()
        {
            UnityEngine.Debug.Log("Lord Ashes States Plugin Active.");

            triggerKey = Config.Bind("Hotkeys", "States Activation", new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl));
            baseColor = Config.Bind("Appearance", "Base Text Color", UnityEngine.Color.black);

            if(System.IO.File.Exists(dir+"Config/"+Guid+"/ColorizedKeywords.json"))
            {
                string json = System.IO.File.ReadAllText(dir + "Config/" + Guid + "/ColorizedKeywords.json");
                colorizations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
        }

        /// <summary>
        /// Function for determining if view mode has been toggled and, if so, activating or deactivating Character View mode.
        /// This function is called periodically by TaleSpire.
        /// </summary>
        void Update()
        {
            if(isBoardLoaded())
            {
                if (triggerKey.Value.IsUp())
                {
                    SetNameRequest();
                }

                CheckNameRequest();
            }
        }

        /// <summary>
        /// Method to write stats to the Creature Name
        /// </summary>
        public void SetNameRequest()
        {
            Debug.Log("Setting asset...");
            CreatureBoardAsset asset;
            CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
            if (asset != null)
            {
                Debug.Log("Got selected asset...");
                string stats = asset.Creature.Name;
                if (!stats.Contains("|"))
                {
                    if (!stats.Contains(":"))
                    {
                        stats = stats + " ||";
                    }
                    else
                    {
                        stats = stats.Substring(0, stats.IndexOf(":")) + " || : " + stats.Substring(stats.IndexOf(":") + 1);
                    }
                }
                string[] statParts = stats.Split('|');
                SystemMessage.AskForTextInput("State", "Enter Creature State(s):", "OK", (s) =>
                {
                    CreatureManager.SetCreatureName(asset.Creature.CreatureId, statParts[0] + " |"+ s +"| " + statParts[2]);
                },
                null, "Clear", ()=>
                {
                    CreatureManager.SetCreatureName(asset.Creature.CreatureId, statParts[0] + " || " + statParts[2]);
                }, 
                statParts[1].Replace("\r\n",","));
            }
        }

        public void CheckNameRequest()
        {
            foreach(CreatureBoardAsset asset in CreaturePresenter.AllCreatureAssets)
            {

                // Sync Hidden Status
                GameObject block = GameObject.Find(asset.Creature.CreatureId + ".StatesBlock");
                if (block != null)
                {
                    if (asset.Creature.IsExplicitlyHidden == true && block.GetComponent<TextMeshPro>().enabled == true)
                    {
                        block.GetComponent<TextMeshPro>().enabled = false;
                    }
                    else if (asset.Creature.IsExplicitlyHidden == false && block.GetComponent<TextMeshPro>().enabled == false)
                    {
                        block.GetComponent<TextMeshPro>().enabled = true;
                    }
                }

                // Look For Text Changes
                string stats = asset.Creature.Name;
                if (stats.Contains("|"))
                {
                    string[] statParts = asset.Creature.Name.Split('|');
                    if (!creatureStates.ContainsKey(asset.Creature.CreatureId)) { creatureStates.Add(asset.Creature.CreatureId, ""); }
                    if(statParts[1]!= creatureStates[asset.Creature.CreatureId])
                    {
                        creatureStates[asset.Creature.CreatureId] = statParts[1];
                        Debug.Log("Accessing CreatureBlock GameObject");
                        GameObject creatureBlock = GameObject.Find(asset.Creature.CreatureId+".StatesBlock");
                        TextMeshPro creatureStateText = null;
                        if (creatureBlock == null)
                        {
                            Debug.Log("Creating CreatureBlock GameObject");
                            creatureBlock = new GameObject(asset.Creature.CreatureId + ".StatesBlock");
                            creatureBlock.transform.position = new Vector3(asset.BaseLoader.transform.position.x+0.25f, asset.BaseLoader.transform.position.y + 1f, asset.BaseLoader.transform.position.z);
                            creatureBlock.transform.rotation = Quaternion.Euler(10,10,-10);
                            creatureBlock.transform.SetParent(asset.BaseLoader.transform);
                            Debug.Log("Creating TextMeshPro");
                            creatureStateText = creatureBlock.AddComponent<TextMeshPro>();
                            creatureStateText.transform.position = creatureBlock.transform.position;
                            creatureStateText.transform.rotation = creatureBlock.transform.rotation;
                            creatureStateText.textStyle = TMP_Style.NormalStyle;
                            creatureStateText.enableWordWrapping = true;
                            creatureStateText.alignment = TextAlignmentOptions.Center;
                            creatureStateText.autoSizeTextContainer = true;
                            creatureStateText.color = baseColor.Value;
                            creatureStateText.fontSize = 1;
                            creatureStateText.fontWeight = FontWeight.Bold;
                        }
                        else
                        {
                            Debug.Log("Accessing TextMeshPro for BaseLoader");
                            creatureStateText = creatureBlock.GetComponent<TextMeshPro>();
                        }
                        Debug.Log("Populating TextMeshPro");
                        creatureStateText.autoSizeTextContainer = false;
                        statParts[1] = statParts[1].Replace(",", "\r\n");
                        if (colorizations.ContainsKey("<Default>")) { statParts[1] = "<Default>" + statParts[1]; }
                        creatureStateText.richText = true;
                        foreach (KeyValuePair<string, string> replacement in colorizations)
                        {
                            statParts[1] = statParts[1].Replace(replacement.Key, replacement.Value);
                        }
                        creatureStateText.text = statParts[1];
                        int lines = statParts[1].Split('\r').Length;
                        creatureStateText.transform.position = new Vector3(creatureBlock.transform.position.x, 1.25f+(0.1f*lines), creatureBlock.transform.position.z);
                        creatureStateText.autoSizeTextContainer = true;

                    }
                }
            }
        }

        /// <summary>
        /// Function to check if the board is loaded
        /// </summary>
        /// <returns></returns>
        public bool isBoardLoaded()
        {
            return CameraController.HasInstance && BoardSessionManager.HasInstance && !BoardSessionManager.IsLoading;
        }
    }
}
