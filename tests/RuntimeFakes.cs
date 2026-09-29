// Environment-only endpoints. Tests reference the compiled production plugin via ProjectReference.
// Nothing here is a replacement DLL for Valheim or Unity; these types stay in the test assembly.
// Shapes follow Valheim 1.0.16 as decompiled for review: private members stay non-public
// (internal here, so fixtures in this assembly can drive them), declaring types match.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public static class GUI
    {
        public static string LastText;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Label(string text) { LastText = text; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Window(int id, string text) { LastText = text; return id; }
    }
}
// These types are fixtures, not game or mod DLL replacements. Never distribute them in BepInEx/plugins.
public sealed class TestLabel { public string text { get; set; } }
public sealed class Minimap
{
    public sealed class PinNameData
    {
        public readonly PinData ParentPin;
        public TestLabel PinNameText { get; private set; }
        public PinNameData(PinData pin) { ParentPin = pin; }
        internal void SetTextAndGameObject(TestLabel text)
        {
            PinNameText = text;
            PinNameText.text = Localization.instance.Localize(ParentPin.m_name);
        }
    }
    public sealed class PinData { public string m_name; public PinNameData m_NamePinData; }
    private static Minimap s_instance;
    private readonly List<PinData> m_pins = new List<PinData>();
    public static string Discovered;
    internal static Minimap CreateForTests() { s_instance = new Minimap(); return s_instance; }
    internal static void ResetForTests() { s_instance = null; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public PinData AddPin(string name)
    {
        PinData pin = new PinData { m_name = name };
        m_pins.Add(pin); return pin;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DiscoverLocation(string name) { Discovered = name; }
    // The game creates the label widget only when the large map shows a named pin.
    internal void ShowNamePin(PinData pin)
    {
        if (pin.m_NamePinData != null) return;
        pin.m_NamePinData = new PinNameData(pin);
        CreateMapNamePin(pin, null);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CreateMapNamePin(PinData namePin, object root) { namePin.m_NamePinData.SetTextAndGameObject(new TestLabel()); }
}
public sealed class Localization
{
    public sealed class LruCache
    {
        internal int Clears;
        internal readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public void EvictAll() { Clears++; Values.Clear(); }
    }
    // Stand-ins for the game's CSV resources and PlatformPrefs("language").
    internal static readonly Dictionary<string, Dictionary<string, string>> Resources = new Dictionary<string, Dictionary<string, string>>();
    internal static string SelectedLanguage = "English";
    private static Localization m_instance;
    private readonly Dictionary<string, string> m_translations = new Dictionary<string, string>();
    private readonly LruCache m_cache = new LruCache();
    internal readonly List<string> AddedKeys = new List<string>();
    internal int CacheClears { get { return m_cache.Clears; } }
    public static Localization instance
    {
        get { if (m_instance == null) m_instance = new Localization(); return m_instance; }
    }
    internal static bool HasInstance { get { return m_instance != null; } }
    private Localization()
    {
        SetupLanguage("English");
        SetupLanguage(SelectedLanguage);
    }
    // Mods (SkillManager) construct extra instances; the constructor still loads the player's language.
    internal static Localization CreateDetachedForTests() { return new Localization(); }
    internal static void ResetForTests() { m_instance = null; SelectedLanguage = "English"; Resources.Clear(); }
    public string GetSelectedLanguage() { return SelectedLanguage; }
    public void SetLanguage(string language)
    {
        if (SelectedLanguage == language) return;
        SelectedLanguage = language;
        Clear();
        SetupLanguage(language);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool SetupLanguage(string language)
    {
        Dictionary<string, string> words;
        if (Resources.TryGetValue(language, out words))
            foreach (KeyValuePair<string, string> word in words) AddWord(word.Key, word.Value);
        return true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void AddWord(string key, string text) { AddedKeys.Add(key); m_translations.Remove(key); m_translations.Add(key, text); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal string Translate(string word) { string value; return m_translations.TryGetValue(word, out value) ? value : "[" + word + "]"; }
    private void Clear() { m_translations.Clear(); m_cache.EvictAll(); }
    private static readonly char[] EndChars = " (){}[]+-!?/\\&%,.:-=<>\n".ToCharArray();
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Localize(string text)
    {
        if (String.IsNullOrEmpty(text)) return text;
        string value;
        if (!m_cache.Values.TryGetValue(text, out value))
        {
            // The game's token scan: every "$word" up to the next end character.
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            int position = 0, start;
            while (position < text.Length - 1 && (start = text.IndexOf('$', position)) != -1)
            {
                int end = text.IndexOfAny(EndChars, start);
                if (end == -1) end = text.Length;
                s.Append(text, position, start - position).Append(Translate(text.Substring(start + 1, end - start - 1)));
                position = end;
            }
            value = s.Append(text.Substring(position)).ToString();
            m_cache.Values[text] = value;
        }
        return value;
    }
}
// Chat log lines are declared on Terminal; Chat only receives and routes messages.
public abstract class Terminal
{
    internal readonly List<string> Lines = new List<string>();
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddString(string title, string text, int type, bool timestamp = false) { Lines.Add(title + ": " + text); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddString(string text) { Lines.Add(text); }
}
public sealed class Chat : Terminal
{
    internal readonly List<string> WorldTexts = new List<string>();
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void OnNewChatMessage(object go, long senderID, object pos, int type, object sender, string text)
    {
        AddString("Viking", text, type);
        AddInworldText(go, senderID, pos, type, sender, text);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddInworldText(object go, long senderID, object position, int type, object user, string text) { WorldTexts.Add(text); }
}
public class StatusEffect { public string m_name = ""; }
public sealed class MessageHud { public enum MessageType { TopLeft = 1, Center = 2 } }
// The game's Character.Message is virtual and does nothing; Player overrides it to show the text.
public class Character
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void Message(MessageHud.MessageType type, string msg, int amount = 0) { }
}
public sealed class Player : Character
{
    internal string LastMessage;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void Message(MessageHud.MessageType type, string msg, int amount = 0) { LastMessage = msg; }
}

namespace FixturePlugin
{
    public static class View
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Draw() { UnityEngine.GUI.Label("Price"); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DrawBranch(bool first)
        {
            if (first) UnityEngine.GUI.Label("Price"); else UnityEngine.GUI.Label("Unknown");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DrawExceptionRegion()
        {
            try { UnityEngine.GUI.Label("Price"); } catch (ArgumentException) { UnityEngine.GUI.Label("Unknown"); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Hover() { return "Price"; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Literal() { return "Literal"; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Minimap.PinData Store(Minimap map) { map.DiscoverLocation("Camp"); return map.AddPin("Camp"); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Name(StatusEffect effect) { effect.m_name = "Price"; }
    }
    // A component default: the field initializer is compiled into the instance constructor.
    public sealed class Built
    {
        public string m_name = "Built literal";
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Built() { }
    }
    // Same exception-handling shape as TradersExtended's ConfigEditor.OnGUI: catch ... when inside try/finally.
    public static class Filtered
    {
        public static bool Fail;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Title()
        {
            try
            {
                try { if (Fail) throw new InvalidOperationException(); return "Filtered literal"; }
                catch (InvalidOperationException) when (Fail) { return "Filtered literal"; }
            }
            finally { Fail = false; }
        }
    }
    // Same shape as MWL's Port.RunWhenReady coroutine: an iterator with try/finally compiles to a
    // MoveNext with a fault block.
    public static class Faulted
    {
        public static bool Done;
        public static IEnumerable<string> Lines()
        {
            try { yield return "Faulted literal"; }
            finally { Done = true; }
        }
    }
    // Two methods of one name, like AzuAutoStore's TryStore overloads: only one holds the literal.
    public static class Overloads
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Pick() { return "Overloaded literal"; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Pick(int count) { return "Other " + count; }
    }
    // Same shape as TakeAllCooked's CookingStationHoverTextPatch.Postfix: the hover line ends with a
    // config value, ConfigEntry<string>.Value, not with a literal.
    public static class Configured
    {
        public static BepInEx.Configuration.ConfigEntry<string> HoverText;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Postfix(ref string __result) { __result = __result + "\n[<color=yellow><b>E</b></color>] " + HoverText.Value; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Constant() { return "Take all cooked"; }
    }
    // Same public shape as the LocalizeKey embedded by Blaxxun's Item/Piece/Creature/SkillManager.
    internal sealed class LocalizeKey
    {
        private static readonly List<LocalizeKey> keys = new List<LocalizeKey>();
        public readonly string Key;
        public readonly Dictionary<string, string> Localizations = new Dictionary<string, string>();
        public LocalizeKey(string key) { Key = key.Replace("$", ""); keys.Add(this); }
        public LocalizeKey English(string key) { return addForLang("English", key); }
        public LocalizeKey Russian(string key) { return addForLang("Russian", key); }
        private LocalizeKey addForLang(string lang, string value)
        {
            Localizations[lang] = value;
            if (Localization.HasInstance && Localization.instance.GetSelectedLanguage() == lang)
                Localization.instance.AddWord(Key, value);
            return this;
        }
        internal static void ResetForTests() { keys.Clear(); }
    }
}

namespace FixtureGame
{
    // Shape of Valheim 1.0.16 Version/GameVersion. The game declares them in the global
    // namespace, where a fake would shadow System.Version in this assembly.
    public struct GameVersion
    {
        public int m_major;
        public int m_minor;
        public int m_patch;
        public GameVersion(int major, int minor, int patch) { m_major = major; m_minor = minor; m_patch = patch; }
    }
    public static class Version
    {
        public static GameVersion CurrentVersion { get; } = new GameVersion(1, 0, 16);
    }
}

namespace FixtureOptional
{
    // UnityEngine.AudioModule is referenced for compilation but deliberately not copied
    // beside the tests, like a mod's optional integration whose dependency is absent.
    public static class Integration
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static UnityEngine.AudioClip OptionalClip() { return null; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Draw() { UnityEngine.GUI.Label("Price"); }
    }
}
// A plugin type declared outside any namespace, like Amazing Nature's MonsterDoorSensor.
public static class GlobalFixture
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static string Label() { return "Global literal"; }
}

namespace CreatureLevelControl
{
    // Same member shape as the language table of Creature Level & Loot Control 4.6.4: a private
    // static current table and private language, public dictionaries, a static English table and
    // static lookups that fall back to English. Only a small English table stands in for its YAML.
    public class LocalizationWrapper
    {
        public struct SettingTexts { public string display; public string desc; }
        private string language;
        public Dictionary<string, string> creatureGender;
        public Dictionary<string, string> genderedCreatureTranslations;
        public Dictionary<string, Dictionary<string, string>> genderedTranslations;
        public Dictionary<string, string> translations;
        public Dictionary<string, Dictionary<string, string>> enumTranslations;
        public Dictionary<string, string> settingGroups;
        public Dictionary<string, SettingTexts> settings;
        private static LocalizationWrapper instance = new LocalizationWrapper();
        private static LocalizationWrapper english;
        // A Russian.yml that a user put next to the mod's DLL.
        internal static LocalizationWrapper UserFile;
        internal static LocalizationWrapper Current { get { return instance; } }
        internal string Language { get { return language; } }
        public static LocalizationWrapper English { get { return english ?? (english = loadLanguage("English")); } }
        public static void LoadLanguage(string language) { instance = loadLanguage(language); }
        private static LocalizationWrapper loadLanguage(string language)
        {
            if (language == "Russian" && UserFile != null) { UserFile.language = language; return UserFile; }
            return new LocalizationWrapper
            {
                language = "English",
                creatureGender = new Dictionary<string, string> { { "default", "n" } },
                genderedCreatureTranslations = new Dictionary<string, string> { { "n", "[{effect} ][{infusion}-Infused ]{name}[ the {affix}]" } },
                translations = new Dictionary<string, string> { { "Aggressive", "Aggressive" }, { "MinimapSectorLevel", "Sector Level: {level}" }, { "ConfigEditorSave", "Save and Apply" } },
                enumTranslations = new Dictionary<string, Dictionary<string, string>>
                {
                    { "Toggle", new Dictionary<string, string> { { "On", "On" }, { "Off", "Off" } } },
                    { "CreatureSectorWorldLevel", new Dictionary<string, string> { { "Six", "Six" } } },
                },
                settingGroups = new Dictionary<string, string> { { "General", "General" }, { "Creature Affix chances", "Creature Affix chances" } },
                settings = new Dictionary<string, SettingTexts>
                {
                    { "Lock Configuration", new SettingTexts { desc = "The configuration is locked and may not be changed by clients." } },
                    { "Chance for {effect} effect to spawn (percentage)", new SettingTexts { desc = "Chance for {effect} effect creatures to spawn (percentage)." } },
                },
            };
        }
        public static string getTranslation(string textEnglish) { return instance.getTranslationInternal(textEnglish) ?? textEnglish; }
        public string getTranslationInternal(string textEnglish)
        {
            string value;
            Dictionary<string, string> forms;
            if (genderedTranslations != null && genderedTranslations.TryGetValue(creatureGender["default"], out forms) && forms.TryGetValue(textEnglish, out value)) return value;
            if (translations.TryGetValue(textEnglish, out value)) return value;
            return this == English ? null : English.getTranslationInternal(textEnglish);
        }
        public static SettingTexts getSettingTranslation(string textEnglish) { return instance.SettingTranslation(textEnglish); }
        public SettingTexts SettingTranslation(string textEnglish)
        {
            SettingTexts texts;
            if (!settings.TryGetValue(textEnglish, out texts))
            {
                if (this == English) throw new InvalidOperationException("Missing settings entry in English for " + textEnglish);
                texts = English.SettingTranslation(textEnglish);
            }
            if (texts.display == null) texts.display = textEnglish;
            return texts;
        }
        public static string getSettingGroupTranslation(string textEnglish)
        {
            string value;
            if (instance.settingGroups.TryGetValue(textEnglish, out value)) return value;
            if (instance == English) throw new InvalidOperationException("Missing settings group in English for " + textEnglish);
            return English.settingGroups[textEnglish];
        }
        internal static void ResetForTests() { instance = new LocalizationWrapper(); english = null; UserFile = null; }
    }
    public class ConfigurationManagerAttributes
    {
        public bool? Browsable;
        public string Category;
        public string Description;
        public string DispName;
        public int? Order;
    }
}
// Methods this Harmony cannot rebuild, with display calls: their text is translated where it arrives.
namespace FixtureCallSites
{
    // Same shape as TradersExtended's ConfigEditor.OnGUI: a window title drawn in a body with catch ... when.
    public static class FilteredWindow
    {
        public static bool Fail;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Draw()
        {
            try { UnityEngine.GUI.Window(1, "Price"); if (Fail) throw new InvalidOperationException(); }
            catch (InvalidOperationException) when (Fail) { Fail = false; }
        }
    }
    // Same shape as MWL's Port.RunWhenReady: a coroutine with try/finally messages the player.
    public static class FaultedMessage
    {
        public static bool Done;
        public static IEnumerable<int> Run(Character user)
        {
            try { user.Message(MessageHud.MessageType.Center, "Price"); yield return 0; }
            finally { Done = true; }
        }
    }
}
// Another mod drawing the same text.
namespace FixtureOther
{
    public static class Window
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Draw() { UnityEngine.GUI.Window(2, "Price"); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Message(Character user) { user.Message(MessageHud.MessageType.Center, "Price"); }
    }
}
// Same shape as MyLittleUI's StationRepair.RepairOnHold: the text goes through Localize, which is left as is.
namespace FixtureLeftAsIs
{
    public static class Filtered
    {
        public static bool Fail;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Draw()
        {
            try { return Localization.instance.Localize("Price"); }
            catch (InvalidOperationException) when (Fail) { return ""; }
        }
    }
}
