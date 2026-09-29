using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tolmach
{
    internal enum RuntimeStatus { Initializing, Disabled, MissingCatalog, Active, Failed, Stopped }

    // One instance per Plugin.Awake. Updating a report never borrows the identity of an earlier run.
    internal sealed class RuntimeReport
    {
        internal readonly string SessionId = Guid.NewGuid().ToString("N");
        internal readonly DateTime StartedUtc = DateTime.UtcNow;
        internal RuntimeStatus Status = RuntimeStatus.Initializing;

        internal string Render(bool russian, IEnumerable<Module> modules, IEnumerable<string> notes)
        {
            StringBuilder s = new StringBuilder();
            s.AppendLine("Tolmach " + Plugin.PluginVersion);
            s.AppendLine("Session: " + SessionId);
            s.AppendLine("Started UTC: " + StartedUtc.ToString("o"));
            s.AppendLine("UTC: " + DateTime.UtcNow.ToString("o"));
            s.AppendLine("Status: " + Status);
            s.AppendLine("Russian selected: " + russian);
            s.AppendLine("Russian active: " + (Status == RuntimeStatus.Active && russian));
            s.AppendLine("Status describes plugin initialization; language flags do not confirm translation of every screen.");
            s.AppendLine("Check the session time against the current BepInEx log. An unloaded plugin cannot update this file.");
            s.AppendLine("Warnings cover known adapter targets, not every untranslated string.");
            s.AppendLine("No network requests; no player names, account IDs or save contents are collected.");
            foreach (Module m in modules.OrderBy(delegate(Module x) { return x.id; }, StringComparer.Ordinal))
            {
                s.AppendLine(m.id + " | package=" + m.version + " | plugin=" + m.pluginVersion + " | exactVersion=" + m.ExactVersion +
                    " | uiAllowed=" + m.UiAllowed + " | keys=" + m.words.Count + " | nativeKeysAdded=" + m.NativeWords +
                    " | methods inspected=" + m.ScannedMethods + " | UI methods patched=" + m.PatchedMethods);
                foreach (PatchEvidence p in m.Patches.Values.OrderBy(delegate(PatchEvidence x) { return x.method; }, StringComparer.Ordinal))
                {
                    if (p.notPatched != null)
                        s.AppendLine("  " + p.method + " | notPatched=" + p.notPatched + " | callSites=" + Listed(p.callSites) + " | leftAsIs=" + Listed(p.leftAsIs));
                    else
                        s.AppendLine("  " + p.method + " | displayCalls=" + p.displayCalls + " | displayFields=" + p.displayFields +
                            " | literals=" + p.literals + " | config=" + p.configTexts + " | return=" + p.returnAdapter + " | skippedEH=" + p.skippedBoundaries);
                }
                foreach (string warning in m.Warnings) s.AppendLine("  WARNING: " + warning);
            }
            foreach (string note in notes) s.AppendLine(note);
            return s.ToString();
        }

        internal void Write(string path, bool russian, IEnumerable<Module> modules, IEnumerable<string> notes)
        {
            File.WriteAllText(path, Render(russian, modules, notes), new UTF8Encoding(false));
        }

        private static string Listed(List<string> names) { return names.Count == 0 ? "none" : String.Join(",", names.ToArray()); }
    }
}
