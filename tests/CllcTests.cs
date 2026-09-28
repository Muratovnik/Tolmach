using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using NUnit.Framework;
using Tolmach;
using Attributes = CreatureLevelControl.ConfigurationManagerAttributes;
using Module = Tolmach.Module;
using Wrapper = CreatureLevelControl.LocalizationWrapper;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class CllcTests : TestContextBase
    {
        [SetUp]
        public void ResetWrapper() { Wrapper.ResetForTests(); }
        [TearDown]
        public void ClearWrapper() { Wrapper.ResetForTests(); }

        private static Module CllcModule()
        {
            Module m = FixtureModule("CreatureLevelControl", "CreatureLevelControl");
            m.assembly = "CreatureLevelControl"; m.version = m.pluginVersion = "4.6.4";
            m.guids.Add("org.bepinex.plugins.creaturelevelcontrol");
            CllcLanguage t = m.cllc = new CllcLanguage();
            t.creatureGender["default"] = "m"; t.creatureGender["Neck"] = "f";
            t.genderedCreatureTranslations["m"] = t.genderedCreatureTranslations["f"] = "[{effect} ][{infusion} ]{name}[ {affix}]";
            t.genderedTranslations["m"] = new Dictionary<string, string> { { "Aggressive", "Агрессивный" } };
            t.genderedTranslations["f"] = new Dictionary<string, string> { { "Aggressive", "Агрессивная" } };
            t.translations["Aggressive"] = "Агрессивный";
            t.translations["MinimapSectorLevel"] = "Уровень сектора: {level}";
            t.translations["ConfigEditorSave"] = "Сохранить и применить";
            t.enumTranslations["Toggle"] = new Dictionary<string, string> { { "On", "Вкл." }, { "Off", "Выкл." } };
            t.enumTranslations["CreatureSectorWorldLevel"] = new Dictionary<string, string> { { "Six", "Шесть" }, { "Seven", "Семь" } };
            t.settingGroups["General"] = "Общие";
            t.settingGroups["Creature Affix chances"] = "Шансы эффектов существ";
            t.settings["Lock Configuration"] = new CllcSetting { display = "Блокировка настроек", desc = "Клиенты не могут менять настройки." };
            t.settings["Chance for {effect} effect to spawn (percentage)"] = new CllcSetting { display = "Шанс эффекта «{effect}» (%)", desc = "Шанс появления существ с эффектом «{effect}» (%)." };
            return m;
        }

        [Test]
        public void EnglishFallbackIsReplacedByTheCatalogTable()
        {
            Wrapper.LoadLanguage("Russian");
            Module m = CllcModule();
            CllcAdapter.Apply(m, null);
            Assert.That(Wrapper.Current.Language, Is.EqualTo("Russian"));
            Assert.That(Wrapper.getTranslation("MinimapSectorLevel"), Is.EqualTo("Уровень сектора: {level}"));
            Assert.That(Wrapper.getTranslation("Aggressive"), Is.EqualTo("Агрессивный"), "The default gender form.");
            Assert.That(Wrapper.Current.genderedTranslations["f"]["Aggressive"], Is.EqualTo("Агрессивная"));
            Assert.That(Wrapper.getSettingTranslation("Lock Configuration").display, Is.EqualTo("Блокировка настроек"));
            Assert.That(Wrapper.Current.enumTranslations["CreatureSectorWorldLevel"]["Seven"], Is.EqualTo("Семь"), "A member the English table lacks is filled too.");
            Assert.That(Wrapper.English.translations["MinimapSectorLevel"], Is.EqualTo("Sector Level: {level}"), "English stays the fallback.");
            Assert.That(Wrapper.getTranslation("Not in any table"), Is.EqualTo("Not in any table"));
            int filled = m.NativeWords;
            Assert.That(filled, Is.GreaterThan(0));
            object table = Wrapper.Current;
            CllcAdapter.Apply(m, null);
            Assert.That(Wrapper.Current, Is.SameAs(table));
            Assert.That(m.NativeWords, Is.EqualTo(filled), "A second refresh changes nothing.");
        }

        [Test]
        public void RussianTableOfSomeoneElseIsOnlyFilled()
        {
            Wrapper.UserFile = new Wrapper
            {
                creatureGender = new Dictionary<string, string> { { "default", "м" } },
                genderedCreatureTranslations = new Dictionary<string, string> { { "м", "{name}[ {affix}]" } },
                translations = new Dictionary<string, string> { { "MinimapSectorLevel", "Их уровень: {level}" }, { "ConfigEditorSave", "Save and Apply" } },
                enumTranslations = new Dictionary<string, Dictionary<string, string>>(),
                settingGroups = new Dictionary<string, string>(),
                settings = new Dictionary<string, Wrapper.SettingTexts>(),
            };
            Wrapper.LoadLanguage("Russian");
            CllcAdapter.Apply(CllcModule(), null);
            Assert.That(Wrapper.Current, Is.SameAs(Wrapper.UserFile));
            Assert.That(Wrapper.getTranslation("MinimapSectorLevel"), Is.EqualTo("Их уровень: {level}"));
            Assert.That(Wrapper.getTranslation("ConfigEditorSave"), Is.EqualTo("Сохранить и применить"), "An entry left in English is filled.");
            Assert.That(Wrapper.Current.creatureGender.Keys, Is.EquivalentTo(new[] { "default" }), "Its gender codes stay whole.");
            Assert.That(Wrapper.Current.genderedCreatureTranslations["м"], Is.EqualTo("{name}[ {affix}]"));
            Assert.That(Wrapper.Current.genderedTranslations, Is.Null.Or.Empty);
        }

        [Test]
        public void EntryWithOtherPlaceholdersThanEnglishIsSkipped()
        {
            Wrapper.LoadLanguage("Russian");
            Module m = CllcModule();
            m.cllc.translations["MinimapSectorLevel"] = "Уровень сектора";
            m.cllc.settings["Chance for {effect} effect to spawn (percentage)"].display = "Шанс эффекта (%)";
            CllcAdapter.Apply(m, null);
            Assert.That(Wrapper.getTranslation("MinimapSectorLevel"), Is.EqualTo("Sector Level: {level}"));
            Assert.That(Wrapper.getSettingTranslation("Chance for {effect} effect to spawn (percentage)").desc, Is.EqualTo("Chance for {effect} effect creatures to spawn (percentage)."));
            Assert.That(m.Warnings, Has.Some.Contains("MinimapSectorLevel"));
            Assert.That(m.Warnings, Has.Some.Contains("Chance for {effect} effect to spawn (percentage)"));
        }

        [Test]
        public void UnknownEntriesAreCountedOnce()
        {
            Wrapper.LoadLanguage("Russian");
            Module m = CllcModule();
            m.cllc.translations["RemovedInThisVersion"] = "Удалено";
            m.cllc.enumTranslations["RemovedEnum"] = new Dictionary<string, string> { { "One", "Один" } };
            CllcAdapter.Apply(m, null);
            CllcAdapter.Apply(m, null);
            Assert.That(m.Warnings.Where(w => w.Contains("unknown to the installed version")), Has.Exactly(1).Items);
            Assert.That(Wrapper.Current.translations.ContainsKey("RemovedInThisVersion"), Is.False);
        }

        [Test]
        public void NothingChangesWhenTheGameIsNotRussian()
        {
            Wrapper.LoadLanguage("English");
            object table = Wrapper.Current;
            TextEngine.IsRussian = false;
            Module m = CllcModule();
            CllcAdapter.Apply(m, null);
            Assert.That(Wrapper.Current, Is.SameAs(table));
            Assert.That(m.NativeWords, Is.Zero);
        }

        [Test]
        public void BakedConfigTextsAreRecomputedAndKeysStayEnglish()
        {
            Wrapper.LoadLanguage("Russian");
            string path = Path.Combine(Path.GetTempPath(), "tolmach-tests-" + Guid.NewGuid().ToString("N"), "cllc.cfg");
            ConfigFile config = new ConfigFile(path, false) { SaveOnConfigSet = false };
            Attributes visibility = new Attributes { Browsable = true };
            Attributes locked = new Attributes { Order = 0, Category = "1 - General", DispName = "Lock Configuration", Description = "The configuration is locked and may not be changed by clients." };
            config.Bind("1 - General", "Lock Configuration", true, new ConfigDescription("The configuration is locked and may not be changed by clients.", null, visibility, locked));
            Attributes chance = new Attributes { Category = "6 - Creature Affix chances", DispName = "Chance for Aggressive effect to spawn (percentage)", Description = "Chance for Aggressive effect creatures to spawn (percentage)." };
            config.Bind("6 - Creature Affix chances", "Chance for Aggressive effect to spawn (percentage)", 5f, new ConfigDescription("Chance for Aggressive effect creatures to spawn (percentage).", null, chance));
            Attributes other = new Attributes { Category = "9 - Unrelated", DispName = "Not a CLLC setting", Description = "Kept." };
            config.Bind("9 - Unrelated", "Not a CLLC setting", 1, new ConfigDescription("Kept.", null, other));

            Module m = CllcModule();
            CllcAdapter.Apply(m, config);
            Assert.That(locked.Category, Is.EqualTo("1 - Общие"));
            Assert.That(locked.DispName, Is.EqualTo("Блокировка настроек"));
            Assert.That(locked.Description, Is.EqualTo("Клиенты не могут менять настройки."));
            Assert.That(visibility.Category, Is.Null, "An attribute the mod passes for visibility carries no texts.");
            Assert.That(visibility.DispName, Is.Null);
            Assert.That(chance.Category, Is.EqualTo("6 - Шансы эффектов существ"));
            Assert.That(chance.DispName, Is.EqualTo("Шанс эффекта «Агрессивный» (%)"));
            Assert.That(chance.Description, Is.EqualTo("Шанс появления существ с эффектом «Агрессивный» (%)."));
            Assert.That(other.DispName, Is.EqualTo("Not a CLLC setting"));
            Assert.That(config.Keys.Select(k => k.Key), Has.Member("Chance for Aggressive effect to spawn (percentage)"));
            Assert.That(m.Warnings, Is.Empty);
            Assert.That(File.Exists(path), Is.False, "No config file is written.");
        }

        [TestCase("[{effect} ][{infusion} ]{name}[ {affix}]")]
        [TestCase("[{effect} ][{infusion}-Infused ]{name}[ the {affix}]")]
        [TestCase("{name}")]
        public void ValidNameplateTemplatesAreAccepted(string template)
        {
            Module m = CllcModule();
            m.cllc.genderedCreatureTranslations["m"] = template;
            Assert.DoesNotThrow(() => CatalogLoader.Validate(m));
        }

        [TestCase("")]
        [TestCase("[{effect} ]")]
        [TestCase("{name}{name}")]
        [TestCase("[{name}]")]
        [TestCase("{name}[ ]")]
        [TestCase("[{effect} {infusion} ]{name}")]
        [TestCase("{effect} {name}")]
        [TestCase("{name} {level}")]
        [TestCase("{name}[ [{affix}]]")]
        [TestCase("{name}[ {affix}")]
        [TestCase("{name}}")]
        public void InvalidNameplateTemplatesAreRejected(string template)
        {
            Module m = CllcModule();
            m.cllc.genderedCreatureTranslations["m"] = template;
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(m));
        }

        [Test]
        public void GendersNeedTemplatesAndSettingsNeedBothTexts()
        {
            Module m = CllcModule();
            m.cllc.creatureGender["Troll"] = "n";
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(m), "A gender without a template.");
            m = CllcModule();
            m.cllc.creatureGender.Remove("default");
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(m), "No default gender.");
            m = CllcModule();
            m.cllc.genderedTranslations["n"] = new Dictionary<string, string> { { "Aggressive", "Агрессивное" } };
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(m), "Forms for a gender without a template.");
            m = CllcModule();
            m.cllc.settings["Lock Configuration"].display = "";
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(m), "A setting without its display name.");
            Assert.DoesNotThrow(() => CatalogLoader.Validate(CllcModule()));
        }
    }
}
