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
        internal static Func<string, Type> TypeResolver = RuntimeAccess.ExactType;
        internal static Func<object, Type, object> ComponentResolver = FindComponent;
        private static Type databaseType;
        private static Type dropType;
        private static readonly Dictionary<Type, Dictionary<string, Func<object, object>>> Readers = new Dictionary<Type, Dictionary<string, Func<object, object>>>();
        private static readonly Dictionary<Type, MethodInfo> PrefabGetters = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<string, string> ItemTokens = new Dictionary<string, string>(StringComparer.Ordinal);
        private static object databaseOf;
        private static object itemsOf;
        private static int itemCount = -1;
        private static Dictionary<string, string> sets;
        private static MethodInfo localize;

        internal static string ItemName(string prefab) { return Shown(Lookup(ItemToken, prefab)); }
        internal static string SetName(string shown) { return Shown(Lookup(SetToken, shown)); }
        internal static void Install(Harmony harmony)
        {
            try
            {
                if (databaseType == null) databaseType = TypeResolver("ObjectDB");
                MethodInfo register = databaseType == null ? null : AccessTools.Method(databaseType, "UpdateRegisters", Type.EmptyTypes);
                if (register == null || register.IsStatic || register.ReturnType != typeof(void) || !RuntimeAccess.ManagedBody(register))
                {
                    Plugin.Warn("ObjectDB.UpdateRegisters API unavailable; game-name caches refresh on database/list changes and adapter refresh.");
                    return;
                }
                harmony.Patch(register, postfix: new HarmonyMethod(AccessTools.Method(typeof(GameItems), "Invalidate")) { priority = Priority.Last });
            }
            catch (Exception e) { Plugin.Warn("Game-name registration hook: " + e.GetType().Name); }
        }
        internal static void Invalidate()
        {
            sets = null;
            ItemTokens.Clear();
            // A translated tooltip can retain a previously unresolved item argument.
            foreach (Module module in TextEngine.Modules.Values)
                if (module.Table != null) module.Table.Clear();
        }
        internal static void Reset()
        {
            ItemToken = FindItemToken; SetToken = FindSetToken;
            TypeResolver = RuntimeAccess.ExactType; ComponentResolver = FindComponent;
            databaseType = null; dropType = null;
            Readers.Clear(); PrefabGetters.Clear();
            databaseOf = null; itemsOf = null; itemCount = -1; localize = null;
            Invalidate();
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
            if (databaseType == null) databaseType = TypeResolver("ObjectDB");
            object db = Read(databaseType, "instance");
            object items = Read(db, "m_items");
            ICollection collection = items as ICollection;
            int count = collection == null ? -1 : collection.Count;
            if (!ReferenceEquals(db, databaseOf) || !ReferenceEquals(items, itemsOf) || count != itemCount)
            {
                databaseOf = db; itemsOf = items; itemCount = count;
                Invalidate();
            }
            return db;
        }
        private static object Read(object instanceOrType, string name)
        {
            if (instanceOrType == null) return null;
            Type type = instanceOrType as Type ?? instanceOrType.GetType();
            Dictionary<string, Func<object, object>> members;
            if (!Readers.TryGetValue(type, out members))
                Readers[type] = members = new Dictionary<string, Func<object, object>>(StringComparer.Ordinal);
            Func<object, object> read;
            if (!members.TryGetValue(name, out read))
            {
                FieldInfo field = AccessTools.FindIncludingBaseTypes(type, delegate(Type t) { return t.GetField(name, AccessTools.all); });
                PropertyInfo property = field == null ? AccessTools.FindIncludingBaseTypes(type, delegate(Type t) { return t.GetProperty(name, AccessTools.all); }) : null;
                read = field != null ? new Func<object, object>(field.GetValue)
                    : property != null ? new Func<object, object>(delegate(object receiver) { return property.GetValue(receiver, null); })
                    : delegate(object receiver) { return null; };
                members[name] = read;
            }
            return read(instanceOrType is Type ? null : instanceOrType);
        }
        private static object FindComponent(object prefab, Type type)
        {
            GameObject item = prefab as GameObject;
            return item == null ? null : item.GetComponent(type);
        }
        private static object Shared(object prefab)
        {
            if (dropType == null) dropType = TypeResolver("ItemDrop");
            object component = prefab == null || dropType == null ? null : ComponentResolver(prefab, dropType);
            return Read(Read(component, "m_itemData"), "m_shared");
        }
        private static string FindItemToken(string prefab)
        {
            object db = Database();
            if (db == null) return null;
            string token;
            if (ItemTokens.TryGetValue(prefab, out token)) return token;
            Type type = db.GetType();
            MethodInfo get;
            if (!PrefabGetters.TryGetValue(type, out get))
                PrefabGetters[type] = get = AccessTools.Method(type, "GetItemPrefab", new[] { typeof(string) });
            token = get == null || get.IsStatic ? null : Read(Shared(get.Invoke(db, new object[] { prefab })), "m_name") as string;
            // A missing prefab may still be awaiting registration; retain only positive raw tokens.
            if (!String.IsNullOrEmpty(token)) ItemTokens[prefab] = token;
            return token;
        }
        // Recipe Description Expansion capitalizes each word of m_setName, so the set is found ignoring case.
        // A set named by a key is shown by that key; a set with an internal name, by the name of its set effect.
        private static string FindSetToken(string shown)
        {
            object db = Database();
            if (db == null) return null;
            if (sets == null)
            {
                IEnumerable items = itemsOf as IEnumerable;
                if (items == null || itemCount == 0) return null;
                if (dropType == null) dropType = TypeResolver("ItemDrop");
                if (dropType == null) return null;
                Dictionary<string, string> found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (object prefab in items)
                {
                    object shared = Shared(prefab);
                    string set = Read(shared, "m_setName") as string;
                    if (String.IsNullOrEmpty(set) || found.ContainsKey(set)) continue;
                    string effect = Read(Read(shared, "m_setStatusEffect"), "m_name") as string;
                    string setToken = set.StartsWith("$", StringComparison.Ordinal) ? set : effect;
                    if (!String.IsNullOrEmpty(setToken)) found[set] = setToken;
                }
                sets = found;
            }
            string token;
            return sets.TryGetValue(shown, out token) ? token : null;
        }
    }
}
