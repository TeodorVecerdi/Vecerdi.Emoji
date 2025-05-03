#nullable enable

using UnityEditor;
using UnityEngine;

namespace Vecerdi.Logging.Unity.Editor {
    internal static class EditorResources {
        public static bool Load<T>(ref T? asset, string assetName, string tag = "Vecerdi.Logging") where T : Object {
            if (asset != null) {
                return true;
            }

            var query = $"t:{typeof(T).Name} l:{tag} {assetName}";
            string[] guids = AssetDatabase.FindAssets(query);
            if (guids.Length == 0) {
                Log.Error($"Could not find {typeof(T).Name} with tag {tag} and name {assetName}");
                return false;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return true;
        }
    }
}