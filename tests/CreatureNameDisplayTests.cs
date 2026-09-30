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
            module = FixtureModule(CreatureNameDisplay.ModuleId, "BalrondHumanoidRandomizer");
            module.rawTexts["Bow"] = "Лук";
            module.rawPatterns.Add(new PatternSpec { source = "{0} Guardian", target = "{0}-страж" });
            Activate(module);
            RawDisplay.Initialize();
            Localization.Resources["Russian"] = new Dictionary<string, string> { { "enemy_greydwarf", "Грейдворф" } };
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
            CreatureNameDisplay.Install(Patcher, typeof(NameCharacter), typeof(NameTameable), typeof(NameScene));
        }
        [Test]
        public void RandomizedSourceNeedsTheActualComponentAndNeverChangesItsStoredName()
        {
            NameCharacter owned = Marked("Alice Guardian");
            NameCharacter unrelated = new NameCharacter { m_name = "Alice Guardian", Marker = new BalrondHumanoidRandomizer.HumanoidExtend() };
            Assert.That(owned.GetHoverName(), Is.EqualTo("Alice-страж"));
            Assert.That(unrelated.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(Localization.instance.Localize("Alice Guardian"), Is.EqualTo("Alice Guardian"));
            Assert.That(RawDisplay.Translate("<b>Alice Guardian</b>"), Is.EqualTo("<b>Alice Guardian</b>"));
            Assert.That(owned.m_name, Is.EqualTo("Alice Guardian"));
        }
        [Test]
        public void CloneWithoutComponentNeedsItsActualNetworkPrefabInTheCurrentOwnerRegistry()
        {
            NameCharacter clone = RegisteredClone("$enemy_greydwarf Guardian");
            Assert.That(clone.Marker, Is.Null, "GeneratePrefabs removes HumanoidRandomizer from clones.");
            Assert.That(clone.GetHoverName(), Is.EqualTo("Грейдворф-страж"));
            Assert.That(clone.m_name, Is.EqualTo("$enemy_greydwarf Guardian"));
            object prefab = NameScene.instance.Prefabs[42];
            NameScene.instance.Prefabs[42] = new object();
            Assert.That(clone.GetHoverName(), Is.EqualTo("Грейдворф Guardian"), "Same ID is insufficient after replacement.");
            NameScene.instance.Prefabs[42] = prefab;
            BalrondHumanoidRandomizer.Launch.itemSetBuilder.prefabs.Clear();
            Assert.That(clone.GetHoverName(), Is.EqualTo("Грейдворф Guardian"), "Do not retain ownership across resets.");
            BalrondHumanoidRandomizer.Launch.itemSetBuilder.prefabs.Add(prefab);
            Assert.That(clone.GetHoverName(), Is.EqualTo("Грейдворф-страж"));
            NameScene.instance = null;
            Assert.That(clone.GetHoverName(), Is.EqualTo("Грейдворф Guardian"));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void TameableDefaultNameUsesTheOwningCharacter(bool clone)
        {
            NameCharacter creature = clone ? RegisteredClone("$enemy_greydwarf Guardian") : Marked("$enemy_greydwarf Guardian");
            creature.Tameable = new NameTameable { m_character = creature };
            Assert.That(creature.GetHoverName(), Is.EqualTo("Грейдворф-страж"));
            Assert.That(creature.GetHoverText(), Is.EqualTo("Грейдворф-страж (wild)"));
            creature.Tameable.Tamed = true;
            Assert.That(creature.GetHoverName(), Is.EqualTo("Грейдворф-страж"));
            creature.Tameable.CustomName = "Alice Guardian";
            Assert.That(creature.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(creature.GetHoverText(), Is.EqualTo("Грейдворф-страж (tame)"));
            Assert.That(creature.Tameable.CustomName, Is.EqualTo("Alice Guardian"));
            Assert.That(creature.m_name, Is.EqualTo("$enemy_greydwarf Guardian"));
        }
        [Test]
        public void PlayersOverridesAndUnrelatedTameablesStayOpaque()
        {
            NameCharacter player = RegisteredClone("Alice Guardian");
            player.Player = true;
            NameCharacter renamed = Marked("$enemy_greydwarf Guardian");
            renamed.OverrideName = "Alice Guardian";
            NameCharacter unrelated = new NameCharacter { m_name = "Alice Guardian" };
            unrelated.Tameable = new NameTameable { m_character = unrelated };
            Assert.That(player.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(renamed.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(renamed.OverrideName, Is.EqualTo("Alice Guardian"));
            Assert.That(unrelated.GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(new NameTameable { PieceName = "Alice Guardian" }.GetName(), Is.EqualTo("Alice Guardian"));
        }
        [Test]
        public void MissingCloneApiRetainsVariantNamesButStillSupportsTheActualComponent()
        {
            Patcher.UnpatchSelf();
            CreatureNameDisplay.Install(Patcher, typeof(NameCharacter), typeof(NameTameable), typeof(object));
            Assert.That(RegisteredClone("Alice Guardian").GetHoverName(), Is.EqualTo("Alice Guardian"));
            Assert.That(Marked("Alice Guardian").GetHoverName(), Is.EqualTo("Alice-страж"));
            Assert.That(module.Warnings, Has.Some.StartsWith("Creature-name prefab ownership API unavailable"));
        }
        [Test]
        public void MissingNetworkStateDoesNotInferOwnershipFromText()
        {
            NameCharacter creature = RegisteredClone("Alice Guardian");
            creature.m_nview.Data = null;
            Assert.That(creature.GetHoverName(), Is.EqualTo("Alice Guardian"));
            creature.m_nview = null;
            Assert.That(creature.GetHoverName(), Is.EqualTo("Alice Guardian"));
        }
        [Test]
        public void TranslationRunsBeforeGameTokensResolveAndRespectsLanguage()
        {
            NameCharacter creature = Marked("$enemy_greydwarf Guardian");
            Assert.That(creature.GetHoverName(), Is.EqualTo("Грейдворф-страж"));
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
            NameCharacter creature = Marked("Alice Guardian");
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
        internal static NameCharacter Marked(string name)
        {
            return new NameCharacter { m_name = name, Marker = new BalrondHumanoidRandomizer.HumanoidRandomizer() };
        }
        internal static NameCharacter RegisteredClone(string name)
        {
            object prefab = new object();
            NameScene.instance = new NameScene();
            NameScene.instance.Prefabs[42] = prefab;
            BalrondHumanoidRandomizer.Launch.itemSetBuilder.prefabs.Add(prefab);
            return new NameCharacter { m_name = name, m_nview = new NameView { Data = new NameData { Prefab = 42 } } };
        }
    }
    // Managed fixtures model Valheim's name/delegation branches and the mod's registry.
    // They exercise Harmony without constructing native Unity objects.
    public class NameCharacter
    {
        public string m_name;
        public string OverrideName;
        public bool Player;
        public object Marker;
        public bool FailComponentLookup;
        public NameView m_nview;
        public NameTameable Tameable;
        public bool IsPlayer() { return Player; }
        public object GetComponent(Type type)
        {
            if (FailComponentLookup) throw new InvalidOperationException("Fixture lookup failure");
            return Marker != null && type.IsInstanceOfType(Marker) ? Marker : null;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public virtual string GetHoverName()
        {
            if (Tameable != null) return Tameable.GetHoverName();
            if (!String.IsNullOrWhiteSpace(OverrideName)) return OverrideName;
            return Localization.instance.Localize(m_name);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverText() { return Tameable == null ? "" : Tameable.GetHoverText(); }
    }
    public sealed class NameTameable
    {
        public NameCharacter m_character;
        public string PieceName;
        public string CustomName;
        public bool Tamed;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetName() { return Localization.instance.Localize(m_character != null ? m_character.m_name : PieceName); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverName() { return Tamed && !String.IsNullOrEmpty(CustomName) ? CustomName : GetName(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverText() { return GetName() + (Tamed ? " (tame)" : " (wild)"); }
    }
    public sealed class NameView { public NameData Data; public NameData GetZDO() { return Data; } }
    public sealed class NameData { public int Prefab; public int GetPrefab() { return Prefab; } }
    public sealed class NameScene
    {
        public static NameScene instance { get; set; }
        public readonly Dictionary<int, object> Prefabs = new Dictionary<int, object>();
        public object GetPrefab(int hash) { object prefab; return Prefabs.TryGetValue(hash, out prefab) ? prefab : null; }
    }
}
namespace BalrondHumanoidRandomizer
{
    public sealed class HumanoidRandomizer { }
    public sealed class HumanoidExtend { }
    public static class Launch { public static ItemSetBuilder itemSetBuilder = new ItemSetBuilder(); }
    public sealed class ItemSetBuilder { public List<object> prefabs = new List<object>(); }
}
