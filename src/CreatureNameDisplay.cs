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
        private static CreatureNameOwner provenance;
        private static FieldInfo nameField;
        private static FieldInfo tameableCharacter;

        internal static void Reset()
        {
            owner = null;
            table = null;
            provenance = null;
            nameField = null;
            tameableCharacter = null;
        }

        internal static void Install(Harmony harmony, Type characterType, Type tameableType = null, Type sceneType = null)
        {
            Reset();
            Module module;
            if (!TextEngine.Modules.TryGetValue(ModuleId, out module) || !module.UiAllowed || module.rawPatterns.Count == 0) return;
            owner = module;
            try
            {
                nameField = characterType == null ? null : AccessTools.Field(characterType, "m_name");
                // Normalize inherited fields to the declaring type used by IL operands.
                if (nameField != null) nameField = AccessTools.DeclaredField(nameField.DeclaringType, nameField.Name);
                provenance = new CreatureNameOwner(module, characterType);
                if (nameField == null || nameField.IsStatic || nameField.FieldType != typeof(string) || !provenance.Available)
                {
                    module.Warn("Creature-name display API unavailable; variant names are left unchanged. No global name-template fallback is used.");
                    return;
                }
                provenance.BindClones(module, characterType, sceneType);
                table = new TextTable(module.rawTexts, module.rawPatterns, new Dictionary<string, string>(), module.terms);
            }
            catch (Exception e)
            {
                module.Warn("Creature-name display initialization: " + e.GetType().Name + "; original names retained, other adapters remain available.");
                Reset();
                return;
            }
            Patch(harmony, characterType, "GetHoverName");
            tameableCharacter = tameableType == null ? null : AccessTools.Field(tameableType, "m_character");
            if (tameableCharacter == null || tameableCharacter.IsStatic || !nameField.DeclaringType.IsAssignableFrom(tameableCharacter.FieldType))
            {
                module.Warn("Creature-name Tameable API unavailable; tameable default names are left unchanged.");
                return;
            }
            Patch(harmony, tameableType, "GetName");
        }

        private static void Patch(Harmony harmony, Type type, string methodName)
        {
            try
            {
                MethodInfo method = AccessTools.Method(type, methodName, Type.EmptyTypes);
                if (method == null || method.IsStatic || method.ReturnType != typeof(string) || !RuntimeAccess.ManagedBody(method))
                {
                    owner.Warn("Creature-name display target unavailable: " + type.FullName + "." + methodName);
                    return;
                }
                // Deferred Harmony patching can otherwise make a filter/fault failure
                // escape the local catch and interrupt game startup.
                string unsupported = DisplayPatches.UnsupportedHandler(method);
                if (unsupported != null)
                {
                    Record(method, 0, unsupported + "; no global fallback");
                    owner.Warn("Creature-name display target skipped " + methodName + ": " + unsupported);
                    return;
                }
                harmony.Patch(method, transpiler: new HarmonyMethod(AccessTools.Method(typeof(CreatureNameDisplay), "Transpile")));
            }
            catch (Exception e) { owner.Warn("Creature-name display target " + methodName + ": " + e.GetType().Name); }
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            int fields = 0;
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i];
                yield return instruction;
                // Restrict the read to this.m_name or this.m_character.m_name,
                // before Localization.Localize. Custom names never use these reads.
                // A branch into ldfld or an exception boundary is not a proven safe insertion.
                if (i == 0 || instruction.opcode != OpCodes.Ldfld ||
                    !Equals(instruction.operand, nameField) || instruction.labels.Count != 0 ||
                    instruction.blocks.Count != 0 || code[i - 1].blocks.Count != 0) continue;
                bool direct = code[i - 1].opcode == OpCodes.Ldarg_0;
                bool tameable = i >= 2 && code[i - 2].opcode == OpCodes.Ldarg_0 && code[i - 1].opcode == OpCodes.Ldfld &&
                    Equals(code[i - 1].operand, tameableCharacter) && code[i - 1].labels.Count == 0 && code[i - 2].blocks.Count == 0;
                if (!direct && !tameable) continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                if (tameable) yield return new CodeInstruction(OpCodes.Ldfld, tameableCharacter);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CreatureNameDisplay), "Display"));
                fields++;
            }
            Record(__originalMethod, fields, fields == 0 ? "no safe creature-name read; no global fallback" : null);
            if (owner != null && fields == 0) owner.Warn("No safe creature-name field read in " + __originalMethod.Name + "; that method was left unchanged.");
        }

        private static void Record(MethodBase method, int fields, string reason)
        {
            if (owner == null) return;
            string key = method.DeclaringType.FullName + "." + method.Name + " [creature name]";
            PatchEvidence previous;
            if (!owner.Patches.TryGetValue(key, out previous)) owner.ScannedMethods++;
            if (previous != null && previous.displayFields > 0) owner.PatchedMethods--;
            if (fields > 0) owner.PatchedMethods++;
            owner.Patches[key] = new PatchEvidence { method = key, displayFields = fields, notPatched = reason };
        }

        public static string Display(string value, object character)
        {
            if (!TextEngine.IsRussian || value == null || character == null || owner == null || table == null || !owner.UiAllowed) return value;
            Module current;
            if (!TextEngine.Modules.TryGetValue(ModuleId, out current) || !ReferenceEquals(owner, current)) return value;
            try
            {
                string translated = table.TranslateWhole(value);
                return translated != value && provenance.Owns(character) ? translated : value;
            }
            catch (Exception e)
            {
                owner.Warn("Creature-name ownership check failed: " + e.GetType().Name + "; original text retained.");
                return value;
            }
        }
    }
}
