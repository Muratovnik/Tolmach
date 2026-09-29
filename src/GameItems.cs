using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Tolmach
{
    // Some mods print a game object by its internal identifier: Recipe Description Expansion lists the parts of
    // a set by prefab name and the set by its m_setName. A pattern argument declared "item" or "itemSet" is shown
    // under the name the game itself uses, already localized, so the result reads the same whether the caller
    // localizes the text afterwards or not. ObjectDB is only read.
    internal static class GameItems
    {
        // The token or name of an item prefab / of a set as a mod shows it; replaceable where ObjectDB does not exist.
        internal static Func<string, string> ItemToken = FindItemToken;
        internal static Func<string, string> SetToken = FindSetToken;
        private static object setsOf;
        private static Dictionary<string, string> sets;
        private static MethodInfo localize;

        internal static string ItemName(string prefab) { return Shown(Lookup(ItemToken, prefab)); }
        internal static string SetName(string shown) { return Shown(Lookup(SetToken, shown)); }
        internal static void Reset()
        {
            ItemToken = FindItemToken; SetToken = FindSetToken;
            setsOf = null; sets = null; localize = null;
        }
        private static string Lookup(Func<string, string> lookup, string value)
        {
            if (String.IsNullOrEmpty(value)) return null;
            try { return lookup(value); }
            catch (Exception e) { Plugin.Warn("Game name lookup failed for " + value + ": " + e.GetType().Name); return null; }
        }
        private static string Shown(string token)
        {
            if (String.IsNullOrEmpty(token)) return null;
            object main = LocalizationBridge.Main;
            if (main == null) return token;
            if (localize == null || localize.DeclaringType != main.GetType())
                localize = AccessTools.Method(main.GetType(), "Localize", new[] { typeof(string) });
            return localize == null ? token : localize.Invoke(main, new object[] { token }) as string ?? token;
        }
        private static object Database()
        {
            Type type = RuntimeAccess.ExactType("ObjectDB");
            return type == null ? null : RuntimeAccess.Read(type, "instance");
        }
        private static object Shared(object prefab)
        {
            GameObject item = prefab as GameObject;
            Type drop = RuntimeAccess.ExactType("ItemDrop");
            Component component = item == null || drop == null ? null : item.GetComponent(drop);
            return RuntimeAccess.Read(RuntimeAccess.Read(component, "m_itemData"), "m_shared");
        }
        private static string FindItemToken(string prefab)
        {
            object db = Database();
            MethodInfo get = db == null ? null : AccessTools.Method(db.GetType(), "GetItemPrefab", new[] { typeof(string) });
            return get == null ? null : RuntimeAccess.Read(Shared(get.Invoke(db, new object[] { prefab })), "m_name") as string;
        }
        // Recipe Description Expansion capitalizes each word of m_setName, so the set is found ignoring case.
        // A set named by a key is shown by that key; a set with an internal name, by the name of its set effect.
        private static string FindSetToken(string shown)
        {
            object db = Database();
            if (db == null) return null;
            if (!ReferenceEquals(db, setsOf)) { setsOf = db; sets = null; }
            if (sets == null)
            {
                Dictionary<string, string> found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                IEnumerable items = RuntimeAccess.Read(db, "m_items") as IEnumerable;
                if (items != null)
                    foreach (object prefab in items)
                    {
                        object shared = Shared(prefab);
                        string set = RuntimeAccess.Read(shared, "m_setName") as string;
                        if (String.IsNullOrEmpty(set) || found.ContainsKey(set)) continue;
                        string effect = RuntimeAccess.Read(RuntimeAccess.Read(shared, "m_setStatusEffect"), "m_name") as string;
                        found[set] = set.StartsWith("$", StringComparison.Ordinal) ? set : effect;
                    }
                sets = found;
            }
            string token;
            return sets.TryGetValue(shown, out token) ? token : null;
        }
    }
}
