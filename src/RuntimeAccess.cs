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
            // The same base-type search as AccessTools.Field/Property, without their warning for a
            // member that is a property rather than a field (or absent from this game version).
            FieldInfo field = AccessTools.FindIncludingBaseTypes(type, delegate(Type t) { return t.GetField(name, AccessTools.all); });
            if (field != null) return field.GetValue(receiver);
            PropertyInfo property = AccessTools.FindIncludingBaseTypes(type, delegate(Type t) { return t.GetProperty(name, AccessTools.all); });
            return property == null ? null : property.GetValue(receiver, null);
        }
        internal static string MethodKey(MethodBase method)
        {
            return method.Module.ModuleVersionId.ToString("N") + ":" + method.MetadataToken;
        }
        internal static bool ManagedBody(MethodBase method)
        {
            if (method.IsAbstract) return false;
            // Mono can crash in ContainsGenericParameters when a signature references
            // an absent optional assembly. Resolve it through the checked reflection
            // API first, so the caller can catch the load failure and skip this method.
            method.GetParameters();
            if (method.ContainsGenericParameters) return false;
            try { return method.GetMethodBody() != null; }
            catch (InvalidOperationException) { return false; }
            catch (NotSupportedException) { return false; }
        }
    }
}
