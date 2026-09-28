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
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Localize(string text)
    {
        if (String.IsNullOrEmpty(text)) return text;
        string value;
        if (!m_cache.Values.TryGetValue(text, out value))
        { value = text.StartsWith("$") ? Translate(text.Substring(1)) : text; m_cache.Values[text] = value; }
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
