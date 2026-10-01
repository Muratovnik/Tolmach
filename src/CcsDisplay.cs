using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Tolmach
{
    internal static class CcsDisplay
    {
        private sealed class Reason
        {
            internal string Prefix, Message;
        }
        private sealed class CaptureContext
        {
            internal CaptureContext Previous;
            internal Module Owner;
            internal object Before, Published;
            internal Dictionary<string, Reason> Lines;
            internal bool Invalid;
        }
        [ThreadStatic] private static CaptureContext context;
        private static readonly object Gate = new object();
        private static Dictionary<string, Reason> lines = new Dictionary<string, Reason>(StringComparer.Ordinal);
        private static readonly HashSet<string> Headers = new HashSet<string>(StringComparer.Ordinal) {
            "Conditional Config Sync rejected this connection.",
            "The following synchronization checks failed:",
            "A missing handshake can mean that the mod is missing or disabled, an older pre-CCS build is installed, Conditional Config Sync did not load, or duplicate or outdated DLL files are present.",
            "Check that the listed mods and Conditional Config Sync are installed, enabled, and up to date. Remove duplicate or outdated DLL files, then fully restart the game.",
            "The server rejected the connection because a required synchronization check failed, but no detailed reason was provided."
        };
        private static Module owner;
        private static FieldInfo modName, message, pendingField, reportMessage;
        private static object boundReport;
        private static string boundMessage;
        internal static void Install(Harmony harmony, Module module)
        {
            if (module.id != "ConditionalConfigSync") return;
            Reset();
            if (!module.UiAllowed || module.RuntimeCodeAssembly == null || module.codeAssembly != "ConditionalConfigSync") return;
            try
            {
                Type version = module.RuntimeCodeAssembly.GetType("ConditionalConfigSync.VersionCheck", false);
                Type reason = version == null ? null : version.GetNestedType("DisconnectReasonItem", BindingFlags.NonPublic);
                Type report = version == null ? null : version.GetNestedType("PendingDisconnectReport", BindingFlags.NonPublic);
                FieldInfo nameField = reason == null ? null : reason.GetField("ModName", AccessTools.allDeclared);
                FieldInfo messageField = reason == null ? null : reason.GetField("Message", AccessTools.allDeclared);
                MethodInfo method = version == null ? null : version.GetMethods(AccessTools.allDeclared)
                    .SingleOrDefault(m => m.Name == "NormalizeDisconnectReasons");
                FieldInfo pending = version == null ? null : version.GetField("pendingDisconnectReport", AccessTools.allDeclared);
                FieldInfo reportText = report == null ? null : report.GetField("Message", AccessTools.allDeclared);
                MethodInfo setter = version == null ? null : version.GetMethods(AccessTools.allDeclared)
                    .SingleOrDefault(m => m.Name == "SetPendingDisconnectReport");
                if (reason == null || nameField == null || messageField == null || nameField.IsStatic || messageField.IsStatic ||
                    !module.OwnType(version) ||
                    nameField.FieldType != typeof(string) || messageField.FieldType != typeof(string) || method == null ||
                    !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != reason.MakeArrayType() ||
                    method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(IEnumerable<>).MakeGenericType(reason) ||
                    !RuntimeAccess.ManagedBody(method) || DisplayPatches.UnsupportedHandler(method) != null ||
                    report == null || pending == null || !pending.IsStatic || pending.FieldType != report ||
                    reportText == null || reportText.IsStatic || reportText.FieldType != typeof(string) ||
                    setter == null || !setter.IsStatic || setter.ContainsGenericParameters || setter.ReturnType != typeof(void) ||
                    !setter.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(long), typeof(string), typeof(IEnumerable<>).MakeGenericType(reason) }) ||
                    !RuntimeAccess.ManagedBody(setter) || DisplayPatches.UnsupportedHandler(setter) != null)
                    throw new MissingMethodException("Structured setter, normalized reasons or pending report shape differs.");
                lock (Gate) { owner = module; modName = nameField; message = messageField; pendingField = pending; reportMessage = reportText; }
                harmony.Patch(setter,
                    prefix: new HarmonyMethod(typeof(CcsDisplay).GetMethod("Begin", AccessTools.all)) { priority = Priority.First },
                    postfix: new HarmonyMethod(typeof(CcsDisplay).GetMethod("Bind", AccessTools.all)) { priority = Priority.Last },
                    finalizer: new HarmonyMethod(typeof(CcsDisplay).GetMethod("Finish", AccessTools.all)) { priority = Priority.Last });
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(CcsDisplay).GetMethod("Capture", AccessTools.all)) { priority = Priority.Last });
            }
            catch (Exception e)
            {
                Reset();
                module.Warn("CCS structured rejection display unavailable: " + e.GetType().Name + "; unstructured reasons stay unchanged.");
            }
        }
        private static void Begin(ref CaptureContext __state)
        {
            lock (Gate)
            {
                __state = new CaptureContext { Previous = context, Owner = owner };
                context = __state;
                if (owner == null) return;
                try { __state.Before = pendingField.GetValue(null); }
                catch (Exception e) { __state.Invalid = true; owner.Warn("CCS report context skipped: " + e.GetType().Name); }
            }
        }
        // Read only the finalized array. Never enumerate the source again or change a reason used by RPC/logging.
        private static void Capture(object __result)
        {
            lock (Gate)
            {
                CaptureContext active = context;
                if (owner == null || active == null || !ReferenceEquals(active.Owner, owner)) return;
                if (active.Lines != null) { active.Invalid = true; return; }
                try
                {
                    active.Lines = new Dictionary<string, Reason>(StringComparer.Ordinal);
                    Array reasons = __result as Array;
                    if (reasons == null || reasons.Rank != 1 || reasons.Length > 1024)
                        throw new InvalidOperationException("Normalized reason array is unavailable or exceeds the display bound.");
                    foreach (object value in reasons)
                    {
                        if (value == null) continue;
                        string name = modName.GetValue(value) as string, source = message.GetValue(value) as string;
                        if (name == null || source == null || (name + source).IndexOfAny(new[] { '\r', '\n' }) >= 0)
                            throw new InvalidOperationException("Reason fields are not normalized single-line strings.");
                        string prefix = "- " + (String.IsNullOrWhiteSpace(name) ? "" : name + ": ");
                        string line = prefix + source;
                        if (line.Length > 4096) throw new InvalidOperationException("Reason line exceeds the display bound.");
                        Reason previous;
                        if (active.Lines.TryGetValue(line, out previous))
                        {
                            if (previous == null || previous.Prefix != prefix || previous.Message != source) active.Lines[line] = null;
                        }
                        else active.Lines.Add(line, new Reason { Prefix = prefix, Message = source });
                    }
                }
                catch (Exception e)
                {
                    active.Invalid = true;
                    owner.Warn("CCS normalized rejection metadata skipped: " + e.GetType().Name);
                }
            }
        }
        private static void Bind(CaptureContext __state)
        {
            lock (Gate)
            {
                if (__state == null || __state.Invalid || __state.Lines == null || owner == null || !ReferenceEquals(__state.Owner, owner)) return;
                try
                {
                    object current = pendingField.GetValue(null);
                    if (current == null || ReferenceEquals(current, __state.Before)) return;
                    string source = reportMessage.GetValue(current) as string;
                    if (source == null) throw new InvalidOperationException("Pending report text is unavailable.");
                    var present = new HashSet<string>(source.Replace("\r", "").Split('\n'), StringComparer.Ordinal);
                    lines = __state.Lines.Where(p => present.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                    boundReport = __state.Published = current;
                    boundMessage = source;
                }
                catch (Exception e) { __state.Invalid = true; owner.Warn("CCS report binding skipped: " + e.GetType().Name); }
            }
        }
        private static Exception Finish(Exception __exception, CaptureContext __state)
        {
            lock (Gate)
            {
                if (__state != null && (__exception != null || __state.Invalid) && ReferenceEquals(boundReport, __state.Published))
                { lines.Clear(); boundReport = null; boundMessage = null; }
                context = __state != null && ReferenceEquals(context, __state) ? __state.Previous : null;
            }
            return __exception;
        }
        internal static string Translate(Module module, string text)
        {
            if (!module.UiAllowed || module.RuntimeCodeAssembly == null) return text;
            return TextTable.TranslateRaw(text, value => {
                bool carriage = value.EndsWith("\r", StringComparison.Ordinal);
                string line = carriage ? value.Substring(0, value.Length - 1) : value;
                string result = line;
                lock (Gate)
                {
                    Reason reason;
                    if (ReferenceEquals(owner, module) && module.UiAllowed && module.RuntimeCodeAssembly != null &&
                        boundReport != null && ReferenceEquals(boundReport, pendingField.GetValue(null)) &&
                        boundMessage == reportMessage.GetValue(boundReport) as string && lines.TryGetValue(line, out reason) && reason != null)
                        result = reason.Prefix + module.Table.Translate(reason.Message);
                    else if (Headers.Contains(line)) result = module.Table.TranslateWhole(line);
                }
                return result + (carriage ? "\r" : "");
            }, splitMarkup: false);
        }
        internal static void Reset()
        {
            lock (Gate) { lines.Clear(); owner = null; modName = message = pendingField = reportMessage = null; boundReport = null; boundMessage = null; context = null; }
        }
    }
}
