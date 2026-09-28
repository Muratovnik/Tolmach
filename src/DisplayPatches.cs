using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Tolmach
{
    internal static class DisplayPatches
    {
        private static readonly Dictionary<string, Module> Owners = new Dictionary<string, Module>();
        private static readonly MethodInfo Display = typeof(TextEngine).GetMethod("Display");
        private static readonly MethodInfo DisplayArray = typeof(TextEngine).GetMethod("DisplayArray");
        private static readonly MethodInfo Literal = typeof(TextEngine).GetMethod("Literal");
        private static readonly HashSet<string> GuiMethods = new HashSet<string>(new string[] {
            "Label", "Button", "RepeatButton", "Toggle", "Box", "Window", "ModalWindow", "Toolbar", "SelectionGrid"
        });
        internal static void Install(Harmony harmony, Module module)
        {
            // Native words and persistent map labels have their own consumers. An empty
            // display table is not a reason to inspect or detour a plugin's methods.
            if (!module.NeedsIlAdapters) return;
            if (!module.UiAllowed)
            { module.Warn("Version differs from snapshot; scoped IL/return adapters skipped. Native key adapters remain enabled."); return; }
            // A type, signature or body referring to an absent optional dependency throws on
            // reflection. Skip only that type or method; the rest of the module stays patched.
            foreach (Type type in AccessTools.GetTypesFromAssembly(module.RuntimeAssembly))
            {
                List<MethodInfo> methods;
                try
                {
                    if (!module.OwnType(type) || type.ContainsGenericParameters) continue;
                    methods = AccessTools.GetDeclaredMethods(type);
                }
                catch (Exception e) { module.Warn("Type skipped " + type.Name + ": " + e.GetType().Name); continue; }
                foreach (MethodInfo method in methods)
                {
                    try { InstallMethod(harmony, module, type, method); }
                    catch (Exception e) { module.Warn("UI adapter skipped " + type.FullName + "." + method.Name + ": " + e.GetType().Name); }
                }
            }
            foreach (MethodSpec expected in module.returns)
                if (!Declares(module, expected.type, expected.method))
                    module.Warn("Expected return adapter not found: " + expected.type + "." + expected.method);
            foreach (LiteralSpec expected in module.literals)
                if (!Declares(module, expected.type, expected.method))
                    module.Warn("Expected literal adapter not found: " + expected.type + "." + expected.method);
        }
        private static void InstallMethod(Harmony harmony, Module module, Type type, MethodInfo method)
        {
            if (!RuntimeAccess.ManagedBody(method)) return;
            module.ScannedMethods++;
            bool literal = module.literals.Any(delegate(LiteralSpec p) { return p.type == type.FullName && p.method == method.Name; });
            bool result = method.ReturnType == typeof(string) &&
                module.returns.Any(delegate(MethodSpec p) { return p.type == type.FullName && p.method == method.Name; });
            bool sink = false;
            try { sink = module.HasDisplayText && PatchProcessor.GetOriginalInstructions(method, null).Any(delegate(CodeInstruction instruction) { return Relevant(instruction, module); }); }
            catch (Exception e) { module.Warn("IL inspection skipped " + type.Name + "." + method.Name + ": " + e.GetType().Name); }
            if (!literal && !result && !sink) return;
            string key = RuntimeAccess.MethodKey(method);
            Owners[key] = module;
            try
            {
                HarmonyMethod tr = literal || sink ? new HarmonyMethod(typeof(DisplayPatches).GetMethod("Transpile", AccessTools.all)) : null;
                HarmonyMethod post = result ? new HarmonyMethod(typeof(DisplayPatches).GetMethod("TranslateReturn", AccessTools.all)) : null;
                if (tr != null) tr.priority = Priority.Last;
                if (post != null) post.priority = Priority.Last;
                harmony.Patch(method, postfix: post, transpiler: tr);
                module.PatchedMethods++;
                PatchEvidence evidence;
                if (!module.Patches.TryGetValue(key, out evidence))
                {
                    evidence = new PatchEvidence { method = type.FullName + "." + method.Name };
                    module.Patches[key] = evidence;
                }
                evidence.returnAdapter = result;
                if (literal)
                    foreach (LiteralSpec spec in module.literals.Where(delegate(LiteralSpec p) { return p.type == type.FullName && p.method == method.Name; }))
                        foreach (string value in spec.values.Keys)
                            if (!evidence.matchedLiterals.Contains(value))
                                module.Warn("Literal not replaced: " + type.FullName + "." + method.Name + " :: " + value);
                if (tr != null && evidence.displayCalls + evidence.displayFields + evidence.literals == 0)
                    module.Warn("Transpiler installed but no display site changed: " + type.FullName + "." + method.Name);
            }
            catch (Exception e)
            {
                // Keep the mapping: a later Harmony rebuild may still refer to this method.
                module.Warn("UI patch skipped " + type.FullName + "." + method.Name + ": " + e.GetType().Name);
            }
        }
        private static bool Declares(Module module, string typeName, string methodName)
        {
            try
            {
                Type t = module.RuntimeAssembly.GetType(typeName, false);
                return t != null && t.GetMethods(AccessTools.allDeclared).Any(delegate(MethodInfo m) { return m.Name == methodName; });
            }
            catch (Exception) { return false; }
        }
        internal static void Reset() { Owners.Clear(); }
        private static bool Relevant(CodeInstruction instruction, Module module)
        {
            MethodBase call = instruction.operand as MethodBase;
            if (call != null && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Newobj))
                return StringSink(call).Any(delegate(bool b) { return b; });
            FieldInfo field = instruction.operand as FieldInfo;
            return field != null && instruction.opcode == OpCodes.Stfld && DisplayField(field, module);
        }
        private static bool DisplayField(FieldInfo f, Module m)
        {
            if (f.FieldType != typeof(string) || f.DeclaringType == null) return false;
            string type = f.DeclaringType.FullName;
            return (m.id == "ExpertExplorer" && type == "MessageHud+UnlockMsg" && (f.Name == "m_topic" || f.Name == "m_description")) ||
                   (m.id == "SpeedyPaths" && type == "StatusEffect" && f.Name == "m_name");
        }
        private static bool[] StringSink(MethodBase method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            bool[] selected = new bool[parameters.Length];
            if (method.DeclaringType == null || method.ContainsGenericParameters) return selected;
            if (parameters.Any(delegate(ParameterInfo p) { return p.ParameterType.IsByRef || p.ParameterType.IsPointer; })) return selected;
            string type = method.DeclaringType.FullName;
            string name = method.Name;
            bool recognized = false;
            if ((type == "UnityEngine.GUI" || type == "UnityEngine.GUILayout") && GuiMethods.Contains(name)) recognized = true;
            if (type == "UnityEngine.GUIContent" && (method.IsConstructor || name == "set_text" || name == "set_tooltip")) recognized = true;
            if ((type == "UnityEngine.UI.Text" || type == "TMPro.TMP_Text" || type == "TMPro.TextMeshProUGUI" || type == "TMPro.TextMeshPro") &&
                (name == "set_text" || name == "SetText")) recognized = true;
            if ((type == "Character" || type == "Player" || type == "MessageHud" || type == "Hud") && (name == "Message" || name == "ShowMessage")) recognized = true;
            // Chat log lines are declared on Terminal and in these mods carry console command
            // output, which this pack deliberately leaves untranslated.
            if (type == "Chat" && (name == "SetNpcText" || name == "AddInworldText")) recognized = true;
            // AddPin/DiscoverLocation create persistent data, not display text. Never translate their arguments.
            if ((type == "Localization" && name == "Localize") || (type == "Jotunn.Entities.CustomLocalization" && name == "TryTranslate")) recognized = true;
            if (!recognized) return selected;
            for (int i = 0; i < parameters.Length; i++)
            {
                Type p = parameters[i].ParameterType;
                selected[i] = p == typeof(string) || p == typeof(string[]);
                // Only the source template/key is translatable; format arguments may be
                // player names or technical IDs, not UI choices like Toolbar's labels.
                if ((type == "Localization" && name == "Localize") ||
                    (type == "Jotunn.Entities.CustomLocalization" && name == "TryTranslate"))
                    selected[i] = i == 0 && p == typeof(string);
            }
            return selected;
        }
        private static bool LocalizedReturn(MethodBase method)
        {
            MethodInfo m = method as MethodInfo;
            if (m == null || m.ReturnType != typeof(string) || m.DeclaringType == null) return false;
            return (m.DeclaringType.FullName == "Localization" && m.Name == "Localize") ||
                   (m.DeclaringType.FullName == "Jotunn.Entities.CustomLocalization" && m.Name == "TryTranslate");
        }
        private static void TranslateReturn(MethodBase __originalMethod, ref string __result)
        {
            Module module;
            if (Owners.TryGetValue(RuntimeAccess.MethodKey(__originalMethod), out module)) __result = TextEngine.Display(__result, module.id);
        }
        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
        {
            List<CodeInstruction> input = instructions.ToList();
            Module module;
            if (!Owners.TryGetValue(RuntimeAccess.MethodKey(__originalMethod), out module)) return input;
            List<CodeInstruction> output = new List<CodeInstruction>();
            PatchEvidence evidence;
            string methodKey = RuntimeAccess.MethodKey(__originalMethod);
            if (!module.Patches.TryGetValue(methodKey, out evidence))
            {
                evidence = new PatchEvidence { method = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name };
                module.Patches[methodKey] = evidence;
            }
            // Harmony may regenerate this transpiler more than once. Record the last plan, not inflated totals.
            evidence.displayCalls = evidence.displayFields = evidence.literals = evidence.skippedBoundaries = 0;
            evidence.matchedLiterals.Clear();
            for (int index = 0; index < input.Count; index++)
            {
                CodeInstruction current = input[index];
                // Leave EH boundary/prefixed instructions intact rather than producing invalid IL.
                if (current.blocks.Count != 0 || (index > 0 && input[index - 1].opcode.OpCodeType == OpCodeType.Prefix))
                { evidence.skippedBoundaries++; output.Add(current); continue; }
                if (current.opcode == OpCodes.Ldstr)
                {
                    string value = current.operand as string;
                    bool found = module.literals.Any(delegate(LiteralSpec p) {
                        return p.type == __originalMethod.DeclaringType.FullName && p.method == __originalMethod.Name && value != null && p.values.ContainsKey(value);
                    });
                    if (found)
                    {
                        evidence.literals++;
                        evidence.matchedLiterals.Add(value);
                        output.Add(current);
                        output.Add(new CodeInstruction(OpCodes.Ldstr, module.id));
                        output.Add(new CodeInstruction(OpCodes.Ldstr, __originalMethod.DeclaringType.FullName));
                        output.Add(new CodeInstruction(OpCodes.Ldstr, __originalMethod.Name));
                        output.Add(new CodeInstruction(OpCodes.Call, Literal));
                        continue;
                    }
                }
                FieldInfo field = current.operand as FieldInfo;
                if (module.HasDisplayText && current.opcode == OpCodes.Stfld && field != null && DisplayField(field, module))
                {
                    evidence.displayFields++;
                    CodeInstruction first = new CodeInstruction(OpCodes.Ldstr, module.id);
                    first.MoveLabelsFrom(current);
                    output.Add(first); output.Add(new CodeInstruction(OpCodes.Call, Display)); output.Add(current);
                    continue;
                }
                MethodBase call = current.operand as MethodBase;
                bool isCall = current.opcode == OpCodes.Call || current.opcode == OpCodes.Callvirt || current.opcode == OpCodes.Newobj;
                if (!module.HasDisplayText || call == null || !isCall) { output.Add(current); continue; }
                bool[] selected = StringSink(call);
                if (!selected.Any(delegate(bool b) { return b; })) { output.Add(current); continue; }
                ParameterInfo[] parameters = call.GetParameters();
                LocalBuilder[] locals = parameters.Select(delegate(ParameterInfo p) { return generator.DeclareLocal(p.ParameterType); }).ToArray();
                List<CodeInstruction> replacement = new List<CodeInstruction>();
                // Spill only call arguments. The instance receiver, if present, stays on the stack.
                for (int i = locals.Length - 1; i >= 0; i--) replacement.Add(new CodeInstruction(OpCodes.Stloc, locals[i]));
                for (int i = 0; i < locals.Length; i++)
                {
                    replacement.Add(new CodeInstruction(OpCodes.Ldloc, locals[i]));
                    if (!selected[i]) continue;
                    replacement.Add(new CodeInstruction(OpCodes.Ldstr, module.id));
                    replacement.Add(new CodeInstruction(OpCodes.Call, parameters[i].ParameterType == typeof(string[]) ? DisplayArray : Display));
                }
                replacement[0].MoveLabelsFrom(current);
                replacement.Add(current);
                if (LocalizedReturn(call))
                {
                    replacement.Add(new CodeInstruction(OpCodes.Ldstr, module.id));
                    replacement.Add(new CodeInstruction(OpCodes.Call, Display));
                }
                evidence.displayCalls++;
                output.AddRange(replacement);
            }
            return output;
        }
    }
}
