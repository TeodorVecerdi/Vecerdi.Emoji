#nullable enable

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vecerdi.Logging.Unity.Editor {
    internal class LoggingSettingsPreprocessBuild : IPreprocessBuildWithReport {
        public int callbackOrder => -200;

        public void OnPreprocessBuild(BuildReport report) {
            var settings = LoggingSettings.GetOrCreateSettings();
            settings.UpdateCategories();

            var resourcesPath = Path.Combine(Application.dataPath, "Resources", "Vecerdi.Logging");
            if (!Directory.Exists(resourcesPath)) {
                Directory.CreateDirectory(resourcesPath);
            }

            var destinationPath = Path.Combine(resourcesPath, LoggingSettings.ASSET_NAME);
            if (File.Exists(destinationPath)) {
                File.Delete(destinationPath);
            }

            var relativePath = Path.Combine("Assets", "Resources", "Vecerdi.Logging", LoggingSettings.ASSET_NAME);
            AssetDatabase.CreateAsset(settings, relativePath);
            AssetDatabase.Refresh();
        }
    }

    internal class LoggingSettingsPostprocessBuild : IPostprocessBuildWithReport {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report) {
            var resourcesPath = Path.Combine(Application.dataPath, "Resources", "Vecerdi.Logging");
            var assetPath = Path.Combine(resourcesPath, LoggingSettings.ASSET_NAME);

            if (File.Exists(assetPath)) {
                File.Delete(assetPath);
                File.Delete($"{assetPath}.meta");
            }

            if (!Directory.EnumerateFiles(resourcesPath).Any()) {
                Directory.Delete(resourcesPath);
                File.Delete($"{resourcesPath}.meta");
            }

            var parentDirectory = Directory.GetParent(resourcesPath)!.FullName;
            if (!Directory.EnumerateFiles(parentDirectory).Any()) {
                Directory.Delete(parentDirectory);
                File.Delete($"{parentDirectory}.meta");
            }

            AssetDatabase.Refresh();
        }
    }
}