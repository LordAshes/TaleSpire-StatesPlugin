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
    [BepInDependency(LordAshes.StatMessaging.Guid)]
    public class StatesPlugin : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "States Plug-In";
        public const string Guid = "org.lordashes.plugins.states";
        public const string Version = "2.0.1.0";

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

            if (System.IO.File.Exists(dir + "Config/" + Guid + "/ColorizedKeywords.json"))
            {
                string json = System.IO.File.ReadAllText(dir + "Config/" + Guid + "/ColorizedKeywords.json");
                colorizations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }

            // Subscrive to Stat Messages
            StatMessaging.Subscribe(StatesPlugin.Guid, HandleRequest);

            // Post plugin on the TaleSpire main page
            StateDetection.Initialize(this.GetType());


        }

        /// <summary>
        /// Function for determining if view mode has been toggled and, if so, activating or deactivating Character View mode.
        /// This function is called periodically by TaleSpire.
        /// </summary>
        void Update()
        {
            if (isBoardLoaded())
            {
                SyncStealthMode();

                if (triggerKey.Value.IsUp())
                {
                    SetRequest();
                }
            }
        }

        public void HandleRequest(StatMessaging.Change[] changes)
        {
            foreach (StatMessaging.Change change in changes)
            {
                if (change.key == StatesPlugin.Guid)
                {
                    try
                    {
                        CreatureBoardAsset asset;
                        CreaturePresenter.TryGetAsset(change.cid, out asset);
                        if (asset != null)
                        {
                            TextMeshPro creatureStateText = null;
                            switch (change.action)
                            {
                                case StatMessaging.ChangeType.added:
                                case StatMessaging.ChangeType.modified:
                                    GameObject creatureBlock = GameObject.Find(asset.Creature.CreatureId + ".StatesBlock");
                                    if (creatureBlock == null)
                                    {
                                        Debug.Log("Creating CreatureBlock GameObject");
                                        creatureBlock = new GameObject(asset.Creature.CreatureId + ".StatesBlock");
                                        creatureBlock.transform.position = asset.BaseLoader.LoadedAsset.transform.position;
                                        creatureBlock.transform.rotation = Quaternion.Euler(0, 0, 0);
                                        creatureBlock.transform.SetParent(asset.BaseLoader.LoadedAsset.transform);

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
                                        Debug.Log("Using Existing TextMeshPro");
                                        creatureStateText = creatureBlock.GetComponent<TextMeshPro>();
                                    }
                                    Debug.Log("Populating TextMeshPro");
                                    creatureStateText.autoSizeTextContainer = false;
                                    string content = change.value.Replace(",", "\r\n");
                                    if (colorizations.ContainsKey("<Default>")) { content = "<Default>" + content; }
                                    creatureStateText.richText = true;
                                    Debug.Log("States: " + content);
                                    foreach (KeyValuePair<string, string> replacement in colorizations)
                                    {
                                        content = content.Replace(replacement.Key, replacement.Value);
                                        Debug.Log("States: " + content+" (After replacing '"+ replacement.Key+"' with '"+replacement.Value+"')");
                                    }
                                    creatureStateText.text = content;
                                    int lines = content.Split('\r').Length;
                                    creatureStateText.transform.position = new Vector3(creatureBlock.transform.position.x, 1.25f + (0.1f * lines), creatureBlock.transform.position.z);
                                    creatureStateText.autoSizeTextContainer = true;
                                    break;
                                case StatMessaging.ChangeType.removed:
                                    Debug.Log("Removing States Block for creature '" + change.cid + "'");
                                    GameObject.Destroy(GameObject.Find(asset.Creature.CreatureId + ".StatesBlock"));
                                    break;
                            }
                        }
                    }
                    catch(Exception x ) { Debug.Log("Exception: "+x); }
                }
            }
        }

        /// <summary>
        /// Method to write stats to the Creature Name
        /// </summary>
        public void SetRequest()
        {
            CreatureBoardAsset asset;
            CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
            if (asset != null)
            {
                string states = StatMessaging.ReadInfo(asset.Creature.CreatureId, StatesPlugin.Guid);

                SystemMessage.AskForTextInput("State", "Enter Creature State(s):", "OK", (newStates) =>
                {
                    StatMessaging.SetInfo(asset.Creature.CreatureId, StatesPlugin.Guid, newStates);
                },
                null, "Clear", () =>
                {
                    StatMessaging.ClearInfo(asset.Creature.CreatureId, StatesPlugin.Guid);
                },
                states);
            }
        }

        public void SyncStealthMode()
        {
            foreach (CreatureBoardAsset asset in CreaturePresenter.AllCreatureAssets)
            {
                // Sync Hidden Status
                try
                {
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
                }
                catch(Exception) { ; }
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
