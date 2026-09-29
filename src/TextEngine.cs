using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Tolmach
{
    // Tables retain their owner's patterns and vocabularies; composition selects an owner per source fragment.
    public sealed class TextTable
    {
        private sealed class Template
        {
            public Regex Matcher;
            public string Target;
            public string RequiredFragment;
            public Dictionary<string, string> Arguments;
        }
        private readonly Dictionary<string, string> exact;
        private readonly Dictionary<string, string> mapLabels;
        private readonly Dictionary<string, Dictionary<string, string>> terms;
        private const int MaxNestedMessages = 4;
        private readonly List<Template> templates = new List<Template>();
        private readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly object cacheLock = new object();
        private static readonly Regex Hole = new Regex(@"\{(\d+)\}", RegexOptions.CultureInvariant);
        private static readonly Regex Tags = new Regex("(<[^>]+>)", RegexOptions.CultureInvariant);

        public TextTable(Module module) : this(module.texts, module.patterns, module.mapLabels, module.terms) { }
        public TextTable(Dictionary<string, string> texts, IEnumerable<PatternSpec> patterns, Dictionary<string, string> labels,
                         Dictionary<string, Dictionary<string, string>> vocabularies)
        {
            exact = new Dictionary<string, string>(texts, StringComparer.Ordinal);
            mapLabels = new Dictionary<string, string>(labels, StringComparer.Ordinal);
            terms = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Dictionary<string, string>> t in vocabularies)
                terms[t.Key] = new Dictionary<string, string>(t.Value, StringComparer.Ordinal);
            foreach (PatternSpec p in patterns)
            {
                MatchCollection holes = Hole.Matches(p.source);
                StringBuilder expression = new StringBuilder(@"\A");
                HashSet<string> seen = new HashSet<string>();
                int offset = 0;
                foreach (Match h in holes)
                {
                    expression.Append(Regex.Escape(p.source.Substring(offset, h.Index - offset)));
                    string group = "p" + h.Groups[1].Value;
                    string body = p.numeric.Contains(Int32.Parse(h.Groups[1].Value)) ? @"[0-9]+(?:[.,][0-9]+)?" : ".*?";
                    expression.Append(seen.Add(group) ? "(?<" + group + ">" + body + ")" : @"\k<" + group + ">");
                    offset = h.Index + h.Length;
                }
                expression.Append(Regex.Escape(p.source.Substring(offset))).Append(@"\z");
                templates.Add(new Template {
                    Matcher = new Regex(expression.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant,
                                        TimeSpan.FromMilliseconds(20)),
                    Target = p.target,
                    RequiredFragment = LongestFragment(p.source),
                    Arguments = new Dictionary<string, string>(p.arguments, StringComparer.Ordinal)
                });
            }
        }
        private static string LongestFragment(string source)
        {
            string longest = "";
            foreach (string part in Hole.Replace(source, "\u001f").Split('\u001f'))
                if (part.Length > longest.Length) longest = part;
            return longest;
        }
        public string Translate(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 32768) return value;
            string cached;
            lock (cacheLock) { if (cache.TryGetValue(value, out cached)) return cached; }
            string result = Whole(value, 0);
            // Keep whitespace and Unity markup intact when translating composed hover text.
            if (result == value && value.IndexOf('\n') >= 0)
            {
                string[] lines = value.Split('\n');
                for (int i = 0; i < lines.Length; i++) lines[i] = Whole(lines[i], 0);
                result = String.Join("\n", lines);
            }
            // Do not retain names/dialogue/player text that did not match a translation.
            if (result != value)
                lock (cacheLock) { if (cache.Count >= 1024) cache.Clear(); cache[value] = result; }
            return result;
        }
        private string Whole(string value, int depth)
        {
            string result;
            if (exact.TryGetValue(value, out result)) return result;
            string trimmed = value.Trim();
            if (trimmed != value && exact.TryGetValue(trimmed, out result))
            {
                int start = value.IndexOf(trimmed, StringComparison.Ordinal);
                return value.Substring(0, start) + result + value.Substring(start + trimmed.Length);
            }
            foreach (Template p in templates)
            {
                if (p.RequiredFragment.Length > 0 && value.IndexOf(p.RequiredFragment, StringComparison.Ordinal) < 0) continue;
                Match match;
                try { match = p.Matcher.Match(value); }
                catch (RegexMatchTimeoutException) { continue; }
                if (!match.Success) continue;
                bool unknownTerm = false;
                string output = Hole.Replace(p.Target, delegate(Match h) {
                    string captured = match.Groups["p" + h.Groups[1].Value].Value;
                    string semantic;
                    if (!p.Arguments.TryGetValue(h.Groups[1].Value, out semantic)) return captured;
                    string translated;
                    if (semantic == "text") return exact.TryGetValue(captured, out translated) ? translated : captured;
                    if (semantic == "mapLabel") return MapLabel(captured);
                    // A game object printed by its identifier: shown under the game's own name for it.
                    if (semantic == "item") return GameItems.ItemName(captured) ?? captured;
                    if (semantic == "itemSet") return GameItems.SetName(captured) ?? captured;
                    Dictionary<string, string> vocabulary;
                    if (semantic.StartsWith("term:", StringComparison.Ordinal))
                    {
                        if (terms.TryGetValue(semantic.Substring(5), out vocabulary) && vocabulary.TryGetValue(captured, out translated)) return translated;
                        unknownTerm = true;
                        return captured;
                    }
                    // Only a declared nested message can recurse; its identifiers still stay opaque.
                    return semantic == "message" && depth < MaxNestedMessages ? Whole(captured, depth + 1) : captured;
                });
                // A half-translated composite name is worse than the original: try the next template.
                if (unknownTerm) continue;
                return output;
            }
            return value;
        }
        internal string TranslateWhole(string value)
        {
            return String.IsNullOrEmpty(value) || value.Length > 32768 ? value : Whole(value, 0);
        }
        // One traversal for both a single table and the cross-module display path. The callback
        // receives only original text: a translation must never become another table's input.
        public string TranslateRaw(string value)
        {
            return TranslateRaw(value, TranslateWhole);
        }
        internal static string TranslateRaw(string value, Func<string, string> translateWhole)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 32768) return value;
            string result = translateWhole(value);
            if (result != value || (value.IndexOf('\n') < 0 && value.IndexOf('<') < 0)) return result;
            string[] spans = Tags.Split(value);
            bool changed = false;
            for (int i = 0; i < spans.Length; i += 2)
            {
                if (spans[i].Length == 0) continue;
                string[] lines = spans[i].Split('\n');
                for (int j = 0; j < lines.Length; j++)
                {
                    string translated = translateWhole(lines[j]);
                    if (translated == lines[j]) continue;
                    lines[j] = translated;
                    changed = true;
                }
                spans[i] = String.Join("\n", lines);
            }
            return changed ? String.Concat(spans) : value;
        }
        public string MapLabel(string value)
        {
            if (value == null) return null;
            string translated;
            return mapLabels.TryGetValue(value, out translated) ? translated : value;
        }
        public void Clear() { lock (cacheLock) cache.Clear(); }
    }

    public static class TextEngine
    {
        public static volatile bool IsRussian;
        internal static readonly Dictionary<string, Module> Modules = new Dictionary<string, Module>(StringComparer.Ordinal);

        // These methods are public because Harmony-generated code lives in other assemblies.
        public static string Display(string text, string moduleId)
        {
            if (!IsRussian || text == null || moduleId == null) return text;
            Module module;
            if (!Modules.TryGetValue(moduleId, out module) || module.Table == null) return text;
            return module.Table.Translate(text);
        }
        public static string[] DisplayArray(string[] texts, string moduleId)
        {
            if (!IsRussian || texts == null) return texts;
            string[] copy = (string[])texts.Clone();
            for (int i = 0; i < copy.Length; i++) copy[i] = Display(copy[i], moduleId);
            return copy;
        }
        public static string Literal(string value, string moduleId, string typeName, string methodName)
        {
            return FromRules(value, moduleId, delegate(Module m) { return m.literals; }, typeName, methodName);
        }
        // A config value read in the named method: only the unchanged English default is replaced.
        public static string ConfigText(string value, string moduleId, string typeName, string methodName)
        {
            return FromRules(value, moduleId, delegate(Module m) { return m.configTexts; }, typeName, methodName);
        }
        private static string FromRules(string value, string moduleId, Func<Module, List<LiteralSpec>> rules, string typeName, string methodName)
        {
            if (!IsRussian || value == null || moduleId == null) return value;
            Module module;
            if (!Modules.TryGetValue(moduleId, out module)) return value;
            foreach (LiteralSpec spec in rules(module))
            {
                string translated;
                if (spec.type == typeName && spec.method == methodName && spec.values.TryGetValue(value, out translated))
                    return translated;
            }
            return value;
        }
    }
}
