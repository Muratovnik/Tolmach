using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class TextEngineTests : TestContextBase
    {
        public static IEnumerable<string> Modules { get { return ModuleIds(); } }
        public static IEnumerable<TestCaseData> Regressions
        {
            get { return Fixture<List<Regression>>("regressions.json").Select(r => new TestCaseData(r.module, r.source, r.expected).SetName("RealString: " + r.module + " / " + r.source)); }
        }
        private static string Fill(string text)
        {
            return Regex.Replace(text, @"\{(\d+)\}", m => (17 + Int32.Parse(m.Groups[1].Value)).ToString(CultureInfo.InvariantCulture));
        }
        [TestCaseSource("Modules")]
        public void CatalogLookupAndLanguageSwitchUseProductionAssembly(string id)
        {
            Module m = LoadModule(id);
            foreach (var pair in m.texts)
            {
                TextEngine.IsRussian = true;
                Assert.That(TextEngine.Display(pair.Key, id), Is.EqualTo(pair.Value), pair.Key);
                TextEngine.IsRussian = false;
                Assert.That(TextEngine.Display(pair.Key, id), Is.EqualTo(pair.Key), "English: " + pair.Key);
            }
            TextEngine.IsRussian = true;
            foreach (PatternSpec pattern in m.patterns)
                Assert.That(TextEngine.Display(Fill(pattern.source), id), Is.EqualTo(Fill(pattern.target)), pattern.source);
            foreach (LiteralSpec rule in m.literals)
                foreach (var pair in rule.values)
                {
                    Assert.That(TextEngine.Literal(pair.Key, id, rule.type, rule.method), Is.EqualTo(pair.Value));
                    Assert.That(TextEngine.Literal(pair.Key, id, "Another.Type", rule.method), Is.EqualTo(pair.Key));
                }
            Assert.That(TextEngine.Display("__UNKNOWN_IDENTIFIER__", id), Is.EqualTo("__UNKNOWN_IDENTIFIER__"));
            Assert.That(TextEngine.Display("LumberAxe", id), Is.EqualTo("LumberAxe"));
            Assert.That(TextEngine.Display(null, id), Is.Null);
        }
        [TestCaseSource("Modules")]
        public void RawCatalogEntriesTranslateOnDisplay(string id)
        {
            Module m = LoadModule(id);
            RawDisplay.Initialize();
            foreach (var pair in m.rawTexts)
                Assert.That(RawDisplay.Translate(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            foreach (PatternSpec pattern in m.rawPatterns)
            {
                // A known term for each term argument, an opaque name otherwise.
                Func<Match, bool, string> fill = (h, russian) =>
                {
                    string semantic;
                    if (pattern.arguments.TryGetValue(h.Groups[1].Value, out semantic) && semantic.StartsWith("term:"))
                    {
                        var term = m.terms[semantic.Substring(5)].OrderBy(t => t.Key, StringComparer.Ordinal).First();
                        return russian ? term.Value : term.Key;
                    }
                    return "Zq" + h.Groups[1].Value;
                };
                string source = Regex.Replace(pattern.source, @"\{(\d+)\}", h => fill(h, false));
                Assert.That(RawDisplay.Translate(source), Is.EqualTo(Regex.Replace(pattern.target, @"\{(\d+)\}", h => fill(h, true))), pattern.source);
            }
        }
        [TestCaseSource("Regressions")]
        public void VerifiedRealStrings(string module, string source, string expected)
        {
            LoadModule(module);
            Assert.That(TextEngine.Display(source, module), Is.EqualTo(expected));
        }
        [Test]
        public void CombinedValidationErrorsPreserveOpaqueArguments()
        {
            LoadModule("TradersExtended");
            foreach (Composition c in Fixture<List<Composition>>("composition.json"))
            {
                TextEngine.IsRussian = true;
                string result = String.Concat(c.parts.Select(p => p.literal == null ? p.opaque :
                    TextEngine.Literal(p.literal, "TradersExtended", "TradersExtended.ConfigEditor", p.method)));
                Assert.That(result, Is.EqualTo(c.expected), c.name);
                Assert.That(TextEngine.Display("Row 1: " + result, "TradersExtended"), Is.EqualTo("Строка 1: " + c.expected));
                TextEngine.IsRussian = false;
                Assert.That(String.Concat(c.parts.Select(p => p.literal == null ? p.opaque :
                    TextEngine.Literal(p.literal, "TradersExtended", "TradersExtended.ConfigEditor", p.method))),
                    Is.EqualTo(String.Concat(c.parts.Select(p => p.literal ?? p.opaque))));
            }
        }
        [Test]
        public void NestedMessagesHaveABudgetAndFilenamesRemainOpaque()
        {
            LoadModule("TradersExtended");
            string nested = String.Concat(Enumerable.Repeat("Row 1: ", 30)) + "Item prefab is required.";
            string translated = TextEngine.Display(nested, "TradersExtended");
            Assert.That(Regex.Matches(translated, "Строка 1: ").Count, Is.EqualTo(5));
            Assert.That(TextEngine.Display("Row Mark: Item prefab is required.", "TradersExtended"), Is.EqualTo("Row Mark: Item prefab is required."));
            Assert.That(TextEngine.Display("Saving Price...", "TradersExtended"), Is.EqualTo("Сохранение Price..."));
        }
        [Test]
        public void ArbitraryNamesSubstringsAndWhitespaceArePreserved()
        {
            Module m = new Module { id = "fixture", UiAllowed = true };
            m.texts["Price"] = "Цена";
            m.patterns.Add(new PatternSpec { source = "Hello, {0}!", target = "Здравствуйте, {0}!" });
            m.Table = new TextTable(m); TextEngine.Modules.Add(m.id, m);
            Assert.That(TextEngine.Display("Hello, Price!", m.id), Is.EqualTo("Здравствуйте, Price!"));
            Assert.That(TextEngine.Display("myPriceID", m.id), Is.EqualTo("myPriceID"));
            Assert.That(TextEngine.Display("  Price\r\nPrice", m.id), Is.EqualTo("  Цена\r\nЦена"));
            Assert.That(TextEngine.Display("Price", "missing"), Is.EqualTo("Price"));
            string[] source = { "Price", "Unrelated" };
            Assert.That(TextEngine.DisplayArray(source, m.id), Is.EqualTo(new[] { "Цена", "Unrelated" }));
            Assert.That(source, Is.EqualTo(new[] { "Price", "Unrelated" }), "UI array adapter must not mutate caller data.");
        }
        [Test]
        public void MissingRuntimeContextDoesNotBreakTheCallingUi()
        {
            Assert.That(TextEngine.Display("Price", null), Is.EqualTo("Price"));
            Assert.That(TextEngine.Literal(null, "fixture", "T", "M"), Is.Null);
            Assert.That(TextEngine.Literal("Price", null, "T", "M"), Is.EqualTo("Price"));
            TextEngine.Modules["unbound"] = new Module { id = "unbound" };
            Assert.That(TextEngine.Display("Price", "unbound"), Is.EqualTo("Price"));
        }
        [TestCase("Animals", "Animals")]
        [TestCase("3m 8s", "3 мин 8 с")]
        public void NumericTimeTemplatesDoNotTranslateArbitraryWords(string source, string expected)
        {
            LoadModule("CarturFeedingTrough");
            Assert.That(TextEngine.Display(source, "CarturFeedingTrough"), Is.EqualTo(expected));
        }
    }
}
