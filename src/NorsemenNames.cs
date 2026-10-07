using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Tolmach
{
    public static class NorsemenNames
    {
        private static Module owner;
        private static Type vikingType;
        private static MethodInfo isTamed;
        private static MethodInfo getName;
        private static MethodInfo getText;
        private static Dictionary<string, string> names;

        internal static void Reset()
        {
            owner = null; vikingType = null; isTamed = getName = getText = null; names = null;
        }

        internal static void Install(Harmony harmony)
        {
            Reset();
            Module module;
            if (!TextEngine.Modules.TryGetValue("Norsemen", out module) || !module.UiAllowed ||
                module.RuntimeAssembly == null || !module.terms.TryGetValue("npcNames", out names)) return;
            owner = module;
            vikingType = module.RuntimeAssembly.GetType("Norsemen.Viking");
            isTamed = vikingType == null ? null : AccessTools.Method(vikingType, "IsTamed", Type.EmptyTypes);
            getName = StringMethod(vikingType, "GetName");
            getText = StringMethod(vikingType, "GetText");
            if (isTamed == null || isTamed.IsStatic || isTamed.ReturnType != typeof(bool) || getName == null || getText == null)
            {
                module.Warn("Norsemen name API unavailable; original names retained.");
                return;
            }
            Patch(harmony, vikingType, "GetHoverName");
            Patch(harmony, vikingType, "GetHoverText");
            Patch(harmony, module.RuntimeAssembly.GetType("Norsemen.VikingGui"), "Show");
            Patch(harmony, module.RuntimeAssembly.GetType("Norsemen.VikingGui+InventoryGUI_UpdateContainer"), "Prefix");
        }

        private static MethodInfo StringMethod(Type type, string name)
        {
            MethodInfo method = type == null ? null : AccessTools.Method(type, name, Type.EmptyTypes);
            return method != null && !method.IsStatic && method.ReturnType == typeof(string) ? method : null;
        }

        private static void Patch(Harmony harmony, Type type, string name)
        {
            try
            {
                MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, name);
                if (method == null || !RuntimeAccess.ManagedBody(method))
                { owner.Warn("Norsemen name display target unavailable: " + name); return; }
                string unsupported = DisplayPatches.UnsupportedHandler(method);
                if (unsupported != null)
                { Record(method, 0, unsupported); return; }
                harmony.Patch(method, transpiler: new HarmonyMethod(AccessTools.Method(typeof(NorsemenNames), "Transpile")));
            }
            catch (Exception e) { owner.Warn("Norsemen name display target " + name + ": " + e.GetType().Name); }
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>(instructions);
            int count = 0;
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i];
                bool nameCall = (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    (Equals(instruction.operand, getName) || Equals(instruction.operand, getText));
                bool safe = nameCall && instruction.labels.Count == 0 && instruction.blocks.Count == 0 &&
                    i > 0 && code[i - 1].blocks.Count == 0 && code[i - 1].opcode.OpCodeType != OpCodeType.Prefix;
                // Keep the original name read and its exceptions. The duplicate receiver
                // proves ownership without changing GetText/GetName or stored ZDO data.
                if (safe) yield return new CodeInstruction(OpCodes.Dup);
                yield return instruction;
                if (!safe) continue;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NorsemenNames), "Display"));
                count++;
            }
            Record(__originalMethod, count, count == 0 ? "no safe Norsemen name call" : null);
        }

        private static void Record(MethodBase method, int count, string reason)
        {
            string key = method.DeclaringType.FullName + "." + method.Name + " [Norsemen name]";
            PatchEvidence previous;
            if (!owner.Patches.TryGetValue(key, out previous)) owner.ScannedMethods++;
            if (previous != null && previous.displayCalls > 0) owner.PatchedMethods--;
            if (count > 0) owner.PatchedMethods++;
            owner.Patches[key] = new PatchEvidence { method = key, displayCalls = count, notPatched = reason };
        }

        public static string Display(object viking, string value)
        {
            if (!TextEngine.IsRussian || owner == null || !owner.UiAllowed || names == null || value == null ||
                vikingType == null || !vikingType.IsInstanceOfType(viking)) return value;
            Module current;
            if (!TextEngine.Modules.TryGetValue("Norsemen", out current) || !ReferenceEquals(current, owner)) return value;
            string translated;
            if (!names.TryGetValue(value, out translated)) return value;
            try
            {
                // Generated names and player renames share s_tamedName. A tamed name
                // has no durable provenance; only untamed Vikings cannot be renamed.
                return (bool)isTamed.Invoke(viking, null) ? value : translated;
            }
            catch (Exception e)
            {
                owner.Warn("Norsemen name state unavailable: " + e.GetType().Name + "; original name retained.");
                return value;
            }
        }
    }
}
