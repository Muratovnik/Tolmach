using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class ContainerNameDisplayTests : TestContextBase
    {
        private Module module;
        [SetUp]
        public void InstallStorageCatalog()
        {
            module = LoadModule("DynamicStorageForge");
            RawDisplay.Initialize();
            Localization.Resources["Russian"] = new Dictionary<string, string> {
                { "piece_container_empty", "ПУСТО" }, { "piece_noaccess", "Нет доступа" }, { "piece_container_open", "Открыть" }
            };
            Localization.Resources["English"] = new Dictionary<string, string> {
                { "piece_container_empty", "EMPTY" }, { "piece_noaccess", "No access" }, { "piece_container_open", "Open" }
            };
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            LocalizationBridge.Install(Patcher, typeof(Localization), () => "Russian");
        }

        [Test]
        public void EmptyHoverExposesTheOldFailureAndTranslatesBeforeComposition()
        {
            var container = new StorageContainer { m_name = "Bronze Stack (Storage)", Empty = true };
            Assert.That(container.GetHoverText(), Is.EqualTo("Bronze Stack (Storage) ( ПУСТО )\nОткрыть"));
            ContainerNameDisplay.Install(Patcher, typeof(StorageContainer));
            Assert.That(container.GetHoverText(), Is.EqualTo("Стопка слитков бронзы (хранилище) ( ПУСТО )\nОткрыть"));
            Assert.That(container.GetHoverName(), Is.EqualTo("Стопка слитков бронзы (хранилище)"));
            Assert.That(container.m_name, Is.EqualTo("Bronze Stack (Storage)"));
            Assert.That(module.Patches[typeof(StorageContainer).FullName + ".GetHoverText [container name]"].displayFields, Is.EqualTo(3));
        }

        [Test]
        public void FullAndDeniedHoverKeepNativeCompositionAndOriginalField()
        {
            ContainerNameDisplay.Install(Patcher, typeof(StorageContainer));
            var container = new StorageContainer { m_name = "Bronze Stack (Storage)" };
            Assert.That(container.GetHoverText(), Is.EqualTo("Стопка слитков бронзы (хранилище)\nОткрыть"));
            container.Denied = true;
            Assert.That(container.GetHoverText(), Is.EqualTo("Стопка слитков бронзы (хранилище)\nНет доступа"));
            Assert.That(container.m_name, Is.EqualTo("Bronze Stack (Storage)"));
        }

        [Test]
        public void OnlyExactActiveStorageNamesAreTranslatedAndEnglishRemainsEnglish()
        {
            var other = FixtureModule("OtherMod", "FixturePlugin");
            other.rawTexts["My configurable container"] = "Мой контейнер";
            Activate(other);
            ContainerNameDisplay.Install(Patcher, typeof(StorageContainer));
            var container = new StorageContainer { m_name = "My configurable container" };
            Assert.That(container.GetHoverName(), Is.EqualTo("My configurable container"));
            container.m_name = "My Bronze Stack (Storage)";
            Assert.That(container.GetHoverName(), Is.EqualTo("My Bronze Stack (Storage)"));
            container.m_name = "Bronze Stack (Storage)";
            Localization.instance.SetLanguage("English");
            Assert.That(container.GetHoverText(), Is.EqualTo("Bronze Stack (Storage)\nOpen"));
            Localization.instance.SetLanguage("Russian");
            Assert.That(container.GetHoverName(), Is.EqualTo("Стопка слитков бронзы (хранилище)"));
            module.UiAllowed = false;
            Assert.That(container.GetHoverName(), Is.EqualTo("Bronze Stack (Storage)"));
        }
    }

    public sealed class StorageContainer
    {
        public string m_name;
        public bool Empty;
        public bool Denied;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverText()
        {
            if (Denied) return Localization.instance.Localize(m_name + "\n$piece_noaccess");
            string text = Empty ? m_name + " ( $piece_container_empty )" : m_name;
            return Localization.instance.Localize(text + "\n$piece_container_open");
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetHoverName() { return m_name; }
    }
}
