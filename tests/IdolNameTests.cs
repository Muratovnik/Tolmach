using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class IdolNameTests : TestContextBase
    {
        public sealed class Idol { public string source; public string nativeRussian; public string english; public string expected; }
        public sealed class NativeNames
        {
            public Dictionary<string, string> russian;
            public Dictionary<string, string> english;
            public List<Idol> idols;
        }
        public static IEnumerable<TestCaseData> Idols
        {
            get { return Fixture<NativeNames>("native-idols.json").idols.Select(i => new TestCaseData(i).SetName("Native idol: " + i.english)); }
        }
        private Localization Prepare(Module module, bool allowDisplay = true)
        {
            NativeNames native = Fixture<NativeNames>("native-idols.json");
            Localization.Resources["Russian"] = native.russian;
            Localization.Resources["English"] = native.english;
            Localization.SelectedLanguage = "Russian";
            module.UiAllowed = allowDisplay;
            RawDisplay.Initialize();
            Localization main = Localization.instance;
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
            LocalizationBridge.InjectMain();
            return main;
        }
        [TestCaseSource("Idols")]
        public void WholeNativeNameIsCorrectAndLanguageSwitchKeepsTheOriginal(Idol idol)
        {
            Module module = LoadModule("Valheim");
            NativeNames native = Fixture<NativeNames>("native-idols.json");
            Localization.Resources["Russian"] = native.russian;
            Localization.SelectedLanguage = "Russian";
            Assert.That(Localization.instance.Localize(idol.source), Is.EqualTo(idol.nativeRussian), "Native resources reproduce the original broken name before patching.");
            Localization main = Prepare(module);
            GameItemsTests.SharedFixture stored = new GameItemsTests.SharedFixture { m_name = idol.source };
            Assert.That(main.Localize(stored.m_name), Is.EqualTo(idol.expected));
            Assert.That(main.Localize(stored.m_name), Is.EqualTo(idol.expected), "Repeat lookup.");
            Assert.That(main.Localize(idol.expected), Is.EqualTo(idol.expected), "No second translation.");
            Assert.That(main.Localize("<b>" + idol.source + "</b>\r\n" + idol.source + "\r\n"),
                Is.EqualTo("<b>" + idol.expected + "</b>\r\n" + idol.expected + "\r\n"));
            Assert.That(main.Localize(" " + idol.source + " \n"), Is.EqualTo(" " + idol.expected + " \n"));
            Assert.That(stored.m_name, Is.EqualTo(idol.source));
            Localization english = Localization.CreateDetachedForTests();
            english.SetupLanguage("English");
            Assert.That(english.Localize(stored.m_name), Is.EqualTo(idol.english));
            main.SetLanguage("English");
            Assert.That(main.Localize(stored.m_name), Is.EqualTo(idol.english));
            main.SetLanguage("Russian");
            Assert.That(main.Localize(stored.m_name), Is.EqualTo(idol.expected));
        }
        [Test]
        public void NativeFragmentsAndUnknownNamesAreNotRewritten()
        {
            Localization main = Prepare(LoadModule("Valheim"));
            NativeNames native = Fixture<NativeNames>("native-idols.json");
            foreach (KeyValuePair<string, string> word in native.russian)
                Assert.That(main.Localize("$" + word.Key), Is.EqualTo(word.Value), word.Key);
            foreach (string unknown in new[] {
                "$item_upgrader_tier8 $item_upgrader_weapon $item_upgrader_name",
                "$item_upgrader_tier0 $item_upgrader_magic $item_upgrader_name",
                "$item_upgrader_tier0 $item_upgrader_weapon",
                "$item_upgrader_tier0 $item_upgrader_weapon $item_upgrader_name II",
                "Upgrader0Weapon", "Дерево Сражение Идол" })
                Assert.That(RawDisplay.Translate(unknown), Is.EqualTo(unknown), "No broad name replacement.");
        }
        [Test]
        public void AliasIsInactiveWithoutDisplayPermission()
        {
            Idol idol = Fixture<NativeNames>("native-idols.json").idols.First();
            Localization main = Prepare(LoadModule("Valheim"), false);
            Assert.That(main.Localize(idol.source), Is.EqualTo(idol.nativeRussian));
        }
        [Test]
        public void ReinitializationDropsCachedNameTranslations()
        {
            Idol idol = Fixture<NativeNames>("native-idols.json").idols.First();
            Module module = LoadModule("Valheim");
            Localization main = Prepare(module);
            Assert.That(main.Localize(idol.source), Is.EqualTo(idol.expected));
            module.UiAllowed = false;
            RawDisplay.Initialize();
            Assert.That(main.Localize(idol.source), Is.EqualTo(idol.nativeRussian));
            module.UiAllowed = true;
            RawDisplay.Initialize();
            Assert.That(main.Localize(idol.source), Is.EqualTo(idol.expected));
        }
        [TestCase("owner")]
        [TestCase("source")]
        [TestCase("target")]
        [TestCase("missing")]
        [TestCase("recursive")]
        [TestCase("markup")]
        [TestCase("raw")]
        [TestCase("replace")]
        public void AliasContractRejectsUnsupportedRewrites(string mutation)
        {
            Module module = CatalogLoader.Read(Catalog("Valheim"));
            const string source = "$item_upgrader_tier0 $item_upgrader_weapon $item_upgrader_name";
            KeyValuePair<string, string> first = new KeyValuePair<string, string>(source, module.nameAliases[source]);
            string key = first.Value.Substring(1);
            if (mutation == "owner") module.id = "Other";
            else if (mutation == "source") { module.nameAliases.Remove(first.Key); module.nameAliases[first.Key.Replace("tier0", "tier8")] = first.Value; }
            else if (mutation == "target") module.nameAliases[first.Key] = "$tolmach_idol_7_armor";
            else if (mutation == "missing") { module.words.Remove(key); module.englishWords.Remove(key); }
            else if (mutation == "recursive") module.words[key] = first.Key;
            else if (mutation == "markup") module.words[key] = "<b>Идол</b>";
            else if (mutation == "raw") module.rawTexts[first.Key] = "Идол";
            else if (mutation == "replace") module.replaceNative.Add(key);
            Assert.Throws<InvalidDataException>(() => CatalogLoader.Validate(module));
        }
    }
}
