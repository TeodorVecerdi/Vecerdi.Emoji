#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vecerdi.Logging.Unity {
    public class UnityLogger : ILogger {
        internal static UnityLogger Instance { get; } = new();

        private static LoggingSettings? s_Settings;
        private static LoggingSettings Settings {
            get {
                if (s_Settings == null) {
                    s_Settings = LoggingSettings.GetOrCreateSettings();
                }

                return s_Settings;
            }
        }

        public static LogLevel LogLevel {
            get => Settings.GlobalLogLevel;
            set => Settings.GlobalLogLevel = value;
        }

        LogLevel ILogger.MinimumLogLevel {
            get => Settings.GlobalLogLevel;
            set => Settings.GlobalLogLevel = value;
        }

        private UnityLogger() {}

        [HideInCallstack]
        void ILogger.Message(ReadOnlySpan<char> message, ReadOnlySpan<char> category, object? context, LogLevel logLevel) {
            Action<string,Object?> logMethod = logLevel switch {
                LogLevel.Trace or LogLevel.Debug or LogLevel.Information => Debug.Log,
                LogLevel.Warning => Debug.LogWarning,
                LogLevel.Error => Debug.LogError,
                LogLevel.Critical => Debug.LogError,
                LogLevel.None => throw new ArgumentException("Log level cannot be None.", nameof(logLevel)),
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null),
            };

            if (!category.IsEmpty) {
                if (Settings.LogCategoriesByName.TryGetValue(category.ToString(), out LogCategory? logCategory)) {
                    if (logLevel < logCategory.LogLevel) {
                        return;
                    }

                    logMethod(GetLogMessage(message, logCategory.CategoryName, logLevel), context as Object);
                } else {
                    logMethod(GetLogMessage(message, Settings.TransformCategoryName(category.ToString()), logLevel), context as Object);
                }
            } else {
                logMethod(GetLogMessage(message.ToString(), logLevel), context as Object);
            }
        }

        [HideInCallstack]
        void ILogger.Exception(Exception? exception, ReadOnlySpan<char> category, object? context, LogLevel logLevel) {
            if (!category.IsEmpty &&
                Settings.LogCategoriesByName.TryGetValue(category.ToString(), out LogCategory? logCategory) &&
                logLevel < logCategory.LogLevel
            ) {
                return;
            }

            Debug.LogException(exception, context as Object);
        }

#if UNITY_EDITOR
        private static readonly Dictionary<LogLevel, string> s_LogLevelColors = new() {
            { LogLevel.Trace, "#A8A8A8" },
            { LogLevel.Debug, "#C7C7C7" },
            { LogLevel.Information, "#62B0D9" },
            { LogLevel.Warning, "#FFA833" },
            { LogLevel.Error, "#FF465F" },
            { LogLevel.Critical, "#E5558C" },
        };
#endif

        private static string GetLogMessage(ReadOnlySpan<char> message, ReadOnlySpan<char> category, LogLevel logLevel) {
#if !UNITY_EDITOR
            return StringUtilities.Format("[{}, {}] {}", logLevel.ToString(), category, message);
#else
            if (Settings.EnableColoredOutputInEditor)
                return StringUtilities.Format("<b><color={}>[{}, {}]</color></b> {}", s_LogLevelColors[logLevel], logLevel.ToString(), category, message);
            return StringUtilities.Format("[{}, {}] {}", logLevel.ToString(), category, message);
#endif
        }

        private static string GetLogMessage(ReadOnlySpan<char> message, LogLevel logLevel) {
#if !UNITY_EDITOR
            return StringUtilities.Format("[{}] {}", logLevel.ToString(), message);
#else
            if (Settings.EnableColoredOutputInEditor)
                return StringUtilities.Format("<b><color={}>[{}]</color></b> {}", s_LogLevelColors[logLevel], logLevel.ToString(), message);
            return StringUtilities.Format("[{}] {}", logLevel.ToString(), message);
#endif
        }
    }
}