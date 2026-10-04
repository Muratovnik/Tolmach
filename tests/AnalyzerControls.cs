using System;
using HarmonyLib;
using UnityEngine;

#if EARLY_CHECK_BAD_DOTNET
internal static class DotnetInvalidControl
{
    internal static bool Same(int left, int right) { return ReferenceEquals(left, right); }
}
#elif EARLY_CHECK_BAD_UNITY
internal static class UnityInvalidControl
{
    internal static string Name(Transform value) { return value?.name; }
}
#elif EARLY_CHECK_BAD_HARMONY
[HarmonyPatch(typeof(HarmonyControlTarget), "Overloaded")]
internal static class HarmonyInvalidControl
{
    private static void Prefix() { }
}
#elif EARLY_CHECK_VALID
internal static class ValidUnityControl
{
    internal static string Name(Transform value) { return value ? value.name : ""; }
    internal static bool Same(int left, int right) { return left == right; }
}
[HarmonyPatch(typeof(HarmonyControlTarget), "PrivateTarget")]
internal static class ValidPrivateTargetControl
{
    private static void Prefix() { }
}
#endif

#if EARLY_CHECK_BAD_HARMONY || EARLY_CHECK_VALID
internal sealed class HarmonyControlTarget
{
    private void PrivateTarget() { }
    public void Overloaded() { }
    public void Overloaded(int value) { }
}
#endif

