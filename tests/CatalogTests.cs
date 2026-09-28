using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
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
            Assert.That(ModuleIds().Count(), Is.EqualTo(37));
            Assert.That(ModuleIds(), Is.EquivalentTo(Fixture<List<Identity>>("snapshot-bindings.json").Select(x => x.id)));
        }
        [TestCaseSource("Modules")]
        public void ActualBinderAcceptsSnapshotIdentity(string id)
        {
            Module m = LoadModule(id);
            Assert.That(m.ExactVersion, Is.True);
            Assert.That(m.UiAllowed, Is.True);
            Assert.That(m.RuntimeAssembly.GetName().Name, Is.EqualTo(m.assembly));
        }
        [TestCaseSource("Modules")]
        public void PlaceholderAndMarkupPreservation(string id)
        {
            Module m = CatalogLoader.Read(Catalog(id));
            var pairs = m.words.Select(p => new KeyValuePair<string, string>(m.englishWords[p.Key], p.Value))
                .Concat(m.texts).Concat(m.mapLabels)
                .Concat(m.patterns.Select(p => new KeyValuePair<string, string>(p.source, p.target)))
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
        public void IdentityAndVersionFailuresCannotSilentlyEnableAdapters()
        {
            Identity id = Fixture<List<Identity>>("snapshot-bindings.json").Single(x => x.id == "StructureTweaks");
            Func<string, PluginIdentity> registry = guid => guid == id.guid ? Installed(id) : null;
            string reason;
            Module absent = CatalogLoader.Read(Catalog(id.id));
            Assert.That(CatalogLoader.TryBind(absent, guid => null, false, out reason), Is.False);
            Module wrongGuid = CatalogLoader.Read(Catalog(id.id)); wrongGuid.guids[0] = "missing.guid";
            Assert.That(CatalogLoader.TryBind(wrongGuid, registry, true, out reason), Is.False);
            Module wrongAssembly = CatalogLoader.Read(Catalog(id.id)); wrongAssembly.assembly = "Unrelated.Assembly";
            Assert.That(CatalogLoader.TryBind(wrongAssembly, registry, true, out reason), Is.False);
            Module changed = CatalogLoader.Read(Catalog(id.id)); changed.pluginVersion = "1.99.0";
            Assert.That(CatalogLoader.TryBind(changed, registry, false, out reason), Is.True);
            Assert.That(changed.UiAllowed, Is.False);
            Assert.That(CatalogLoader.TryBind(changed, registry, true, out reason), Is.True);
            Assert.That(changed.UiAllowed, Is.True);
            Assert.That(CatalogLoader.TryBind(changed, guid => null, true, out reason), Is.False);
            Assert.That(changed.RuntimeAssembly, Is.Null, "Failed rebind must not retain the old target.");
            Assert.That(changed.Table, Is.Null);
            Assert.That(changed.UiAllowed, Is.False);
            Assert.That(changed.ExactVersion, Is.False);
        }
        [Test]
        public void InvalidCatalogDataIsRejectedByProductionLoader()
        {
            Module m = CatalogLoader.Read(Catalog("StructureTweaks")); m.patterns = null;
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(m));
            m = CatalogLoader.Read(Catalog("StructureTweaks"));
            m.patterns.Add(new PatternSpec { source = "Hi {0}", target = "Hi {0}", arguments = new Dictionary<string, string> { { "0", "arbitrary" } } });
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(m));
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
