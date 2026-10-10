using System;
using System.IO;
using System.Reflection;
using BepInEx;
using NUnit.Framework;

namespace Tolmach.Tests
{
    [SetUpFixture]
    public sealed class BepInExEnvironment
    {
        private string environmentRoot;

        [OneTimeSetUp]
        public void InitializeBepInExPaths()
        {
            environmentRoot = Path.Combine(Path.GetTempPath(), "Tolmach.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(environmentRoot);

            // NUnit does not run the game preloader. Use BepInEx's own initializer before
            // ConfigFile creates its static CoreConfig, and keep its writes away from the game.
            MethodInfo initializePaths = typeof(Paths).GetMethod("SetExecutablePath", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(string), typeof(string), typeof(string[]) }, null);
            Assert.That(initializePaths, Is.Not.Null, "BepInEx 5 Paths.SetExecutablePath initializer was not found.");
            initializePaths.Invoke(null, new object[]
            {
                Path.Combine(environmentRoot, "Tolmach.Tests.exe"),
                Path.Combine(environmentRoot, "BepInEx"),
                TestContext.CurrentContext.TestDirectory,
                new string[0]
            });
        }

        [OneTimeTearDown]
        public void DeleteBepInExEnvironment()
        {
            if (environmentRoot != null && Directory.Exists(environmentRoot))
                Directory.Delete(environmentRoot, true);
        }
    }
}
