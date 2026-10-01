using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Tolmach;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class GameItemsTests : TestContextBase
    {
        public sealed class DatabaseFixture
        {
            internal static DatabaseFixture instance;
            internal List<PrefabFixture> m_items = new List<PrefabFixture>();
            internal int PrefabLookups;
            internal bool Registered = true;
            public PrefabFixture GetItemPrefab(string name)
            {
                PrefabLookups++;
                return Registered ? m_items.Find(item => item.Identifier == name) : null;
            }
            [MethodImpl(MethodImplOptions.NoInlining)]
            private void UpdateRegisters() { Registered = true; }
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Register() { UpdateRegisters(); }
        }
        public sealed class PrefabFixture
        {
            internal string Identifier;
            internal DropFixture Drop;
        }
        public sealed class DropFixture { internal DataFixture m_itemData; }
        public sealed class DataFixture { internal object m_shared; }
        public sealed class SharedFixture
        {
            internal string m_name;
            internal string m_setName;
            internal EffectFixture m_setStatusEffect;
        }
        public sealed class EffectFixture { internal string m_name; }
        public sealed class PropertySharedFixture
        {
            public string m_name { get; set; }
            public string m_setName { get; set; }
            public EffectFixture m_setStatusEffect { get; set; }
        }
        private int typeResolutions;
        private int componentReads;

        [TearDown]
        public void ClearDatabase() { DatabaseFixture.instance = null; }

        private void Use(DatabaseFixture db)
        {
            DatabaseFixture.instance = db;
            typeResolutions = 0;
            componentReads = 0;
            GameItems.TypeResolver = name =>
            {
                typeResolutions++;
                return name == "ObjectDB" ? typeof(DatabaseFixture) : name == "ItemDrop" ? typeof(DropFixture) : null;
            };
            GameItems.ComponentResolver = (prefab, type) =>
            {
                componentReads++;
                Assert.That(type, Is.EqualTo(typeof(DropFixture)));
                return ((PrefabFixture)prefab).Drop;
            };
        }
        private static PrefabFixture Item(string identifier, string name, string set, string effect)
        {
            return new PrefabFixture { Identifier = identifier, Drop = new DropFixture { m_itemData = new DataFixture {
                m_shared = new SharedFixture { m_name = name, m_setName = set,
                    m_setStatusEffect = effect == null ? null : new EffectFixture { m_name = effect } }
            } } };
        }

        [TestCase(1)]
        [TestCase(256)]
        public void ColdSetScanResolvesTypesOnceAndWarmLookupsDoNotReadComponents(int count)
        {
            DatabaseFixture db = new DatabaseFixture();
            for (int i = 0; i < count; i++)
                db.m_items.Add(Item("Helmet" + i, "$item_helmet", i == count - 1 ? "troll" : "", "$se_troll"));
            Use(db);

            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$se_troll"));
            Assert.That(componentReads, Is.EqualTo(count), "The cold index must inspect each item once.");
            Assert.That(typeResolutions, Is.EqualTo(2), "Assembly/type discovery must be independent of item count.");
            for (int i = 0; i < 20; i++)
            {
                Assert.That(GameItems.SetName("TROLL"), Is.EqualTo("$se_troll"));
                Assert.That(GameItems.SetName("missing"), Is.Null);
            }
            Assert.That(componentReads, Is.EqualTo(count));
            Assert.That(typeResolutions, Is.EqualTo(2));
            Assert.That(db.m_items[count - 1].Identifier, Is.EqualTo("Helmet" + (count - 1)));
            Assert.That(((SharedFixture)db.m_items[count - 1].Drop.m_itemData.m_shared).m_setName, Is.EqualTo("troll"));
        }

        [Test]
        public void PositiveItemLookupCachesTheRawTokenAndRetainsThePrefabIdentifier()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("HelmetTrollLeather", "$item_helmet_trollleather", "troll", "$se_troll"));
            Use(db);
            for (int i = 0; i < 20; i++)
                Assert.That(GameItems.ItemName("HelmetTrollLeather"), Is.EqualTo("$item_helmet_trollleather"));
            Assert.That(db.PrefabLookups, Is.EqualTo(1));
            Assert.That(componentReads, Is.EqualTo(1));
            Assert.That(typeResolutions, Is.EqualTo(2));
            Assert.That(db.m_items[0].Identifier, Is.EqualTo("HelmetTrollLeather"));
            Assert.That(GameItems.ItemName("helm troll leather"), Is.Null);
        }

        [Test]
        public void AnAbsentDatabaseAndEmptyListDoNotPoisonLateItemLookups()
        {
            Use(null);
            Assert.That(GameItems.SetName("Troll"), Is.Null);
            Assert.That(GameItems.ItemName("Helmet"), Is.Null);
            DatabaseFixture db = DatabaseFixture.instance = new DatabaseFixture();
            Assert.That(GameItems.SetName("Troll"), Is.Null);
            Assert.That(GameItems.ItemName("Helmet"), Is.Null);
            db.m_items.Add(Item("Helmet", "$item_helmet", "troll", "$se_troll"));
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$se_troll"));
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$item_helmet"));
        }

        [Test]
        public void ATypeUnavailableDuringStartupCanBeBoundAfterItAppears()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("Helmet", "$item_helmet", "troll", "$se_troll"));
            Use(db);
            bool available = false;
            GameItems.TypeResolver = name => name == "ObjectDB" ? typeof(DatabaseFixture)
                : name == "ItemDrop" && available ? typeof(DropFixture) : null;
            Assert.That(GameItems.SetName("Troll"), Is.Null);
            available = true;
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$se_troll"));
        }

        [Test]
        public void LateRegistrationDoesNotCacheAMissingItem()
        {
            DatabaseFixture db = new DatabaseFixture { Registered = false };
            db.m_items.Add(Item("Helmet", "$item_helmet", "troll", "$se_troll"));
            Use(db);
            Assert.That(GameItems.ItemName("Helmet"), Is.Null);
            db.Register();
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$item_helmet"));
        }

        [Test]
        public void ListGrowthReplacementAndDatabaseReplacementRefreshTheIndex()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("Helmet", "$old_item", "troll", "$old_set"));
            Use(db);
            Assert.That(GameItems.SetName("New"), Is.Null);
            db.m_items.Add(Item("NewHelmet", "$new_item", "new", "$new_set"));
            Assert.That(GameItems.SetName("New"), Is.EqualTo("$new_set"));
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$old_item"));
            db.m_items = new List<PrefabFixture> {
                Item("Helmet", "$replacement_item", "troll", "$replacement_set"),
                Item("Other", "$other", "other", "$other_set") };
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$replacement_set"));
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$replacement_item"));
            DatabaseFixture.instance = new DatabaseFixture();
            DatabaseFixture.instance.m_items.Add(Item("Helmet", "$world_item", "troll", "$world_set"));
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$world_set"));
            Assert.That(GameItems.SetName("New"), Is.Null);
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$world_item"));
            Assert.That(typeResolutions, Is.EqualTo(2), "Changing databases must retain runtime type bindings.");
        }

        [Test]
        public void RegisterInvalidatesSameCountChangesWithoutScanningUntilTheNextLookup()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("Helmet", "$old_item", "troll", "$old_set"));
            Use(db);
            GameItems.Install(Patcher);
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$old_set"));
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$old_item"));
            db.m_items[0] = Item("Helmet", "$new_item", "troll", "$new_set");
            int previousReads = componentReads;
            db.Register();
            Assert.That(componentReads, Is.EqualTo(previousReads), "Registration only invalidates; it must not scan the list.");
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$new_set"));
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$new_item"));
            Assert.That(typeResolutions, Is.EqualTo(2));
        }

        [Test]
        public void AdapterInvalidationRefreshesSharedDataInPlace()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("Helmet", "$item", "troll", "$old_set"));
            Use(db);
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$old_set"));
            ((SharedFixture)db.m_items[0].Drop.m_itemData.m_shared).m_setStatusEffect.m_name = "$new_set";
            GameItems.Invalidate();
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$new_set"));
            Assert.That(typeResolutions, Is.EqualTo(2));
        }

        [Test]
        public void RegistrationRefreshesTheSamePartiallyTranslatedTooltip()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("Helmet", "$item_helmet", "troll", "$se_troll"));
            Use(db);
            GameItems.Install(Patcher);
            Localization.SelectedLanguage = "Russian";
            Localization.Resources["Russian"] = new Dictionary<string, string> {
                { "item_helmet", "Шлем" }, { "item_cape", "Плащ" }, { "se_troll", "Комплект тролля" } };
            LocalizationBridge.Install(Patcher, typeof(Localization), () => Localization.SelectedLanguage);
            Assert.That(Localization.instance, Is.Not.Null);
            Module module = FixtureModule("item-registration", "FixturePlugin");
            module.patterns.Add(new PatternSpec { source = "Set: {0} ({1}/{2})", target = "Комплект: {0} ({1} из {2})",
                arguments = { { "0", "itemSet" } }, numeric = { 1, 2 } });
            module.patterns.Add(new PatternSpec { source = "Part: [{0}]", target = "Часть: [{0}]", arguments = { { "0", "item" } } });
            Activate(module);
            const string block = "Set: Troll (1/2)\nPart: [Helmet]\nPart: [Cape]";
            Assert.That(TextEngine.Display(block, module.id), Is.EqualTo("Комплект: Комплект тролля (1 из 2)\nЧасть: [Шлем]\nЧасть: [Cape]"));
            int previousLookups = db.PrefabLookups;
            Assert.That(TextEngine.Display(block, module.id), Is.EqualTo("Комплект: Комплект тролля (1 из 2)\nЧасть: [Шлем]\nЧасть: [Cape]"));
            Assert.That(db.PrefabLookups, Is.EqualTo(previousLookups), "The partial tooltip must be cached before registration.");
            db.m_items.Add(Item("Cape", "$item_cape", "troll", "$se_troll"));
            db.Register();
            Assert.That(TextEngine.Display(block, module.id), Is.EqualTo("Комплект: Комплект тролля (1 из 2)\nЧасть: [Шлем]\nЧасть: [Плащ]"));
            Assert.That(db.m_items[1].Identifier, Is.EqualTo("Cape"));
        }

        [Test]
        public void CachedTokensFollowTheCurrentLanguageWithoutAnotherItemScan()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("Helmet", "$item_helmet", "troll", "$se_troll"));
            Use(db);
            Localization.Resources["English"] = new Dictionary<string, string> { { "item_helmet", "Helmet" }, { "se_troll", "Troll set" } };
            Localization.Resources["Russian"] = new Dictionary<string, string> { { "item_helmet", "Шлем" }, { "se_troll", "Комплект тролля" } };
            LocalizationBridge.Install(Patcher, typeof(Localization), () => Localization.SelectedLanguage);
            Localization main = Localization.instance;
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("Helmet"));
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("Troll set"));
            int previousReads = componentReads;
            main.SetLanguage("Russian");
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("Шлем"));
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("Комплект тролля"));
            Assert.That(componentReads, Is.EqualTo(previousReads));
            Assert.That(db.PrefabLookups, Is.EqualTo(1));
        }

        [Test]
        public void ReflectionBindingsFollowEachActualSharedDataTypeAndTokenNamedSets()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("Helmet", "$item_helmet", "troll", "$se_troll"));
            PrefabFixture propertyItem = Item("Cape", null, null, null);
            propertyItem.Drop.m_itemData.m_shared = new PropertySharedFixture { m_name = "$item_cape", m_setName = "$set_key" };
            db.m_items.Add(propertyItem);
            Use(db);
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$se_troll"));
            Assert.That(GameItems.SetName("$SET_KEY"), Is.EqualTo("$set_key"));
            Assert.That(GameItems.ItemName("Cape"), Is.EqualTo("$item_cape"));
        }

        [Test]
        public void ResetDropsTokensBindingsAndInjectedLookupDelegates()
        {
            DatabaseFixture db = new DatabaseFixture();
            db.m_items.Add(Item("Helmet", "$old_item", "troll", "$old_set"));
            Use(db);
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$old_set"));
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$old_item"));
            GameItems.ItemToken = ignored => "injected item";
            GameItems.SetToken = ignored => "injected set";
            GameItems.Invalidate();
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("injected item"));
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("injected set"));
            GameItems.Reset();
            db.m_items[0] = Item("Helmet", "$new_item", "troll", "$new_set");
            Use(db);
            Assert.That(GameItems.ItemName("Helmet"), Is.EqualTo("$new_item"));
            Assert.That(GameItems.SetName("Troll"), Is.EqualTo("$new_set"));
            Assert.That(typeResolutions, Is.EqualTo(2), "Reset must resolve both runtime types anew.");
        }
    }
}
