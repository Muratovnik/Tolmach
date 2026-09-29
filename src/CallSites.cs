using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Tolmach
{
    // A method this Harmony cannot rebuild (DisplayPatches.UnsupportedHandler) keeps its body. The display
    // methods it calls are patched instead: their text argument is translated on entry, and only when the
    // call comes from that method, so another mod's identical text stays as it is.
    internal static class CallSites
    {
        private sealed class Site { public Module Module; public MethodBase Caller; }
        // Keyed by method handle: Harmony passes the patched display method itself as __originalMethod.
        private static readonly Dictionary<IntPtr, List<Site>> Sinks = new Dictionary<IntPtr, List<Site>>();
        private static readonly HashSet<IntPtr> Patched = new HashSet<IntPtr>();
        // A virtual call reaches the override of the runtime type: Player.Message for Character.Message.
        private static readonly string[] DerivedSinkTypes = { "Player", "TMPro.TextMeshProUGUI", "TMPro.TextMeshPro" };
        private const int LastArgument = 7;

        internal static void Cover(Harmony harmony, Module module, string owner, MethodBase method, List<CodeInstruction> body, string reason)
        {
            PatchEvidence evidence = new PatchEvidence { method = owner, notPatched = reason };
            module.Patches[RuntimeAccess.MethodKey(method)] = evidence;
            HashSet<IntPtr> seen = new HashSet<IntPtr>();
            foreach (CodeInstruction instruction in body)
            {
                FieldInfo field = instruction.operand as FieldInfo;
                if (field != null && instruction.opcode == OpCodes.Stfld && DisplayPatches.DisplayField(field, module))
                { Add(evidence.leftAsIs, field.DeclaringType.Name + "." + field.Name); continue; }
                MethodBase call = instruction.operand as MethodBase;
                if (call == null || !(instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Newobj)) continue;
                bool[] selected = DisplayPatches.StringSink(call);
                if (!selected.Any(delegate(bool b) { return b; }) || !seen.Add(call.MethodHandle.Value)) continue;
                // Localize runs for nearly every text on screen; its keys come from the game's own dictionaries.
                if (DisplayPatches.LocalizedReturn(call)) { Add(evidence.leftAsIs, Name(call)); continue; }
                foreach (MethodBase target in WithOverrides(call))
                {
                    if (!Install(harmony, target, selected)) { Add(evidence.leftAsIs, Name(target)); continue; }
                    List<Site> sites;
                    if (!Sinks.TryGetValue(target.MethodHandle.Value, out sites)) Sinks[target.MethodHandle.Value] = sites = new List<Site>();
                    sites.Add(new Site { Module = module, Caller = method });
                    Add(evidence.callSites, Name(target));
                }
            }
            // Report a loss only where the body itself holds catalog text; the rest is recorded in the runtime report.
            if (evidence.leftAsIs.Count != 0 && HoldsCatalogText(body, module))
                module.Warn("UI patch skipped " + owner + ": " + reason + "; left untranslated: " + String.Join(", ", evidence.leftAsIs.ToArray()));
        }
        internal static void Reset() { Sinks.Clear(); Patched.Clear(); }
        private static void Add(List<string> list, string name) { if (!list.Contains(name)) list.Add(name); }
        private static string Name(MethodBase method) { return method.DeclaringType.Name + "." + method.Name; }
        private static bool HoldsCatalogText(List<CodeInstruction> body, Module module)
        {
            return module.Table != null && body.Any(delegate(CodeInstruction i) {
                string text = i.opcode == OpCodes.Ldstr ? i.operand as string : null;
                return text != null && module.Table.Translate(text) != text;
            });
        }
        private static IEnumerable<MethodBase> WithOverrides(MethodBase sink)
        {
            yield return sink;
            MethodInfo method = sink as MethodInfo;
            if (method == null || !method.IsVirtual || method.IsFinal) yield break;
            Type[] parameters = method.GetParameters().Select(delegate(ParameterInfo p) { return p.ParameterType; }).ToArray();
            foreach (string name in DerivedSinkTypes)
            {
                Type derived = RuntimeAccess.ExactType(name);
                if (derived == null || derived == method.DeclaringType || !method.DeclaringType.IsAssignableFrom(derived)) continue;
                MethodInfo over = AccessTools.DeclaredMethod(derived, method.Name, parameters);
                if (over != null && over.GetBaseDefinition() == method.GetBaseDefinition()) yield return over;
            }
        }
        private static bool Install(Harmony harmony, MethodBase target, bool[] selected)
        {
            if (Patched.Contains(target.MethodHandle.Value)) return true;
            if (!RuntimeAccess.ManagedBody(target) || DisplayPatches.UnsupportedHandler(target) != null) return false;
            ParameterInfo[] parameters = target.GetParameters();
            if (parameters.Length != selected.Length) return false;
            List<MethodInfo> prefixes = new List<MethodInfo>();
            for (int i = 0; i < selected.Length; i++)
            {
                if (!selected[i]) continue;
                if (i > LastArgument) return false;
                prefixes.Add(typeof(CallSites).GetMethod((parameters[i].ParameterType == typeof(string[]) ? "Texts" : "Text") + i, AccessTools.all));
            }
            try
            {
                foreach (MethodInfo prefix in prefixes)
                    harmony.Patch(target, prefix: new HarmonyMethod(prefix) { priority = Priority.Last });
            }
            catch (Exception) { return false; }
            Patched.Add(target.MethodHandle.Value);
            return true;
        }
        // One prefix per argument position: Harmony binds __N by index, so these need no argument array.
        private static void Text0(MethodBase __originalMethod, ref string __0) { __0 = Translate(__originalMethod, __0); }
        private static void Text1(MethodBase __originalMethod, ref string __1) { __1 = Translate(__originalMethod, __1); }
        private static void Text2(MethodBase __originalMethod, ref string __2) { __2 = Translate(__originalMethod, __2); }
        private static void Text3(MethodBase __originalMethod, ref string __3) { __3 = Translate(__originalMethod, __3); }
        private static void Text4(MethodBase __originalMethod, ref string __4) { __4 = Translate(__originalMethod, __4); }
        private static void Text5(MethodBase __originalMethod, ref string __5) { __5 = Translate(__originalMethod, __5); }
        private static void Text6(MethodBase __originalMethod, ref string __6) { __6 = Translate(__originalMethod, __6); }
        private static void Text7(MethodBase __originalMethod, ref string __7) { __7 = Translate(__originalMethod, __7); }
        private static void Texts0(MethodBase __originalMethod, ref string[] __0) { __0 = TranslateAll(__originalMethod, __0); }
        private static void Texts1(MethodBase __originalMethod, ref string[] __1) { __1 = TranslateAll(__originalMethod, __1); }
        private static void Texts2(MethodBase __originalMethod, ref string[] __2) { __2 = TranslateAll(__originalMethod, __2); }
        private static void Texts3(MethodBase __originalMethod, ref string[] __3) { __3 = TranslateAll(__originalMethod, __3); }
        private static void Texts4(MethodBase __originalMethod, ref string[] __4) { __4 = TranslateAll(__originalMethod, __4); }
        private static void Texts5(MethodBase __originalMethod, ref string[] __5) { __5 = TranslateAll(__originalMethod, __5); }
        private static void Texts6(MethodBase __originalMethod, ref string[] __6) { __6 = TranslateAll(__originalMethod, __6); }
        private static void Texts7(MethodBase __originalMethod, ref string[] __7) { __7 = TranslateAll(__originalMethod, __7); }
        private static string Translate(MethodBase sink, string value)
        {
            List<Site> sites;
            if (!TextEngine.IsRussian || String.IsNullOrEmpty(value) || !Sinks.TryGetValue(sink.MethodHandle.Value, out sites)) return value;
            MethodBase caller = null;
            foreach (Site site in sites)
            {
                string translated = TextEngine.Display(value, site.Module.id);
                if (translated == value) continue;
                // The stack is read only for text a covered module translates.
                if (caller == null) caller = Caller(sink);
                if (caller != null && Same(caller, site.Caller)) return translated;
            }
            return value;
        }
        private static string[] TranslateAll(MethodBase sink, string[] values)
        {
            if (!TextEngine.IsRussian || values == null || !Sinks.ContainsKey(sink.MethodHandle.Value)) return values;
            string[] copy = null;
            for (int i = 0; i < values.Length; i++)
            {
                string translated = Translate(sink, values[i]);
                if (translated == values[i]) continue;
                if (copy == null) copy = (string[])values.Clone();
                copy[i] = translated;
            }
            return copy ?? values;
        }
        // The first frame below the display method. Harmony's replacement of that method is generated code:
        // without a declaring type (a dynamic method) or in a dynamic assembly.
        private static MethodBase Caller(MethodBase sink)
        {
            StackTrace trace = new StackTrace(false);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                MethodBase frame = trace.GetFrame(i).GetMethod();
                if (frame == null || frame.DeclaringType == null || frame.DeclaringType == typeof(CallSites) ||
                    frame.DeclaringType.Assembly.IsDynamic || Same(frame, sink)) continue;
                return frame;
            }
            return null;
        }
        private static bool Same(MethodBase a, MethodBase b)
        {
            try { return a.MethodHandle.Value == b.MethodHandle.Value; }
            catch (Exception) { return false; }
        }
    }
}
