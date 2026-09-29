using System;
using System.IO;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture]
    public sealed class RuntimeReportTests
    {
        [TestCase("Initializing")]
        [TestCase("Disabled")]
        [TestCase("MissingCatalog")]
        [TestCase("Failed")]
        [TestCase("Stopped")]
        public void InactiveStateReplacesAnEarlierSuccessfulFile(string state)
        {
            string directory = Path.Combine(Path.GetTempPath(), "tolmach-report-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Tolmach.runtime.txt");
            try
            {
                RuntimeReport old = new RuntimeReport { Status = RuntimeStatus.Active };
                old.Write(path, true, new Module[0], new[] { "old-session-only-marker" });
                Assert.That(File.ReadAllText(path), Does.Contain("Russian active: True"));

                RuntimeReport current = new RuntimeReport { Status = (RuntimeStatus)Enum.Parse(typeof(RuntimeStatus), state) };
                // Even stale language state cannot turn an inactive plugin into an active one.
                current.Write(path, true, new Module[0], new[] { "current-session-note" });
                string actual = File.ReadAllText(path);
                Assert.That(actual, Does.Contain("Session: " + current.SessionId));
                Assert.That(actual, Does.Not.Contain(old.SessionId));
                Assert.That(actual, Does.Contain("Status: " + state));
                Assert.That(actual, Does.Contain("Russian selected: True"));
                Assert.That(actual, Does.Contain("Russian active: False"));
                Assert.That(actual, Does.Not.Contain("old-session-only-marker"));
                Assert.That(actual, Does.Contain("current-session-note"));
            }
            finally
            {
                File.Delete(path);
                Directory.Delete(directory);
            }
        }

        [Test]
        public void RefreshKeepsSessionIdentityButNotOldStatusOrNotes()
        {
            RuntimeReport report = new RuntimeReport();
            string first = report.Render(false, new Module[0], new[] { "initial note" });
            report.Status = RuntimeStatus.Active;
            string next = report.Render(true, new Module[0], new string[0]);
            Assert.That(first, Does.Contain("Session: " + report.SessionId));
            Assert.That(next, Does.Contain("Session: " + report.SessionId));
            Assert.That(next, Does.Contain("Started UTC: " + report.StartedUtc.ToString("o")));
            Assert.That(next, Does.Contain("Status: Active"));
            Assert.That(next, Does.Not.Contain("initial note"));
            Assert.That(next, Does.Not.Contain("Status: Initializing"));
        }

        [Test]
        public void ActivePluginOnAnotherLanguageIsNotRussianActive()
        {
            RuntimeReport report = new RuntimeReport { Status = RuntimeStatus.Active };
            string text = report.Render(false, new Module[0], new string[0]);
            Assert.That(text, Does.Contain("Status: Active"));
            Assert.That(text, Does.Contain("Russian selected: False"));
            Assert.That(text, Does.Contain("Russian active: False"));
        }

        [Test]
        public void ReportRetainsAdapterDetailsAlongsideLanguageAndState()
        {
            Module m = new Module { id = "example", UiAllowed = false };
            m.Warnings.Add("known target missing");
            m.Patches["method"] = new PatchEvidence { method = "method", notPatched = "unsupported boundary" };
            RuntimeReport report = new RuntimeReport { Status = RuntimeStatus.Active };
            string text = report.Render(true, new[] { m }, new[] { "another module: disabled by user" });
            Assert.That(text, Does.Contain("Russian active: True"));
            Assert.That(text, Does.Contain("uiAllowed=False"));
            Assert.That(text, Does.Contain("notPatched=unsupported boundary"));
            Assert.That(text, Does.Contain("WARNING: known target missing"));
            Assert.That(text, Does.Contain("another module: disabled by user"));
            Assert.That(text, Does.Contain("not every untranslated string"));
        }
    }
}
