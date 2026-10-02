using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class CatalogTests : TestContextBase
    {
        public static IEnumerable<string> Modules { get { return ModuleIds(); } }
        [Test]
        public void SnapshotHasAllExpectedModules()
        {
            Assert.That(ModuleIds(), Is.Not.Empty);
            Assert.That(ModuleIds(), Is.EquivalentTo(Fixture<List<Identity>>("snapshot-bindings.json").Select(x => x.id)));
        }
        [Test]
        public void CatalogFilesCarryThePackPrefix()
        {
            // A catalog named after a mod ("SleepSkip.json") is read by that mod's LocalizationManager.
            foreach (string path in Directory.GetFiles(Path.Combine(Root, "catalog")))
                Assert.That(Path.GetFileName(path), Is.EqualTo(CatalogLoader.FilePrefix + CatalogLoader.Read(path).id + ".json"));
        }
        [Test]
        public void GameModulesBindToTheGameVersion()
        {
            PluginIdentity game = CatalogLoader.GameIdentity(typeof(FixtureGame.Version));
            Assert.That(game.Guid, Is.EqualTo(CatalogLoader.GameGuid));
            Assert.That(game.Version, Is.EqualTo(new Version(1, 0, 16)));
            Assert.That(game.Assembly, Is.EqualTo(typeof(FixtureGame.Version).Assembly));
            Assert.That(CatalogLoader.GameIdentity(null), Is.Null);
            Assert.That(CatalogLoader.GameIdentity(typeof(FixtureGame.GameVersion)), Is.Null, "A type without CurrentVersion is not the game.");
            Module m = new Module { id = "Captions", assembly = game.Assembly.GetName().Name, pluginVersion = "1.0.16" };
            m.guids.Add(CatalogLoader.GameGuid);
            string reason;
            Assert.That(CatalogLoader.TryBind(m, guid => guid == CatalogLoader.GameGuid ? game : null, false, out reason), Is.True, reason);
            Assert.That(m.ExactVersion, Is.True);
            m.assembly = "assembly_valheim";
            Assert.That(CatalogLoader.TryBind(m, guid => guid == CatalogLoader.GameGuid ? game : null, false, out reason), Is.False, "Another assembly declaring Version is not the game.");
        }
        [Test]
        public void InvalidRawDisplayDataIsRejected()
        {
            Func<Module> valid = () =>
            {
                Module m = CatalogLoader.Read(Catalog("StructureTweaks"));
                m.terms["ship"] = new Dictionary<string, string> { { "Karve", "Карви" } };
                m.rawPatterns.Add(new PatternSpec { source = "Plan {0}", target = "Чертёж ({0})", arguments = new Dictionary<string, string> { { "0", "term:ship" } } });
                return m;
            };
            Assert.DoesNotThrow(() => CatalogLoader.Validate(valid()));
            Module bare = valid(); bare.rawPatterns.Add(new PatternSpec { source = " {0} ", target = "{0}" });
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(bare), "A raw pattern must contain fixed text.");
            Module unknown = valid(); unknown.rawPatterns[0].arguments["0"] = "term:missing";
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(unknown));
            Module message = valid(); message.rawPatterns[0].arguments["0"] = "message";
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(message), "Raw patterns take text and term arguments only.");
            Module empty = valid(); empty.terms["empty"] = new Dictionary<string, string>();
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(empty));
            Module blank = valid(); blank.rawTexts["Bleeding"] = "";
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(blank));
        }
        [Test]
        public void InvalidConfigTextRulesAreRejected()
        {
            Func<Module> valid = () =>
            {
                Module m = CatalogLoader.Read(Catalog("TakeAllCooked"));
                Assert.That(m.configTexts, Is.Not.Empty, "TakeAllCooked carries the config text rule");
                return m;
            };
            Assert.DoesNotThrow(() => CatalogLoader.Validate(valid()));
            Module empty = valid(); empty.configTexts[0].values.Clear();
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(empty), "A rule without values replaces nothing.");
            Module blank = valid(); blank.configTexts[0].values["Take all cooked"] = "";
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(blank));
            Module unnamed = valid(); unnamed.configTexts[0].method = "";
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(unnamed));
        }
        [Test]
        public void CatalogShapeIsEnforcedWhileReading()
        {
            string unchanged = Rewritten("TakeAllCooked", d => { });
            try { Assert.DoesNotThrow(() => CatalogLoader.Read(unchanged), "The rewritten but unchanged catalog loads."); }
            finally { File.Delete(unchanged); }
            AssertRejected("TakeAllCooked", d => d["patterns"] = JValue.CreateNull(), "A section may be absent, never null.");
            AssertRejected("TakeAllCooked", d => d["codeAssembly"] = JValue.CreateNull(), "An optional helper name cannot be null.");
            AssertRejected("ConditionalConfigSync", d => d["patterns"][0]["singleLine"] = JValue.CreateNull(), "A boolean flag cannot be null.");
            AssertRejected("TakeAllCooked", d => d["configTexts"][0]["values"] = JValue.CreateNull(), "Nor a member of a rule.");
            AssertRejected("TakeAllCooked", d => d["rawText"] = new JObject(), "A misspelt section would drop its translations.");
            AssertRejected("Valheim", d => d["nameAliases"] = JValue.CreateNull(), "A name alias table cannot be null.");
            AssertRejected("TakeAllCooked", d => d["configTexts"][0]["value"] = new JObject(), "Members of a rule are checked too.");
            AssertRejected("CreatureLevelControl", d => d["cllc"]["settings"].First.First["tooltip"] = "x", "And those of the CLLC table.");
        }
        private static string Rewritten(string id, Action<JObject> edit)
        {
            JObject data = JObject.Parse(File.ReadAllText(Catalog(id)));
            edit(data);
            string scratch = Path.GetTempFileName();
            File.WriteAllText(scratch, data.ToString());
            return scratch;
        }
        private static void AssertRejected(string id, Action<JObject> edit, string because)
        {
            string scratch = Rewritten(id, edit);
            try { Assert.Throws<JsonSerializationException>(() => CatalogLoader.Read(scratch), because); }
            finally { File.Delete(scratch); }
        }
        [TestCaseSource("Modules")]
        public void ActualBinderAcceptsSnapshotIdentity(string id)
        {
            Module m = LoadModule(id);
            Assert.That(m.ExactVersion, Is.True);
            Assert.That(m.UiAllowed, Is.True);
            Assert.That(m.RuntimeAssembly.GetName().Name, Is.EqualTo(m.assembly));
            if (m.codeAssembly.Length != 0) Assert.That(m.RuntimeCodeAssembly.GetName().Name, Is.EqualTo(m.codeAssembly));
        }
        private static Module HelperModule()
        {
            Module m = new Module { id = "helper", assembly = typeof(CatalogTests).Assembly.GetName().Name,
                codeAssembly = typeof(TextEngine).Assembly.GetName().Name, pluginVersion = "1.0.0", version = "1.0.0" };
            m.guids.Add("fixture.helper"); m.namespaces.Add("Tolmach"); m.texts["Example"] = "Пример";
            return m;
        }
        private static PluginIdentity HelperPlugin()
        { return new PluginIdentity("fixture.helper", typeof(CatalogTests).Assembly, new Version(1, 0, 0)); }
        [Test]
        public void DisplayHelperRequiresARealDirectReferenceAndKeepsPluginIdentity()
        {
            Module m = HelperModule(); string reason;
            Assert.That(typeof(CatalogTests).Assembly.GetReferencedAssemblies().Select(n => n.FullName),
                Does.Contain(typeof(TextEngine).Assembly.GetName().FullName), "Fixture uses a real compile-time plugin-to-helper reference.");
            Assert.That(CatalogLoader.TryBind(m, guid => HelperPlugin(), false, out reason), Is.True, reason);
            Assert.That(m.RuntimeAssembly, Is.SameAs(typeof(CatalogTests).Assembly));
            Assert.That(m.RuntimeCodeAssembly, Is.SameAs(typeof(TextEngine).Assembly));
            Assert.That(m.UiAllowed, Is.True);
            m.codeAssembly = typeof(CatalogTests).Assembly.GetName().Name;
            Assert.That(CatalogLoader.TryBind(m, guid => HelperPlugin(), false, out reason), Is.True);
            Assert.That(m.RuntimeCodeAssembly, Is.Null, "Loaded assemblies are insufficient without a direct reference.");
            Assert.That(m.UiAllowed, Is.False);
            Assert.That(m.RuntimeAssembly, Is.SameAs(typeof(CatalogTests).Assembly));
            Assert.That(m.Table.Translate("Example"), Is.EqualTo("Пример"), "The native dictionary remains usable.");
        }
        [Test]
        public void MissingMismatchedAndAmbiguousLoadedHelpersFailClosed()
        {
            Module m = HelperModule(); string reason;
            Func<IEnumerable<Assembly>> missing = () => new[] { typeof(CatalogTests).Assembly };
            Assert.That(CatalogLoader.TryBind(m, guid => HelperPlugin(), false, a => a.GetReferencedAssemblies(), missing, out reason), Is.True);
            Assert.That(m.UiAllowed, Is.False); Assert.That(m.RuntimeCodeAssembly, Is.Null);
            AssemblyName expected = typeof(TextEngine).Assembly.GetName();
            AssemblyName different = new AssemblyName(expected.FullName) { Version = new Version(99, 0, 0, 0) };
            Assembly wrong = AppDomain.CurrentDomain.DefineDynamicAssembly(different, AssemblyBuilderAccess.Run);
            Assert.That(CatalogLoader.TryBind(m, guid => HelperPlugin(), false, a => a.GetReferencedAssemblies(), () => new[] { wrong }, out reason), Is.True);
            Assert.That(m.UiAllowed, Is.False, "A matching simple name with another full identity is insufficient.");
            Assembly duplicate = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(expected.FullName), AssemblyBuilderAccess.Run);
            Assert.That(CatalogLoader.TryBind(m, guid => HelperPlugin(), false, a => a.GetReferencedAssemblies(),
                () => new[] { typeof(TextEngine).Assembly, duplicate }, out reason), Is.True);
            Assert.That(m.UiAllowed, Is.False); Assert.That(m.RuntimeCodeAssembly, Is.Null);
        }
        [Test]
        public void HelperBindingHonorsVersionPolicyAndClearsBothAssembliesOnFailedRebind()
        {
            Module m = HelperModule(); m.pluginVersion = "1.1.0"; string reason;
            Func<IEnumerable<Assembly>> loaded = () => new[] { typeof(TextEngine).Assembly };
            Assert.That(CatalogLoader.TryBind(m, guid => HelperPlugin(), false, a => a.GetReferencedAssemblies(), loaded, out reason), Is.True);
            Assert.That(m.UiAllowed, Is.True); Assert.That(m.ExactVersion, Is.False);
            Assert.That(CatalogLoader.TryBind(m, guid => HelperPlugin(), true, a => a.GetReferencedAssemblies(), loaded, out reason), Is.True);
            Assert.That(m.UiAllowed, Is.False); Assert.That(m.RuntimeCodeAssembly, Is.Not.Null);
            Assert.That(CatalogLoader.TryBind(m, guid => null, false, out reason), Is.False);
            Assert.That(m.RuntimeAssembly, Is.Null); Assert.That(m.RuntimeCodeAssembly, Is.Null);
            Assert.That(m.Table, Is.Null); Assert.That(m.ExactVersion, Is.False); Assert.That(m.UiAllowed, Is.False);
            Module ordinary = HelperModule(); ordinary.codeAssembly = "";
            Assert.That(CatalogLoader.TryBind(ordinary, guid => HelperPlugin(), false,
                a => { throw new InvalidOperationException("Ordinary modules need no helper resolution."); }, loaded, out reason), Is.True);
            Assert.That(ordinary.UiAllowed, Is.True); Assert.That(ordinary.RuntimeCodeAssembly, Is.Null);
        }
        [TestCaseSource("Modules")]
        public void PlaceholderAndMarkupPreservation(string id)
        {
            Module m = CatalogLoader.Read(Catalog(id));
            var pairs = m.words.Select(p => new KeyValuePair<string, string>(m.englishWords[p.Key], p.Value))
                .Concat(m.texts).Concat(m.mapLabels).Concat(m.rawTexts).Concat(m.terms.Values.SelectMany(t => t))
                .Concat(m.patterns.Concat(m.rawPatterns).Select(p => new KeyValuePair<string, string>(p.source, p.target)))
                .Concat(m.literals.SelectMany(p => p.values));
            const string tokens = @"\{\d+[^{}]*\}|\$\d+|\$[A-Za-z_]\w*|</?[^>\n]+>";
            foreach (var pair in pairs)
            {
                Assert.That(Regex.Matches(pair.Value, tokens).Cast<Match>().Select(x => x.Value),
                    Is.EquivalentTo(Regex.Matches(pair.Key, tokens).Cast<Match>().Select(x => x.Value)), pair.Key);
                Assert.That(Regex.Matches(pair.Value, @"</?[^>\n]+>").Cast<Match>().Select(x => x.Value),
                    Is.EqualTo(Regex.Matches(pair.Key, @"</?[^>\n]+>").Cast<Match>().Select(x => x.Value)), pair.Key);
            }
        }
        [TestCase("1.37", "1.37.0", true)]
        [TestCase("1.37.0", "1.37.0.0", true)]
        [TestCase("1.37.0", "1.37.0.1", false)]
        [TestCase("1.37.0", "1.38.0", false)]
        [TestCase("1.37.0", "2.37.0", false)]
        public void VersionNormalizationKeepsNonzeroComponents(string expected, string actual, bool same)
        {
            Assert.That(CatalogLoader.SameVersion(new Version(expected), new Version(actual)), Is.EqualTo(same));
        }
        [Test]
        public void IdentityFailuresRejectTheModuleAndAnotherVersionIsReported()
        {
            Identity id = Fixture<List<Identity>>("snapshot-bindings.json").Single(x => x.id == "StructureTweaks");
            Func<string, PluginIdentity> registry = guid => guid == id.guid ? Installed(id) : null;
            string reason;
            Module absent = CatalogLoader.Read(Catalog(id.id));
            Assert.That(CatalogLoader.TryBind(absent, guid => null, false, out reason), Is.False);
            Module wrongGuid = CatalogLoader.Read(Catalog(id.id)); wrongGuid.guids[0] = "missing.guid";
            Assert.That(CatalogLoader.TryBind(wrongGuid, registry, false, out reason), Is.False);
            Module wrongAssembly = CatalogLoader.Read(Catalog(id.id)); wrongAssembly.assembly = "Unrelated.Assembly";
            Assert.That(CatalogLoader.TryBind(wrongAssembly, registry, false, out reason), Is.False);
            Module changed = CatalogLoader.Read(Catalog(id.id)); changed.pluginVersion = "1.99.0";
            Assert.That(CatalogLoader.TryBind(changed, registry, false, out reason), Is.True);
            Assert.That(changed.UiAllowed, Is.True, "Another version keeps its adapters by default.");
            Assert.That(changed.ExactVersion, Is.False);
            Assert.That(changed.Warnings, Has.Some.StartsWith("Expected plugin 1.99.0"));
            Assert.That(reason, Does.StartWith("loaded; different plugin version"));
            Assert.That(CatalogLoader.TryBind(changed, registry, true, out reason), Is.True);
            Assert.That(changed.UiAllowed, Is.False, "OnlyAuditedVersions keeps native dictionaries only.");
            Assert.That(CatalogLoader.TryBind(changed, guid => null, false, out reason), Is.False);
            Assert.That(changed.RuntimeAssembly, Is.Null, "Failed rebind must not retain the old target.");
            Assert.That(changed.RuntimeCodeAssembly, Is.Null);
            Assert.That(changed.Table, Is.Null);
            Assert.That(changed.UiAllowed, Is.False);
            Assert.That(changed.ExactVersion, Is.False);
        }
        [Test]
        public void InvalidCatalogDataIsRejectedByProductionLoader()
        {
            Module m = CatalogLoader.Read(Catalog("StructureTweaks"));
            m.patterns.Add(new PatternSpec { source = "Hi {0}", target = "Hi {0}", arguments = new Dictionary<string, string> { { "0", "arbitrary" } } });
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(m));
            Module replacing = CatalogLoader.Read(Catalog("MissingPieces"));
            Assert.That(replacing.replaceNative, Is.Not.Empty);
            replacing.replaceNative.Add("not_a_word");
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(replacing), "A replaceNative key must be one of the words.");
            string scratch = Path.GetTempFileName();
            try
            {
                File.WriteAllText(scratch, File.ReadAllText(Catalog("StructureTweaks")).Replace("\"id\":", "\"id\":\"duplicate\",\"id\":"));
                Assert.Throws<JsonReaderException>(() => CatalogLoader.Read(scratch));
            }
            finally { File.Delete(scratch); }
        }
        [Test]
        public void MetadataAndAssemblyVersionAgree()
        {
            Assembly assembly = typeof(TextEngine).Assembly;
            Assert.That(assembly.GetName().Version, Is.EqualTo(new Version(Plugin.PluginVersion + ".0")));
            Assert.That(assembly.GetName().Name, Is.EqualTo("Tolmach"));
        }
    }
}
