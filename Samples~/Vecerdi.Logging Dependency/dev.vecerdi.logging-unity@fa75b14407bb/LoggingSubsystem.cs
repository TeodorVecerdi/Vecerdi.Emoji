#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vecerdi.Logging.Unity {
    internal static partial class LoggingSubsystem {
#if !UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        private static void Register() {
            Log.RegisterLogger(UnityLogger.Instance);
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    internal static partial class LoggingSubsystem {
        static LoggingSubsystem() {
            Register();
            EditorApplication.playModeStateChanged -= RegisterWhenExitingPlayMode;
            EditorApplication.playModeStateChanged += RegisterWhenExitingPlayMode;
        }

        private static void RegisterWhenExitingPlayMode(PlayModeStateChange state) {
            if (state is PlayModeStateChange.EnteredEditMode) {
                Register();
            }
        }
    }
#endif
}
