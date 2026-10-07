using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace Tolmach
{
    [BepInPlugin(PluginId, "Tolmach", PluginVersion)]
    [BepInDependency("shudnal.CircletExtended", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ru.ivest.portalpreview", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("MidnightsFX.AchievementEnabler", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("MidnightsFX.ValheimCommunityPatch", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("gravebear.odinsfoodbarrels", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("upgrade_world", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("goldenrevolver.SmartWishbone", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Rock3t.RecipeSync", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AzuAntiArthriticCrafting", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.muji.DynamicStorageAmmunitionPiles", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.modularvalheim.DynamicStorageForge", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.muji.DynamicStorageMaterials", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.muji.DynamicStorageMeatRack", BepInDependency.DependencyFlags.SoftDependency)]
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
    [BepInDependency("org.bepinex.plugins.passivepowers", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.odinssteelworks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.maxsch.valheim.vnei", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("sighsorry.InventorySlots", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Insanity.Culinary_Horizons", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Dreanegade.Hunter_Legacy", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Dreanegade.Magic_Supremacy", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("yay.spikehimself.xportal", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("petri.valheim.takeallcooked", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("obelisk.valheim.movebuildpieces", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.FoodDurationMultiplier", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("bossadd", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("nightofgames.huginn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("balrond.astafaraios.BalrondAmazingNature", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("balrond.astafaraios.ZBalrondArsenalReborn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("balrond.astafaraios.BalrondConstructions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("balrond.astafaraios.BalrondFurnitureReborn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("balrond.astafaraios.BalrondDualMastery", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("balrond.astafaraios.BalrondLightkeeper", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Dreanegade.Eternal_Legends", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.FirstPersonMode", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt_and_ValheimPlusDevs.PerfectPlacement", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("nickpappas.locationplacementaccelerator", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AzuWorkbenchTweaks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.creaturelevelcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("M2Valheim.SocialSystem", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("maxfoxgaming.betterbeehives", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("shudnal.MyLittleUI", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("MidnightsFX.NetworkPerformanceSystem", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("goldenrevolver.quick_stack_store", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("elg.QuickTeleport", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("zl.smelterupgrades", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.solid-hitboxes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.spearfishing", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.TorchesAreFires", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Transmog", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("MidnightsFX.InfiniteFire", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("blacks7ar.WieldEquipmentWhileSwimming", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Therzie.Armory", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("infinity_hammer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("server_devcommands", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("world_edit_commands", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Bento.MissingPieces", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.RecipeDescriptionExpansion", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("_shudnal.ConfigurationManager", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("_shudnal.ConditionalConfigSync", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("MidnightsFX.AsyncSave", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginId = "muratovnik.tolmach";
        public const string PluginVersion = "0.9.0";
        private static ManualLogSource Log;
        private static readonly List<string> Notes = new List<string>();
        private static RuntimeReport report;
        private Harmony harmony;
        private bool started;

        private void Awake()
        {
            Log = Logger;
            Notes.Clear();
            report = new RuntimeReport();
            DisplayPatches.Reset();
            CreatureNameDisplay.Reset();
            NorsemenNames.Reset();
            ContainerNameDisplay.Reset();
            TextEngine.IsRussian = false;
            Logger.LogInfo("Runtime report session: " + report.SessionId);
            WriteReport(); // Invalidate a previous successful report before configuration or I/O can fail.
            try { Initialize(); }
            catch (Exception e)
            {
                report.Status = RuntimeStatus.Failed;
                Notes.Add("Initialization failed: " + e.GetType().Name + ". See the current BepInEx log.");
                Logger.LogError("Initialization failed: " + e);
                WriteReport(); // Preserve module warnings before cleanup clears runtime state.
                Cleanup();
            }
        }

        private void Initialize()
        {
            bool enabled = Config.Bind("General", "Enabled", true, "Enable Russian translations. Restart the game after changing this setting.").Value;
            if (!enabled)
            {
                report.Status = RuntimeStatus.Disabled;
                Logger.LogInfo("Disabled in configuration.");
                WriteReport();
                return;
            }
            // Replaces AllowOtherVersions (default false), whose saved value would have kept the old behavior.
            bool onlyAudited = Config.Bind("Compatibility", "OnlyAuditedVersions", false,
                "Apply code and screen adapters only to the catalog's checked mod versions. Off: try known strings on other versions. Warnings describe missing known adapter targets, not every untranslated string. Native dictionaries work either way.").Value;
            PersistentUi.StandardMapLabels = Config.Bind("Display", "TranslateStandardMapLabels", true,
                "Translate exact standard map labels on screen only; stored names remain unchanged. A custom name identical to a standard label is displayed translated too. Disable to keep all map names as entered. Restart required.").Value;
            string directory = Path.Combine(Path.GetDirectoryName(Info.Location), "catalog");
            if (!Directory.Exists(directory))
            {
                report.Status = RuntimeStatus.MissingCatalog;
                Notes.Add("Missing catalog directory next to plugin DLL. Reinstall the complete package.");
                Logger.LogError("Missing catalog directory next to plugin DLL.");
                WriteReport();
                return;
            }
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
                    }, onlyAudited, out reason);
                    Notes.Add(module.id + ": " + reason);
                    if (!loaded) continue;
                    TextEngine.Modules.Add(module.id, module);
                }
                catch (Exception e)
                {
                    string warning = Path.GetFileName(path) + ": catalog skipped: " + e.GetType().Name;
                    Notes.Add(warning);
                    Warn(warning + " " + e.Message);
                }
            }
            harmony = new Harmony(PluginId);
            GameItems.Install(harmony);
            RawDisplay.Initialize();
            // Fallback only: once the game's Localization exists, its GetSelectedLanguage decides.
            LocalizationBridge.Install(harmony, RuntimeAccess.ExactType("Localization"), delegate { return UnityEngine.PlayerPrefs.GetString("language", "English"); });
            CreatureNameDisplay.Install(harmony, RuntimeAccess.ExactType("Character"), RuntimeAccess.ExactType("Tameable"), RuntimeAccess.ExactType("ZNetScene"));
            NorsemenNames.Install(harmony);
            ContainerNameDisplay.Install(harmony, RuntimeAccess.ExactType("Container"));
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
            report.Status = RuntimeStatus.Active;
            Logger.LogInfo("Loaded " + TextEngine.Modules.Count + " translation modules. Runtime coverage report: Tolmach.runtime.txt in BepInEx/config.");
            WriteReport();
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
            report.Status = RuntimeStatus.Stopped;
            WriteReport();
            Cleanup();
        }
        private void Cleanup()
        {
            NativeAdapters.Unsubscribe();
            if (harmony != null) harmony.UnpatchSelf();
            PersistentUi.Reset();
            LocalizationBridge.Reset();
            RawDisplay.Reset();
            CreatureNameDisplay.Reset();
            NorsemenNames.Reset();
            ContainerNameDisplay.Reset();
            DisplayPatches.Reset();
            GameItems.Reset();
            TextEngine.Modules.Clear();
            started = false;
        }
        internal static void Warn(string text)
        {
            if (Log != null) Log.LogWarning(text);
        }
        internal static void WriteReport()
        {
            if (report == null) return;
            try
            {
                report.Write(Path.Combine(Paths.ConfigPath, "Tolmach.runtime.txt"), TextEngine.IsRussian, TextEngine.Modules.Values, Notes);
            }
            catch (Exception e) { Warn("Could not write runtime report: " + e.GetType().Name); }
        }
    }
}
