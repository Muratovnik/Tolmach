using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Tolmach
{
    internal static class ExtraHooks
    {
        internal static void Install(Harmony harmony)
        {
            // Re-register dictionaries after Jotunn/native loaders run at menu/world startup.
            Type startup = RuntimeAccess.ExactType("FejdStartup");
            if (startup != null)
                foreach (MethodInfo m in startup.GetMethods(AccessTools.allDeclared).Where(delegate(MethodInfo x) { return x.Name == "SetupGui"; }))
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(ExtraHooks).GetMethod("AfterMenu", AccessTools.all)) { priority = Priority.Last });
            // OreMines broadcasts English with Chat.SendText. Every client receives it in
            // Chat.OnNewChatMessage, which feeds both the chat log (Terminal.AddString) and
            // the in-world bubble. Translate only that exact received text, not SendText,
            // RPC payloads, usernames or persisted settings.
            Module mines;
            if (!TextEngine.Modules.TryGetValue("OreMines", out mines) || !mines.UiAllowed) return;
            Type chat = RuntimeAccess.ExactType("Chat");
            MethodInfo received = chat == null ? null : chat.GetMethods(AccessTools.allDeclared).FirstOrDefault(delegate(MethodInfo x) {
                return x.Name == "OnNewChatMessage" && x.GetParameters().Any(delegate(ParameterInfo p) { return p.Name == "text" && p.ParameterType == typeof(string); });
            });
            if (received == null) { mines.Warn("Chat.OnNewChatMessage(text) was not found; mine-reset chat message is not patched."); return; }
            try { harmony.Patch(received, prefix: new HarmonyMethod(typeof(ExtraHooks).GetMethod("ReceivedChat", AccessTools.all)) { priority = Priority.Last }); }
            catch (Exception e) { mines.Warn("Chat message adapter: " + e.GetType().Name); }
        }
        private static void AfterMenu()
        {
            NativeAdapters.Refresh();
            Plugin.WriteReport();
        }
        private static void ReceivedChat(ref string text)
        {
            if (TextEngine.IsRussian && text == "Mines have been reset!") text = TextEngine.Display(text, "OreMines");
        }
    }
}
