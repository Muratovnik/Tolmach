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
        private static readonly MethodInfo ConfigText = typeof(TextEngine).GetMethod("ConfigText");
        private static readonly HashSet<string> GuiMethods = new HashSet<string>(new string[] {
            "Label", "Button", "RepeatButton", "Toggle", "Box", "Window", "ModalWindow", "Toolbar", "SelectionGrid"
        });
        internal static void Install(Harmony harmony, Module module)
        {
            // Native words and persistent map labels have their own consumers. An empty
            // display table is not a reason to inspect or detour a plugin's methods.
            if (!module.NeedsIlAdapters) return;
            if (!module.UiAllowed)
            {
                if (module.codeAssembly.Length == 0 || module.RuntimeCodeAssembly != null)
                    module.Warn("Version differs from snapshot and OnlyAuditedVersions is on; scoped IL/return adapters skipped. Native key adapters remain enabled.");
                return;
            }
            Assembly codeAssembly = module.RuntimeCodeAssembly ?? module.RuntimeAssembly;
            if (codeAssembly == null || (module.codeAssembly.Length != 0 && module.RuntimeCodeAssembly == null))
            { module.Warn("Scoped code assembly is unavailable; IL/return adapters skipped."); return; }
            if (!module.ExactVersion)
                module.Warn("Version differs from snapshot; adapters apply where the catalog strings are found. Strings not found are listed below and stay in English.");
            // Literal values found in the original IL of their methods, over all overloads of the name.
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            // A type, signature or body referring to an absent optional dependency throws on
            // reflection. Skip only that type or method; the rest of the module stays patched.
            foreach (Type type in AccessTools.GetTypesFromAssembly(codeAssembly))
            {
                List<MethodBase> methods;
                try
                {
                    if (!module.OwnType(type) || type.ContainsGenericParameters) continue;
                    methods = AccessTools.GetDeclaredMethods(type).Cast<MethodBase>().ToList();
                    // Instance constructors carry field initializers (component defaults) too.
                    methods.AddRange(AccessTools.GetDeclaredConstructors(type, false).Cast<MethodBase>());
                }
                catch (Exception e) { module.Warn("Type skipped " + type.Name + ": " + e.GetType().Name); continue; }
                foreach (MethodBase method in methods)
                {
                    try { InstallMethod(harmony, module, type, method, seen); }
                    catch (Exception e) { module.Warn("UI adapter skipped " + type.FullName + "." + method.Name + ": " + e.GetType().Name); }
                }
            }
            foreach (MethodSpec expected in module.returns)
                if (!Declares(module, expected.type, expected.method))
                    module.Warn("Expected return adapter not found: " + expected.type + "." + expected.method);
            foreach (LiteralSpec expected in module.literals)
            {
                if (!Declares(module, expected.type, expected.method))
                { module.Warn("Expected literal adapter not found: " + expected.type + "." + expected.method); continue; }
                // Known from the original IL, so a mod that delays patching does not affect this check.
                foreach (string value in expected.values.Keys)
                    if (!seen.Contains(LiteralKey(expected, value)))
                        module.Warn("Literal not found in IL: " + expected.type + "." + expected.method + " :: " + Shown(value));
            }
            foreach (LiteralSpec expected in module.configTexts)
            {
                if (!Declares(module, expected.type, expected.method))
                { module.Warn("Expected config text adapter not found: " + expected.type + "." + expected.method); continue; }
                if (!seen.Contains(ConfigKey(expected)))
                    module.Warn("Config value read not found in IL: " + expected.type + "." + expected.method);
            }
        }
        private static string LiteralKey(LiteralSpec spec, string value) { return spec.type + "\n" + spec.method + "\n" + value; }
        private static string ConfigKey(LiteralSpec spec) { return "config\n" + spec.type + "\n" + spec.method; }
        // The getter of ConfigEntry<string>.Value: the only config read a config text rule replaces.
        private static bool ConfigRead(CodeInstruction instruction)
        {
            MethodInfo getter = instruction.operand as MethodInfo;
            return getter != null && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                   getter.Name == "get_Value" && getter.ReturnType == typeof(string) && getter.DeclaringType != null &&
                   getter.DeclaringType.IsGenericType && getter.DeclaringType.GetGenericTypeDefinition() == typeof(BepInEx.Configuration.ConfigEntry<>);
        }
        // A literal may start with a line break; warnings show control characters escaped.
        private static string Shown(string value) { return value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"); }
        private static void InstallMethod(Harmony harmony, Module module, Type type, MethodBase method, HashSet<string> seen)
        {
            if (!RuntimeAccess.ManagedBody(method)) return;
            module.ScannedMethods++;
            LiteralSpec[] specs = module.literals.Where(delegate(LiteralSpec p) { return p.type == type.FullName && p.method == method.Name; }).ToArray();
            LiteralSpec[] configs = module.configTexts.Where(delegate(LiteralSpec p) { return p.type == type.FullName && p.method == method.Name; }).ToArray();
            MethodInfo function = method as MethodInfo;
            bool result = function != null && function.ReturnType == typeof(string) &&
                module.returns.Any(delegate(MethodSpec p) { return p.type == type.FullName && p.method == method.Name; });
            if (specs.Length == 0 && configs.Length == 0 && !result && !module.HasDisplayText) return;
            bool literal = false, sink = false, config = false;
            List<CodeInstruction> original = null;
            try
            {
                original = PatchProcessor.GetOriginalInstructions(method, null);
                // A literal rule names a method, and overloads share the name: only a body that holds
                // one of its values gets the literal transpiler, so the others are neither patched nor reported.
                foreach (CodeInstruction instruction in original)
                {
                    string text = instruction.opcode == OpCodes.Ldstr ? instruction.operand as string : null;
                    if (text == null) continue;
                    foreach (LiteralSpec spec in specs)
                        if (spec.values.ContainsKey(text)) { literal = true; seen.Add(LiteralKey(spec, text)); }
                }
                sink = module.HasDisplayText && original.Any(delegate(CodeInstruction instruction) { return Relevant(instruction, module); });
                // Like a literal rule, a config text rule patches only the overload that reads a string config value.
                config = configs.Length != 0 && original.Any(ConfigRead);
                if (config) foreach (LiteralSpec spec in configs) seen.Add(ConfigKey(spec));
            }
            catch (Exception e)
            {
                module.Warn("IL inspection skipped " + type.Name + "." + method.Name + ": " + e.GetType().Name);
                // Without the body the rule cannot be narrowed to an overload: try it as named.
                literal = specs.Length != 0;
                foreach (LiteralSpec spec in specs) foreach (string value in spec.values.Keys) seen.Add(LiteralKey(spec, value));
                config = configs.Length != 0;
                foreach (LiteralSpec spec in configs) seen.Add(ConfigKey(spec));
            }
            if (!literal && !result && !sink && !config) return;
            // HarmonyX 2.9 cannot rebuild two kinds of exception blocks: it never marks the handler of an
            // exception filter (catch ... when), and it ends a fault handler (iterators with try/finally)
            // with leave instead of endfinally. Any patch of such a method fails to compile or yields
            // invalid IL. A mod that delays patching (StartupAccelerator) applies it after this try/catch
            // has returned, and the failure stops the game instead. Its display calls are translated in
            // the display methods it calls; literal, config and return rules need its own body.
            string unsupported = UnsupportedHandler(method);
            if (unsupported != null)
            {
                string owner = type.FullName + "." + method.Name;
                if (sink) CallSites.Cover(harmony, module, owner, method, original, unsupported);
                if (literal || config || result) module.Warn("UI patch skipped " + owner + ": " + unsupported);
                return;
            }
            string key = RuntimeAccess.MethodKey(method);
            Owners[key] = module;
            try
            {
                HarmonyMethod tr = literal || sink || config ? new HarmonyMethod(typeof(DisplayPatches).GetMethod("Transpile", AccessTools.all)) : null;
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
                Type t = (module.RuntimeCodeAssembly ?? module.RuntimeAssembly).GetType(typeName, false);
                if (t != null && methodName == ".ctor") return AccessTools.GetDeclaredConstructors(t, false).Count != 0;
                return t != null && t.GetMethods(AccessTools.allDeclared).Any(delegate(MethodInfo m) { return m.Name == methodName; });
            }
            catch (Exception) { return false; }
        }
        internal static void Reset() { Owners.Clear(); CallSites.Reset(); }
        private static bool Relevant(CodeInstruction instruction, Module module)
        {
            MethodBase call = instruction.operand as MethodBase;
            if (call != null && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Newobj))
                return StringSink(call).Any(delegate(bool b) { return b; });
            FieldInfo field = instruction.operand as FieldInfo;
            return field != null && instruction.opcode == OpCodes.Stfld && DisplayField(field, module);
        }
        internal static bool DisplayField(FieldInfo f, Module m)
        {
            if (f.FieldType != typeof(string) || f.DeclaringType == null) return false;
            string type = f.DeclaringType.FullName;
            return (m.id == "ExpertExplorer" && type == "MessageHud+UnlockMsg" && (f.Name == "m_topic" || f.Name == "m_description")) ||
                   (m.id == "SpeedyPaths" && type == "StatusEffect" && f.Name == "m_name");
        }
        internal static bool[] StringSink(MethodBase method)
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
        internal static bool LocalizedReturn(MethodBase method)
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
            evidence.displayCalls = evidence.displayFields = evidence.literals = evidence.configTexts = evidence.skippedBoundaries = 0;
            evidence.matchedLiterals.Clear();
            string declaring = __originalMethod.DeclaringType.FullName;
            bool configRule = module.configTexts.Any(delegate(LiteralSpec p) { return p.type == declaring && p.method == __originalMethod.Name; });
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
                if (configRule && ConfigRead(current))
                {
                    // The value stays on the stack: the call returns it translated or unchanged.
                    evidence.configTexts++;
                    output.Add(current);
                    output.Add(new CodeInstruction(OpCodes.Ldstr, module.id));
                    output.Add(new CodeInstruction(OpCodes.Ldstr, declaring));
                    output.Add(new CodeInstruction(OpCodes.Ldstr, __originalMethod.Name));
                    output.Add(new CodeInstruction(OpCodes.Call, ConfigText));
                    continue;
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
            // Checked when the plan is known: a mod that delays patching runs this transpiler
            // long after harmony.Patch has returned.
            string owner = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name;
            HashSet<string> present = new HashSet<string>(input.Where(delegate(CodeInstruction i) { return i.opcode == OpCodes.Ldstr && i.operand is string; })
                .Select(delegate(CodeInstruction i) { return (string)i.operand; }), StringComparer.Ordinal);
            foreach (LiteralSpec spec in module.literals.Where(delegate(LiteralSpec p) { return p.type == __originalMethod.DeclaringType.FullName && p.method == __originalMethod.Name; }))
                foreach (string value in spec.values.Keys)
                    // Another overload may hold the value; only a value left in this body is a miss.
                    if (present.Contains(value) && !evidence.matchedLiterals.Contains(value))
                        module.Warn("Literal not replaced: " + owner + " :: " + Shown(value));
            if (configRule && evidence.configTexts == 0 && input.Any(ConfigRead))
                module.Warn("Config value read not replaced: " + owner);
            if (evidence.displayCalls + evidence.displayFields + evidence.literals + evidence.configTexts == 0)
                module.Warn("Transpiler installed but no display site changed: " + owner);
            return output;
        }
        internal static string UnsupportedHandler(MethodBase method)
        {
            MethodBody body = method.GetMethodBody();
            if (body == null) return null;
            foreach (ExceptionHandlingClause clause in body.ExceptionHandlingClauses)
            {
                if (clause.Flags == ExceptionHandlingClauseOptions.Filter) return "exception filter";
                if (clause.Flags == ExceptionHandlingClauseOptions.Fault) return "fault block";
            }
            return null;
        }
    }
}
