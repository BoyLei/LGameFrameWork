using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace LGameFramework.Editor
{
    public static class ResourceDemoBuilder
    {
        [MenuItem("LGameFramework/Resources/Create Demo Addressable")]
        public static void CreateDemoAddressable()
        {
            const string folder = "Assets/Game/Content/Prefabs";
            const string path = folder + "/AddressableDemoCube.prefab";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Game/Content", "Prefabs");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try { PrefabUtility.SaveAsPrefabAsset(cube, path); }
                finally { Object.DestroyImmediate(cube); }
            }
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var existing = settings.FindAssetEntry(guid);
            if (existing == null)
            {
                var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                entry.SetAddress("demo/cube");
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
            }
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            Debug.Log("Demo prefab ready. Check its address in Addressables Groups; add ResourceDemo to a scene object and enter Play Mode.");
        }
    }
}
