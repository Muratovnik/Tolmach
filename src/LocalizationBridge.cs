using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Tolmach
{
    // Fill-only policy over Valheim's Translate/AddWord API, not a second dictionary
    // implementation. The sole private game member used for mutation is the cache's
    // existing EvictAll API: this snapshot exposes no public invalidation method.
    internal static class LocalizationBridge
    {
        private sealed class LanguageState { internal string Language; }
        private static ConditionalWeakTable<object, LanguageState> Languages = new ConditionalWeakTable<object, LanguageState>();
        private static readonly Dictionary<string, string> Russian = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Type gameType;
        private static MethodInfo translate;
        private static MethodInfo addWord;
        private static MethodInfo selectedLanguage;
        private static Func<string> readPreference;
        private static bool injecting;
        private static bool warnedCache;
        internal static object Main { get { return gameType == null ? null : RuntimeAccess.Read(gameType, "m_instance"); } }

        internal static void Install(Harmony harmony, Type localizationType, Func<string> languagePreference)
        {
            if (localizationType == null) throw new InvalidOperationException("Game Localization class was not found.");
            if (languagePreference == null) throw new ArgumentNullException("languagePreference");
            gameType = localizationType;
            readPreference = languagePreference;
            translate = AccessTools.Method(gameType, "Translate", new[] { typeof(string) });
            addWord = AccessTools.Method(gameType, "AddWord", new[] { typeof(string), typeof(string) });
            if (translate == null || translate.IsStatic || translate.ReturnType != typeof(string) || addWord == null || addWord.IsStatic)
                throw new MissingMethodException("Localization.Translate(string)/AddWord(string,string) API unavailable.");
            selectedLanguage = AccessTools.Method(gameType, "GetSelectedLanguage", Type.EmptyTypes);
            if (selectedLanguage != null && (selectedLanguage.IsStatic || selectedLanguage.ReturnType != typeof(string))) selectedLanguage = null;
            Rebuild();
            object initialMain = Main;
            string initialLanguage = initialMain == null ? readPreference() : SelectedLanguage(initialMain);
            TextEngine.IsRussian = initialLanguage == "Russian";
            if (initialMain != null) SetLanguage(initialMain, initialLanguage);
            bool setupFound = false;
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(gameType))
            {
                ParameterInfo[] args = method.GetParameters();
                if (method.Name == "SetupLanguage" && !method.IsStatic && args.Length > 0 && args[0].ParameterType == typeof(string))
                {
                    harmony.Patch(method, HM("BeforeSetup", Priority.First), HM("AfterSetup", Priority.Last));
                    setupFound = true;
                }
                else if (LegacyDisplay.HasEntries && method.Name == "Localize" && !method.IsStatic && method.ReturnType == typeof(string) &&
                    args.Length == 1 && args[0].ParameterType == typeof(string))
                    harmony.Patch(method, postfix: HM("AfterLocalize", Priority.Last));
            }
            if (!setupFound) throw new MissingMethodException("Localization.SetupLanguage API unavailable.");
            harmony.Patch(addWord, HM("BeforeAddWord", Priority.Last), HM("AfterAddWord", Priority.Last));
        }
        private static HarmonyMethod HM(string name, int priority)
        {
            return new HarmonyMethod(AccessTools.Method(typeof(LocalizationBridge), name)) { priority = priority };
        }
        internal static void Rebuild()
        {
            Russian.Clear(); English.Clear();
            foreach (Module m in TextEngine.Modules.Values)
                foreach (KeyValuePair<string, string> word in m.words)
                {
                    string en;
                    if (!m.englishWords.TryGetValue(word.Key, out en)) continue;
                    if (Russian.ContainsKey(word.Key) && (Russian[word.Key] != word.Value || English[word.Key] != en))
                    { m.Warn("Conflicting localization key skipped: " + word.Key); continue; }
                    Russian[word.Key] = word.Value; English[word.Key] = en;
                }
        }
        private static void SetLanguage(object instance, string language)
        {
            Languages.GetValue(instance, delegate(object ignored) { return new LanguageState(); }).Language = language;
        }
        private static string Language(object instance)
        {
            LanguageState state;
            if (instance != null && Languages.TryGetValue(instance, out state)) return state.Language;
            // Native skill managers deliberately create a separate English Localization.
            if (!Object.ReferenceEquals(instance, Main)) return "";
            return SelectedLanguage(instance);
        }
        // The game's own selection reads its platform preferences (for example the Steam
        // Deck key). The plugin-supplied reader is only a fallback for an older API.
        private static string SelectedLanguage(object main)
        {
            try
            {
                if (selectedLanguage != null) return (string)selectedLanguage.Invoke(main, null) ?? "";
            }
            catch (Exception e) { Plugin.Warn("Localization.GetSelectedLanguage failed: " + e.GetType().Name); }
            return readPreference == null ? "" : readPreference();
        }
        private static void BeforeSetup(object __instance, string __0)
        {
            SetLanguage(__instance, __0);
            if (Object.ReferenceEquals(__instance, Main)) TextEngine.IsRussian = __0 == "Russian";
        }
        private static void AfterSetup(object __instance, string __0)
        {
            // Only the game's main Localization is filled. Mods also create separate
            // instances (SkillManager keeps an English one for config names); the game
            // constructor briefly loads the player's language into each of them, and
            // Russian written there would leak into those English lookups. The main
            // instance is still being constructed here too (m_instance is unset) and is
            // filled by the next Refresh (plugin Start, menu, Jotunn localization event).
            if (!Object.ReferenceEquals(__instance, Main)) return;
            TextEngine.IsRussian = __0 == "Russian";
            foreach (Module m in TextEngine.Modules.Values) if (m.Table != null) m.Table.Clear();
            Inject(__instance, __0);
            PersistentUi.Refresh();
        }
        internal static bool ShouldFill(string current, string english, string key)
        {
            return String.IsNullOrEmpty(current) || current == english || current == key || current == "$" + key || current == "[" + key + "]" || current == "[$" + key + "]";
        }
        private static string CurrentWord(object instance, string key)
        {
            return (string)translate.Invoke(instance, new object[] { key });
        }
        private static void BeforeAddWord(object __instance, string __0, ref string __1, out bool __state)
        {
            __state = false;
            if (Language(__instance) != "Russian" || String.IsNullOrEmpty(__0)) return;
            string ru, en;
            if (!Russian.TryGetValue(__0, out ru) || !English.TryGetValue(__0, out en)) return;
            string current = CurrentWord(__instance, __0);
            // Native scope registration can feed our Russian value back into AddWord.
            // It must not replace an existing third-party Russian translation.
            if (__1 == ru || ShouldFill(__1, en, __0)) __1 = ShouldFill(current, en, __0) ? ru : current;
            __state = !injecting && current != __1;
        }
        private static void AfterAddWord(object __instance, bool __state)
        {
            if (__state) InvalidateCache(__instance);
        }
        private static void AfterLocalize(object __instance, ref string __result)
        {
            if (Language(__instance) == "Russian") __result = LegacyDisplay.Translate(__result);
        }
        internal static void InjectMain()
        {
            object main = Main;
            if (main == null) return;
            string language = Language(main);
            // The language may have been chosen inside the game constructor (first launch
            // from the system locale), before m_instance identified the main instance.
            bool russian = language == "Russian";
            if (TextEngine.IsRussian != russian)
            {
                TextEngine.IsRussian = russian;
                PersistentUi.Refresh();
            }
            Inject(main, language);
            // A native loader may have registered a key before Rebuild made it
            // known to our AddWord patch. Clear stale full-text results as well.
            InvalidateCache(main);
        }
        private static void Inject(object instance, string language)
        {
            if (injecting || instance == null || language != "Russian") return;
            injecting = true;
            bool changed = false;
            try
            {
                foreach (KeyValuePair<string, string> entry in English)
                    if (ShouldFill(CurrentWord(instance, entry.Key), entry.Value, entry.Key))
                    {
                        addWord.Invoke(instance, new object[] { entry.Key, Russian[entry.Key] });
                        changed = true;
                    }
            }
            catch (Exception e) { Plugin.Warn("Localization registration: " + e.GetType().Name); }
            finally
            {
                injecting = false;
                if (changed) InvalidateCache(instance);
            }
        }
        private static void InvalidateCache(object instance)
        {
            try
            {
                object cache = RuntimeAccess.Read(instance, "m_cache");
                if (cache == null) return;
                MethodInfo evict = AccessTools.Method(cache.GetType(), "EvictAll", Type.EmptyTypes);
                if (evict == null) throw new MissingMethodException("EvictAll");
                evict.Invoke(cache, null);
            }
            catch (Exception e)
            {
                if (!warnedCache) Plugin.Warn("Localization cache invalidation unavailable: " + e.GetType().Name);
                warnedCache = true;
            }
        }
        internal static void RegisterNativeKey(Module module, string key, string en, string ru)
        {
            key = key.TrimStart('$');
            module.englishWords[key] = en; module.words[key] = ru;
            // Make the AddWord guard aware of the key immediately; Rebuild later applies
            // the cross-module conflict check to the complete table.
            if (!Russian.ContainsKey(key)) { Russian[key] = ru; English[key] = en; }
        }
        internal static void Reset()
        {
            Russian.Clear(); English.Clear();
            Languages = new ConditionalWeakTable<object, LanguageState>();
            gameType = null; translate = null; addWord = null; selectedLanguage = null; readPreference = null;
            injecting = warnedCache = false;
            TextEngine.IsRussian = false;
        }
    }
}
