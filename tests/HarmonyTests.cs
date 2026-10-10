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
        [TestCase("\n")]
        [TestCase("\r\n")]
        public void HelperDisplayTranslatesFinalRemoteReportWithoutChangingWireLogsOrState(string newline)
        {
            Module m = CatalogLoader.Read(Catalog("ConditionalConfigSync"));
            m.RuntimeAssembly = typeof(TextEngine).Assembly;
            m.RuntimeCodeAssembly = typeof(ConditionalConfigSync.VersionCheck).Assembly;
            m.ExactVersion = true; m.UiAllowed = true; Activate(m);
            DisplayPatches.Install(Patcher, m);
            string[] names = { "My: The client has version 1", "Again: The client has version 1: The client has version 1", "Opaque: Mod Name", "Unknown: The client has version 9", "", "Rich<b>Conditional Config Sync rejected this connection.</b>Name" };
            string[] messages = {
                "The client has version 1.2.3, but the server requires at least 1.4.0.",
                "The client has version 1.2.3, but the server requires at least 1.4.0.",
                "The server reported version v-invalid, but this client did not process that mod's Conditional Config Sync handshake.",
                "an unknown reason containing RPC_ID",
                "No version handshake was received from the server.", "The client reported an invalid mod version or minimum version." };
            string report = ConditionalConfigSync.VersionCheck.PrepareReport(names, messages, newline);
            string expected = "Vanilla failure\n\n" + ("Conditional Config Sync отклонил подключение.\n\nНе пройдены следующие проверки синхронизации:\n" +
                "- My: The client has version 1: Клиент имеет версию 1.2.3, но сервер требует не ниже 1.4.0.\n" +
                "- Again: The client has version 1: The client has version 1: Клиент имеет версию 1.2.3, но сервер требует не ниже 1.4.0.\n" +
                "- Opaque: Mod Name: Сервер сообщил версию v-invalid, но клиент не обработал обмен данными Conditional Config Sync для этого мода.\n" +
                "- Unknown: The client has version 9: an unknown reason containing RPC_ID\n" +
                "- Сервер не прислал данные для проверки версий.\n" +
                "- Rich<b>Conditional Config Sync rejected this connection.</b>Name: Клиент сообщил некорректную версию мода или минимальную версию.").Replace("\n", newline);
            var label = new TMPro.TMP_Text { text = "Vanilla failure" };
            ConditionalConfigSync.VersionCheck.ShowConnectionError(label);
            Assert.That(label.text, Is.EqualTo(expected));
            Assert.That(ConditionalConfigSync.VersionCheck.RpcPayload, Is.EqualTo(report));
            Assert.That(ConditionalConfigSync.VersionCheck.LogPayload, Is.EqualTo(report));
            Assert.That(ConditionalConfigSync.VersionCheck.PendingReport, Is.EqualTo(report));
            Array raw = ConditionalConfigSync.VersionCheck.NormalizedReasons;
            for (int i = 0; i < raw.Length; i++)
            {
                object reason = raw.GetValue(i);
                Assert.That(reason.GetType().GetField("ModName", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reason), Is.EqualTo(names[i]));
                Assert.That(reason.GetType().GetField("Message", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reason), Is.EqualTo(messages[i]));
            }
            Assert.That(m.Patches.Values.Sum(p => p.displayCalls), Is.EqualTo(1));
            Assert.That(m.Patches.Values.Sum(p => p.literals), Is.Zero);
            Assert.That(ConditionalConfigSync.VersionCheck.EnumeratedReasons, Is.EqualTo(names.Length), "The source is enumerated only by normalization, never by the observer.");
            Assert.That(m.Patches.Values.All(p => p.method.StartsWith("ConditionalConfigSync.")), Is.True);
            var outside = new TMPro.TMP_Text { text = "Conditional Config Sync rejected this connection." };
            Assert.That(outside.text, Is.EqualTo("Conditional Config Sync rejected this connection."), "No global TMP setter patch.");
            TextEngine.IsRussian = false;
            label.text = "Vanilla failure";
            ConditionalConfigSync.VersionCheck.PrepareReport(names, messages, newline);
            ConditionalConfigSync.VersionCheck.ShowConnectionError(label);
            Assert.That(label.text, Is.EqualTo("Vanilla failure\n\n" + report));
        }
        [Test]
        public void MissingHelperCannotFallBackToPatchingThePluginAssembly()
        {
            Module m = ViewModule(); m.codeAssembly = "unavailable.helper"; m.RuntimeCodeAssembly = null;
            DisplayPatches.Install(Patcher, m);
            Assert.That(m.ScannedMethods, Is.Zero); Assert.That(m.PatchedMethods, Is.Zero);
            FixturePlugin.View.Draw(); Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Price"));
        }
        private Module StructuredCcsModule(bool install = true)
        {
            Module m = CatalogLoader.Read(Catalog("ConditionalConfigSync"));
            m.RuntimeAssembly = typeof(TextEngine).Assembly;
            m.RuntimeCodeAssembly = typeof(ConditionalConfigSync.VersionCheck).Assembly;
            m.ExactVersion = true; m.UiAllowed = true; Activate(m);
            if (install) DisplayPatches.Install(Patcher, m);
            return m;
        }
        [Test]
        public void CcsMetadataReplacesThePreviousReportAndResetClearsItsBoundary()
        {
            Module m = StructuredCcsModule();
            const string reason = "The client has version 1.2.3, but the server requires at least 1.4.0.";
            const string line = "- My: The client has version 1: " + reason;
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My: The client has version 1" }, new[] { reason }, "\n");
            Assert.That(TextEngine.Display(line, m.id), Is.EqualTo("- My: The client has version 1: Клиент имеет версию 1.2.3, но сервер требует не ниже 1.4.0."));
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "Other" }, new[] { "Unknown reason: RPC_ID" }, "\n");
            Assert.That(TextEngine.Display(line, m.id), Is.EqualTo(line), "Metadata of the previous report cannot parse later legacy input.");
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My: The client has version 1" }, new[] { reason }, "\n");
            DisplayPatches.Reset();
            Assert.That(TextEngine.Display(line, m.id), Is.EqualTo(line));
        }
        [Test]
        public void CcsUnstructuredTaggedNameCannotTranslateItsHeaderLookingFragment()
        {
            Module m = StructuredCcsModule();
            const string line = "- Unknown<b>Conditional Config Sync rejected this connection.</b>Name: arbitrary RPC_ID";
            Assert.That(TextEngine.Display(line, m.id), Is.EqualTo(line));
            Assert.That(TextEngine.Display("Conditional Config Sync rejected this connection.\n" + line, m.id),
                Is.EqualTo("Conditional Config Sync отклонил подключение.\n" + line));
        }
        [Test]
        public void CcsCollidingRenderedLinesWithDifferentStructuralBoundariesStayUnchanged()
        {
            Module m = StructuredCcsModule();
            const string reason = "The client has version 1.2.3, but the server requires at least 1.4.0.";
            const string line = "- My: The client has version 1: " + reason;
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My: The client has version 1", "My" }, new[] { reason, "The client has version 1: " + reason }, "\n");
            Assert.That(TextEngine.Display(line, m.id), Is.EqualTo(line));
        }
        [TestCase(true)]
        [TestCase(false)]
        public void CcsMissingHelperAndDisallowedVersionInstallNoHooks(bool missingHelper)
        {
            Module m = StructuredCcsModule(false);
            if (missingHelper) m.RuntimeCodeAssembly = null;
            else { m.ExactVersion = false; m.UiAllowed = false; }
            DisplayPatches.Install(Patcher, m);
            const string line = "- Name: The client reported an invalid mod version or minimum version.";
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "Name" }, new[] { "The client reported an invalid mod version or minimum version." }, "\n");
            Assert.That(TextEngine.Display(line, m.id), Is.EqualTo(line));
            Assert.That(TextEngine.Display("Conditional Config Sync rejected this connection.", m.id), Is.EqualTo("Conditional Config Sync rejected this connection."));
            Assert.That(Patcher.GetPatchedMethods(), Is.Empty);
        }
        [Test]
        public void CcsApiDriftAllowsOnlyKnownHeadersAndPreservesUnstructuredReasons()
        {
            Module m = StructuredCcsModule(false); m.RuntimeCodeAssembly = typeof(TextEngine).Assembly;
            DisplayPatches.Install(Patcher, m);
            Assert.That(m.Warnings, Has.Some.StartsWith("CCS structured rejection display unavailable:"));
            const string value = "Conditional Config Sync rejected this connection.\n- My: The client has version 1: The client has version 1.2.3, but the server requires at least 1.4.0.";
            Assert.That(TextEngine.Display(value, m.id), Is.EqualTo("Conditional Config Sync отклонил подключение.\n- My: The client has version 1: The client has version 1.2.3, but the server requires at least 1.4.0."));
        }
        [TestCase("No version handshake was received from the server.", "Сервер не прислал данные для проверки версий.")]
        [TestCase("The version handshake was rejected for an unknown compatibility reason.", "Обмен данными для проверки версий отклонен по неизвестной причине несовместимости.")]
        [TestCase("The server did not receive the required synchronization handshake. The server requires ThirdPartyMod 1.0.0 or newer and Conditional Config Sync protocol 1.", "Сервер не получил обязательные данные синхронизации. Требуется ThirdPartyMod 1.0.0 или новее и протокол Conditional Config Sync 1.")]
        [TestCase("The server did not receive the required synchronization handshake. The server requires Third-party Mod 1.0.0 or newer and Conditional Config Sync protocol 1.", "The server did not receive the required synchronization handshake. The server requires Third-party Mod 1.0.0 or newer and Conditional Config Sync protocol 1.")]
        [TestCase("The client sent a legacy or incomplete handshake without Conditional Config Sync protocol metadata. The server requires protocol 1.", "Клиент прислал устаревшие или неполные данные обмена без версии протокола Conditional Config Sync. Сервер требует протокол 1.")]
        [TestCase("The server reported Conditional Config Sync protocol 0, but the client requires protocol 1.", "Сервер сообщил протокол Conditional Config Sync 0, но клиент требует протокол 1.")]
        [TestCase("The client reported an invalid mod version or minimum version.", "Клиент сообщил некорректную версию мода или минимальную версию.")]
        [TestCase("The server has an invalid local version requirement and could not validate the connection.", "Сервер имеет некорректное локальное требование к версии и не смог проверить подключение.")]
        [TestCase("The server requires at least version 1.3.0, but the client has 1.0.0.", "Сервер требует версию не ниже 1.3.0, но клиент имеет 1.0.0.")]
        [TestCase("The server sent a malformed version-handshake package: wire_GUID", "Сервер прислал некорректный пакет проверки версий: wire_GUID")]
        [TestCase("The mystery reported an invalid mod version or minimum version.", "The mystery reported an invalid mod version or minimum version.")]
        public void KnownCcsReasonsTranslateAndUnknownRolesStayOpaque(string source, string expected)
        {
            Module m = CatalogLoader.Read(Catalog("ConditionalConfigSync"));
            Assert.That(new TextTable(m).Translate(source), Is.EqualTo(expected));
        }
        private const string StructuredVersion = "The server reported version v-invalid: detail, but this client did not process that mod's Conditional Config Sync handshake.";
        private const string CollidingLine = "- My: " + StructuredVersion;
        [TestCase(false)]
        [TestCase(true)]
        public void CcsLegacyReplacementCannotReuseMetadataFromThePreviousSessionOrReport(bool reset)
        {
            StructuredCcsModule();
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My" }, new[] { StructuredVersion }, "\n");
            if (reset) ConditionalConfigSync.VersionCheck.ResetSessionState(false);
            ConditionalConfigSync.VersionCheck.PrepareLegacyReport(CollidingLine);
            var label = new TMPro.TMP_Text { text = "Vanilla failure" };
            ConditionalConfigSync.VersionCheck.ShowConnectionError(label);
            Assert.That(label.text, Is.EqualTo("Vanilla failure\n\n" + CollidingLine));
            Assert.That(ConditionalConfigSync.VersionCheck.PendingReport, Is.EqualTo(CollidingLine));
        }
        [Test]
        public void CcsPreservedPendingReportKeepsItsOwnMetadataUntilTheSinkClearsIt()
        {
            Module m = StructuredCcsModule();
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My" }, new[] { StructuredVersion }, "\n");
            object report = ConditionalConfigSync.VersionCheck.CurrentPending;
            ConditionalConfigSync.VersionCheck.ResetSessionState(true);
            Assert.That(ConditionalConfigSync.VersionCheck.CurrentPending, Is.SameAs(report));
            var label = new TMPro.TMP_Text { text = "" };
            ConditionalConfigSync.VersionCheck.ShowConnectionError(label);
            Assert.That(label.text, Does.Contain("- My: Сервер сообщил версию v-invalid: detail, но клиент не обработал обмен данными Conditional Config Sync для этого мода."));
            Assert.That(ConditionalConfigSync.VersionCheck.CurrentPending, Is.Null);
            Assert.That(TextEngine.Display(CollidingLine, m.id), Is.EqualTo(CollidingLine), "Upstream clearing pending state expires its metadata.");
        }
        [Test]
        public void CcsUnrelatedNormalizationDoesNotReplaceTheActiveReportMetadata()
        {
            StructuredCcsModule();
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My" }, new[] { StructuredVersion }, "\n");
            ConditionalConfigSync.VersionCheck.NormalizeUnrelated(new[] { "My: The server reported version v-invalid" },
                new[] { "detail, but this client did not process that mod's Conditional Config Sync handshake." });
            var label = new TMPro.TMP_Text { text = "" };
            ConditionalConfigSync.VersionCheck.ShowConnectionError(label);
            Assert.That(label.text, Does.Contain("- My: Сервер сообщил версию v-invalid: detail, но клиент не обработал обмен данными Conditional Config Sync для этого мода."));
        }
        [Test]
        public void CcsStructuredReplacementWithEqualRenderedLineUsesTheNewReportBoundary()
        {
            StructuredCcsModule();
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My" }, new[] { StructuredVersion }, "\n");
            object before = ConditionalConfigSync.VersionCheck.CurrentPending;
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My: The server reported version v-invalid" },
                new[] { "detail, but this client did not process that mod's Conditional Config Sync handshake." }, "\n");
            Assert.That(ConditionalConfigSync.VersionCheck.CurrentPending, Is.Not.SameAs(before));
            var label = new TMPro.TMP_Text { text = "" };
            ConditionalConfigSync.VersionCheck.ShowConnectionError(label);
            Assert.That(label.text, Does.Contain(CollidingLine));
        }
        [Test]
        public void CcsFailedSetterClearsCaptureContextAndKeepsThePreviousPendingIdentity()
        {
            StructuredCcsModule();
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "My" }, new[] { StructuredVersion }, "\n");
            object before = ConditionalConfigSync.VersionCheck.CurrentPending;
            ConditionalConfigSync.VersionCheck.FailNextSetter = true;
            Assert.Throws<InvalidOperationException>(() => ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "Fail" }, new[] { "Unknown reason" }, "\n"));
            Assert.That(ConditionalConfigSync.VersionCheck.CurrentPending, Is.SameAs(before));
            ConditionalConfigSync.VersionCheck.NormalizeUnrelated(new[] { "My: The server reported version v-invalid" },
                new[] { "detail, but this client did not process that mod's Conditional Config Sync handshake." });
            var label = new TMPro.TMP_Text { text = "" };
            ConditionalConfigSync.VersionCheck.ShowConnectionError(label);
            Assert.That(label.text, Does.Contain("- My: Сервер сообщил версию v-invalid: detail, но клиент не обработал обмен данными Conditional Config Sync для этого мода."));
        }
        [Test]
        public void CcsAmbiguousVersionDelimiterStaysEnglishInTheFinalReport()
        {
            StructuredCcsModule();
            const string reason = "The client has version 1, but the server requires at least 2, but the server requires at least 3.";
            ConditionalConfigSync.VersionCheck.PrepareReport(new[] { "Opaque Mod" }, new[] { reason }, "\n");
            var label = new TMPro.TMP_Text { text = "" };
            ConditionalConfigSync.VersionCheck.ShowConnectionError(label);
            Assert.That(label.text, Does.Contain("- Opaque Mod: " + reason));
            Assert.That(ConditionalConfigSync.VersionCheck.RpcPayload, Does.Contain(reason));
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
        public void LiteralRuleOnOverloadsPatchesOnlyTheBodyWithTheLiteral()
        {
            Module m = FixtureModule("overloads", "FixturePlugin");
            m.literals.Add(new LiteralSpec { type = "FixturePlugin.Overloads", method = "Pick", values = new Dictionary<string, string> { { "Overloaded literal", "Литерал перегрузки" } } });
            Activate(m);
            DisplayPatches.Install(Patcher, m);
            Assert.That(FixturePlugin.Overloads.Pick(), Is.EqualTo("Литерал перегрузки"));
            Assert.That(FixturePlugin.Overloads.Pick(2), Is.EqualTo("Other 2"));
            Assert.That(m.PatchedMethods, Is.EqualTo(1));
            Assert.That(m.Warnings, Is.Empty, "The overload without the literal is neither patched nor reported.");
        }
        [Test]
        public void AnotherPluginVersionTranslatesTheLiteralsStillThereAndReportsTheRest()
        {
            Module m = FixtureModule("other", "FixturePlugin"); m.ExactVersion = false;
            m.literals.Add(new LiteralSpec { type = "FixturePlugin.View", method = "Literal", values = new Dictionary<string, string> { { "Literal", "Литерал" } } });
            m.literals.Add(new LiteralSpec { type = "FixturePlugin.Overloads", method = "Pick", values = new Dictionary<string, string> { { "Gone literal", "Пропавший литерал" } } });
            Activate(m);
            DisplayPatches.Install(Patcher, m);
            Assert.That(FixturePlugin.View.Literal(), Is.EqualTo("Литерал"));
            Assert.That(m.Warnings, Has.Some.StartsWith("Version differs from snapshot; adapters apply"));
            Assert.That(m.Warnings, Has.Some.EqualTo("Literal not found in IL: FixturePlugin.Overloads.Pick :: Gone literal"));
        }
        [Test]
        public void OnlyAuditedVersionsLeavesAnotherVersionUnpatched()
        {
            Module m = ViewModule(); m.ExactVersion = false; m.UiAllowed = false;
            DisplayPatches.Install(Patcher, m);
            Assert.That(FixturePlugin.View.Literal(), Is.EqualTo("Literal"));
            Assert.That(Patcher.GetPatchedMethods(), Is.Empty);
            Assert.That(m.Warnings, Has.Some.StartsWith("Version differs from snapshot and OnlyAuditedVersions is on"));
        }
        [Test]
        public void LiteralMissingFromEveryOverloadIsReportedOnce()
        {
            Module m = FixtureModule("missing", "FixturePlugin");
            m.literals.Add(new LiteralSpec { type = "FixturePlugin.Overloads", method = "Pick", values = new Dictionary<string, string> { { "\nGone literal", "\nПропавший литерал" } } });
            Activate(m);
            DisplayPatches.Install(Patcher, m);
            Assert.That(m.PatchedMethods, Is.Zero);
            Assert.That(m.Warnings, Is.EqualTo(new[] { "Literal not found in IL: FixturePlugin.Overloads.Pick :: \\nGone literal" }));
        }
        [Test]
        public void ConfigTextReplacesOnlyTheUnchangedDefault()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tolmach-tests-" + Guid.NewGuid().ToString("N"), "hover.cfg");
            try
            {
                var config = new BepInEx.Configuration.ConfigFile(path, false) { SaveOnConfigSet = false };
                FixturePlugin.Configured.HoverText = config.Bind("UI", "HoverText", "Take all cooked", "Text shown in the cooking station hover prompt.");
                Module m = FixtureModule("configured", "FixturePlugin");
                m.configTexts.Add(new LiteralSpec { type = "FixturePlugin.Configured", method = "Postfix",
                    values = new Dictionary<string, string> { { "Take all cooked", "Взять все готовое" } } });
                DisplayPatches.Install(Patcher, Activate(m));
                string hover = "Стойка для готовки";
                FixturePlugin.Configured.Postfix(ref hover);
                Assert.That(hover, Is.EqualTo("Стойка для готовки\n[<color=yellow><b>E</b></color>] Взять все готовое"));
                // A player's own text is shown as typed.
                FixturePlugin.Configured.HoverText.Value = "Grab it all";
                hover = ""; FixturePlugin.Configured.Postfix(ref hover);
                Assert.That(hover, Does.EndWith("] Grab it all"));
                FixturePlugin.Configured.HoverText.Value = "Take all cooked";
                TextEngine.IsRussian = false;
                hover = ""; FixturePlugin.Configured.Postfix(ref hover);
                Assert.That(hover, Does.EndWith("] Take all cooked"));
                Assert.That(FixturePlugin.Configured.HoverText.Value, Is.EqualTo("Take all cooked"), "the stored value never changes");
                Assert.That(m.PatchedMethods, Is.EqualTo(1));
                Assert.That(m.Patches.Values.Sum(x => x.configTexts), Is.EqualTo(1));
                Assert.That(m.Warnings, Is.Empty);
            }
            finally
            {
                // Saving is off, so the file exists only if a test binding wrote it.
                string folder = System.IO.Path.GetDirectoryName(path);
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            }
        }
        [Test]
        public void ConfigTextRuleWithoutAConfigReadIsReported()
        {
            Module m = FixtureModule("configured", "FixturePlugin");
            var values = new Dictionary<string, string> { { "Take all cooked", "Взять все готовое" } };
            m.configTexts.Add(new LiteralSpec { type = "FixturePlugin.Configured", method = "Constant", values = values });
            m.configTexts.Add(new LiteralSpec { type = "FixturePlugin.Configured", method = "Gone", values = values });
            DisplayPatches.Install(Patcher, Activate(m));
            // A literal equal to the default is not a config read: it stays, and nothing is patched.
            Assert.That(FixturePlugin.Configured.Constant(), Is.EqualTo("Take all cooked"));
            Assert.That(m.PatchedMethods, Is.Zero);
            Assert.That(m.Warnings, Is.EquivalentTo(new[] {
                "Config value read not found in IL: FixturePlugin.Configured.Constant",
                "Expected config text adapter not found: FixturePlugin.Configured.Gone" }));
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
            // Runtimes differ in when they reject rewritten fault IL. The contract
            // is to leave this method unpatched before a deferred patcher can see it.
            Module m = FixtureModule("fault", "FixturePlugin");
            m.literals.Add(new LiteralSpec { type = iterator.FullName, method = "MoveNext", values = new Dictionary<string, string> { { "Faulted literal", "Литерал итератора" } } });
            Activate(m);
            Assert.DoesNotThrow(() => DisplayPatches.Install(Patcher, m));
            Assert.That(HarmonyLib.Harmony.GetPatchInfo(moveNext), Is.Null, "The fault guard must not queue a patch.");
            Assert.That(m.Warnings, Has.Some.EqualTo("UI patch skipped " + iterator.FullName + ".MoveNext: fault block"));
            Assert.That(FixturePlugin.Faulted.Lines().ToList(), Is.EqualTo(new[] { "Faulted literal" }));
        }
        private static IEnumerable<HarmonyLib.CodeInstruction> Unchanged(IEnumerable<HarmonyLib.CodeInstruction> instructions) { return instructions; }
        [Test]
        public void UnpatchableMethodHasItsTextTranslatedInTheDisplayMethodsItCalls()
        {
            Module m = FixtureModule("callsites", "FixtureCallSites");
            m.texts["Price"] = "Цена";
            DisplayPatches.Install(Patcher, Activate(m));
            FixtureCallSites.FilteredWindow.Draw();
            Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Цена"));
            // The same text drawn by another mod stays as it is.
            FixtureOther.Window.Draw();
            Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Price"));
            Player player = new Player();
            FixtureCallSites.FaultedMessage.Run(player).ToList();
            Assert.That(player.LastMessage, Is.EqualTo("Цена"), "A call to Character.Message reaches the Player override.");
            Assert.That(FixtureCallSites.FaultedMessage.Done, Is.True);
            FixtureOther.Window.Message(player);
            Assert.That(player.LastMessage, Is.EqualTo("Price"));
            TextEngine.IsRussian = false;
            FixtureCallSites.FilteredWindow.Draw();
            Assert.That(UnityEngine.GUI.LastText, Is.EqualTo("Price"));
            Assert.That(m.Warnings, Is.Empty, "Nothing from the catalog is lost, so nothing is reported.");
            Assert.That(m.PatchedMethods, Is.Zero, "The methods themselves stay unpatched.");
            PatchEvidence window = m.Patches.Values.Single(p => p.method == "FixtureCallSites.FilteredWindow.Draw");
            Assert.That(window.notPatched, Is.EqualTo("exception filter"));
            Assert.That(window.callSites, Is.EqualTo(new[] { "GUI.Window" }));
            PatchEvidence coroutine = m.Patches.Values.Single(p => p.method.EndsWith(".MoveNext"));
            Assert.That(coroutine.notPatched, Is.EqualTo("fault block"));
            Assert.That(coroutine.callSites, Is.EquivalentTo(new[] { "Character.Message", "Player.Message" }));
            Assert.That(coroutine.leftAsIs, Is.Empty);
        }
        [Test]
        public void UnpatchableMethodWarnsAboutACallLeftAsIsOnlyWhenItsBodyHoldsCatalogText()
        {
            Module quiet = FixtureModule("quiet", "FixtureLeftAsIs");
            quiet.texts["Other"] = "Другое";
            DisplayPatches.Install(Patcher, Activate(quiet));
            Assert.That(quiet.Warnings, Is.Empty);
            PatchEvidence evidence = quiet.Patches.Values.Single();
            Assert.That(evidence.leftAsIs, Is.EqualTo(new[] { "Localization.Localize" }));
            Assert.That(evidence.callSites, Is.Empty);
            Module loud = FixtureModule("loud", "FixtureLeftAsIs");
            loud.texts["Price"] = "Цена";
            DisplayPatches.Install(Patcher, Activate(loud));
            Assert.That(loud.Warnings, Is.EqualTo(new[] {
                "UI patch skipped FixtureLeftAsIs.Filtered.Draw: exception filter; left untranslated: Localization.Localize" }));
            Assert.That(FixtureLeftAsIs.Filtered.Draw(), Is.EqualTo("Price"));
        }
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
            Assert.That(m.Warnings, Has.Some.StartsWith("UI adapter skipped FixtureOptional.Integration.OptionalArgument"));
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
        public void ReplaceNativeWordTakesOverTheModsOwnRussianAndOtherKeysStayFillOnly()
        {
            Module m = FixtureModule("mistakes", "FixturePlugin");
            m.words["wall_quarter"] = "Деревянная стена 1x1"; m.englishWords["wall_quarter"] = "Wood Wall Quarter Upper";
            m.words["wall_half"] = "Верхняя половина стены"; m.englishWords["wall_half"] = "Wood Wall Half Upper";
            m.replaceNative.Add("wall_quarter");
            Activate(m);
            Localization.SelectedLanguage = "Russian";
            Localization main = Localization.instance;
            InstallBridge(() => "Russian");
            main.AddWord("wall_quarter", "Деревянная стена 1х1");
            main.AddWord("wall_half", "Деревянная стена (половина)");
            LocalizationBridge.InjectMain();
            Assert.That(main.Translate("wall_quarter"), Is.EqualTo("Деревянная стена 1x1"));
            Assert.That(main.Translate("wall_half"), Is.EqualTo("Деревянная стена (половина)"), "Every other key stays fill-only.");
            main.AddWord("wall_quarter", "Деревянная стена 1х1");
            Assert.That(main.Translate("wall_quarter"), Is.EqualTo("Деревянная стена 1x1"), "A later registration by the mod does not bring the mistake back.");
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

namespace TMPro
{
    public sealed class TMP_Text
    {
        private string value;
        public string text
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            get { return value; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set { this.value = value; }
        }
    }
}
namespace ConditionalConfigSync
{
    public static class VersionCheck
    {
        public static string RpcPayload, LogPayload, PendingReport;
        public static Array NormalizedReasons;
        public static int EnumeratedReasons;
        public static bool FailNextSetter;
        private static string nextNewline = "\n";
        private sealed class PendingDisconnectReport
        {
            internal string Message;
        }
        private static PendingDisconnectReport pendingDisconnectReport;
        public static object CurrentPending { get { return pendingDisconnectReport; } }
        private sealed class DisconnectReasonItem
        {
            internal string ModName, Message;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static DisconnectReasonItem[] NormalizeDisconnectReasons(IEnumerable<DisconnectReasonItem> reasons)
        { return reasons.ToArray(); }
        public static string PrepareReport(string[] names, string[] messages, string newline)
        {
            EnumeratedReasons = 0;
            nextNewline = newline;
            SetPendingDisconnectReport(1, "structured", names.Select((name, index) => {
                if (++EnumeratedReasons > names.Length) throw new InvalidOperationException("Source reasons were enumerated again.");
                return new DisconnectReasonItem { ModName = name, Message = messages[index] };
            }));
            return pendingDisconnectReport.Message;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void SetPendingDisconnectReport(long connectionGeneration, string reportId, IEnumerable<DisconnectReasonItem> source)
        {
            var reasons = NormalizeDisconnectReasons(source);
            NormalizedReasons = reasons;
            if (FailNextSetter) { FailNextSetter = false; throw new InvalidOperationException("Setter failed after normalization."); }
            pendingDisconnectReport = new PendingDisconnectReport { Message =
                "Conditional Config Sync rejected this connection." + nextNewline + nextNewline + "The following synchronization checks failed:" + nextNewline +
                String.Join(nextNewline, reasons.Select(reason => "- " + (String.IsNullOrWhiteSpace(reason.ModName) ? "" : reason.ModName + ": ") + reason.Message)) };
        }
        public static void NormalizeUnrelated(string[] names, string[] messages)
        {
            NormalizeDisconnectReasons(names.Select((name, index) => new DisconnectReasonItem { ModName = name, Message = messages[index] }));
        }
        public static void PrepareLegacyReport(string text)
        {
            pendingDisconnectReport = new PendingDisconnectReport { Message = text };
        }
        public static void ResetSessionState(bool preserveConnectionError)
        {
            if (!preserveConnectionError) pendingDisconnectReport = null;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ShowConnectionError(TMPro.TMP_Text label)
        {
            var current = pendingDisconnectReport;
            if (current == null) return;
            string report = current.Message;
            RpcPayload = report; LogPayload = report; PendingReport = report;
            label.text = label.text + "\n\n" + report;
            pendingDisconnectReport = null;
        }
    }
}
