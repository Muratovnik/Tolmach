using System;
using System.Collections.Generic;
using System.Linq;

namespace Tolmach
{
    // Raw display strings: text stored in game objects (item, piece and status names,
    // descriptions, Compendium entries, composed creature names) that the game passes
    // through Localization.Localize unchanged. Translated on display only: SharedData.m_name,
    // ZDO and save files keep the original text.
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
                // Older catalogs list raw prefab fields whose display translation lives in texts.
                foreach (PrefabSpec prefab in m.prefabs)
                    foreach (string english in prefab.fields.Values)
                    {
                        string russian;
                        if (!exact.ContainsKey(english) && m.texts.TryGetValue(english, out russian)) exact[english] = russian;
                    }
                if (exact.Count == 0 && m.rawPatterns.Count == 0) continue;
                foreach (KeyValuePair<string, string> entry in exact)
                {
                    string earlier;
                    // The first module in ID order wins; the later value is never reached.
                    if (seen.TryGetValue(entry.Key, out earlier) && earlier != entry.Value) m.Warn("Conflicting raw display text ignored: " + entry.Key);
                    else seen[entry.Key] = entry.Value;
                }
                m.RawTable = new TextTable(exact, m.rawPatterns, new Dictionary<string, string>(), m.terms);
                Owners.Add(m);
            }
        }
        internal static string Translate(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 32768 || Owners.Count == 0) return value;
            string result;
            lock (Cache) { if (Cache.TryGetValue(value, out result)) return result; }
            result = value;
            foreach (Module m in Owners)
            {
                string translated = m.RawTable.TranslateRaw(value);
                if (translated != value) { result = translated; break; }
            }
            lock (Cache)
            {
                if (Cache.Count >= CacheLimit) Cache.Clear();
                Cache[value] = result;
            }
            return result;
        }
    }
}
