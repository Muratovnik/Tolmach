using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace Tolmach
{
    // Creature Level & Loot Control keeps its texts in its own YAML schema, LocalizationWrapper. It
    // loads the table once, in its Awake, falls back to English without a Russian file, and bakes the
    // setting names and tooltips into its ConfigurationManagerAttributes there. Tolmach loads after it
    // (soft dependency): the adapter replaces the English fallback with the catalog table, or only
    // fills the gaps of a Russian table supplied by someone else, and recomputes the baked setting
    // texts with the mod's own lookups. The rest of the mod reads the current table when it shows a
    // text, so no Harmony patch is needed.
    internal static class CllcAdapter
    {
        private const string WrapperType = "CreatureLevelControl.LocalizationWrapper";
        private const string AttributesType = "CreatureLevelControl.ConfigurationManagerAttributes";
        private static readonly string[] Tables = { "creatureGender", "genderedCreatureTranslations", "genderedTranslations", "translations", "enumTranslations", "settingGroups", "settings" };
        private static readonly Regex Placeholder = new Regex(@"\{[^{}]+\}", RegexOptions.CultureInvariant);
        private static readonly Regex GroupNumber = new Regex(@"\A\d+ - ", RegexOptions.CultureInvariant);

        internal static void Refresh(Module m)
        {
            if (m.cllc == null) return;
            ConfigFile config = null;
            foreach (string guid in m.guids)
            {
                PluginInfo info;
                if (Chainloader.PluginInfos.TryGetValue(guid, out info) && info.Instance != null) { config = info.Instance.Config; break; }
            }
            Apply(m, config);
        }
        internal static void Apply(Module m, ConfigFile config)
        {
            if (m.cllc == null || m.RuntimeAssembly == null || !TextEngine.IsRussian) return;
            Type wrapper = m.RuntimeAssembly.GetType(WrapperType);
            FieldInfo instance = wrapper == null ? null : AccessTools.Field(wrapper, "instance");
            FieldInfo language = wrapper == null ? null : AccessTools.Field(wrapper, "language");
            object english = wrapper == null ? null : RuntimeAccess.Read(wrapper, "English");
            object current = instance == null || !instance.IsStatic ? null : instance.GetValue(null);
            if (current == null || english == null || language == null || language.IsStatic ||
                Tables.Any(delegate(string name) { FieldInfo f = AccessTools.Field(wrapper, name); return f == null || f.IsStatic || !typeof(IDictionary).IsAssignableFrom(f.FieldType); }))
            { m.Warn("CLLC LocalizationWrapper API not found; the mod keeps its own texts."); return; }
            object table = current;
            if ((language.GetValue(current) as string) != "Russian")
            {
                table = Activator.CreateInstance(wrapper, true);
                language.SetValue(table, "Russian");
            }
            m.NativeWords += Fill(table, english, m);
            if (!ReferenceEquals(table, current)) instance.SetValue(null, table);
            if (config != null) Relabel(m, wrapper, english, config);
        }
        private static int Fill(object table, object english, Module m)
        {
            CllcLanguage ours = m.cllc;
            int filled = 0, unknown = 0;
            // Genders, templates and gendered forms refer to each other by gender code: a Russian
            // table that already has templates keeps all three, since mixed codes would not resolve.
            IDictionary templates = Dictionary(table, "genderedCreatureTranslations", true);
            if (templates.Count == 0)
            {
                IDictionary genders = Dictionary(table, "creatureGender", true), forms = Dictionary(table, "genderedTranslations", true);
                foreach (KeyValuePair<string, string> g in ours.creatureGender) genders[g.Key] = g.Value;
                foreach (KeyValuePair<string, string> t in ours.genderedCreatureTranslations) templates[t.Key] = t.Value;
                foreach (KeyValuePair<string, Dictionary<string, string>> f in ours.genderedTranslations) forms[f.Key] = new Dictionary<string, string>(f.Value);
                filled += ours.creatureGender.Count + ours.genderedCreatureTranslations.Count + ours.genderedTranslations.Values.Sum(delegate(Dictionary<string, string> f) { return f.Count; });
            }
            filled += FillWords(Dictionary(table, "translations", true), Dictionary(english, "translations", false), ours.translations, true, m, ref unknown);
            filled += FillWords(Dictionary(table, "settingGroups", true), Dictionary(english, "settingGroups", false), ours.settingGroups, false, m, ref unknown);
            IDictionary enums = Dictionary(table, "enumTranslations", true), englishEnums = Dictionary(english, "enumTranslations", false);
            foreach (KeyValuePair<string, Dictionary<string, string>> type in ours.enumTranslations)
            {
                IDictionary englishMembers = englishEnums[type.Key] as IDictionary;
                if (englishMembers == null) { unknown += type.Value.Count; continue; }
                IDictionary members = enums[type.Key] as IDictionary;
                if (members == null) { members = new Dictionary<string, string>(); enums[type.Key] = members; }
                foreach (KeyValuePair<string, string> member in type.Value)
                {
                    // The code declares members its English table lacks; the mod then shows the member name.
                    string en = englishMembers[member.Key] as string ?? member.Key;
                    if (!LocalizationBridge.ShouldFill(members[member.Key] as string, en, member.Key)) continue;
                    members[member.Key] = member.Value;
                    filled++;
                }
            }
            filled += FillSettings(Dictionary(table, "settings", true), Dictionary(english, "settings", false), ours.settings, m, ref unknown);
            if (unknown != 0) m.Warn(unknown + " CLLC catalog entries are unknown to the installed version and were skipped.");
            return filled;
        }
        private static int FillWords(IDictionary target, IDictionary english, Dictionary<string, string> ours, bool placeholders, Module m, ref int unknown)
        {
            int filled = 0;
            foreach (KeyValuePair<string, string> word in ours)
            {
                string en = english[word.Key] as string;
                if (en == null) { unknown++; continue; }
                if (placeholders && !SamePlaceholders(en, word.Value)) { m.Warn("CLLC entry skipped, placeholders differ from English: " + word.Key); continue; }
                if (!LocalizationBridge.ShouldFill(target[word.Key] as string, en, word.Key)) continue;
                target[word.Key] = word.Value;
                filled++;
            }
            return filled;
        }
        private static int FillSettings(IDictionary target, IDictionary english, Dictionary<string, CllcSetting> ours, Module m, ref int unknown)
        {
            Type texts = target.GetType().GetGenericArguments()[1];
            FieldInfo display = AccessTools.Field(texts, "display"), desc = AccessTools.Field(texts, "desc");
            if (display == null || desc == null) { m.Warn("CLLC SettingTexts fields not found; setting texts are not filled."); return 0; }
            int filled = 0;
            foreach (KeyValuePair<string, CllcSetting> setting in ours)
            {
                object en = english[setting.Key];
                if (en == null) { unknown++; continue; }
                string enDisplay = display.GetValue(en) as string ?? setting.Key;
                string enDesc = desc.GetValue(en) as string ?? "";
                if (!SamePlaceholders(enDisplay, setting.Value.display) || !SamePlaceholders(enDesc, setting.Value.desc))
                { m.Warn("CLLC setting skipped, placeholders differ from English: " + setting.Key); continue; }
                object existing = target[setting.Key];
                // A partially translated setting has two independent gaps. An English description
                // must not authorize replacing a Russian name (or prevent filling an English name).
                bool fillDisplay = LocalizationBridge.ShouldFill(existing == null ? null : display.GetValue(existing) as string, enDisplay, setting.Key);
                bool fillDesc = LocalizationBridge.ShouldFill(existing == null ? null : desc.GetValue(existing) as string, enDesc, setting.Key);
                if (!fillDisplay && !fillDesc) continue;
                object value = existing ?? Activator.CreateInstance(texts);
                // A third-party table can share its fallback entry with English. Copy on write in
                // that case only, preserving any other fields without mutating the fallback object.
                if (ReferenceEquals(value, en)) value = AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(value, null);
                if (fillDisplay) display.SetValue(value, setting.Value.display);
                if (fillDesc) desc.SetValue(value, setting.Value.desc);
                target[setting.Key] = value; // Also writes back boxed SettingTexts value types.
                filled++;
            }
            return filled;
        }
        // Repeats CLLC's config() with the current table: category = "<N> - " + group, display and
        // description from the setting whose name, with its first placeholder replaced by the
        // English value, is the entry key; the placeholder shows the translated value. Section and
        // key names, which the .cfg file stores, stay English.
        private static void Relabel(Module m, Type wrapper, object english, ConfigFile config)
        {
            MethodInfo group = AccessTools.Method(wrapper, "getSettingGroupTranslation", new[] { typeof(string) });
            MethodInfo setting = AccessTools.Method(wrapper, "getSettingTranslation", new[] { typeof(string) });
            MethodInfo word = AccessTools.Method(wrapper, "getTranslation", new[] { typeof(string) });
            Type attributes = m.RuntimeAssembly.GetType(AttributesType);
            FieldInfo display = setting == null ? null : AccessTools.Field(setting.ReturnType, "display");
            FieldInfo desc = setting == null ? null : AccessTools.Field(setting.ReturnType, "desc");
            FieldInfo category = attributes == null ? null : AccessTools.Field(attributes, "Category");
            FieldInfo dispName = attributes == null ? null : AccessTools.Field(attributes, "DispName");
            FieldInfo description = attributes == null ? null : AccessTools.Field(attributes, "Description");
            if (group == null || setting == null || word == null || display == null || desc == null || category == null || dispName == null || description == null)
            { m.Warn("CLLC setting lookups not found; the config window keeps the texts the mod bound."); return; }
            IDictionary names = Dictionary(english, "settings", false);
            List<KeyValuePair<string, Regex>> templates = new List<KeyValuePair<string, Regex>>();
            foreach (object key in names.Keys)
            {
                string name = key as string;
                Match token = name == null ? Match.Empty : Placeholder.Match(name);
                if (token.Success)
                    templates.Add(new KeyValuePair<string, Regex>(name, new Regex(@"\A" + Regex.Escape(name.Substring(0, token.Index)) + "(.+?)" +
                        Regex.Escape(name.Substring(token.Index + token.Length)) + @"\z", RegexOptions.CultureInvariant)));
            }
            int failed = 0;
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entry in config)
            {
                object[] tags = entry.Value == null || entry.Value.Description == null ? null : entry.Value.Description.Tags;
                if (tags == null || !tags.Any(delegate(object tag) { return tag != null && tag.GetType() == attributes; })) continue;
                string template = null, value = null;
                if (names.Contains(entry.Key.Key)) template = entry.Key.Key;
                else foreach (KeyValuePair<string, Regex> t in templates)
                {
                    Match match = t.Value.Match(entry.Key.Key);
                    if (match.Success) { template = t.Key; value = match.Groups[1].Value; break; }
                }
                if (template == null) continue;
                try
                {
                    string number = GroupNumber.Match(entry.Key.Section).Value;
                    string shownCategory = number + (string)group.Invoke(null, new object[] { entry.Key.Section.Substring(number.Length) });
                    object texts = setting.Invoke(null, new object[] { template });
                    string shownName = display.GetValue(texts) as string ?? template, shownDesc = desc.GetValue(texts) as string;
                    if (value != null)
                    {
                        string token = Placeholder.Match(template).Value, shownValue = (string)word.Invoke(null, new object[] { value });
                        shownName = shownName.Replace(token, shownValue);
                        if (shownDesc != null) shownDesc = shownDesc.Replace(token, shownValue);
                    }
                    foreach (object tag in tags)
                    {
                        if (tag == null || tag.GetType() != attributes) continue;
                        // Attributes the mod passes for other purposes (drawers, visibility) carry no texts.
                        if (category.GetValue(tag) != null) category.SetValue(tag, shownCategory);
                        if (dispName.GetValue(tag) != null) dispName.SetValue(tag, shownName);
                        if (description.GetValue(tag) != null && shownDesc != null) description.SetValue(tag, shownDesc);
                    }
                }
                catch (TargetInvocationException) { failed++; }
            }
            if (failed != 0) m.Warn(failed + " CLLC config entries kept their texts: the mod's lookup failed.");
        }
        private static IDictionary Dictionary(object owner, string name, bool create)
        {
            FieldInfo field = AccessTools.Field(owner.GetType(), name);
            IDictionary value = field.GetValue(owner) as IDictionary;
            if (value == null && create)
            {
                value = (IDictionary)Activator.CreateInstance(field.FieldType);
                field.SetValue(owner, value);
            }
            return value ?? new Hashtable();
        }
        private static bool SamePlaceholders(string english, string russian)
        {
            return new HashSet<string>(Placeholder.Matches(english).Cast<Match>().Select(delegate(Match p) { return p.Value; }))
                .SetEquals(Placeholder.Matches(russian ?? "").Cast<Match>().Select(delegate(Match p) { return p.Value; }));
        }
    }
}
