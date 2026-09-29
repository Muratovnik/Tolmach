using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tolmach
{
    internal sealed class PluginIdentity
    {
        internal readonly string Guid;
        internal readonly Assembly Assembly;
        internal readonly Version Version;
        internal PluginIdentity(string guid, Assembly assembly, Version version)
        { Guid = guid; Assembly = assembly; Version = version; }
    }

    // One loading/binding path for the plugin and executable regression fixtures.
    // Registry lookup is the external boundary; catalog DTOs and binding policy are production code.
    internal static class CatalogLoader
    {
        private static readonly Regex Hole = new Regex(@"\{(\d+)\}", RegexOptions.CultureInvariant);
        // The catalog types declare their shape (Models.cs); an undeclared member is an error.
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        internal static Module Read(string path)
        {
            if (new FileInfo(path).Length > 2 * 1024 * 1024)
                throw new InvalidDataException("Catalog is larger than 2 MiB.");
            using (StreamReader stream = new StreamReader(path, Encoding.UTF8, true))
            using (JsonTextReader reader = new JsonTextReader(stream) { MaxDepth = 32, DateParseHandling = DateParseHandling.None })
            {
                JObject data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidDataException("Unexpected data after catalog object.");
                Module module = data.ToObject<Module>(Serializer);
                Validate(module);
                return module;
            }
        }
        internal static void Validate(Module m)
        {
            if (m == null || String.IsNullOrEmpty(m.id) || !Regex.IsMatch(m.id, @"\A[A-Za-z0-9_.]+\z") ||
                String.IsNullOrEmpty(m.assembly) || m.guids.Count == 0 ||
                m.guids.Any(String.IsNullOrWhiteSpace) || m.namespaces.Any(String.IsNullOrWhiteSpace))
                throw new InvalidDataException("Missing or invalid module identity.");
            Version parsed;
            if (!Version.TryParse(m.version, out parsed) || !Version.TryParse(m.pluginVersion, out parsed))
                throw new InvalidDataException("Package version and BepInPlugin version must both be declared.");
            // IL adapters are confined to the plugin's namespaces; a dictionary-only module has none.
            if (m.namespaces.Count == 0 && m.NeedsIlAdapters) throw new InvalidDataException("Scoped adapters need plugin namespaces.");
            if (!new HashSet<string>(m.words.Keys).SetEquals(m.englishWords.Keys))
                throw new InvalidDataException("English/Russian localization keys differ.");
            if (m.terms.Any(delegate(KeyValuePair<string, Dictionary<string, string>> t) { return !Regex.IsMatch(t.Key, @"\A[A-Za-z0-9_]+\z") || t.Value == null || t.Value.Count == 0; }))
                throw new InvalidDataException("Invalid term vocabulary.");
            foreach (Dictionary<string, string> table in new[] { m.words, m.englishWords, m.texts, m.mapLabels, m.rawTexts }.Concat(m.terms.Values))
                if (table.Any(delegate(KeyValuePair<string, string> p) { return String.IsNullOrEmpty(p.Key) || String.IsNullOrEmpty(p.Value); }))
                    throw new InvalidDataException("Empty translation key/value.");
            foreach (PatternSpec p in m.patterns) ValidatePattern(m, p, "text", "message", "mapLabel");
            foreach (PatternSpec p in m.rawPatterns)
            {
                ValidatePattern(m, p, "text");
                // Raw templates see every localized string in the game: a bare "{0}" would match them all.
                if (Hole.Replace(p.source, "").Trim().Length == 0) throw new InvalidDataException("Raw pattern has no fixed text.");
            }
            foreach (MethodSpec r in m.returns)
                if (r == null || String.IsNullOrEmpty(r.type) || String.IsNullOrEmpty(r.method)) throw new InvalidDataException("Invalid return adapter.");
            foreach (LiteralSpec r in m.literals.Concat(m.configTexts))
                if (r == null || String.IsNullOrEmpty(r.type) || String.IsNullOrEmpty(r.method) || r.values.Count == 0 ||
                    r.values.Any(delegate(KeyValuePair<string, string> p) { return String.IsNullOrEmpty(p.Key) || String.IsNullOrEmpty(p.Value); }))
                    throw new InvalidDataException("Invalid literal or config text adapter.");
            foreach (PrefabSpec r in m.prefabs)
                if (r == null || r.fields.Values.Any(String.IsNullOrEmpty)) throw new InvalidDataException("Invalid display fallback.");
            if (m.cllc != null) ValidateCllc(m.cllc);
        }
        // CLLC parses its nameplate templates itself: {name} once, and optional [...] groups with one
        // of the other placeholders each. On anything else it logs an error and shows the prefab name.
        // The same expression is the "nameplate" pattern of tools/catalog.schema.json.
        internal static readonly Regex CllcNameplate = new Regex(@"\A(?:[^\[\]{}]|\[[^\[\]{}]*\{(?:effect|infusion|affix)\}[^\[\]{}]*\])*\{name\}" +
            @"(?:[^\[\]{}]|\[[^\[\]{}]*\{(?:effect|infusion|affix)\}[^\[\]{}]*\])*\z", RegexOptions.CultureInvariant);
        private static void ValidateCllc(CllcLanguage t)
        {
            if (!t.creatureGender.ContainsKey("default") ||
                t.creatureGender.Values.Any(delegate(string g) { return g == null || !t.genderedCreatureTranslations.ContainsKey(g); }))
                throw new InvalidDataException("Every CLLC gender, including the default one, needs a nameplate template.");
            foreach (string template in t.genderedCreatureTranslations.Values)
                if (template == null || !CllcNameplate.IsMatch(template)) throw new InvalidDataException("Invalid CLLC nameplate template: " + template);
            if (t.genderedTranslations.Any(delegate(KeyValuePair<string, Dictionary<string, string>> g) { return g.Value == null || !t.genderedCreatureTranslations.ContainsKey(g.Key); }))
                throw new InvalidDataException("CLLC gendered words for a gender without a template.");
            foreach (Dictionary<string, string> table in new[] { t.creatureGender, t.genderedCreatureTranslations, t.translations, t.settingGroups }
                .Concat(t.genderedTranslations.Values).Concat(t.enumTranslations.Values))
                if (table == null || table.Any(delegate(KeyValuePair<string, string> p) { return String.IsNullOrEmpty(p.Key) || String.IsNullOrEmpty(p.Value); }))
                    throw new InvalidDataException("Empty CLLC key/value.");
            if (t.settings.Any(delegate(KeyValuePair<string, CllcSetting> p) { return String.IsNullOrEmpty(p.Key) || p.Value == null || String.IsNullOrEmpty(p.Value.display) || String.IsNullOrEmpty(p.Value.desc); }))
                throw new InvalidDataException("A CLLC setting needs a name and a description.");
        }
        private static void ValidatePattern(Module m, PatternSpec p, params string[] semantics)
        {
            if (p == null || String.IsNullOrEmpty(p.source) || String.IsNullOrEmpty(p.target))
                throw new InvalidDataException("Invalid pattern.");
            HashSet<string> holes = new HashSet<string>(Hole.Matches(p.source).Cast<Match>().Select(delegate(Match h) { return h.Groups[1].Value; }));
            if (!holes.SetEquals(Hole.Matches(p.target).Cast<Match>().Select(delegate(Match h) { return h.Groups[1].Value; })))
                throw new InvalidDataException("Pattern placeholders differ.");
            if (p.numeric.Any(delegate(int n) { return !holes.Contains(n.ToString(System.Globalization.CultureInfo.InvariantCulture)); }))
                throw new InvalidDataException("Numeric argument has no source placeholder.");
            foreach (KeyValuePair<string, string> a in p.arguments)
            {
                bool known = a.Value != null && (Array.IndexOf(semantics, a.Value) >= 0 ||
                    (a.Value.StartsWith("term:", StringComparison.Ordinal) && m.terms.ContainsKey(a.Value.Substring(5))));
                if (!holes.Contains(a.Key) || !known || p.numeric.Contains(Int32.Parse(a.Key, System.Globalization.CultureInfo.InvariantCulture)))
                    throw new InvalidDataException("Unknown or conflicting semantic argument.");
            }
        }
        // Valheim itself has no BepInEx plugin record. Game modules (closed captions) bind to
        // this reserved GUID: the assembly declaring the game's Version type and its CurrentVersion.
        internal const string GameGuid = "valheim";
        // LocalizationManager, embedded in many mods, reads every "<plugin name>.*" file under
        // BepInEx as its own translation and fails on a name like "SleepSkip.json".
        internal const string FilePrefix = "tolmach-";
        internal static PluginIdentity GameIdentity(Type versionType)
        {
            object current = versionType == null ? null : RuntimeAccess.Read(versionType, "CurrentVersion");
            object major = RuntimeAccess.Read(current, "m_major"), minor = RuntimeAccess.Read(current, "m_minor"), patch = RuntimeAccess.Read(current, "m_patch");
            if (!(major is int) || !(minor is int) || !(patch is int) || (int)major < 0 || (int)minor < 0 || (int)patch < 0) return null;
            return new PluginIdentity(GameGuid, versionType.Assembly, new Version((int)major, (int)minor, (int)patch));
        }
        internal static bool SameVersion(Version expected, Version actual)
        {
            if (expected == null || actual == null) return false;
            // BepInPlugin("1.37") and ("1.37.0") denote the same zero-build version.
            // Do not ignore nonzero revisions, as the previous three-component check did.
            return expected.Major == actual.Major && expected.Minor == actual.Minor &&
                Math.Max(0, expected.Build) == Math.Max(0, actual.Build) &&
                Math.Max(0, expected.Revision) == Math.Max(0, actual.Revision);
        }
        internal static bool TryBind(Module module, Func<string, PluginIdentity> lookup, bool allowOtherVersions, out string reason)
        {
            // A failed rebind must not leave the previous plugin's assembly/permissions live.
            module.RuntimeAssembly = null;
            module.Table = null;
            module.ExactVersion = false;
            module.UiAllowed = false;
            PluginIdentity installed = null;
            foreach (string guid in module.guids)
            {
                installed = lookup(guid);
                if (installed != null) break;
            }
            if (installed == null || installed.Assembly == null)
            { reason = "not installed/loaded"; return false; }
            if (!module.guids.Contains(installed.Guid) || installed.Assembly.GetName().Name != module.assembly)
            { reason = "GUID/assembly identity mismatch; skipped"; return false; }
            module.RuntimeAssembly = installed.Assembly;
            module.ExactVersion = SameVersion(new Version(module.pluginVersion), installed.Version);
            module.UiAllowed = module.ExactVersion || allowOtherVersions;
            if (!module.ExactVersion)
                module.Warn("Expected plugin " + module.pluginVersion + " (package " + module.version + "), loaded " + installed.Version + ".");
            module.Table = new TextTable(module);
            reason = module.UiAllowed ? "loaded" : "native dictionaries only; different plugin version";
            return true;
        }
    }
}
