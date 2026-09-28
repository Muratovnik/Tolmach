using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    // Real profile Harmony, compiled production patcher, managed endpoints only.
    // This fixture does not assert that a Unity scene or Valheim save was exercised.
    [TestFixture, NonParallelizable]
    public sealed class HarmonyTests : TestContextBase
    {
        private Module ViewModule()
        {
            Module m = FixtureModule("fixture", "FixturePlugin");
            m.texts["Price"] = "Цена"; m.texts["Camp"] = "Лагерь";
            m.returns.Add(new MethodSpec { type = "FixturePlugin.View", method = "Hover" });
            m.literals.Add(new LiteralSpec { type = "FixturePlugin.View", method = "Literal", values = new Dictionary<string, string> { { "Literal", "Литерал" } } });
            return Activate(m);
        }
        private void InstallBridge(Func<string> preference)
        {
            LocalizationBridge.Install(Patcher, typeof(Localization), preference);
        }
        [Test]
        public void DictionaryOnlyModuleDoesNotInspectOrPatchMethods()
        {
            Module m = ViewModule(); m.texts.Clear(); m.returns.Clear(); m.literals.Clear();
            m.words["native_key"] = "Слово"; m.englishWords["native_key"] = "Word";
            DisplayPatches.Install(Patcher, m);
            Assert.That(m.NeedsIlAdapters, Is.False);
            Assert.That(m.ScannedMethods, Is.Zero);
            Assert.That(m.PatchedMethods, Is.Zero);
            Assert.That(Patcher.GetPatchedMethods(), Is.Empty);
        }
        [Test]
        public void LiteralOnlyModuleDoesNotPatchUnrelatedDisplayCalls()
        {
            Module m = ViewModule(); m.texts.Clear(); m.returns.Clear(); m.Table = new TextTable(m);
            DisplayPatches.Install(Patcher, m);
            Assert.That(FixturePlugin.View.Literal(), Is.EqualTo("Литерал"));
            Assert.That(m.Patches.Values.Sum(x => x.displayCalls), Is.Zero);
            Assert.That(m.PatchedMethods, Is.EqualTo(1));
        }
        [Test]
        public void GenuineHarmonyAppliesArgumentsReturnsBranchesAndLiterals()
        {
            Module m = ViewModule(); DisplayPatches.Install(Patcher, m);
            FixturePlugin.View.Draw(); Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Цена"));
            FixturePlugin.View.DrawBranch(true); Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Цена"));
            FixturePlugin.View.DrawBranch(false); Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Unknown"));
            FixturePlugin.View.DrawExceptionRegion(); Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Цена"));
            Assert.That(FixturePlugin.View.Hover(), Is.EqualTo("Цена"));
            Assert.That(FixturePlugin.View.Literal(), Is.EqualTo("Литерал"));
            Minimap map = Minimap.CreateForTests(); Minimap.PinData pin = FixturePlugin.View.Store(map);
            Assert.That(pin.m_name, Is.EqualTo("Camp"));
            Assert.That(Minimap.Discovered, Is.EqualTo("Camp"));
            TextEngine.IsRussian = false;
            FixturePlugin.View.Draw(); Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Price"));
            Assert.That(FixturePlugin.View.Literal(), Is.EqualTo("Literal"));
        }
        [Test]
        public void DeclaredStatusEffectNameIsTranslatedWhenWritten()
        {
            // SpeedyPaths writes its surface name into StatusEffect.m_name for the HUD.
            Module m = FixtureModule("SpeedyPaths", "FixturePlugin");
            m.texts["Price"] = "Цена";
            Activate(m);
            DisplayPatches.Install(Patcher, m);
            StatusEffect effect = new StatusEffect();
            FixturePlugin.View.Name(effect);
            Assert.That(effect.m_name, Is.EqualTo("Цена"));
            TextEngine.IsRussian = false;
            FixturePlugin.View.Name(effect);
            Assert.That(effect.m_name, Is.EqualTo("Price"));
            Assert.That(m.Patches.Values.Sum(p => p.displayFields), Is.EqualTo(1));
        }
        [Test]
        public void ConstructorLiteralIsTranslatedWhenTheObjectIsBuilt()
        {
            Module m = FixtureModule("ctor", "FixturePlugin");
            m.literals.Add(new LiteralSpec { type = "FixturePlugin.Built", method = ".ctor", values = new Dictionary<string, string> { { "Built literal", "Литерал конструктора" } } });
            Activate(m);
            DisplayPatches.Install(Patcher, m);
            Assert.That(new FixturePlugin.Built().m_name, Is.EqualTo("Литерал конструктора"));
            TextEngine.IsRussian = false;
            Assert.That(new FixturePlugin.Built().m_name, Is.EqualTo("Built literal"));
            Assert.That(m.Warnings, Has.None.StartsWith("Expected literal adapter not found"));
            Assert.That(m.Warnings, Has.None.StartsWith("Literal not replaced"));
        }
        [Test]
        public void MethodWithExceptionFilterIsLeftUnpatched()
        {
            // The reason for the skip: this Harmony cannot rebuild such a method even unchanged.
            MethodInfo title = typeof(FixturePlugin.Filtered).GetMethod("Title");
            Assert.Throws<HarmonyLib.HarmonyException>(() => Patcher.Patch(title,
                transpiler: new HarmonyLib.HarmonyMethod(typeof(HarmonyTests).GetMethod("Unchanged", BindingFlags.NonPublic | BindingFlags.Static))));
            Patcher.UnpatchSelf();
            Module m = FixtureModule("filter", "FixturePlugin");
            m.literals.Add(new LiteralSpec { type = "FixturePlugin.Filtered", method = "Title", values = new Dictionary<string, string> { { "Filtered literal", "Литерал с фильтром" } } });
            Activate(m);
            Assert.DoesNotThrow(() => DisplayPatches.Install(Patcher, m));
            Assert.That(m.Warnings, Has.Some.EqualTo("UI patch skipped FixturePlugin.Filtered.Title: exception filter"));
            Assert.That(m.PatchedMethods, Is.EqualTo(0));
            Assert.That(FixturePlugin.Filtered.Title(), Is.EqualTo("Filtered literal"));
        }
        [Test]
        public void IteratorWithFaultBlockIsLeftUnpatched()
        {
            Type iterator = typeof(FixturePlugin.Faulted).GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name.StartsWith("<Lines>", StringComparison.Ordinal));
            MethodInfo moveNext = iterator.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(moveNext.GetMethodBody().ExceptionHandlingClauses.Select(c => c.Flags), Has.Member(ExceptionHandlingClauseOptions.Fault));
            // The reason for the skip: this Harmony ends the fault handler with leave, and the runtime rejects the IL.
            Assert.Throws<HarmonyLib.HarmonyException>(() => Patcher.Patch(moveNext,
                transpiler: new HarmonyLib.HarmonyMethod(typeof(HarmonyTests).GetMethod("Unchanged", BindingFlags.NonPublic | BindingFlags.Static))));
            Patcher.UnpatchSelf();
            Module m = FixtureModule("fault", "FixturePlugin");
            m.literals.Add(new LiteralSpec { type = iterator.FullName, method = "MoveNext", values = new Dictionary<string, string> { { "Faulted literal", "Литерал итератора" } } });
            Activate(m);
            Assert.DoesNotThrow(() => DisplayPatches.Install(Patcher, m));
            Assert.That(m.Warnings, Has.Some.EqualTo("UI patch skipped " + iterator.FullName + ".MoveNext: fault block"));
            Assert.That(FixturePlugin.Faulted.Lines().ToList(), Is.EqualTo(new[] { "Faulted literal" }));
        }
        private static IEnumerable<HarmonyLib.CodeInstruction> Unchanged(IEnumerable<HarmonyLib.CodeInstruction> instructions) { return instructions; }
        [Test]
        public void LiteralInATypeWithoutNamespaceIsTranslated()
        {
            Module m = FixtureModule("global", Module.GlobalNamespace);
            m.literals.Add(new LiteralSpec { type = "GlobalFixture", method = "Label", values = new Dictionary<string, string> { { "Global literal", "Литерал без namespace" } } });
            Activate(m);
            DisplayPatches.Install(Patcher, m);
            Assert.That(GlobalFixture.Label(), Is.EqualTo("Литерал без namespace"));
            Assert.That(m.OwnType(typeof(FixturePlugin.Built)), Is.False, "The marker admits only types without a namespace.");
        }
        [Test]
        public void MethodWithAbsentOptionalDependencyIsSkippedWithoutAbortingTheModule()
        {
            Module m = FixtureModule("optional", "FixtureOptional");
            m.texts["Price"] = "Цена";
            m.returns.Add(new MethodSpec { type = "FixtureOptional.Integration", method = "Missing" });
            Activate(m);
            Assert.DoesNotThrow(() => DisplayPatches.Install(Patcher, m));
            FixtureOptional.Integration.Draw();
            Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Цена"), "Methods beside the unloadable one stay patched.");
            Assert.That(m.Warnings, Has.Some.StartsWith("UI adapter skipped FixtureOptional.Integration.OptionalClip"));
            Assert.That(m.Warnings, Has.Some.StartsWith("Expected return adapter not found"), "Final adapter checks still run.");
        }
        [Test]
        public void NativeRefreshClearsTextCachedBeforeAKeyWasKnown()
        {
            Module m = ViewModule();
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            InstallBridge(() => "Russian");
            Assert.That(main.Localize("$late_key"), Is.EqualTo("[late_key]"));
            // A native loader registers the key immediately before our tables learn it.
            main.AddWord("late_key", "Нативный перевод");
            m.words["late_key"] = "Наш перевод"; m.englishWords["late_key"] = "Late";
            LocalizationBridge.Rebuild(); LocalizationBridge.InjectMain();
            Assert.That(main.Localize("$late_key"), Is.EqualTo("Нативный перевод"));
        }
        [Test]
        public void NativeRegistrationUsesAddWordAndInvalidatesStaleCache()
        {
            Module m = ViewModule(); m.words["test_key"] = "Проверка словаря"; m.englishWords["test_key"] = "Dictionary fixture";
            Localization main = Localization.instance;
            InstallBridge(() => "English");
            main.SetLanguage("Russian");
            Assert.That(main.AddedKeys.Count(k => k == "test_key"), Is.EqualTo(1), "Registration goes through AddWord, not the private dictionary.");
            Assert.That(main.Localize("$test_key"), Is.EqualTo("Проверка словаря"));
            int clears = main.CacheClears;
            main.AddWord("test_key", "Существующий перевод");
            Assert.That(main.Localize("$test_key"), Is.EqualTo("Существующий перевод"));
            main.AddWord("test_key", "Проверка словаря");
            Assert.That(main.Localize("$test_key"), Is.EqualTo("Существующий перевод"), "Our native Russian value must not overwrite existing Russian.");
            Assert.That(main.CacheClears, Is.GreaterThan(clears));
            main.AddWord("test_key", "Dictionary fixture");
            Assert.That(main.Translate("test_key"), Is.EqualTo("Существующий перевод"));
            LocalizationBridge.InjectMain();
            Assert.That(main.Translate("test_key"), Is.EqualTo("Существующий перевод"));
            main.SetLanguage("English");
            Assert.That(TextEngine.IsRussian, Is.False);
            Assert.That(main.Translate("test_key"), Is.EqualTo("[test_key]"));
        }
        [Test]
        public void SeparateEnglishLocalizationStaysEnglish()
        {
            // SkillManager builds an English instance for config names. The game constructor
            // first loads the player's language into it, then the mod switches it to English.
            Module m = ViewModule(); m.words["skill_key"] = "Навык"; m.englishWords["skill_key"] = "Skill";
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            InstallBridge(() => "Russian");
            LocalizationBridge.InjectMain();
            Localization english = Localization.CreateDetachedForTests();
            english.SetupLanguage("English");
            Assert.That(english.Translate("skill_key"), Is.EqualTo("[skill_key]"), "No Russian leaks into the separate instance.");
            english.AddWord("skill_key", "Skill");
            Assert.That(english.Translate("skill_key"), Is.EqualTo("Skill"));
            Assert.That(TextEngine.IsRussian, Is.True, "A separate English instance is not a global language switch.");
            Assert.That(main.Translate("skill_key"), Is.EqualTo("Навык"));
        }
        [Test]
        public void LanguageChosenInsideTheGameConstructorIsAppliedOnRefresh()
        {
            // First launch: no stored preference yet, the game picks Russian from the system
            // locale while constructing Localization (m_instance is still unset at that time).
            Module m = ViewModule(); m.words["first_key"] = "Первый запуск"; m.englishWords["first_key"] = "First launch";
            InstallBridge(() => "English");
            Assert.That(TextEngine.IsRussian, Is.False);
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            NativeAdapters.Refresh();
            Assert.That(TextEngine.IsRussian, Is.True);
            Assert.That(main.Translate("first_key"), Is.EqualTo("Первый запуск"));
        }
        [Test]
        public void NativeLocalizeKeyKeepsExistingThirdPartyRussian()
        {
            Module m = ViewModule();
            m.texts["Sea gull"] = "Морская чайка"; m.texts["Puffin"] = "Тупик";
            m.Table = new TextTable(m);
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            InstallBridge(() => "Russian");
            new FixturePlugin.LocalizeKey("$bird_gull").English("Sea gull");
            FixturePlugin.LocalizeKey puffin = new FixturePlugin.LocalizeKey("$bird_puffin").English("Puffin");
            main.AddWord("bird_gull", "Чайка из другого перевода");
            NativeAdapters.Refresh();
            Assert.That(main.Translate("bird_gull"), Is.EqualTo("Чайка из другого перевода"));
            Assert.That(main.Translate("bird_puffin"), Is.EqualTo("Тупик"));
            Assert.That(puffin.Localizations["Russian"], Is.EqualTo("Тупик"), "The mod's own Russian registry receives the translation.");
            Assert.That(m.NativeWords, Is.EqualTo(2));
            NativeAdapters.Refresh();
            Assert.That(m.NativeWords, Is.EqualTo(2), "Repeated refresh does not re-count keys.");
        }
        [Test]
        public void OreMinesResetNoticeIsTranslatedInChatLogAndBubble()
        {
            Module mines = LoadModule("OreMines");
            ExtraHooks.Install(Patcher);
            Assert.That(mines.Warnings, Is.Empty);
            Chat chat = new Chat();
            chat.OnNewChatMessage(null, 1, null, 2, null, "Mines have been reset!");
            chat.OnNewChatMessage(null, 1, null, 2, null, "Mines have been reset! (typed by a player)");
            Assert.That(chat.Lines, Is.EqualTo(new[] { "Viking: Шахты восстановлены!", "Viking: Mines have been reset! (typed by a player)" }));
            Assert.That(chat.WorldTexts[0], Is.EqualTo("Шахты восстановлены!"));
            TextEngine.IsRussian = false;
            chat.OnNewChatMessage(null, 1, null, 2, null, "Mines have been reset!");
            Assert.That(chat.Lines.Last(), Is.EqualTo("Viking: Mines have been reset!"));
        }
    }
}
