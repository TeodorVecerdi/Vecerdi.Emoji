using System.IO;
using UnityEngine;

namespace Vecerdi.Logging.Unity {
    internal static class CustomProjectSettings {
        public static string ProjectPath { get; } = Directory.GetParent(Application.dataPath)!.FullName;
        public static string ProjectSettingsPath { get; } = Path.Combine(ProjectPath, "Library/Vecerdi.Logging");
    }
}