using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class NorsemenNameTests : TestContextBase
    {
        private Module module;
        [SetUp]
        public void InstallNames()
        {
            module = FixtureModule("Norsemen", "Norsemen");
            module.terms["npcNames"] = new Dictionary<string, string> { { "Ulf", "Ульф" }, { "Astrid", "Астрид" } };
            Activate(module);
            RawDisplay.Initialize();
            NorsemenNames.Install(Patcher);
        }
        [TearDown]
        public void ResetNames() { NorsemenNames.Reset(); }

        [Test]
        public void WildNameChangesAtEveryDisplayConsumerAndNeverAtPersistenceOrInput()
        {
            var viking = new Norsemen.Viking { StoredName = "Ulf" };
            Patcher.UnpatchSelf();
            Assert.That(viking.GetHoverText(), Is.EqualTo("Ulf (wild)"), "The unpatched display exposes the missing-name route.");
            NorsemenNames.Install(Patcher);
            Assert.That(viking.GetHoverName(), Is.EqualTo("<color=red>Ульф</color>"));
            Assert.That(viking.GetHoverText(), Is.EqualTo("Ульф (wild)"));
            Assert.That(Norsemen.VikingGui.Show(viking), Is.EqualTo("Ульф"));
            Assert.That(Norsemen.VikingGui.InventoryGUI_UpdateContainer.Prefix(viking), Is.EqualTo("Ульф"));
            Assert.That(viking.GetName(), Is.EqualTo("Ulf"));
            Assert.That(viking.GetText(), Is.EqualTo("Ulf"));
            Assert.That(viking.SaveTombstoneName(), Is.EqualTo("Ulf"));
            Assert.That(viking.StoredName, Is.EqualTo("Ulf"));
            Assert.That(module.Patches[typeof(Norsemen.Viking).FullName + ".GetHoverName [Norsemen name]"].displayCalls, Is.EqualTo(2));
            Assert.That(module.Patches[typeof(Norsemen.Viking).FullName + ".GetHoverText [Norsemen name]"].displayCalls, Is.EqualTo(1));
        }

        [Test]
        public void TamedAndUnknownNamesRemainVerbatimAndStateIsNotCached()
        {
            var viking = new Norsemen.Viking { StoredName = "Ulf", Tamed = true };
            Assert.That(viking.GetHoverText(), Is.EqualTo("Ulf (wild)"), "A renamed Ulf and a generated Ulf share the same persisted key.");
            viking.Tamed = false;
            Assert.That(viking.GetHoverText(), Is.EqualTo("Ульф (wild)"));
            viking.StoredName = "My Ulf";
            Assert.That(viking.GetHoverText(), Is.EqualTo("My Ulf (wild)"));
            Assert.That(NorsemenNames.Display(new object(), "Ulf"), Is.EqualTo("Ulf"));
        }

        [Test]
        public void LanguageAndModuleRebindingDoNotLeakNames()
        {
            var viking = new Norsemen.Viking { StoredName = "Astrid" };
            TextEngine.IsRussian = false;
            Assert.That(viking.GetHoverText(), Is.EqualTo("Astrid (wild)"));
            TextEngine.IsRussian = true;
            Assert.That(viking.GetHoverText(), Is.EqualTo("Астрид (wild)"));
            module.UiAllowed = false;
            Assert.That(viking.GetHoverText(), Is.EqualTo("Astrid (wild)"));
            module.UiAllowed = true;
            TextEngine.Modules["Norsemen"] = FixtureModule("Norsemen", "Norsemen");
            Assert.That(viking.GetHoverText(), Is.EqualTo("Astrid (wild)"));
        }

        [Test]
        public void StateFailureRetainsOriginalAndWarnsOnce()
        {
            var viking = new Norsemen.Viking { StoredName = "Ulf", FailState = true };
            Assert.That(viking.GetHoverText(), Is.EqualTo("Ulf (wild)"));
            Assert.That(viking.GetHoverText(), Is.EqualTo("Ulf (wild)"));
            Assert.That(module.Warnings.Count, Is.EqualTo(1));
        }
    }
}

namespace Norsemen
{
    public sealed class Viking
    {
        public string StoredName;
        public bool Tamed;
        public bool FailState;
        public bool IsTamed() { if (FailState) throw new InvalidOperationException(); return Tamed; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetText() { return StoredName; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetName() { return GetText(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverName() { if (StoredName == "Plain") return GetName(); return "<color=red>" + GetName() + "</color>"; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverText() { return GetText() + " (wild)"; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string SaveTombstoneName() { return GetName(); }
    }
    public static class VikingGui
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Show(Viking viking) { return viking.GetName(); }
        public static class InventoryGUI_UpdateContainer
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string Prefix(Viking viking) { return viking.GetName(); }
        }
    }
}
