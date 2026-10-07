using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Tolmach
{
    public static class ContainerNameDisplay
    {
        private static readonly List<Module> Owners = new List<Module>();
        private static FieldInfo nameField;
        internal static void Reset() { Owners.Clear(); nameField = null; }

        internal static void Install(Harmony harmony, Type containerType)
        {
            Reset();
            Owners.AddRange(TextEngine.Modules.Values.Where(m => m.UiAllowed && m.id.StartsWith("DynamicStorage", StringComparison.Ordinal) && m.rawTexts.Count != 0)
                .OrderBy(m => m.id, StringComparer.Ordinal));
            if (Owners.Count == 0) return;
            nameField = containerType == null ? null : AccessTools.Field(containerType, "m_name");
            if (nameField == null || nameField.IsStatic || nameField.FieldType != typeof(string))
            { Warn("Container name field unavailable; original names retained."); return; }
            nameField = AccessTools.DeclaredField(nameField.DeclaringType, nameField.Name);
            foreach (string name in new[] { "GetHoverName", "GetHoverText" })
            {
                try
                {
                    MethodInfo method = AccessTools.Method(containerType, name, Type.EmptyTypes);
                    if (method == null || method.IsStatic || method.ReturnType != typeof(string) || !RuntimeAccess.ManagedBody(method))
                    { Warn("Container name display target unavailable: " + name); continue; }
                    string unsupported = DisplayPatches.UnsupportedHandler(method);
                    if (unsupported != null) { Record(method, 0, unsupported); continue; }
                    harmony.Patch(method, transpiler: new HarmonyMethod(AccessTools.Method(typeof(ContainerNameDisplay), "Transpile")));
                }
                catch (Exception e) { Warn("Container name display target " + name + ": " + e.GetType().Name); }
            }
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>(instructions);
            int count = 0;
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i];
                yield return instruction;
                if (i == 0 || instruction.opcode != OpCodes.Ldfld || !Equals(instruction.operand, nameField) ||
                    code[i - 1].opcode != OpCodes.Ldarg_0 || instruction.labels.Count != 0 ||
                    instruction.blocks.Count != 0 || code[i - 1].blocks.Count != 0) continue;
                // The empty/access suffix is composed afterward. Translating the whole
                // hover line cannot match an exact catalog name once that suffix exists.
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ContainerNameDisplay), "Display"));
                count++;
            }
            Record(__originalMethod, count, count == 0 ? "no safe container name read" : null);
        }

        private static void Record(MethodBase method, int count, string reason)
        {
            string key = method.DeclaringType.FullName + "." + method.Name + " [container name]";
            foreach (Module owner in Owners)
            {
                PatchEvidence previous;
                if (!owner.Patches.TryGetValue(key, out previous)) owner.ScannedMethods++;
                if (previous != null && previous.displayFields > 0) owner.PatchedMethods--;
                if (count > 0) owner.PatchedMethods++;
                owner.Patches[key] = new PatchEvidence { method = key, displayFields = count, notPatched = reason };
            }
        }

        private static void Warn(string message) { foreach (Module owner in Owners) owner.Warn(message); }
        public static string Display(string value)
        {
            if (!TextEngine.IsRussian || value == null) return value;
            foreach (Module owner in Owners)
            {
                Module current;
                string translated;
                if (owner.UiAllowed && TextEngine.Modules.TryGetValue(owner.id, out current) && ReferenceEquals(owner, current) &&
                    owner.rawTexts.TryGetValue(value, out translated)) return translated;
            }
            return value;
        }
    }
}
