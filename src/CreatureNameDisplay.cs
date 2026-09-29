using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Tolmach
{
    // Public because Harmony-generated methods in the game assembly call Display.
    // Never writes Character.m_name, a tame name, a ZDO, or a localization dictionary.
    public static class CreatureNameDisplay
    {
        internal const string ModuleId = "HumanoidRandomizer";
        private static Module owner;
        private static TextTable table;
        private static Type markerType;
        private static FieldInfo nameField;
        private static MethodInfo getComponent;
        private static MethodInfo isPlayer;

        internal static void Reset()
        {
            owner = null;
            table = null;
            markerType = null;
            nameField = null;
            getComponent = null;
            isPlayer = null;
        }

        internal static void Install(Harmony harmony, Type characterType)
        {
            Reset();
            Module module;
            if (!TextEngine.Modules.TryGetValue(ModuleId, out module) || !module.UiAllowed || module.rawPatterns.Count == 0) return;
            owner = module;
            markerType = module.RuntimeAssembly == null ? null : module.RuntimeAssembly.GetType("BalrondHumanoidRandomizer.HumanoidExtend");
            nameField = characterType == null ? null : AccessTools.Field(characterType, "m_name");
            getComponent = characterType == null ? null : AccessTools.Method(characterType, "GetComponent", new[] { typeof(Type) });
            isPlayer = characterType == null ? null : AccessTools.Method(characterType, "IsPlayer", Type.EmptyTypes);
            if (markerType == null || nameField == null || nameField.IsStatic || nameField.FieldType != typeof(string) ||
                getComponent == null || getComponent.IsStatic || getComponent.ReturnType == typeof(void) ||
                isPlayer == null || isPlayer.IsStatic || isPlayer.ReturnType != typeof(bool))
            {
                module.Warn("Creature-name display API unavailable; variant names are left unchanged. No global name-template fallback is used.");
                return;
            }
            table = new TextTable(module.rawTexts, module.rawPatterns, new Dictionary<string, string>(), module.terms);
            foreach (string methodName in new[] { "GetHoverName", "GetHoverText" })
            {
                MethodInfo method = AccessTools.Method(characterType, methodName, Type.EmptyTypes);
                if (method == null || method.IsStatic || method.ReturnType != typeof(string) || method.GetMethodBody() == null)
                {
                    module.Warn("Creature-name display target unavailable: " + methodName);
                    continue;
                }
                try
                {
                    harmony.Patch(method, transpiler: new HarmonyMethod(AccessTools.Method(typeof(CreatureNameDisplay), "Transpile")));
                }
                catch (Exception e) { module.Warn("Creature-name display target " + methodName + ": " + e.GetType().Name); }
            }
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            int fields = 0;
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i];
                yield return instruction;
                // Restrict the read to this.m_name, before any Localization.Localize call.
                // A branch into ldfld or an exception boundary is not a proven safe insertion.
                if (i == 0 || code[i - 1].opcode != OpCodes.Ldarg_0 || instruction.opcode != OpCodes.Ldfld ||
                    !Equals(instruction.operand, nameField) || instruction.labels.Count != 0 ||
                    instruction.blocks.Count != 0 || code[i - 1].blocks.Count != 0) continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CreatureNameDisplay), "Display"));
                fields++;
            }
            if (owner == null) yield break;
            string key = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name + " [creature name]";
            PatchEvidence previous;
            if (!owner.Patches.TryGetValue(key, out previous)) owner.ScannedMethods++;
            if (previous != null && previous.displayFields > 0) owner.PatchedMethods--;
            if (fields > 0) owner.PatchedMethods++;
            owner.Patches[key] = new PatchEvidence {
                method = key, displayFields = fields,
                notPatched = fields == 0 ? "no safe this.m_name read; no global fallback" : null
            };
            if (fields == 0) owner.Warn("No safe creature-name field read in " + __originalMethod.Name + "; that method was left unchanged.");
        }

        public static string Display(string value, object character)
        {
            if (!TextEngine.IsRussian || value == null || character == null || owner == null || table == null || !owner.UiAllowed) return value;
            Module current;
            if (!TextEngine.Modules.TryGetValue(ModuleId, out current) || !ReferenceEquals(owner, current)) return value;
            try
            {
                if ((bool)isPlayer.Invoke(character, null)) return value;
                object marker = getComponent.Invoke(character, new object[] { markerType });
                if (marker == null || !markerType.IsInstanceOfType(marker)) return value;
                return table.TranslateWhole(value);
            }
            catch (Exception e)
            {
                owner.Warn("Creature-name ownership check failed: " + e.GetType().Name + "; original text retained.");
                return value;
            }
        }
    }
}
