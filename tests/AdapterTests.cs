using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class AdapterTests : TestContextBase
    {
        [Test]
        public void StandardMapLabelsChangeOnlyTheWidget()
        {
            LoadModule("ExpertExplorer");
            Minimap map = Minimap.CreateForTests(); Minimap.PinData pin = map.AddPin("Camp");
            map.ShowNamePin(pin);
            PersistentUi.ApplyPin(pin);
            Assert.That(pin.m_name, Is.EqualTo("Camp"));
            Assert.That(pin.m_NamePinData.PinNameText.text, Is.EqualTo("Лагерь"));
            TextEngine.IsRussian = false; PersistentUi.ApplyPin(pin);
            Assert.That(pin.m_NamePinData.PinNameText.text, Is.EqualTo("Camp"));
            Assert.That(pin.m_name, Is.EqualTo("Camp"));
            TextEngine.IsRussian = true;
            Minimap.PinData custom = map.AddPin("My Camp"); map.ShowNamePin(custom); PersistentUi.ApplyPin(custom);
            Assert.That(custom.m_NamePinData.PinNameText.text, Is.EqualTo("My Camp"));
            PersistentUi.StandardMapLabels = false; PersistentUi.ApplyPin(pin);
            Assert.That(pin.m_NamePinData.PinNameText.text, Is.EqualTo("Camp"));
        }
        [Test]
        public void PatchedMapLabelFollowsGameLanguageSwitch()
        {
            LoadModule("ExpertExplorer");
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
            PersistentUi.Install(Patcher);
            Minimap map = Minimap.CreateForTests();
            Minimap.PinData pin = map.AddPin("Camp");
            map.ShowNamePin(pin);
            Assert.That(pin.m_NamePinData.PinNameText.text, Is.EqualTo("Лагерь"), "The game-created widget is relabelled.");
            Assert.That(pin.m_name, Is.EqualTo("Camp"), "Stored pin data stays unchanged.");
            main.SetLanguage("English");
            Assert.That(pin.m_NamePinData.PinNameText.text, Is.EqualTo("Camp"));
            main.SetLanguage("Russian");
            Assert.That(pin.m_NamePinData.PinNameText.text, Is.EqualTo("Лагерь"));
        }
        [Test]
        public void ExistingThirdPartyWidgetTranslationWins()
        {
            TestLabel label = new TestLabel { text = "Линии моих порталов" };
            PersistentUi.UpdateLabel(label, "Portal lines", "Линии порталов");
            Assert.That(label.text, Is.EqualTo("Линии моих порталов"));
        }
        [TestCase("AddPin")]
        [TestCase("DiscoverLocation")]
        public void PersistentOperationsAreNotClassifiedAsTextSinks(string name)
        {
            MethodInfo classifier = typeof(DisplayPatches).GetMethod("StringSink", BindingFlags.NonPublic | BindingFlags.Static);
            bool[] selected = (bool[])classifier.Invoke(null, new object[] { typeof(Minimap).GetMethod(name) });
            Assert.That(selected.Any(value => value), Is.False);
        }
#pragma warning disable CS0414 // Read through reflection by the test below.
        private class BaseMember { private string Hidden { get { return "base property"; } } private string hiddenField = "base field"; }
#pragma warning restore CS0414
        private sealed class DerivedMember : BaseMember { }
        [Test]
        public void ReflectionUsesHarmonyForInheritedPrivateMembers()
        {
            object target = new DerivedMember();
            Assert.That(RuntimeAccess.Read(target, "Hidden"), Is.EqualTo("base property"));
            Assert.That(RuntimeAccess.Read(target, "hiddenField"), Is.EqualTo("base field"));
            Assert.That(RuntimeAccess.Read(target, "absent"), Is.Null);
            Assert.That(RuntimeAccess.ExactType("OnlyShortName"), Is.Null, "A short-name fallback is not an exact identity.");
            Assert.That(RuntimeAccess.ExactType("FixtureShadow.OnlyShortName"), Is.EqualTo(typeof(FixtureShadow.OnlyShortName)));
        }
        [TestCase(null, true)]
        [TestCase("English", true)]
        [TestCase("$key", true)]
        [TestCase("[key]", true)]
        [TestCase("Мой перевод", false)]
        public void FillOnlyPolicy(string current, bool fill)
        {
            Assert.That(LocalizationBridge.ShouldFill(current, "English", "key"), Is.EqualTo(fill));
        }
        public sealed class InScope
        {
            public int Reads;
            public int Writes;
            public Localization LiveGame;
            public readonly Dictionary<string, string> Values = new Dictionary<string, string> { { "existing", "Перевод другого мода" }, { "fallback", "English fallback" } };
            public object Map { get { throw new InvalidOperationException("Private-map path must not be used."); } }
            public IReadOnlyDictionary<string, string> GetTranslations(in string language)
            { Reads++; Assert.That(language, Is.EqualTo("Russian")); return Values; }
            public void AddTranslation(in string language, Dictionary<string, string> additions)
            {
                Writes++;
                foreach (var p in additions)
                {
                    Values[p.Key] = p.Value;
                    // CustomLocalization.AddTranslationToMap in Jotunn 2.30.2 also
                    // publishes absent keys to the active table, regardless of language.
                    if (LiveGame != null && LiveGame.Translate(p.Key) == "[" + p.Key + "]")
                        LiveGame.AddWord(p.Key, p.Value);
                }
            }
        }
        public sealed class UnsupportedScope
        {
            public bool WasWritten;
            public void AddTranslation(string language, Dictionary<string, string> additions) { WasWritten = true; }
        }
        [Test]
        public void JotunnUsesPublicInParameterApisWithoutOverwritingExistingRussian()
        {
            Module m = new Module { id = "scope" };
            m.words["existing"] = "Наш перевод"; m.englishWords["existing"] = "English";
            m.words["fallback"] = "Запасной перевод"; m.englishWords["fallback"] = "English fallback";
            m.words["new_key"] = "Новый перевод"; m.englishWords["new_key"] = "New";
            InScope scope = new InScope();
            NativeAdapters.FillJotunnScope(scope, m);
            Assert.That(scope.Values["existing"], Is.EqualTo("Перевод другого мода"));
            Assert.That(scope.Values["fallback"], Is.EqualTo("Запасной перевод"));
            Assert.That(scope.Values["new_key"], Is.EqualTo("Новый перевод"));
            Assert.That(scope.Reads, Is.EqualTo(1)); Assert.That(scope.Writes, Is.EqualTo(1));
            NativeAdapters.FillJotunnScope(scope, m);
            Assert.That(scope.Writes, Is.EqualTo(1), "Registration is idempotent.");
            UnsupportedScope unsupported = new UnsupportedScope();
            NativeAdapters.FillJotunnScope(unsupported, m);
            Assert.That(unsupported.WasWritten, Is.False, "No blind write if existing translations cannot be read.");
            Assert.That(m.Warnings, Has.Count.EqualTo(1));
        }
        [TestCase("English")]
        [TestCase("German")]
        public void JotunnRegistrationDoesNotPublishRussianWordsInAnotherLanguage(string language)
        {
            Localization.SelectedLanguage = language;
            Localization main = Localization.instance;
            LocalizationBridge.Install(Patcher, typeof(Localization), () => language);
            Module m = new Module { id = "scope" };
            m.words["missing_jotunn_key"] = "Новый перевод";
            m.englishWords["missing_jotunn_key"] = "New translation";
            InScope scope = new InScope { LiveGame = main };
            scope.Values.Clear();

            NativeAdapters.FillJotunnScope(scope, m);
            Assert.That(main.Translate("missing_jotunn_key"), Is.EqualTo("[missing_jotunn_key]"),
                "Registering a Russian scope must not leak Russian into the active game language.");
            Assert.That(scope.Writes, Is.Zero);

            main.SetLanguage("Russian");
            NativeAdapters.FillJotunnScope(scope, m);
            Assert.That(scope.Values["missing_jotunn_key"], Is.EqualTo("Новый перевод"));
            Assert.That(main.Translate("missing_jotunn_key"), Is.EqualTo("Новый перевод"));
            Assert.That(scope.Writes, Is.EqualTo(1), "The same native registration still works in Russian.");
        }
        [Test]
        public void JotunnScopeTakesAReplaceNativeWordOverTheModsOwnRussian()
        {
            Module m = new Module { id = "scope" };
            m.words["existing"] = "Наш перевод"; m.englishWords["existing"] = "English";
            m.replaceNative.Add("existing");
            InScope scope = new InScope();
            NativeAdapters.FillJotunnScope(scope, m);
            Assert.That(scope.Values["existing"], Is.EqualTo("Наш перевод"));
            NativeAdapters.FillJotunnScope(scope, m);
            Assert.That(scope.Writes, Is.EqualTo(1), "Registration is idempotent.");
        }
        [Test]
        public void SocialSystemGetsEveryWordInItsOwnJotunnTable()
        {
            // SocialSystem 1.0.1 ships English and German only; its windows read words from this table.
            Module m = LoadModule("SocialSystem");
            InScope scope = new InScope();
            scope.Values.Clear();
            NativeAdapters.FillJotunnScope(scope, m);
            Assert.That(scope.Values, Is.EquivalentTo(m.words));
            Assert.That(scope.Values["socialsystem_ui_friends_header"], Is.EqualTo("Друзья (в сети: {0}/{1})"));
            Assert.That(scope.Writes, Is.EqualTo(1));
        }
    }
}
namespace FixtureShadow { public sealed class OnlyShortName { } }
