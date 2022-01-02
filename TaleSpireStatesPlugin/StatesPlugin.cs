using System;
using System.Collections.Generic;

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace LordAshes
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(LordAshes.FileAccessPlugin.Guid)]
    [BepInDependency(LordAshes.StatMessaging.Guid)]
    [BepInDependency(RadialUI.RadialUIPlugin.Guid)]
    public partial class StatesPlugin : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "States Plug-In";
        public const string Guid = "org.lordashes.plugins.states";
        public const string Version = "2.5.1.0";

        // Configuration
        private ConfigEntry<KeyboardShortcut> triggerKey { get; set; }
        private ConfigEntry<UnityEngine.Color> baseColor { get; set; }

        // Content directory
        private string dir = UnityEngine.Application.dataPath.Substring(0, UnityEngine.Application.dataPath.LastIndexOf("/")) + "/TaleSpire_CustomData/";

        // Internal Variables
        private Queue<StatMessaging.Change> backlogChangeQueue = new Queue<StatMessaging.Change>();
        private Dictionary<string, string> colorizations = new Dictionary<string, string>();
        private float baseSize = 16.0f;
        private OffsetMethod offsetMethod = OffsetMethod.boundsOffset;
        private float offsetValue = 1.0f;

        public enum OffsetMethod
        {
            fixedOffset = 0,
            baseScaleFixedOffet,
            headHookMultiplierOffset,
            boundsOffset
        }

        /// <summary>
        /// Function for initializing plugin
        /// This function is called once by TaleSpire
        /// </summary>
        void Awake()
        {
            UnityEngine.Debug.Log("States Plugin: Active.");

            new Harmony(Guid).PatchAll();

            triggerKey = Config.Bind("Hotkeys", "States Activation", new KeyboardShortcut(KeyCode.S, KeyCode.LeftControl));
            baseColor = Config.Bind("Appearance", "Base Text Color", UnityEngine.Color.black);
            baseSize = Config.Bind("Appearance", "Base Text Size", 2.0f).Value;

            offsetMethod = Config.Bind("Settings", "Height Offset Method", OffsetMethod.boundsOffset).Value;
            offsetValue = Config.Bind("Settings", "Height Offset Value", 1.0f).Value;

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
                                                        "States",
                                                        FileAccessPlugin.Image.LoadSprite("States.png"),
                                                        (cid, menu, mmi) => { SetRequest(cid); },
                                                        false,
                                                        null
                                                    );

            // Subscribe to Stat Messages
            StatMessaging.Subscribe(StatesPlugin.Guid, HandleRequest);

            // Post plugin on the TaleSpire main page
            Utility.Initialize(this.GetType());
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

                /*
                if(Input.GetKeyDown(KeyCode.Q))
                {
                    CreatureBoardAsset asset;
                    CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
                    if (asset != null)
                    {
                        Debug.Log("Creature Name: " + StatMessaging.GetCreatureName(asset));
                        Debug.Log("Creature Cid:  " + asset.Creature.CreatureId);
                        Debug.Log("Creature Scale:" + asset.CreatureScale.ToString());
                        Debug.Log("Creature Fix:  " + offsetValue);
                        Debug.Log("Creature SBFix:" + asset.CreatureScale*offsetValue);
                        Debug.Log("Creature HHFix:" + asset.HookHead.position.y * offsetValue);
                        Debug.Log("Creature Offset: " + calculateYPos(asset, true));
                    }
                }
                */

                foreach (CreatureBoardAsset asset in CreaturePresenter.AllCreatureAssets)
                {
                    try
                    {
                        GameObject creatureBlock = GameObject.Find("Effect:"+asset.Creature.CreatureId + ".StatesBlock");
                        if (creatureBlock != null)
                        {

                            creatureBlock.transform.rotation = Quaternion.LookRotation(creatureBlock.transform.position - Camera.main.transform.position);

                            TextMeshPro creatureStateText = creatureBlock.GetComponent<TextMeshPro>();
                            if (creatureStateText == null) { creatureStateText = creatureBlock.AddComponent<TextMeshPro>(); }
                            creatureStateText.transform.rotation = creatureBlock.transform.rotation;
                            creatureStateText.transform.position = new Vector3(asset.CreatureRoot.transform.position.x, calculateYPos(asset) + creatureStateText.preferredHeight, asset.CreatureRoot.transform.position.z);
                        }
                    }
                    catch (Exception) { }
                }


                while (backlogChangeQueue.Count > 0)
                {
                    StatMessaging.Change tempChange = backlogChangeQueue.Peek();
                    CreatureBoardAsset asset;
                    CreaturePresenter.TryGetAsset(tempChange.cid, out asset);

                    if (asset != null)
                    {
                        if (asset.CreatureLoaders[0].LoadedAsset == null)
                        {
                            //still not ready
                            break;
                        }
                        else
                        {
                            //pop the next one out of the queue
                            backlogChangeQueue.Dequeue();

                            TextMeshPro tempTMP = null;
                            GameObject tempGO = null;

                            Debug.Log("States Plugin: Processing Queued Request = Creature ID: " + tempChange.cid + ", Action: " + tempChange.action + ", Key: " + tempChange.key + ", Previous Value: " + tempChange.previous + ", New Value: " + tempChange.value);

                            createNewCreatureStateText(out tempTMP, out tempGO, asset);
                            populateCreatureStateText(tempTMP, tempChange, asset);
                        }
                    }
                }
            }
        }

        public void HandleRequest(StatMessaging.Change[] changes)
        {
            foreach (StatMessaging.Change change in changes)
            {
                if (change == null)
                {
                    Debug.Log("States Plugin: ERROR: StatMessaging change was NULL;");
                }
                else
                {
                    Debug.Log("States Plugin: Handle Request, Creature ID: " + change.cid + ", Action: " + change.action + ", Key: " + change.key + ", Previous Value: " + change.previous + ", New Value: " + change.value);
                }
                if (change.key == StatesPlugin.Guid)
                {
                    try
                    {
                        CreatureBoardAsset asset;
                        CreaturePresenter.TryGetAsset(change.cid, out asset);
                        if (asset != null)
                        {
                            switch (change.action)
                            {
                                case StatMessaging.ChangeType.added:
                                case StatMessaging.ChangeType.modified:
                                    Debug.Log("States Plugin: Updating States Block for creature '" + change.cid + "'");
                                    backlogChangeQueue.Enqueue(change);
                                    break;

                                case StatMessaging.ChangeType.removed:
                                    Debug.Log("States Plugin: Removing States Block for creature '" + change.cid + "'");
                                    GameObject.Destroy(GameObject.Find("Effect:"+asset.Creature.CreatureId + ".StatesBlock"));
                                    break;
                            }
                        }
                        else
                        {
                            Debug.Log("States Plugin: Received States update for invalid asset (" + change.cid + ")");
                        }
                    }
                    catch (Exception x) { Debug.Log("Exception: " + x); }
                }
            }
        }

        private void createNewCreatureStateText(out TextMeshPro creatureStateText, out GameObject creatureBlock, CreatureBoardAsset asset)
        {

            if (GameObject.Find("Effect:"+asset.Creature.CreatureId + ".StatesBlock") != null)
            {
                // Use Existing States Text
                Debug.Log("States Plugin: StatesText already exists.");
                creatureBlock = GameObject.Find("Effect:" + asset.Creature.CreatureId + ".StatesBlock"); ;
                creatureStateText = creatureBlock.GetComponentInChildren<TextMeshPro>();
                return;
            }

            // Create New States Text 
            Debug.Log("States Plugin: Creating CreatureBlock GameObject");
            creatureBlock = new GameObject("Effect:" + asset.Creature.CreatureId + ".StatesBlock");

            Debug.Log("States Plugin: Checking Source");
            if (asset != null)
            {
                if (asset.CreatureRoot != null)
                {
                    if (asset.CreatureRoot.transform != null)
                    {
                        Debug.Log("States Plugin: Creating Creature Block");

                        Vector3 pos = asset.CreatureRoot.transform.position;
                        Vector3 rot = asset.CreatureRoot.transform.eulerAngles;

                        if (creatureBlock != null)
                        {
                            Debug.Log("States Plugin: Applying Creature Block Position And Rotation");

                            creatureBlock.transform.position = Vector3.zero;
                            creatureBlock.transform.eulerAngles = Vector3.zero;

                            Debug.Log("States Plugin: Creating StatesText (TextMeshPro)");

                            creatureStateText = creatureBlock.AddComponent<TextMeshPro>();

                            Debug.Log("States Plugin: Applying StatesText (TextMeshPro) Properties");
                            creatureStateText.transform.position = Vector3.zero;
                            creatureStateText.transform.eulerAngles = Vector3.zero;
                            creatureStateText.textStyle = TMP_Style.NormalStyle;
                            creatureStateText.enableWordWrapping = true;
                            creatureStateText.alignment = TextAlignmentOptions.Center;
                            creatureStateText.autoSizeTextContainer = true;
                            creatureStateText.color = baseColor.Value;
                            creatureStateText.fontSize = 1;
                            creatureStateText.fontWeight = FontWeight.Bold;
                            creatureStateText.isTextObjectScaleStatic = true;
                        }
                        else
                        {
                            Debug.Log("States Plugin: Newly Create CreatureBlock Is Null");
                            creatureStateText = null;
                        }
                    }
                    else
                    {
                        Debug.Log("States Plugin: Invalid Transform Provided");
                        creatureStateText = null;
                    }
                }
                else
                {
                    Debug.Log("States Plugin: Invalid Creature Root Provided");
                    creatureStateText = null;
                }
            }
            else
            {
                Debug.Log("States Plugin: Invalid Asset Provided");
                creatureStateText = null;
            }
        }

        private void populateCreatureStateText(TextMeshPro creatureStateText, StatMessaging.Change change, CreatureBoardAsset asset)
        {
            if (creatureStateText == null) { return; }

            Debug.Log("States Plugin: Populating StatesText (TextMeshPro)");

            creatureStateText.autoSizeTextContainer = false;
            string content = change.value.Replace(",", "\r\n");
            if (colorizations.ContainsKey("<Default>")) { content = "<Default>" + content; }
            creatureStateText.richText = true;
            creatureStateText.fontSize = baseSize;
            creatureStateText.alignment = TextAlignmentOptions.Bottom;
            foreach (KeyValuePair<string, string> replacement in colorizations)
            {
                content = content.Replace(replacement.Key, replacement.Value);
            }

            creatureStateText.text = content;
            creatureStateText.autoSizeTextContainer = true;

            creatureStateText.transform.position = new Vector3(asset.CreatureRoot.transform.position.x, calculateYPos(asset) + creatureStateText.preferredHeight, asset.CreatureRoot.transform.position.z);
        }

        private float calculateYPos(CreatureBoardAsset asset, bool diagnostic = false)
        {
            float yMin = 1000.0f;
            float yMax = 0.0f;

            try
            {
                switch(offsetMethod)
                {
                    case OffsetMethod.fixedOffset:
                        yMin = 0;
                        yMax = 1;
                        break;
                    case OffsetMethod.baseScaleFixedOffet:
                        yMin = 0;
                        yMax = 1 * asset.CreatureScale;
                        break;
                    case OffsetMethod.headHookMultiplierOffset:
                        yMin = 0;
                        yMax = asset.HookHead.position.y;
                        break;
                    case OffsetMethod.boundsOffset:
                        if (asset.CreatureLoaders[0].LoadedAsset.GetComponentInChildren<MeshFilter>() != null)
                        {
                            foreach (MeshFilter mf in asset.CreatureLoaders[0].LoadedAsset.GetComponentsInChildren<MeshFilter>())
                            {
                                Bounds bounds = mf.mesh.bounds;
                                if (bounds != null)
                                {
                                    yMin = Math.Min(yMin, bounds.min.y);
                                    yMax = Math.Max(yMax, bounds.max.y);
                                    if (diagnostic) { Debug.Log("Mesh " + mf.mesh.name + ": Bounds " + mf.mesh.bounds.min.y + "->" + mf.mesh.bounds.max.y+" | Max: "+yMax); }
                                }
                            }
                            foreach (MeshRenderer mr in asset.CreatureLoaders[0].LoadedAsset.GetComponentsInChildren<MeshRenderer>())
                            {
                                Bounds bounds = mr.bounds;
                                if (bounds != null)
                                {
                                    yMin = Math.Min(yMin, bounds.min.y);
                                    yMax = Math.Max(yMax, bounds.max.y);
                                    if (diagnostic) { Debug.Log("Mesh " + mr.name + ": Bounds " + mr.bounds.min.y + "->" + mr.bounds.max.y + " | Max: " + yMax); }
                                }
                            }
                            foreach (SkinnedMeshRenderer smr in asset.CreatureLoaders[0].LoadedAsset.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {
                                Bounds bounds = smr.bounds;
                                if (bounds != null)
                                {
                                    yMin = Math.Min(yMin, bounds.min.y);
                                    yMax = Math.Max(yMax, bounds.max.y);
                                    if (diagnostic) { Debug.Log("Mesh " + smr.name + ": Bounds " + smr.bounds.min.y + "->" + smr.bounds.max.y + " | Max: " + yMax); }
                                }
                            }
                        }

                        // Legacy CMP Support
                        GameObject cmpGO = GameObject.Find("CustomContent:" + asset.Creature.CreatureId);
                        if (cmpGO != null)
                        {
                            if (cmpGO.GetComponentInChildren<MeshFilter>() != null)
                            {
                                foreach (MeshFilter mf in cmpGO.GetComponentsInChildren<MeshFilter>())
                                {
                                    Bounds bounds = cmpGO.GetComponentInChildren<MeshFilter>().mesh.bounds;
                                    if (bounds != null)
                                    {
                                        yMin = Math.Min(yMin, bounds.min.y);
                                        yMax = Math.Max(yMax, bounds.max.y);
                                        if (diagnostic) { Debug.Log("Mesh " + cmpGO.GetComponentInChildren<MeshFilter>().mesh.name + ": Bounds " + cmpGO.GetComponentInChildren<MeshFilter>().mesh.bounds.min.y + "->" + cmpGO.GetComponentInChildren<MeshFilter>().mesh.bounds.max.y + " | Max: " + yMax); }
                                    }
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception x) 
            {
                Debug.Log("States Plugin: Exception");
                Debug.LogException(x);
                yMin = 0;
                yMax = 1;
            }

            return yMax * offsetValue;
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
