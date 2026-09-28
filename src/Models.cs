using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace Tolmach
{
    public sealed class PatternSpec
    {
        public string source = "";
        public string target = "";
        public List<int> numeric = new List<int>();
        // Only explicitly declared semantic arguments may be translated. Others stay verbatim.
        public Dictionary<string, string> arguments = new Dictionary<string, string>();
    }
    public sealed class MethodSpec
    {
        public string type = "";
        public string method = "";
    }
    public sealed class LiteralSpec
    {
        public string type = "";
        public string method = "";
        public Dictionary<string, string> values = new Dictionary<string, string>();
    }
    public sealed class PrefabSpec
    {
        public string name = "";
        public string kind = "";
        public Dictionary<string, string> fields = new Dictionary<string, string>();
    }
    public sealed class PatchEvidence
    {
        public string method;
        public int displayCalls;
        public int displayFields;
        public int literals;
        public int skippedBoundaries;
        public bool returnAdapter;
        public readonly HashSet<string> matchedLiterals = new HashSet<string>(StringComparer.Ordinal);
    }
    public sealed class Module
    {
        public string id = "";
        public string package = "";
        public string version = "";
        public string pluginVersion = "";
        public string assembly = "";
        public List<string> guids = new List<string>();
        public List<string> namespaces = new List<string>();
        public Dictionary<string, string> words = new Dictionary<string, string>();
        public Dictionary<string, string> englishWords = new Dictionary<string, string>();
        public Dictionary<string, string> texts = new Dictionary<string, string>();
        public Dictionary<string, string> mapLabels = new Dictionary<string, string>();
        public List<PatternSpec> patterns = new List<PatternSpec>();
        public List<MethodSpec> returns = new List<MethodSpec>();
        public List<LiteralSpec> literals = new List<LiteralSpec>();
        public List<PrefabSpec> prefabs = new List<PrefabSpec>();
        [JsonIgnore] public Assembly RuntimeAssembly;
        [JsonIgnore] public TextTable Table;
        [JsonIgnore] public bool ExactVersion;
        [JsonIgnore] public int PatchedMethods;
        [JsonIgnore] public int ScannedMethods;
        [JsonIgnore] public bool HasDisplayText { get { return texts.Count != 0 || patterns.Count != 0; } }
        [JsonIgnore] public bool NeedsIlAdapters { get { return HasDisplayText || literals.Count != 0 || returns.Count != 0; } }
        [JsonIgnore] public int NativeWords;
        [JsonIgnore] public bool UiAllowed;
        [JsonIgnore] public readonly Dictionary<string, PatchEvidence> Patches = new Dictionary<string, PatchEvidence>();
        [JsonIgnore] public readonly List<string> Warnings = new List<string>();

        public void Warn(string message)
        {
            if (Warnings.Contains(message)) return;
            Warnings.Add(message);
            Plugin.Warn(id + ": " + message);
        }
        public bool OwnType(Type type)
        {
            string ns = type.Namespace ?? "";
            foreach (string allowed in namespaces)
                if (ns == allowed || ns.StartsWith(allowed + ".", StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
