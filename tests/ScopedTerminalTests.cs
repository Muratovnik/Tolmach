using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NUnit.Framework;
using Tolmach;
using Module = Tolmach.Module;

namespace Tolmach.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class ScopedTerminalTests : TestContextBase
    {
        private const string Source = "nearest 42 -> 43, 2.5 m: exit capture pending: settling loaded scene (3 s)";
        private const string Russian = "ближайший 42 -> 43, 2.5 м: захват выхода ожидает: стабилизация загруженной сцены (3 с)";
        private Type callbackType;
        private Module module;
        private object callback;

        [SetUp]
        public void CreateDiagnosticCaller()
        {
            string name = "PortalConsumer." + Guid.NewGuid().ToString("N");
            string directory = Path.Combine(Path.GetTempPath(), "Tolmach." + name);
            const string fileName = "PortalConsumer.dll";
            Directory.CreateDirectory(directory);
            Assembly runtimeAssembly;
            try
            {
                // Emit the verified compiler-generated identity; C# cannot declare <>c
                // or <Initialize>b__49_0 directly. The sink is the real fixture Terminal.
                AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.RunAndSave, directory);
                ModuleBuilder builder = assembly.DefineDynamicModule("PortalConsumer", fileName);
                TypeBuilder outer = builder.DefineType("PortalPreview.FrozenPortals", TypeAttributes.Public);
                TypeBuilder nested = outer.DefineNestedType("<>c", TypeAttributes.NestedPublic);
                nested.DefineDefaultConstructor(MethodAttributes.Public);
                FieldBuilder state = nested.DefineField("_status", typeof(string), FieldAttributes.Public | FieldAttributes.Static);
                FieldBuilder command = nested.DefineField("CommandId", typeof(string), FieldAttributes.Public | FieldAttributes.Static);
                Emit(nested, state, command, "<Initialize>b__49_0");
                Emit(nested, state, command, "OtherCaller");
                nested.CreateType();
                outer.CreateType();
                // Mono 6.8 omits nested types from in-memory AssemblyBuilder.GetTypes().
                // Reload metadata as a plugin DLL so the normal ownership scan sees <>c.
                assembly.Save(fileName);
                runtimeAssembly = Assembly.Load(File.ReadAllBytes(Path.Combine(directory, fileName)));
            }
            finally { Directory.Delete(directory, true); }
            callbackType = runtimeAssembly.GetType("PortalPreview.FrozenPortals+<>c", true);
            callback = Activator.CreateInstance(callbackType);
            module = CatalogLoader.Read(Catalog("PortalPreview"));
            module.RuntimeAssembly = runtimeAssembly; module.UiAllowed = module.ExactVersion = true;
            Activate(module);
        }

        private static void Emit(TypeBuilder type, FieldBuilder state, FieldBuilder command, string name)
        {
            MethodBuilder method = type.DefineMethod(name, MethodAttributes.Public, typeof(void), new[] { typeof(Terminal), typeof(string) });
            method.SetImplementationFlags(MethodImplAttributes.IL | MethodImplAttributes.NoInlining);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Stsfld, state);
            il.Emit(OpCodes.Ldstr, "off"); il.Emit(OpCodes.Stsfld, command);
            il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldsfld, state);
            il.Emit(OpCodes.Callvirt, typeof(Terminal).GetMethod("AddString", new[] { typeof(string) }));
            il.Emit(OpCodes.Ret);
        }

        private string Run(string method, string value = Source)
        {
            var terminal = new Chat();
            callbackType.GetMethod(method).Invoke(callback, new object[] { terminal, value });
            return terminal.Lines[0];
        }

        [Test]
        public void OnlyTheVerifiedDiagnosticCallerTranslatesAtTheTerminalBoundary()
        {
            Assert.That(Run("<Initialize>b__49_0"), Is.EqualTo(Source), "Unpatched completed output reproduces the missing consumer.");
            DisplayPatches.Install(Patcher, module);
            Assert.That(Run("<Initialize>b__49_0"), Is.EqualTo(Russian));
            Assert.That(callbackType.GetField("_status").GetValue(null), Is.EqualTo(Source));
            Assert.That(callbackType.GetField("CommandId").GetValue(null), Is.EqualTo("off"), "A translated vocabulary value must not alter the command identifier.");
            Assert.That(Run("OtherCaller"), Is.EqualTo(Source));
            var external = new Chat(); external.AddString(Source);
            Assert.That(external.Lines[0], Is.EqualTo(Source));
            Assert.That(DisplayPatches.StringSink(typeof(Terminal).GetMethod("AddString", new[] { typeof(string) })), Is.EqualTo(new[] { false }));
            Assert.That(module.Patches[RuntimeAccess.MethodKey(callbackType.GetMethod("<Initialize>b__49_0"))].displayCalls, Is.EqualTo(1));
        }

        [Test]
        public void LanguagePermissionAndReplacingTheModuleRetainOriginalOutput()
        {
            DisplayPatches.Install(Patcher, module);
            TextEngine.IsRussian = false;
            Assert.That(Run("<Initialize>b__49_0"), Is.EqualTo(Source));
            TextEngine.IsRussian = true;
            module.UiAllowed = false;
            Assert.That(Run("<Initialize>b__49_0"), Is.EqualTo(Source));
            module.UiAllowed = true;
            Assert.That(Run("<Initialize>b__49_0"), Is.EqualTo(Russian));
            TextEngine.Modules["PortalPreview"] = FixtureModule("PortalPreview", "PortalPreview");
            Assert.That(Run("<Initialize>b__49_0"), Is.EqualTo(Source));
        }

        [Test]
        public void IdenticalCallerIdentityInAnotherModuleDoesNotEnableTerminalTranslation()
        {
            TextEngine.Modules.Clear(); module.id = "OtherMod"; Activate(module);
            DisplayPatches.Install(Patcher, module);
            Assert.That(Run("<Initialize>b__49_0"), Is.EqualTo(Source));
            Assert.That(module.PatchedMethods, Is.Zero);
        }
    }
}
