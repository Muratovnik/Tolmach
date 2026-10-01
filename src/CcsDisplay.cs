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
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Reason> Lines = new Dictionary<string, Reason>(StringComparer.Ordinal);
        private static readonly HashSet<string> Headers = new HashSet<string>(StringComparer.Ordinal) {
            "Conditional Config Sync rejected this connection.",
            "The following synchronization checks failed:",
            "A missing handshake can mean that the mod is missing or disabled, an older pre-CCS build is installed, Conditional Config Sync did not load, or duplicate or outdated DLL files are present.",
            "Check that the listed mods and Conditional Config Sync are installed, enabled, and up to date. Remove duplicate or outdated DLL files, then fully restart the game.",
            "The server rejected the connection because a required synchronization check failed, but no detailed reason was provided."
        };
        private static Module owner;
        private static FieldInfo modName, message;
        internal static void Install(Harmony harmony, Module module)
        {
            if (module.id != "ConditionalConfigSync") return;
            Reset();
            if (!module.UiAllowed || module.RuntimeCodeAssembly == null || module.codeAssembly != "ConditionalConfigSync") return;
            try
            {
                Type version = module.RuntimeCodeAssembly.GetType("ConditionalConfigSync.VersionCheck", false);
                Type reason = version == null ? null : version.GetNestedType("DisconnectReasonItem", BindingFlags.NonPublic);
                FieldInfo nameField = reason == null ? null : reason.GetField("ModName", AccessTools.allDeclared);
                FieldInfo messageField = reason == null ? null : reason.GetField("Message", AccessTools.allDeclared);
                MethodInfo method = version == null ? null : version.GetMethods(AccessTools.allDeclared)
                    .SingleOrDefault(m => m.Name == "NormalizeDisconnectReasons");
                if (reason == null || nameField == null || messageField == null || nameField.IsStatic || messageField.IsStatic ||
                    !module.OwnType(version) ||
                    nameField.FieldType != typeof(string) || messageField.FieldType != typeof(string) || method == null ||
                    !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != reason.MakeArrayType() ||
                    method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(IEnumerable<>).MakeGenericType(reason) ||
                    !RuntimeAccess.ManagedBody(method) || DisplayPatches.UnsupportedHandler(method) != null)
                    throw new MissingMethodException("NormalizeDisconnectReasons/DisconnectReasonItem shape differs.");
                lock (Gate) { owner = module; modName = nameField; message = messageField; }
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(CcsDisplay).GetMethod("Capture", AccessTools.all)) { priority = Priority.Last });
            }
            catch (Exception e)
            {
                Reset();
                module.Warn("CCS structured rejection display unavailable: " + e.GetType().Name + "; unstructured reasons stay unchanged.");
            }
        }
        // Read only the finalized array. Never enumerate the source again or change a reason used by RPC/logging.
        private static void Capture(object __result)
        {
            lock (Gate)
            {
                Lines.Clear();
                if (owner == null) return;
                try
                {
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
                        if (Lines.TryGetValue(line, out previous))
                        {
                            if (previous == null || previous.Prefix != prefix || previous.Message != source) Lines[line] = null;
                        }
                        else Lines.Add(line, new Reason { Prefix = prefix, Message = source });
                    }
                }
                catch (Exception e)
                {
                    Lines.Clear();
                    owner.Warn("CCS normalized rejection metadata skipped: " + e.GetType().Name);
                }
            }
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
                        Lines.TryGetValue(line, out reason) && reason != null)
                        result = reason.Prefix + module.Table.Translate(reason.Message);
                    else if (Headers.Contains(line)) result = module.Table.TranslateWhole(line);
                }
                return result + (carriage ? "\r" : "");
            }, splitMarkup: false);
        }
        internal static void Reset()
        {
            lock (Gate) { Lines.Clear(); owner = null; modName = message = null; }
        }
    }
}
