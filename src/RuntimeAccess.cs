using System;
using System.Reflection;
using HarmonyLib;

namespace Tolmach
{
    // Product policy only. Harmony owns type enumeration and member/base-type lookup.
    internal static class RuntimeAccess
    {
        internal static Type ExactType(string fullName)
        {
            Type type = AccessTools.TypeByName(fullName);
            // Do not accept AccessTools' short-name fallback for an unrelated type.
            return type != null && type.FullName == fullName ? type : null;
        }
        internal static object Read(object instanceOrType, string name)
        {
            if (instanceOrType == null) return null;
            Type type = instanceOrType as Type ?? instanceOrType.GetType();
            object receiver = instanceOrType is Type ? null : instanceOrType;
            FieldInfo field = AccessTools.Field(type, name);
            if (field != null) return field.GetValue(receiver);
            PropertyInfo property = AccessTools.Property(type, name);
            return property == null ? null : property.GetValue(receiver, null);
        }
        internal static string MethodKey(MethodBase method)
        {
            return method.Module.ModuleVersionId.ToString("N") + ":" + method.MetadataToken;
        }
        internal static bool ManagedBody(MethodBase method)
        {
            if (method.IsAbstract || method.ContainsGenericParameters) return false;
            try { return method.GetMethodBody() != null; }
            catch (InvalidOperationException) { return false; }
            catch (NotSupportedException) { return false; }
        }
    }
}
