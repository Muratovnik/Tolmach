using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using Newtonsoft.Json;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    public abstract class TestContextBase
    {
        // Populated by Json.NET from snapshot-bindings.json.
        internal sealed class Identity { public string id = null; public string guid = null; public string assembly = null; public string pluginVersion = null; public string codeAssembly = null; }
        protected sealed class Regression { public string module; public string source; public string expected; }
        protected sealed class Part { public string method; public string literal; public string opaque; }
        protected sealed class Composition { public string name; public List<Part> parts; public string expected; }
        private static readonly Dictionary<string, Assembly> Assemblies = new Dictionary<string, Assembly>();
        private CultureInfo previousCulture;
        protected Harmony Patcher;
        protected static string Root { get { return TestContext.CurrentContext.TestDirectory; } }
        protected static string Catalog(string id) { return Path.Combine(Root, "catalog", CatalogLoader.FilePrefix + id + ".json"); }
        protected static T Fixture<T>(string name)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(Path.Combine(Root, "fixtures", name)));
        }
        protected static IEnumerable<string> ModuleIds()
        {
            return Directory.GetFiles(Path.Combine(Root, "catalog"), "*.json").Select(Path.GetFileNameWithoutExtension)
                .Select(x => x.StartsWith(CatalogLoader.FilePrefix, StringComparison.Ordinal) ? x.Substring(CatalogLoader.FilePrefix.Length) : x)
                .OrderBy(x => x, StringComparer.Ordinal);
        }
        internal static PluginIdentity Installed(Identity id)
        {
            Assembly assembly;
            if (!Assemblies.TryGetValue(id.assembly, out assembly))
            {
                assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(id.assembly), AssemblyBuilderAccess.Run);
                Assemblies.Add(id.assembly, assembly);
            }
            return new PluginIdentity(id.guid, assembly, new Version(id.pluginVersion));
        }
        private static IEnumerable<AssemblyName> SnapshotReferences(Identity snapshot)
        {
            if (String.IsNullOrEmpty(snapshot.codeAssembly)) return new AssemblyName[0];
            PluginIdentity helper = Installed(new Identity { assembly = snapshot.codeAssembly, guid = snapshot.guid, pluginVersion = snapshot.pluginVersion });
            return new[] { helper.Assembly.GetName() };
        }
        protected static Module LoadModule(string id)
        {
            Module m = CatalogLoader.Read(Catalog(id));
            Identity snapshot = Fixture<List<Identity>>("snapshot-bindings.json").Single(x => x.id == id);
            string reason;
            Assert.That(CatalogLoader.TryBind(m, guid => guid == snapshot.guid ? Installed(snapshot) : null, false,
                assembly => SnapshotReferences(snapshot), () => AppDomain.CurrentDomain.GetAssemblies(), out reason), Is.True, reason);
            TextEngine.Modules.Add(m.id, m);
            return m;
        }
        [SetUp]
        public void SetUpContext()
        {
            previousCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            TextEngine.Modules.Clear();
            DisplayPatches.Reset(); LocalizationBridge.Reset(); RawDisplay.Reset(); PersistentUi.Reset(); GameItems.Reset();
            ResetGameFakes();
            PersistentUi.StandardMapLabels = true;
            TextEngine.IsRussian = true;
            Patcher = new Harmony("muratovnik.tolmach.tests." + Guid.NewGuid().ToString("N"));
        }
        [TearDown]
        public void TearDownContext()
        {
            try { Patcher.UnpatchSelf(); }
            finally
            {
                NativeAdapters.Unsubscribe();
                TextEngine.Modules.Clear();
                DisplayPatches.Reset(); LocalizationBridge.Reset(); RawDisplay.Reset(); PersistentUi.Reset(); GameItems.Reset();
                ResetGameFakes();
                Thread.CurrentThread.CurrentCulture = previousCulture;
            }
        }
        private static void ResetGameFakes()
        {
            CreatureNameDisplay.Reset();
            NameScene.instance = null;
            BalrondHumanoidRandomizer.Launch.itemSetBuilder = new BalrondHumanoidRandomizer.ItemSetBuilder();
            Localization.ResetForTests(); Minimap.ResetForTests(); FixturePlugin.LocalizeKey.ResetForTests();
        }
        // A test plugin whose code lives in the given namespace of this assembly.
        internal static Module FixtureModule(string id, string ns)
        {
            Module m = new Module { id = id, RuntimeAssembly = Assembly.GetExecutingAssembly(), ExactVersion = true, UiAllowed = true };
            m.namespaces.Add(ns);
            return m;
        }
        // Registers the module and prepares the translation table from its current catalog data.
        internal static Module Activate(Module m)
        {
            m.Table = new TextTable(m);
            TextEngine.Modules.Add(m.id, m);
            return m;
        }
    }
}
