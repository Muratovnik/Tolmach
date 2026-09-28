using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Tolmach
{
    internal static class NativeAdapters
    {
        private static bool busy;
        private static EventInfo jotunnEvent;
        private static Action jotunnHandler;

        internal static void Subscribe()
        {
            Unsubscribe();
            Type manager = RuntimeAccess.ExactType("Jotunn.Managers.LocalizationManager");
            if (manager == null) return;
            jotunnEvent = manager.GetEvent("OnLocalizationAdded", AccessTools.all);
            if (jotunnEvent != null && jotunnEvent.EventHandlerType == typeof(Action))
            {
                jotunnHandler = Refresh;
                jotunnEvent.AddEventHandler(null, jotunnHandler);
            }
        }
        internal static void Unsubscribe()
        {
            try
            {
                if (jotunnEvent != null && jotunnHandler != null) jotunnEvent.RemoveEventHandler(null, jotunnHandler);
            }
            catch (Exception e) { Plugin.Warn("Jotunn unsubscribe: " + e.GetType().Name); }
            finally { jotunnEvent = null; jotunnHandler = null; }
        }
        internal static void Refresh()
        {
            if (busy) return;
            busy = true;
            try
            {
                foreach (Module m in TextEngine.Modules.Values)
                {
                    try { AddManagerKeys(m); }
                    catch (Exception e) { m.Warn("LocalizeKey adapter: " + e.GetType().Name); }
                    try { AddJotunnScope(m); }
                    catch (Exception e) { m.Warn("Jotunn adapter: " + e.GetType().Name); }
                    try { CllcAdapter.Refresh(m); }
                    catch (Exception e) { m.Warn("CLLC language adapter: " + e.GetType().Name); }
                }
                LocalizationBridge.Rebuild();
                LocalizationBridge.InjectMain();
            }
            finally { busy = false; }
        }
        private static void AddManagerKeys(Module m)
        {
            if (m.texts.Count == 0) return;
            // Item/Piece/Creature/SkillManager each embed their own LocalizeKey registry.
            foreach (Type type in AccessTools.GetTypesFromAssembly(m.RuntimeAssembly))
            {
                try { if (type.Name == "LocalizeKey" && !type.ContainsGenericParameters) AddManagerKeys(m, type); }
                catch (Exception e) { m.Warn("LocalizeKey adapter skipped " + type.Name + ": " + e.GetType().Name); }
            }
        }
        private static void AddManagerKeys(Module m, Type type)
        {
            FieldInfo field = type.GetField("keys", AccessTools.all);
            if (field == null || !field.IsStatic) return;
            IEnumerable list = field.GetValue(null) as IEnumerable;
            if (list == null) return;
            // Calling the established Russian registration API also invalidates native caches.
            MethodInfo setter = type.GetMethod("Russian", AccessTools.all, null, new Type[] { typeof(string) }, null);
            foreach (object record in list.Cast<object>().ToArray())
            {
                IDictionary langs = RuntimeAccess.Read(record, "Localizations") as IDictionary;
                string key = RuntimeAccess.Read(record, "Key") as string;
                if (langs == null || key == null) continue;
                string en = langs["English"] as string;
                string ru;
                if (en == null || !m.texts.TryGetValue(en, out ru)) continue;
                string existing = langs["Russian"] as string;
                if (!LocalizationBridge.ShouldFill(existing, en, key)) continue;
                bool isNew = !m.words.ContainsKey(key.TrimStart('$'));
                // Register first: the Russian setter writes through Localization.AddWord at
                // once, and the fill-only AddWord guard must already know this key so a
                // third-party Russian word in the game table is not replaced.
                LocalizationBridge.RegisterNativeKey(m, key, en, ru);
                if (setter != null) setter.Invoke(record, new object[] { ru });
                else m.Warn("LocalizeKey.Russian API missing for " + type.FullName + "; global key fallback only.");
                if (isNew) m.NativeWords++;
            }
        }
        private static void AddJotunnScope(Module m)
        {
            Type type = RuntimeAccess.ExactType("Jotunn.Managers.LocalizationManager");
            if (type == null || m.words.Count == 0) return;
            object manager = RuntimeAccess.Read(type, "Instance");
            IDictionary all = RuntimeAccess.Read(manager, "Localizations") as IDictionary;
            if (all == null) return;
            foreach (string guid in m.guids)
            {
                object scope = all[guid];
                if (scope == null) continue;
                FillJotunnScope(scope, m);
            }
        }
        internal static void FillJotunnScope(object scope, Module m)
        {
            // Jotunn 2.30.2 uses `in string` (String&) here; older variants may use
            // String by value. Match the actual public API, never read its private Map.
            Type type = scope.GetType();
            MethodInfo get = AccessTools.Method(type, "GetTranslations", new[] { typeof(string).MakeByRefType() }) ??
                AccessTools.Method(type, "GetTranslations", new[] { typeof(string) });
            MethodInfo add = AccessTools.Method(type, "AddTranslation", new[] { typeof(string).MakeByRefType(), typeof(Dictionary<string, string>) }) ??
                AccessTools.Method(type, "AddTranslation", new[] { typeof(string), typeof(Dictionary<string, string>) });
            if (get == null || add == null || !get.IsPublic || !add.IsPublic || get.IsStatic || add.IsStatic)
            { m.Warn("Jotunn public GetTranslations/AddTranslation API unavailable; scope left untouched."); return; }
            IReadOnlyDictionary<string, string> existing = get.Invoke(scope, new object[] { "Russian" }) as IReadOnlyDictionary<string, string>;
            if (existing == null) { m.Warn("Jotunn returned no readable translation table; scope left untouched."); return; }
            Dictionary<string, string> additions = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> word in m.words)
            {
                string current, en;
                existing.TryGetValue(word.Key, out current);
                if (m.englishWords.TryGetValue(word.Key, out en) && LocalizationBridge.ShouldFill(current, en, word.Key))
                    additions[word.Key] = word.Value;
            }
            if (additions.Count != 0) add.Invoke(scope, new object[] { "Russian", additions });
        }

    }
}
