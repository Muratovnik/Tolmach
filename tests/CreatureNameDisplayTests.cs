using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class CreatureNameDisplayTests : TestContextBase
    {
        private Module module;

        [SetUp]
        public void InstallNameAdapter()
        {
            CreatureNameDisplay.Reset();
            module = FixtureModule(CreatureNameDisplay.ModuleId, "BalrondHumanoidRandomizer");
            module.rawTexts["Bow"] = "Лук";
            module.rawPatterns.Add(new PatternSpec { source = "{0} Guardian", target = "{0}-страж" });
            Activate(module);
            RawDisplay.Initialize();
            Localization.Resources["Russian"] = new Dictionary<string, string> { { "enemy_greydwarf", "Грейдворф" } };
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
            CreatureNameDisplay.Install(Patcher, typeof(NameCharacter));
        }

        [TearDown]
        public void ClearNameAdapter() { CreatureNameDisplay.Reset(); }

        [Test]
        public void IdenticalTextNeedsTheOwningObjectAndNeverChangesItsStoredName()
        {
            NameCharacter owned = new NameCharacter { m_name = "Alice Guardian", Marker = new BalrondHumanoidRandomizer.HumanoidExtend() };
            NameCharacter unrelated = new NameCharacter { m_name = "Alice Guardian" };
            Assert.That(owned.GetHoverName(), Is.EqualTo("Alice-страж"));
            Assert.That(unrelated.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(Localization.instance.Localize("Alice Guardian"), Is.EqualTo("Alice Guardian"));
            Assert.That(RawDisplay.Translate("<b>Alice Guardian</b>"), Is.EqualTo("<b>Alice Guardian</b>"));
            Assert.That(owned.m_name, Is.EqualTo("Alice Guardian"));
            Assert.That(unrelated.m_name, Is.EqualTo("Alice Guardian"));
        }

        [Test]
        public void PlayerAndCustomRenameStayOpaqueEvenOnAMarkedObject()
        {
            NameCharacter player = new NameCharacter { m_name = "Alice Guardian", Player = true, Marker = new BalrondHumanoidRandomizer.HumanoidExtend() };
            NameCharacter renamed = new NameCharacter { m_name = "$enemy_greydwarf Guardian", CustomName = "Alice Guardian", Marker = new BalrondHumanoidRandomizer.HumanoidExtend() };
            Assert.That(player.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(renamed.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(renamed.CustomName, Is.EqualTo("Alice Guardian"));
        }

        [Test]
        public void TranslationRunsBeforeGameTokensResolveAndRespectsLanguage()
        {
            NameCharacter creature = new NameCharacter { m_name = "$enemy_greydwarf Guardian", Marker = new BalrondHumanoidRandomizer.HumanoidExtend() };
            Assert.That(creature.GetHoverName(), Is.EqualTo("Грейдворф-страж"));
            Assert.That(creature.GetHoverText(), Is.EqualTo("Name: $enemy_greydwarf-страж"));
            TextEngine.IsRussian = false;
            Assert.That(CreatureNameDisplay.Display("Alice Guardian", creature), Is.EqualTo("Alice Guardian"));
            Assert.That(creature.m_name, Is.EqualTo("$enemy_greydwarf Guardian"));
        }

        [Test]
        public void UnknownOwnershipFailsClosedWithoutAGlobalFallback()
        {
            NameCharacter creature = new NameCharacter { m_name = "Alice Guardian", FailComponentLookup = true };
            Assert.That(creature.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(creature.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(module.Warnings, Has.Exactly(1).StartsWith("Creature-name ownership check failed"));
            Assert.That(RawDisplay.Translate("Alice Guardian"), Is.EqualTo("Alice Guardian"));
        }

        [Test]
        public void RemovedOrDisabledOwnerCannotUseAnInstalledAdapter()
        {
            NameCharacter creature = new NameCharacter { m_name = "Alice Guardian", Marker = new BalrondHumanoidRandomizer.HumanoidExtend() };
            module.UiAllowed = false;
            Assert.That(creature.GetHoverName(), Is.EqualTo("Alice Guardian"));
            module.UiAllowed = true;
            TextEngine.Modules.Remove(module.id);
            Assert.That(creature.GetHoverName(), Is.EqualTo("Alice Guardian"));
        }

        [Test]
        public void MissingCharacterApiWarnsAndDoesNotEnableGlobalTemplates()
        {
            CreatureNameDisplay.Install(Patcher, typeof(object));
            Assert.That(module.Warnings, Has.Some.StartsWith("Creature-name display API unavailable"));
            Assert.That(RawDisplay.Translate("Alice Guardian"), Is.EqualTo("Alice Guardian"));
        }
    }

    // Ordinary CLR fixture: no Unity object is fabricated. It exercises the actual Harmony
    // field-read adapter and the same fake Localization used by the existing integration suite.
    public sealed class NameCharacter
    {
        public string m_name;
        public string CustomName;
        public bool Player;
        public object Marker;
        public bool FailComponentLookup;
        public bool IsPlayer() { return Player; }
        public object GetComponent(Type type)
        {
            if (FailComponentLookup) throw new InvalidOperationException("Fixture lookup failure");
            return Marker != null && type.IsInstanceOfType(Marker) ? Marker : null;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverName()
        {
            if (CustomName != null) return Localization.instance.Localize(CustomName);
            return Localization.instance.Localize(m_name);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverText() { return "Name: " + m_name; }
    }
}

namespace BalrondHumanoidRandomizer
{
    public sealed class HumanoidExtend { }
}
