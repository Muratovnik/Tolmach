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
            public readonly Dictionary<string, string> Values = new Dictionary<string, string> { { "existing", "Перевод другого мода" }, { "fallback", "English fallback" } };
            public object Map { get { throw new InvalidOperationException("Private-map path must not be used."); } }
            public IReadOnlyDictionary<string, string> GetTranslations(in string language)
            { Reads++; Assert.That(language, Is.EqualTo("Russian")); return Values; }
            public void AddTranslation(in string language, Dictionary<string, string> additions)
            { Writes++; foreach (var p in additions) Values[p.Key] = p.Value; }
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
    }
}
namespace FixtureShadow { public sealed class OnlyShortName { } }
