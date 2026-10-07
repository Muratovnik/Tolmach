using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class RawDisplayTests : TestContextBase
    {
        private static Module RawModule(string id)
        {
            Module m = FixtureModule(id, "FixturePlugin");
            m.rawTexts["Bleeding"] = "Кровотечение";
            m.rawTexts["$piece_workbench . For ship work."] = "$piece_workbench: для работ с кораблями.";
            m.rawTexts["Rancid"] = "Гнилой";
            m.rawTexts["A simple bow."] = "Простой лук.";
            m.terms["ship"] = new Dictionary<string, string> { { "Karve", "Карви" } };
            m.terms["feature"] = new Dictionary<string, string> { { "Anchor", "Якорь" } };
            m.rawPatterns.Add(new PatternSpec { source = "{0} Guardian", target = "{0}-страж", arguments = new Dictionary<string, string> { { "0", "text" } } });
            m.rawPatterns.Add(new PatternSpec { source = "Plan {0}: {1}", target = "Чертёж ({0}): {1}",
                arguments = new Dictionary<string, string> { { "0", "term:ship" }, { "1", "term:feature" } } });
            return Activate(m);
        }
        [Test]
        public void ExactLinesAndMarkupSpansAreTranslated()
        {
            RawModule("raw"); RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate("Bleeding"), Is.EqualTo("Кровотечение"));
            Assert.That(RawDisplay.Translate("  Bleeding "), Is.EqualTo("  Кровотечение "));
            Assert.That(RawDisplay.Translate("<color=orange>Bleeding</color>\nA simple bow."), Is.EqualTo("<color=orange>Кровотечение</color>\nПростой лук."));
            Assert.That(RawDisplay.Translate("Bleeding heavily"), Is.EqualTo("Bleeding heavily"), "No substring replacement.");
            Assert.That(RawDisplay.Translate("<Bleeding>"), Is.EqualTo("<Bleeding>"), "Markup is not text.");
            Assert.That(RawDisplay.Translate(null), Is.Null);
        }
        [Test]
        public void ComposedNamesUseTemplatesAndOnlyKnownTerms()
        {
            RawModule("raw"); RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate("Грейдворф Guardian"), Is.EqualTo("Грейдворф-страж"));
            Assert.That(RawDisplay.Translate("Rancid Guardian"), Is.EqualTo("Гнилой-страж"));
            Assert.That(RawDisplay.Translate("Plan Karve: Anchor"), Is.EqualTo("Чертёж (Карви): Якорь"));
            Assert.That(RawDisplay.Translate("Plan Karve: Ram"), Is.EqualTo("Plan Karve: Ram"), "An unknown term keeps the whole original name.");
            Assert.That(RawDisplay.Translate("Plan Raft: Anchor"), Is.EqualTo("Plan Raft: Anchor"));
        }
        [Test]
        public void OnlyVersionCheckedModulesAndLegacyPrefabFieldsTakePart()
        {
            Module legacy = FixtureModule("legacy", "FixturePlugin");
            legacy.texts["Lumber axe"] = "Топор лесоруба";
            legacy.prefabs.Add(new PrefabSpec { name = "AxeLumber", kind = "item", fields = new Dictionary<string, string> { { "m_name", "Lumber axe" } } });
            Activate(legacy);
            RawModule("other").UiAllowed = false;
            RawDisplay.Initialize();
            Assert.That(RawDisplay.HasEntries, Is.True);
            Assert.That(RawDisplay.Translate("Lumber axe"), Is.EqualTo("Топор лесоруба"));
            Assert.That(RawDisplay.Translate("Bleeding"), Is.EqualTo("Bleeding"), "A module on another plugin version adds no raw display text.");
        }
        [Test]
        public void ConflictingRawTextKeepsTheFirstModuleAndWarns()
        {
            RawModule("a_raw");
            Module b = FixtureModule("b_raw", "FixturePlugin"); b.rawTexts["Bleeding"] = "Кровь"; Activate(b);
            RawDisplay.Initialize();
            Assert.That(RawDisplay.Translate("Bleeding"), Is.EqualTo("Кровотечение"));
            Assert.That(b.Warnings, Has.Some.StartsWith("Conflicting raw display text ignored"));
        }
        [Test]
        public void CacheStaysBoundedAndCorrect()
        {
            RawModule("raw"); RawDisplay.Initialize();
            for (int i = 0; i < 10000; i++) RawDisplay.Translate("Player text " + i);
            Assert.That(RawDisplay.Translate("Bleeding"), Is.EqualTo("Кровотечение"));
            Assert.That(RawDisplay.Translate("Player text 3"), Is.EqualTo("Player text 3"));
            ICollection cache = (ICollection)typeof(RawDisplay).GetField("Cache", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.That(cache.Count, Is.LessThanOrEqualTo(4096));
        }
        [Test]
        public void ForgeContainerTitleUsesTheSameRawLocalizationBoundaryAsHover()
        {
            LoadModule("DynamicStorageForge");
            RawDisplay.Initialize();
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
            string inventoryName = "Bronze Stack (Storage)";
            Assert.That(main.Localize(inventoryName), Is.EqualTo("Стопка слитков бронзы (хранилище)"));
            Assert.That(inventoryName, Is.EqualTo("Bronze Stack (Storage)"));
            main.SetLanguage("English");
            Assert.That(main.Localize(inventoryName), Is.EqualTo("Bronze Stack (Storage)"));
        }
        [Test]
        public void LocalizeShowsRawTextInRussianOnlyAndStoredNamesStayOriginal()
        {
            RawModule("raw");
            Localization.Resources["Russian"] = new Dictionary<string, string> { { "enemy_greydwarf", "Грейдворф" }, { "piece_workbench", "Верстак" } };
            Localization.Resources["English"] = new Dictionary<string, string> { { "enemy_greydwarf", "Greydwarf" }, { "piece_workbench", "Workbench" } };
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            RawDisplay.Initialize();
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
            // HumanoidRandomizer stores "<prototype m_name> <variant>" in Character.m_name.
            string stored = "$enemy_greydwarf Guardian";
            Assert.That(main.Localize(stored), Is.EqualTo("Грейдворф-страж"));
            Assert.That(main.Localize("Bleeding"), Is.EqualTo("Кровотечение"));
            Assert.That(main.Localize("$piece_workbench . For ship work."), Is.EqualTo("Верстак: для работ с кораблями."), "Tokens inside raw text still resolve.");
            Assert.That(main.Localize("<b>Bleeding</b>\n$piece_workbench"), Is.EqualTo("<b>Кровотечение</b>\nВерстак"), "Composed tooltip lines.");
            Assert.That(stored, Is.EqualTo("$enemy_greydwarf Guardian"));
            Localization english = Localization.CreateDetachedForTests();
            english.SetupLanguage("English");
            Assert.That(english.Localize("Bleeding"), Is.EqualTo("Bleeding"), "A separate English instance keeps raw text.");
            main.SetLanguage("English");
            Assert.That(main.Localize("Bleeding"), Is.EqualTo("Bleeding"));
            Assert.That(main.Localize(stored), Is.EqualTo("Greydwarf Guardian"));
            Assert.That(main.Localize("$piece_workbench . For ship work."), Is.EqualTo("Workbench . For ship work."));
        }
    }
}
