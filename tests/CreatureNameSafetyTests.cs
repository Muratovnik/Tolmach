using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class CreatureNameSafetyTests : TestContextBase
    {
        private Module module;

        [SetUp]
        public void PrepareOwner()
        {
            CreatureNameDisplay.Reset();
            module = FixtureModule(CreatureNameDisplay.ModuleId, "BalrondHumanoidRandomizer");
            module.rawPatterns.Add(new PatternSpec { source = "{0} Guardian", target = "{0}-страж" });
            Activate(module);
            RawDisplay.Initialize();
        }

        [TearDown]
        public void ClearOwner() { CreatureNameDisplay.Reset(); }

        [Test]
        public void FilterIsRejectedBeforeHarmonyButOtherSafeTargetStillWorks()
        {
            var target = typeof(FilteredNameCharacter).GetMethod("GetHoverName");
            Assert.That(DisplayPatches.UnsupportedHandler(target), Is.EqualTo("exception filter"));

            CreatureNameDisplay.Install(Patcher, typeof(FilteredNameCharacter));

            Assert.That(Harmony.GetPatchInfo(target), Is.Null, "Do not queue an unsafe patch for a deferred patcher either.");
            Assert.That(module.Warnings, Has.Some.Contains("exception filter"));
            Assert.That(module.Patches[typeof(FilteredNameCharacter).FullName + ".GetHoverName [creature name]"].notPatched,
                Does.Contain("exception filter"));
            var character = new FilteredNameCharacter { m_name = "Alice Guardian" };
            Assert.That(character.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(character.GetHoverText(), Is.EqualTo("Alice-страж"));
            Assert.That(RawDisplay.Translate("Alice Guardian"), Is.EqualTo("Alice Guardian"));
        }

        [Test]
        public void ComponentReturnTypeMustAcceptTheMarkerType()
        {
            CreatureNameDisplay.Install(Patcher, typeof(IncompatibleNameCharacter));
            Assert.That(module.Warnings, Has.Some.StartsWith("Creature-name display API unavailable"));
            Assert.That(Harmony.GetPatchInfo(typeof(IncompatibleNameCharacter).GetMethod("GetHoverName")), Is.Null);
            Assert.That(CreatureNameDisplay.Display("Alice Guardian", new IncompatibleNameCharacter()), Is.EqualTo("Alice Guardian"));
        }
    }

    public sealed class FilteredNameCharacter
    {
        public string m_name;
        public bool Throw;
        public bool IsPlayer() { return false; }
        public object GetComponent(Type type) { return new BalrondHumanoidRandomizer.HumanoidExtend(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverName()
        {
            try
            {
                if (Throw) throw new InvalidOperationException("fixture");
                return m_name;
            }
            catch (InvalidOperationException e) when (e.Message == "fixture") { return m_name; }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverText() { return m_name; }
    }

    public sealed class IncompatibleNameCharacter
    {
        public string m_name;
        public bool IsPlayer() { return false; }
        public string GetComponent(Type type) { return "not a component"; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverName() { return m_name; }
    }
}
