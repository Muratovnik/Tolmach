using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;

namespace Tolmach
{
    [BepInPlugin(PluginId, "Tolmach", PluginVersion)]
    [BepInDependency("com.ValheimModding.NewtonsoftJsonDetector")]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("obelisk.ru.localization", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.jumpingmushroom.portallines", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("xyz.alcan.comfortcalc", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("shudnal.TradersExtended", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.groups", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("dev.local.valheimvisualenhanced", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("RustyMods.Norsemen", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.maxsch.valheim.DynamicStoragePiles", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("cartur.feedingtrough", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.maxsch.valheim.HammerTime", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.BuildCameraCHE", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("nex.SpeedyPaths", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.foodstaminaregen", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("warpalicious.More_World_Locations_AIO", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("structure_tweaks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("WackyMole.WackysDatabase", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.SNEAKer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.CraftyCarts", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.RenegadeVikings", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("marlthon.SeaAnimals", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.jotunn.trustymod", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("digitalroot.mods.GoldBars", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("randyknapp.mods.equipmentandquickslots", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.SeedBed", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AzuAutoStore", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.mining", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.Fermenting", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ishid4.mods.betterarchery", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.SleepSkip", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("balrond.astafaraios.BalrondShipyard", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.OreMines", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("marlthon.AirAnimals", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Therzie.Warfare", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("sighsorry.UsefulRunestones", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.Recycle_N_Reclaim", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.milkwyzard.ExpertExplorer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("advize.PlantEverything", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AzuCraftyBoxes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("vapok.mods.adventurebackpacks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AzuAreaRepair", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("advize.PlantEasily", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("balrond.astafaraios.BalrondHumanoidRandomizer", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginId = "muratovnik.tolmach";
        public const string PluginVersion = "0.3.0";
        private static ManualLogSource Log;
        private static readonly List<string> Notes = new List<string>();
        private Harmony harmony;
        private bool started;

        private void Awake()
        {
            Log = Logger;
            Notes.Clear();
            DisplayPatches.Reset();
            bool enabled = Config.Bind("General", "Enabled", true, "Enable Russian translations. Restart the game after changing this setting.").Value;
            if (!enabled) { Logger.LogInfo("Disabled in configuration."); return; }
            bool allowVersion = Config.Bind("Compatibility", "AllowOtherVersions", false,
                "Allow scoped UI adapters on mod versions different from the audited snapshot. Unverified; use at your own risk. Native dictionaries do not require this option.").Value;
            PersistentUi.StandardMapLabels = Config.Bind("Display", "TranslateStandardMapLabels", true,
                "Translate exact standard map labels on screen only; stored names remain unchanged. A custom name identical to a standard label is displayed translated too. Disable to keep all map names as entered. Restart required.").Value;
            string directory = Path.Combine(Path.GetDirectoryName(Info.Location), "catalog");
            if (!Directory.Exists(directory)) { Logger.LogError("Missing catalog directory next to plugin DLL."); return; }
            foreach (string path in Directory.GetFiles(directory, CatalogLoader.FilePrefix + "*.json").OrderBy(delegate(string s) { return s; }, StringComparer.Ordinal))
            {
                try
                {
                    Module module = CatalogLoader.Read(path);
                    if (!Config.Bind("Modules", module.id, true, "Enable this module. Restart required.").Value)
                    { Notes.Add(module.id + ": disabled by user"); continue; }
                    string reason;
                    bool loaded = CatalogLoader.TryBind(module, delegate(string guid) {
                        if (guid == CatalogLoader.GameGuid) return CatalogLoader.GameIdentity(RuntimeAccess.ExactType("Version"));
                        PluginInfo info;
                        if (!Chainloader.PluginInfos.TryGetValue(guid, out info) || info.Instance == null) return null;
                        return new PluginIdentity(info.Metadata.GUID, info.Instance.GetType().Assembly, info.Metadata.Version);
                    }, allowVersion, out reason);
                    Notes.Add(module.id + ": " + reason);
                    if (!loaded) continue;
                    TextEngine.Modules.Add(module.id, module);
                }
                catch (Exception e) { Warn(Path.GetFileName(path) + ": catalog skipped: " + e.GetType().Name + " " + e.Message); }
            }
            try
            {
                harmony = new Harmony(PluginId);
                RawDisplay.Initialize();
                // Fallback only: once the game's Localization exists, its GetSelectedLanguage decides.
                LocalizationBridge.Install(harmony, RuntimeAccess.ExactType("Localization"), delegate { return UnityEngine.PlayerPrefs.GetString("language", "English"); });
                foreach (Module m in TextEngine.Modules.Values)
                {
                    try { DisplayPatches.Install(harmony, m); }
                    catch (Exception e) { m.Warn("UI adapter initialization: " + e.GetType().Name); }
                }
                ExtraHooks.Install(harmony);
                PersistentUi.Install(harmony);
                NativeAdapters.Subscribe();
                NativeAdapters.Refresh();
                started = true;
                Logger.LogInfo("Loaded " + TextEngine.Modules.Count + " translation modules. Runtime coverage report: Tolmach.runtime.txt in BepInEx/config.");
                WriteReport();
            }
            catch (Exception e)
            {
                Logger.LogError("Initialization failed: " + e);
                NativeAdapters.Unsubscribe();
                if (harmony != null) harmony.UnpatchSelf();
                PersistentUi.Reset();
                LocalizationBridge.Reset();
                RawDisplay.Reset();
                DisplayPatches.Reset();
                TextEngine.Modules.Clear();
            }
        }
        private IEnumerator Start()
        {
            if (!started) yield break;
            yield return null;
            NativeAdapters.Refresh();
            WriteReport();
        }
        private void OnDestroy()
        {
            if (!started) return;
            NativeAdapters.Unsubscribe();
            if (harmony != null) harmony.UnpatchSelf();
            PersistentUi.Reset();
            LocalizationBridge.Reset();
            RawDisplay.Reset();
            DisplayPatches.Reset();
            TextEngine.Modules.Clear();
            started = false;
        }
        internal static void Warn(string text)
        {
            if (Log != null) Log.LogWarning(text);
        }
        internal static void WriteReport()
        {
            try
            {
                StringBuilder s = new StringBuilder();
                s.AppendLine("Tolmach " + PluginVersion);
                s.AppendLine("UTC: " + DateTime.UtcNow.ToString("u"));
                s.AppendLine("Russian active: " + TextEngine.IsRussian);
                s.AppendLine("Installed adapters are NOT proof of every in-game screen being tested.");
                s.AppendLine("No network requests; no player names, account IDs or save contents are collected.");
                foreach (Module m in TextEngine.Modules.Values.OrderBy(delegate(Module x) { return x.id; }))
                {
                    s.AppendLine(m.id + " | package=" + m.version + " | plugin=" + m.pluginVersion + " | exactVersion=" + m.ExactVersion +
                        " | keys=" + m.words.Count + " | nativeKeysAdded=" + m.NativeWords + " | methods inspected=" + m.ScannedMethods + " | UI methods patched=" + m.PatchedMethods);
                    foreach (PatchEvidence p in m.Patches.Values.OrderBy(delegate(PatchEvidence x) { return x.method; }))
                        s.AppendLine("  " + p.method + " | displayCalls=" + p.displayCalls + " | displayFields=" + p.displayFields +
                            " | literals=" + p.literals + " | return=" + p.returnAdapter + " | skippedEH=" + p.skippedBoundaries);
                    foreach (string w in m.Warnings) s.AppendLine("  WARNING: " + w);
                }
                foreach (string note in Notes) s.AppendLine(note);
                File.WriteAllText(Path.Combine(Paths.ConfigPath, "Tolmach.runtime.txt"), s.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { Warn("Could not write runtime report: " + e.GetType().Name); }
        }
    }
}
