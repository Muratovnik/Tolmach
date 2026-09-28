using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Tolmach
{
    // Refresh only known persistent widgets. No PinData, config, ZDO, network or input fields are written.
    internal static class PersistentUi
    {
        private static Type minimap;
        private static readonly List<WeakReference> PortalLabels = new List<WeakReference>();
        internal static bool StandardMapLabels = true;

        internal static void Install(Harmony harmony)
        {
            minimap = RuntimeAccess.ExactType("Minimap");
            if (StandardMapLabels && TextEngine.Modules.Values.Any(delegate(Module m) { return m.UiAllowed && m.mapLabels.Count != 0; }))
            {
                bool found = false;
                if (minimap != null)
                    foreach (MethodInfo m in minimap.GetMethods(AccessTools.allDeclared))
                    {
                        if (m.Name != "CreateMapNamePin" || !m.GetParameters().Any(delegate(ParameterInfo p) { return p.ParameterType.FullName == "Minimap+PinData"; })) continue;
                        try
                        {
                            harmony.Patch(m, postfix: new HarmonyMethod(typeof(PersistentUi).GetMethod("AfterPinCreated", AccessTools.all)) { priority = Priority.Last });
                            found = true;
                        }
                        catch (Exception e) { Plugin.Warn("Map label adapter: " + e.GetType().Name); }
                    }
                if (!found) Plugin.Warn("Minimap.CreateMapNamePin(PinData, ...) unavailable: map labels remain original; no data-changing fallback.");
            }
            Module portal;
            if (TextEngine.Modules.TryGetValue("PortalLines", out portal) && portal.UiAllowed)
            {
                Type toggle = portal.RuntimeAssembly.GetType("PortalLines.UI.MapToggle", false);
                MethodInfo create = toggle == null ? null : toggle.GetMethods(AccessTools.allDeclared).FirstOrDefault(delegate(MethodInfo m) { return m.Name == "Create" && m.GetParameters().Length == 1; });
                if (create == null) portal.Warn("Persistent MapToggle.Create adapter unavailable.");
                else
                    try { harmony.Patch(create, postfix: new HarmonyMethod(typeof(PersistentUi).GetMethod("AfterToggleCreated", AccessTools.all)) { priority = Priority.Last }); }
                    catch (Exception e) { portal.Warn("Persistent toggle adapter: " + e.GetType().Name); }
            }
            Refresh();
        }
        private static void AfterPinCreated(MethodBase __originalMethod, object[] __args)
        {
            ParameterInfo[] parameters = __originalMethod.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].ParameterType.FullName == "Minimap+PinData") ApplyPin(__args[i]);
        }
        internal static void ApplyPin(object pin)
        {
            if (!StandardMapLabels || pin == null) return;
            try
            {
                string original = RuntimeAccess.Read(pin, "m_name") as string;
                object nameData = RuntimeAccess.Read(pin, "m_NamePinData");
                object label = RuntimeAccess.Read(nameData, "PinNameText");
                if (String.IsNullOrEmpty(original) || label == null) return;
                foreach (Module m in TextEngine.Modules.Values)
                {
                    string translated;
                    if (!m.UiAllowed || !m.mapLabels.TryGetValue(original, out translated)) continue;
                    UpdateLabel(label, original, translated);
                    break;
                }
            }
            catch (Exception e) { Plugin.Warn("Map display refresh skipped: " + e.GetType().Name); }
        }
        private static void AfterToggleCreated(object __instance)
        {
            try
            {
                object toggle = RuntimeAccess.Read(__instance, "_toggle");
                object gameObject = RuntimeAccess.Read(toggle, "gameObject");
                Type tmp = RuntimeAccess.ExactType("TMPro.TMP_Text");
                if (gameObject == null || tmp == null) return;
                MethodInfo find = gameObject.GetType().GetMethod("GetComponentInChildren", AccessTools.all, null, new[] { typeof(Type), typeof(bool) }, null);
                object label = find == null ? null : find.Invoke(gameObject, new object[] { tmp, true });
                if (label == null) return;
                PortalLabels.RemoveAll(delegate(WeakReference w) { return !w.IsAlive; });
                if (!PortalLabels.Any(delegate(WeakReference w) { return Object.ReferenceEquals(w.Target, label); })) PortalLabels.Add(new WeakReference(label));
                ApplyToggle(label);
            }
            catch (Exception e) { WarnPortal("Toggle label refresh skipped: " + e.GetType().Name); }
        }
        private static bool ApplyToggle(object label)
        {
            Module m;
            string translated;
            if (label == null || !TextEngine.Modules.TryGetValue("PortalLines", out m) || !m.UiAllowed || !m.texts.TryGetValue("Portal lines", out translated)) return false;
            try { UpdateLabel(label, "Portal lines", translated); return true; }
            catch { return false; } // Destroyed Unity widgets must not survive scene changes in the weak registry.
        }
        internal static void UpdateLabel(object label, string original, string translated)
        {
            PropertyInfo text = label.GetType().GetProperty("text", AccessTools.all);
            if (text == null || !text.CanRead || !text.CanWrite || text.PropertyType != typeof(string)) return;
            string current = text.GetValue(label, null) as string;
            // Preserve an existing third-party translation or unrelated dynamic UI content.
            if (current != original && current != translated) return;
            string desired = TextEngine.IsRussian ? translated : original;
            if (current != desired) text.SetValue(label, desired, null);
        }
        internal static void Refresh()
        {
            // Language changes are rare. Never scan every pin on every frame.
            try
            {
                object map = minimap == null ? null : (RuntimeAccess.Read(minimap, "s_instance") ?? RuntimeAccess.Read(minimap, "m_instance"));
                IEnumerable pins = RuntimeAccess.Read(map, "m_pins") as IEnumerable;
                if (pins != null && StandardMapLabels)
                    foreach (object pin in pins) ApplyPin(pin);
            }
            catch (Exception e) { Plugin.Warn("Map language refresh: " + e.GetType().Name); }
            PortalLabels.RemoveAll(delegate(WeakReference w) { return !ApplyToggle(w.Target); });
        }
        private static void WarnPortal(string message)
        {
            Module m;
            if (TextEngine.Modules.TryGetValue("PortalLines", out m)) m.Warn(message);
        }
        internal static void Reset() { PortalLabels.Clear(); minimap = null; }
    }
}
