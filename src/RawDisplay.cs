using System;
using System.Collections.Generic;
using System.Linq;

namespace Tolmach
{
    // Exact raw display strings and bounded catalog templates. Creature variant-name templates
    // require object provenance and are applied separately by CreatureNameDisplay.
    // Stored object fields and save files always retain their original values.
    internal static class RawDisplay
    {
        private const int CacheLimit = 4096;
        private static readonly List<Module> Owners = new List<Module>();
        // Hover texts and HUD lines repeat every frame, so misses are kept as well as hits.
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        internal static bool HasEntries { get { return Owners.Count != 0; } }
        internal static void Reset()
        {
            Owners.Clear();
            lock (Cache) Cache.Clear();
        }
        internal static void Initialize()
        {
            Reset();
            Dictionary<string, string> seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Module m in TextEngine.Modules.Values.Where(delegate(Module item) { return item.UiAllowed; }).OrderBy(delegate(Module item) { return item.id; }, StringComparer.Ordinal))
            {
                Dictionary<string, string> exact = new Dictionary<string, string>(m.rawTexts, StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> alias in m.nameAliases) exact.Add(alias.Key, m.words[alias.Value.Substring(1)]);
                // Older catalogs list raw prefab fields whose display translation lives in texts.
                foreach (PrefabSpec prefab in m.prefabs)
                    foreach (string english in prefab.fields.Values)
                    {
                        string russian;
                        if (!exact.ContainsKey(english) && m.texts.TryGetValue(english, out russian)) exact[english] = russian;
                    }
                IEnumerable<PatternSpec> patterns = m.id == CreatureNameDisplay.ModuleId ? Enumerable.Empty<PatternSpec>() : m.rawPatterns;
                if (exact.Count == 0 && !patterns.Any()) continue;
                foreach (KeyValuePair<string, string> entry in exact)
                {
                    string earlier;
                    if (seen.TryGetValue(entry.Key, out earlier))
                    {
                        if (earlier != entry.Value) m.Warn("Conflicting raw display text ignored: " + entry.Key);
                    }
                    else seen[entry.Key] = entry.Value;
                    // Keep every owner's vocabulary: its declared 'text' arguments resolve in
                    // its own table, even when another owner wins a standalone raw-text conflict.
                }
                m.RawTable = new TextTable(exact, patterns, new Dictionary<string, string>(), m.terms);
                Owners.Add(m);
            }
        }
        private static string TranslateWhole(string value)
        {
            foreach (Module m in Owners)
            {
                string translated = m.RawTable.TranslateWhole(value);
                if (translated != value) return translated;
            }
            return value;
        }
        internal static string Translate(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 32768 || Owners.Count == 0) return value;
            string result;
            lock (Cache) { if (Cache.TryGetValue(value, out result)) return result; }
            // Try whole-message matches across all owners before splitting the original text.
            // Each original line/span then gets its own owner; output is never translated again.
            result = TextTable.TranslateRaw(value, TranslateWhole);
            lock (Cache)
            {
                if (Cache.Count >= CacheLimit) Cache.Clear();
                Cache[value] = result;
            }
            return result;
        }
    }
}
