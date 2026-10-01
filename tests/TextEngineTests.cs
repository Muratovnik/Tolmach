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
        private static string Fill(string text, PatternSpec pattern, Module module, bool russian)
        {
            return Regex.Replace(text, @"\{(\d+)\}", m => {
                string semantic;
                if (pattern.arguments.TryGetValue(m.Groups[1].Value, out semantic) && semantic.StartsWith("term:", StringComparison.Ordinal))
                {
                    var term = module.terms[semantic.Substring(5)].OrderBy(t => t.Key, StringComparer.Ordinal).First();
                    return russian ? term.Value : term.Key;
                }
                return (17 + Int32.Parse(m.Groups[1].Value)).ToString(CultureInfo.InvariantCulture);
            });
        }
        [TestCaseSource("Modules")]
        public void CatalogLookupAndLanguageSwitchUseProductionAssembly(string id)
        {
            Module m = LoadModule(id);
            foreach (var pair in m.texts)
            {
                TextEngine.IsRussian = true;
                Assert.That(id == "ConditionalConfigSync" ? m.Table.Translate(pair.Key) : TextEngine.Display(pair.Key, id), Is.EqualTo(pair.Value), pair.Key);
                TextEngine.IsRussian = false;
                Assert.That(TextEngine.Display(pair.Key, id), Is.EqualTo(pair.Key), "English: " + pair.Key);
            }
            TextEngine.IsRussian = true;
            foreach (PatternSpec pattern in m.patterns)
                Assert.That(id == "ConditionalConfigSync" ? m.Table.Translate(Fill(pattern.source, pattern, m, false)) : TextEngine.Display(Fill(pattern.source, pattern, m, false), id), Is.EqualTo(Fill(pattern.target, pattern, m, true)), pattern.source);
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
        [Test]
        public void SingleLinePatternsCannotAbsorbAnotherLineAndExistingPatternsStillCan()
        {
            Module m = new Module();
            m.patterns.Add(new PatternSpec { source = "Name {0}.", target = "Имя {0}." });
            Assert.That(new TextTable(m).Translate("Name first\nsecond."), Is.EqualTo("Имя first\nsecond."), "Absent flag preserves existing multiline contracts.");
            m.patterns[0].singleLine = true;
            Assert.That(new TextTable(m).Translate("Name first\nsecond."), Is.EqualTo("Name first\nsecond."));
            Assert.That(new TextTable(m).Translate("Name first\r\nsecond."), Is.EqualTo("Name first\r\nsecond."));
            Assert.That(new TextTable(m).Translate("Name first.\r\nName second."), Is.EqualTo("Имя first.\r\nИмя second."));
        }
        [Test]
        public void CcsCaptureAmbiguityGuardLeavesOtherModulesAndTheFourArgumentConstructorUnchanged()
        {
            const string value = "The client has version 1, but the server requires at least 2, but the server requires at least 3.";
            Module m = CatalogLoader.Read(Catalog("ConditionalConfigSync"));
            Assert.That(new TextTable(m).Translate(value), Is.EqualTo(value));
            const string prior = "Клиент имеет версию 1, но сервер требует не ниже 2, but the server requires at least 3.";
            Assert.That(new TextTable(m.texts, m.patterns, m.mapLabels, m.terms).Translate(value), Is.EqualTo(prior));
            m.id = "ordinary";
            Assert.That(new TextTable(m).Translate(value), Is.EqualTo(prior));
        }
        [TestCaseSource("Modules")]
        public void RawCatalogEntriesTranslateOnDisplay(string id)
        {
            Module m = LoadModule(id);
            RawDisplay.Initialize();
            bool creatureNames = id == CreatureNameDisplay.ModuleId;
            if (creatureNames)
            {
                // The binding fixture is metadata-only; supply the executable mod API fixture.
                m.RuntimeAssembly = typeof(NameCharacter).Assembly;
                CreatureNameDisplay.Install(Patcher, typeof(NameCharacter), typeof(NameTameable), typeof(NameScene));
            }
            foreach (var pair in m.rawTexts)
                Assert.That(RawDisplay.Translate(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            foreach (PatternSpec pattern in m.rawPatterns)
            {
                // A known term for each term argument, a number for a numeric one, an opaque name otherwise.
                Func<Match, bool, string> fill = (h, russian) =>
                {
                    string semantic;
                    if (pattern.arguments.TryGetValue(h.Groups[1].Value, out semantic) && semantic.StartsWith("term:"))
                    {
                        var term = m.terms[semantic.Substring(5)].OrderBy(t => t.Key, StringComparer.Ordinal).First();
                        return russian ? term.Value : term.Key;
                    }
                    int index = Int32.Parse(h.Groups[1].Value, CultureInfo.InvariantCulture);
                    return pattern.numeric.Contains(index) ? (17 + index).ToString(CultureInfo.InvariantCulture) : "Zq" + h.Groups[1].Value;
                };
                string source = Regex.Replace(pattern.source, @"\{(\d+)\}", h => fill(h, false));
                string expected = Regex.Replace(pattern.target, @"\{(\d+)\}", h => fill(h, true));
                if (creatureNames)
                {
                    NameCharacter marked = CreatureNameDisplayTests.Marked(source);
                    NameCharacter clone = CreatureNameDisplayTests.RegisteredClone(source);
                    Assert.That(marked.GetHoverName(), Is.EqualTo(expected), pattern.source);
                    Assert.That(clone.GetHoverName(), Is.EqualTo(expected), pattern.source);
                    Assert.That(RawDisplay.Translate(source), Is.EqualTo(source), "Global: " + pattern.source);
                    Assert.That(new NameCharacter { m_name = source }.GetHoverName(), Is.EqualTo(source), "Unrelated: " + pattern.source);
                    TextEngine.IsRussian = false;
                    Assert.That(marked.GetHoverName(), Is.EqualTo(source), "English: " + pattern.source);
                    TextEngine.IsRussian = true;
                }
                else Assert.That(RawDisplay.Translate(source), Is.EqualTo(expected), pattern.source);
            }
        }
        [Test]
        public void ItemAndSetArgumentsShowTheGameNamesOfObjectsAModPrintsByIdentifier()
        {
            // The set block of Recipe Description Expansion: the set by its capitalized m_setName, parts by prefab name.
            Localization.SelectedLanguage = "Russian";
            Localization.Resources["Russian"] = new Dictionary<string, string> {
                { "item_helmet_trollleather", "Кожаный шлем тролля" }, { "se_trollseteffect_name", "Скрытный" }, { "hunter_Bronze", "Охотник" } };
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
            Assert.That(Localization.instance, Is.Not.Null);
            GameItems.ItemToken = prefab => prefab == "HelmetTrollLeather" ? "$item_helmet_trollleather" : null;
            GameItems.SetToken = shown => shown.Equals("troll", StringComparison.OrdinalIgnoreCase) ? "$se_trollseteffect_name"
                : shown.Equals("$hunter_Bronze", StringComparison.OrdinalIgnoreCase) ? "$hunter_Bronze" : null;
            Module m = FixtureModule("sets", "FixturePlugin");
            string part = "<color=#008000ff>{0}</color>\t├> <color={1}>{2}</color>";
            m.patterns.Add(new PatternSpec { source = part, target = part, arguments = { { "2", "item" } } });
            m.patterns.Add(new PatternSpec { source = "<color={0}>{1} ({2}/{3}):</color>", target = "<color={0}>{1} ({2} из {3}):</color>",
                                             arguments = { { "1", "itemSet" } }, numeric = { 2, 3 } });
            Activate(m);
            string block = "\n\n<color=#96d4fd>Troll (1/4):</color>\n<color=#008000ff>✔️</color>\t├> <color=#FDFD96>HelmetTrollLeather</color>\n" +
                           "<color=#008000ff>❌</color>\t├> <color=#808080ff>CapeTrollHide</color>\n";
            Assert.That(TextEngine.Display(block, "sets"), Is.EqualTo(
                "\n\n<color=#96d4fd>Скрытный (1 из 4):</color>\n<color=#008000ff>✔️</color>\t├> <color=#FDFD96>Кожаный шлем тролля</color>\n" +
                "<color=#008000ff>❌</color>\t├> <color=#808080ff>CapeTrollHide</color>\n"), "An unknown prefab keeps its identifier.");
            Assert.That(TextEngine.Display("<color=#808080ff>$hunter_bronze (0/4):</color>", "sets"),
                Is.EqualTo("<color=#808080ff>Охотник (0 из 4):</color>"), "A lowercased set key is found again.");
            TextEngine.IsRussian = false;
            Assert.That(TextEngine.Display(block, "sets"), Is.EqualTo(block));
        }
        [TestCase("\n")]
        [TestCase("\r\n")]
        public void RecipeSetTooltipsTranslateEveryPartAndPreserveTheirLineEndings(string partEnding)
        {
            Localization.SelectedLanguage = "Russian";
            Localization.Resources["Russian"] = new Dictionary<string, string> {
                { "item_armor_berserkerchest", "Медвежья повязка" },
                { "item_armor_berserkerlegs", "Медвежьи штаны" },
                { "item_armor_berserkerhood", "Медвежий капюшон" },
                { "item_helmet_trollleather", "Кожаный шлем тролля" },
                { "se_berserkerset_name", "Берсерк" }, { "se_trollseteffect_name", "Скрытный" }
            };
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
            Assert.That(Localization.instance, Is.Not.Null);
            Dictionary<string, string> itemTokens = new Dictionary<string, string> {
                { "ArmorBerserkerChest", "$item_armor_berserkerchest" },
                { "ArmorBerserkerLegs", "$item_armor_berserkerlegs" },
                { "ArmorBerserkerHood", "$item_armor_berserkerhood" },
                { "HelmetTrollLeather", "$item_helmet_trollleather" }
            };
            GameItems.ItemToken = prefab => itemTokens.ContainsKey(prefab) ? itemTokens[prefab] : null;
            GameItems.SetToken = shown => shown.Equals("berserker", StringComparison.OrdinalIgnoreCase) ? "$se_berserkerset_name"
                : shown.Equals("troll", StringComparison.OrdinalIgnoreCase) ? "$se_trollseteffect_name" : null;
            LoadModule("RecipeDescriptionExpansion");
            string block = "\n\n<color=#96d4fd>Berserker (2/3):</color>\n" +
                "<color=#008000ff>✔️</color>\t├> <color=#FDFD96>ArmorBerserkerChest</color>" + partEnding +
                "<color=#008000ff>✔️</color>\t├> <color=#FDFD96>ArmorBerserkerLegs</color>" + partEnding +
                "<color=#008000ff>❌</color>\t├> <color=#808080ff>ArmorBerserkerHood</color>" + partEnding +
                "\n<color=#96d4fd>Troll (1/4):</color>\n" +
                "<color=#008000ff>✔️</color>\t├> <color=#FDFD96>HelmetTrollLeather</color>" + partEnding +
                "<color=#008000ff>❌</color>\t├> <color=#808080ff>UnknownSetPart</color>" + partEnding;
            string expected = "\n\n<color=#96d4fd>Берсерк (2/3):</color>\n" +
                "<color=#008000ff>✔️</color>\t├> <color=#FDFD96>Медвежья повязка</color>" + partEnding +
                "<color=#008000ff>✔️</color>\t├> <color=#FDFD96>Медвежьи штаны</color>" + partEnding +
                "<color=#008000ff>❌</color>\t├> <color=#808080ff>Медвежий капюшон</color>" + partEnding +
                "\n<color=#96d4fd>Скрытный (1/4):</color>\n" +
                "<color=#008000ff>✔️</color>\t├> <color=#FDFD96>Кожаный шлем тролля</color>" + partEnding +
                "<color=#008000ff>❌</color>\t├> <color=#808080ff>UnknownSetPart</color>" + partEnding;
            Assert.That(TextEngine.Display(block, "RecipeDescriptionExpansion"), Is.EqualTo(expected));
            TextEngine.IsRussian = false;
            Assert.That(TextEngine.Display(block, "RecipeDescriptionExpansion"), Is.EqualTo(block));
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
        // A no-break space between a number and its unit, as in the game's own strings.
        [TestCase("3m 8s", "3\u00a0мин 8\u00a0с")]
        public void NumericTimeTemplatesDoNotTranslateArbitraryWords(string source, string expected)
        {
            LoadModule("CarturFeedingTrough");
            Assert.That(TextEngine.Display(source, "CarturFeedingTrough"), Is.EqualTo(expected));
        }
    }
}
