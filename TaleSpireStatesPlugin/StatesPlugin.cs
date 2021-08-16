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
    [BepInDependency(LordAshes.FileAccessPlugin.Guid)]
    [BepInDependency(LordAshes.StatMessaging.Guid)]
    [BepInDependency(RadialUI.RadialUIPlugin.Guid)]
    public class StatesPlugin : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "States Plug-In";
        public const string Guid = "org.lordashes.plugins.states";
        public const string Version = "2.3.0.0";

        // Configuration
        private ConfigEntry<KeyboardShortcut> triggerKey { get; set; }
        private ConfigEntry<UnityEngine.Color> baseColor { get; set; }
        private Queue<StatMessaging.Change> backlogChangeQueue = new Queue<StatMessaging.Change>();

        // Content directory
        private string dir = UnityEngine.Application.dataPath.Substring(0, UnityEngine.Application.dataPath.LastIndexOf("/")) + "/TaleSpire_CustomData/";

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
                string json = FileAccessPlugin.File.ReadAllText(dir + "Config/" + Guid + "/ColorizedKeywords.json");
                colorizations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }

            // Add Info menu selection to main character menu
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RadialUI.RadialUIPlugin.Guid + ".Info",
                                                        RadialUI.RadialSubmenu.MenuType.character,
                                                        "Info",
                                                        FileAccessPlugin.Image.LoadSprite("Info.png")
                                                     );

            // Add Icons sub menu item
            RadialUI.RadialSubmenu.CreateSubMenuItem(RadialUI.RadialUIPlugin.Guid + ".Info",
                                                        "Icons",
                                                        FileAccessPlugin.Image.LoadSprite("States.png"),
                                                        (cid, menu, mmi) => { SetRequest(cid); },
                                                        false,
                                                        null
                                                    );

            // Subscribe to Stat Messages
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
                syncStealthMode();

                if (triggerKey.Value.IsUp())
                {
                    SetRequest(LocalClient.SelectedCreatureId);
                }

                foreach (CreatureBoardAsset asset in CreaturePresenter.AllCreatureAssets)
                {
                    try
                    {
                        GameObject creatureBlock = GameObject.Find("Effect:"+asset.Creature.CreatureId + ".StatesBlock");
                        if (creatureBlock != null)
                        {

                            creatureBlock.transform.rotation = Quaternion.LookRotation(creatureBlock.transform.position - Camera.main.transform.position);

                            TextMeshPro creatureStateText = creatureBlock.GetComponent<TextMeshPro>();
                            //creatureStateText.transform.position = creatureBlock.transform.position;
                            creatureStateText.transform.rotation = creatureBlock.transform.rotation;

                            creatureStateText.transform.position = new Vector3(asset.CreatureLoaders[0].LoadedAsset.transform.position.x, calculateYMax(asset) + creatureStateText.preferredHeight, asset.CreatureLoaders[0].LoadedAsset.transform.position.z);
                        }
                    }
                    catch (Exception) { }
                }


                while (backlogChangeQueue.Count > 0)
                {
                    StatMessaging.Change tempChange = backlogChangeQueue.Peek();
                    CreatureBoardAsset asset;
                    CreaturePresenter.TryGetAsset(tempChange.cid, out asset);

                    if (asset.CreatureLoaders[0].LoadedAsset == null) //still not ready
                        break;
                    else
                    {
                        backlogChangeQueue.Dequeue(); //pop the next one out of the queue

                        TextMeshPro tempTMP = null;
                        GameObject tempGO = null;
                        createNewCreatureStateText(out tempTMP, out tempGO, asset);
                        populateCreatureStateText(tempTMP, tempChange, asset);
                    }
                }
            }
        }

        public void HandleRequest(StatMessaging.Change[] changes)
        {
            foreach (StatMessaging.Change change in changes)
            {
                if (change == null)
                    Debug.Log("ERROR: StatMessaging change was NULL;");
                else
                    Debug.Log("StatesPlugin-HandleRequest, Creature ID: " + change.cid + ", Action: " + change.action + ", Key: " + change.key + ", Previous Value: " + change.previous + ", New Value: " + change.value);
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
                                    GameObject creatureBlock = GameObject.Find("Effect:"+asset.Creature.CreatureId + ".StatesBlock");
                                    if (creatureBlock == null)
                                    {
                                        if (asset.CreatureLoaders[0].LoadedAsset != null)
                                            createNewCreatureStateText(out creatureStateText, out creatureBlock, asset);
                                        else
                                            backlogChangeQueue.Enqueue(change);
                                    }
                                    else
                                    {
                                        Debug.Log("Using Existing TextMeshPro");
                                        creatureStateText = creatureBlock.GetComponent<TextMeshPro>();
                                    }

                                    if (creatureBlock != null)
                                        populateCreatureStateText(creatureStateText, change, asset);
                                    break;

                                case StatMessaging.ChangeType.removed:
                                    Debug.Log("Removing States Block for creature '" + change.cid + "'");
                                    GameObject.Destroy(GameObject.Find("Effect:"+asset.Creature.CreatureId + ".StatesBlock"));
                                    break;
                            }
                        }
                    }
                    catch (Exception x) { Debug.Log("Exception: " + x); }
                }
            }
        }

        private void createNewCreatureStateText(out TextMeshPro creatureStateText, out GameObject creatureBlock, CreatureBoardAsset asset)
        {
            Debug.Log("Creating CreatureBlock GameObject");

            if (GameObject.Find("Effect:"+asset.Creature.CreatureId + ".StatesBlock") != null)
            {
                Debug.Log("StatesText already exists.  Ignoring duplicate");
                creatureStateText = null;
                creatureBlock = null;
                return; //we have a duplicate
            }

            creatureBlock = new GameObject("Effect:"+asset.Creature.CreatureId + ".StatesBlock");

            Vector3 tempV3;

            tempV3 = new Vector3(asset.CreatureLoaders[0].LoadedAsset.transform.position.x, calculateYMax(asset), asset.CreatureLoaders[0].LoadedAsset.transform.position.z);

            creatureBlock.transform.position = tempV3;
            creatureBlock.transform.rotation = Quaternion.LookRotation(creatureBlock.transform.position - Camera.main.transform.position);

            creatureBlock.transform.SetParent(asset.CreatureLoaders[0].LoadedAsset.transform);

            Debug.Log("Creating TextMeshPro");
            creatureStateText = creatureBlock.AddComponent<TextMeshPro>();
            creatureStateText.transform.rotation = creatureBlock.transform.rotation;
            creatureStateText.textStyle = TMP_Style.NormalStyle;
            creatureStateText.enableWordWrapping = true;
            creatureStateText.alignment = TextAlignmentOptions.Center;
            creatureStateText.autoSizeTextContainer = true;
            creatureStateText.color = baseColor.Value;
            creatureStateText.fontSize = 1;
            creatureStateText.fontWeight = FontWeight.Bold;
            creatureStateText.isTextObjectScaleStatic = true;
        }

        private void populateCreatureStateText(TextMeshPro creatureStateText, StatMessaging.Change change, CreatureBoardAsset asset)
        {
            if (creatureStateText == null)
                return;

            Debug.Log("Populating TextMeshPro");
            creatureStateText.autoSizeTextContainer = false;
            string content = change.value.Replace(",", "\r\n");
            if (colorizations.ContainsKey("<Default>")) { content = "<Default>" + content; }
            creatureStateText.richText = true;
            //Debug.Log("States: " + content);
            foreach (KeyValuePair<string, string> replacement in colorizations)
            {
                content = content.Replace(replacement.Key, replacement.Value);
                //Debug.Log("States: " + content + " (After replacing '" + replacement.Key + "' with '" + replacement.Value + "')");
            }

            creatureStateText.text = content;
            creatureStateText.autoSizeTextContainer = true;

            creatureStateText.transform.position = new Vector3(asset.CreatureLoaders[0].LoadedAsset.transform.position.x, calculateYMax(asset) + creatureStateText.preferredHeight, asset.CreatureLoaders[0].LoadedAsset.transform.position.z);
        }

        private float calculateYMax(CreatureBoardAsset asset)
        {
            float yMax = 0;
            //Debug.Log("CreatureLoader AssetLoader Count: " + asset.CreatureLoaders.Length);
            yMax = asset.CreatureLoaders[0].LoadedAsset.GetComponent<MeshRenderer>().bounds.max.y;

            GameObject cmpGO = GameObject.Find("CustomContent:" + asset.Creature.CreatureId);
            if (cmpGO != null)
            {
                SkinnedMeshRenderer tempSMR = cmpGO.GetComponentInChildren<SkinnedMeshRenderer>();
                if (tempSMR != null)
                {
                    yMax = Mathf.Max(yMax, tempSMR.bounds.max.y);
                }
            }

            return yMax;
        }

        /// <summary>
        /// Method to write stats to the Creature Name
        /// </summary>
        public void SetRequest(CreatureGuid cid)
        {
            CreatureBoardAsset asset;
            CreaturePresenter.TryGetAsset(cid, out asset);
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

        public void syncStealthMode()
        {
            foreach (CreatureBoardAsset asset in CreaturePresenter.AllCreatureAssets)
            {
                // Sync Hidden Status
                try
                {
                    GameObject block = GameObject.Find("Effect:"+asset.Creature.CreatureId + ".StatesBlock");
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
                catch (Exception) {; }
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
