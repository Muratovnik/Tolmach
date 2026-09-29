using System.Collections.Generic;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;
using Wrapper = CreatureLevelControl.LocalizationWrapper;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class CllcMergeTests : TestContextBase
    {
        private const string Key = "Lock Configuration";
        private const string EnglishDescription = "The configuration is locked and may not be changed by clients.";

        [SetUp]
        public void ResetWrapper() { Wrapper.ResetForTests(); }
        [TearDown]
        public void ClearWrapper() { Wrapper.ResetForTests(); }

        private static Module SettingsModule()
        {
            Module m = FixtureModule("CreatureLevelControl", "CreatureLevelControl");
            m.cllc = new CllcLanguage();
            m.cllc.settings[Key] = new CllcSetting { display = "Блокировка настроек", desc = "Клиенты не могут менять настройки." };
            return m;
        }

        [TestCase("Их название", EnglishDescription, "Их название", "Клиенты не могут менять настройки.")]
        [TestCase(Key, "Их описание", "Блокировка настроек", "Их описание")]
        [TestCase("Их название", "Их описание", "Их название", "Их описание")]
        [TestCase(Key, EnglishDescription, "Блокировка настроек", "Клиенты не могут менять настройки.")]
        [TestCase(null, "Их описание", "Блокировка настроек", "Их описание")]
        [TestCase("Их название", null, "Их название", "Клиенты не могут менять настройки.")]
        [TestCase("", "", "Блокировка настроек", "Клиенты не могут менять настройки.")]
        public void EachFieldKeepsItsTranslationAndFillsOnlyItsOwnGap(string name, string description, string expectedName, string expectedDescription)
        {
            Wrapper.UserFile = new Wrapper
            {
                settings = new Dictionary<string, Wrapper.SettingTexts>
                {
                    { Key, new Wrapper.SettingTexts { display = name, desc = description } }
                }
            };
            Wrapper.LoadLanguage("Russian");
            Module m = SettingsModule();

            CllcAdapter.Apply(m, null);

            Wrapper.SettingTexts actual = Wrapper.Current.settings[Key];
            Assert.That(actual.display, Is.EqualTo(expectedName));
            Assert.That(actual.desc, Is.EqualTo(expectedDescription));
            Assert.That(Wrapper.Current, Is.SameAs(Wrapper.UserFile));
            Assert.That(Wrapper.English.settings[Key].display, Is.Null, "The English fixture stores the name implicitly in its key.");
            Assert.That(Wrapper.English.settings[Key].desc, Is.EqualTo(EnglishDescription));
            int filled = m.NativeWords;
            Assert.That(filled, Is.EqualTo(name == expectedName && description == expectedDescription ? 0 : 1));

            CllcAdapter.Apply(m, null);

            Assert.That(Wrapper.Current.settings[Key].display, Is.EqualTo(expectedName));
            Assert.That(Wrapper.Current.settings[Key].desc, Is.EqualTo(expectedDescription));
            Assert.That(m.NativeWords, Is.EqualTo(filled), "A refresh must not fill or count the same setting again.");
        }

        [Test]
        public void ExplicitEnglishDisplayIsComparedWithTheDisplayNotTheKey()
        {
            Wrapper.English.settings[Key] = new Wrapper.SettingTexts { display = "Lock this configuration", desc = EnglishDescription };
            Wrapper.UserFile = new Wrapper
            {
                settings = new Dictionary<string, Wrapper.SettingTexts>
                {
                    { Key, new Wrapper.SettingTexts { display = "Lock this configuration", desc = "Их описание" } }
                }
            };
            Wrapper.LoadLanguage("Russian");

            CllcAdapter.Apply(SettingsModule(), null);

            Assert.That(Wrapper.Current.settings[Key].display, Is.EqualTo("Блокировка настроек"));
            Assert.That(Wrapper.Current.settings[Key].desc, Is.EqualTo("Их описание"));
            Assert.That(Wrapper.English.settings[Key].display, Is.EqualTo("Lock this configuration"));
        }
    }
}
