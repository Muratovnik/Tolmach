using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace Tolmach
{
    // HumanoidRandomizer stays on randomized source prefabs, but GeneratePrefabs
    // removes it from variants. Their ownership comes from the mod's live registry.
    internal sealed class CreatureNameOwner
    {
        private readonly Type markerType;
        private readonly MethodInfo getComponent;
        private readonly MethodInfo isPlayer;
        private FieldInfo viewField;
        private FieldInfo builderField;
        private FieldInfo prefabsField;
        private MethodInfo getZdo;
        private MethodInfo getPrefabId;
        private MethodInfo sceneInstance;
        private MethodInfo getPrefab;
        private bool clonesAvailable;

        internal CreatureNameOwner(Module module, Type characterType)
        {
            markerType = module.RuntimeAssembly == null ? null : module.RuntimeAssembly.GetType("BalrondHumanoidRandomizer.HumanoidRandomizer");
            getComponent = characterType == null ? null : AccessTools.Method(characterType, "GetComponent", new[] { typeof(Type) });
            isPlayer = characterType == null ? null : AccessTools.Method(characterType, "IsPlayer", Type.EmptyTypes);
        }

        internal bool Available
        {
            get { return markerType != null && getComponent != null && !getComponent.IsStatic &&
                getComponent.ReturnType.IsAssignableFrom(markerType) && isPlayer != null && !isPlayer.IsStatic && isPlayer.ReturnType == typeof(bool); }
        }

        internal void BindClones(Module module, Type characterType, Type sceneType)
        {
            Type launch = module.RuntimeAssembly.GetType("BalrondHumanoidRandomizer.Launch");
            builderField = launch == null ? null : AccessTools.Field(launch, "itemSetBuilder");
            prefabsField = builderField == null ? null : AccessTools.Field(builderField.FieldType, "prefabs");
            viewField = AccessTools.Field(characterType, "m_nview");
            getZdo = viewField == null ? null : AccessTools.Method(viewField.FieldType, "GetZDO", Type.EmptyTypes);
            getPrefabId = getZdo == null ? null : AccessTools.Method(getZdo.ReturnType, "GetPrefab", Type.EmptyTypes);
            sceneInstance = sceneType == null ? null : AccessTools.PropertyGetter(sceneType, "instance");
            getPrefab = sceneType == null ? null : AccessTools.Method(sceneType, "GetPrefab", new[] { typeof(int) });
            clonesAvailable = builderField != null && builderField.IsStatic && prefabsField != null && !prefabsField.IsStatic &&
                typeof(IList).IsAssignableFrom(prefabsField.FieldType) && viewField != null && !viewField.IsStatic &&
                getZdo != null && !getZdo.IsStatic && getPrefabId != null && !getPrefabId.IsStatic && getPrefabId.ReturnType == typeof(int) &&
                sceneInstance != null && sceneInstance.IsStatic && sceneType.IsAssignableFrom(sceneInstance.ReturnType) &&
                getPrefab != null && !getPrefab.IsStatic && !getPrefab.ReturnType.IsValueType;
            if (!clonesAvailable) module.Warn("Creature-name prefab ownership API unavailable; unmarked variants are left unchanged.");
        }

        internal bool Owns(object character)
        {
            if ((bool)isPlayer.Invoke(character, null)) return false;
            object marker = getComponent.Invoke(character, new object[] { markerType });
            if (marker != null && markerType.IsInstanceOfType(marker)) return true;
            if (!clonesAvailable) return false;
            object view = viewField.GetValue(character);
            object zdo = view == null ? null : getZdo.Invoke(view, null);
            object scene = sceneInstance.Invoke(null, null);
            if (zdo == null || scene == null) return false;
            object prefab = getPrefab.Invoke(scene, new[] { getPrefabId.Invoke(zdo, null) });
            object builder = builderField.GetValue(null);
            IList prefabs = builder == null ? null : prefabsField.GetValue(builder) as IList;
            if (prefab == null || prefabs == null) return false;
            // Do not cache membership: the mod clears/rebuilds this list on scene load.
            foreach (object candidate in prefabs)
                if (ReferenceEquals(prefab, candidate)) return true;
            return false;
        }
    }
}
