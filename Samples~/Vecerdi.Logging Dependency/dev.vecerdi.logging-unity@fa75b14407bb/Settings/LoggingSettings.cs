#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vecerdi.Logging.Unity {
    public class LoggingSettings : ScriptableObject {
        public const string ASSET_NAME = "LoggingSettings.asset";
        public static string LoggingSettingsPath => Path.Combine(CustomProjectSettings.ProjectSettingsPath, ASSET_NAME);
        private static LoggingSettings? s_Instance;

        [Tooltip("This takes precedence over the individual log categories. If set to None, all logging is disabled.")]
        [SerializeField] private LogLevel m_GlobalLogLevel = LogLevel.Debug;
        [SerializeField] private CategoryNameTransform m_CategoryNameTransforms = CategoryNameTransform.AllUppercase | CategoryNameTransform.ReplaceSpacesWithUnderscores;
        [SerializeField] private bool m_EnableColoredOutputInEditor;
        [SerializeField] private List<LogCategory> m_LogCategories = new();
        [SerializeField] private bool m_OverrideGlobalLogLevelInBuilds;
        [SerializeField] private LogLevel m_GlobalLogLevelInBuilds = LogLevel.Information;

        private bool m_IsDirty;

#if UNITY_EDITOR
        public LogLevel GlobalLogLevel {
            get => this.m_GlobalLogLevel;
            set => this.m_GlobalLogLevel = value;
        }
#else
        public LogLevel GlobalLogLevel {
            get => this.m_OverrideGlobalLogLevelInBuilds ? this.m_GlobalLogLevelInBuilds : this.m_GlobalLogLevel;
            set {
                if (this.m_OverrideGlobalLogLevelInBuilds) {
                    this.m_GlobalLogLevelInBuilds = value;
                } else {
                    this.m_GlobalLogLevel = value;
                }
            }
        }
#endif

        public bool OverrideGlobalLogLevelInBuilds {
            get => m_OverrideGlobalLogLevelInBuilds;
            set => m_OverrideGlobalLogLevelInBuilds = value;
        }

        public LogLevel GlobalLogLevelInBuilds {
            get => m_GlobalLogLevelInBuilds;
            set => m_GlobalLogLevelInBuilds = value;
        }

        public List<LogCategory> LogCategories => this.m_LogCategories;

        public Dictionary<string, LogCategory> LogCategoriesByName { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public CategoryNameTransform CategoryNameTransforms {
            get => this.m_CategoryNameTransforms;
            set => this.m_CategoryNameTransforms = value;
        }

        public bool EnableColoredOutputInEditor {
            get => this.m_EnableColoredOutputInEditor;
            set => this.m_EnableColoredOutputInEditor = value;
        }

        public void UpdateCategories() {
#if UNITY_EDITOR
            using PooledObject<HashSet<string>> _ = CollectionPool<HashSet<string>, string>.Get(out HashSet<string> existingCategories);
            bool dirty = false;

            foreach (FieldInfo fieldInfo in TypeCache.GetFieldsWithAttribute<LogCategoryAttribute>()) {
                if (!fieldInfo.IsStatic || fieldInfo.FieldType != typeof(string)) {
                    continue;
                }

                LogCategoryAttribute attribute = fieldInfo.GetCustomAttribute<LogCategoryAttribute>();
                string value = (fieldInfo.GetRawConstantValue() as string)!;
                existingCategories.Add(value);

                if (!this.ContainsCategoryWithName(value)) {
                    string categoryName = this.TransformCategoryName(value);
                    this.m_LogCategories.Add(new LogCategory(categoryName, value, attribute.DefaultLogLevel));
                    dirty = true;
                }
            }

            this.LogCategoriesByName = this.m_LogCategories.ToDictionary(logCategory => logCategory.OriginalCategoryName, logCategory => logCategory);
            dirty |= this.m_LogCategories.RemoveAll(logCategory => !existingCategories.Contains(logCategory.OriginalCategoryName)) > 0;
            if (dirty) {
                this.Save();
            }
#else
            this.LogCategoriesByName = this.m_LogCategories.ToDictionary(logCategory => logCategory.OriginalCategoryName, logCategory => logCategory);
#endif
        }

        public bool ContainsCategoryWithName(string categoryName) {
            return this.m_LogCategories.Any(logCategory => string.Equals(logCategory.OriginalCategoryName, categoryName, StringComparison.OrdinalIgnoreCase));
        }

        public void Save() {
            string json = JsonUtility.ToJson(this, true);
            DirectoryInfo directoryInfo = new(CustomProjectSettings.ProjectSettingsPath);
            if (!directoryInfo.Exists) {
                directoryInfo.Create();
            }

            File.WriteAllText(LoggingSettingsPath, json);
        }

        public static LoggingSettings GetOrCreateSettings() {
            if (s_Instance != null) {
                return s_Instance;
            }

#if UNITY_EDITOR
            DirectoryInfo directoryInfo = new(CustomProjectSettings.ProjectSettingsPath);
            if (!directoryInfo.Exists) {
                directoryInfo.Create();
            }

            if (File.Exists(LoggingSettingsPath)) {
                string json = File.ReadAllText(LoggingSettingsPath);
                s_Instance = CreateInstance<LoggingSettings>();
                JsonUtility.FromJsonOverwrite(json, s_Instance);
            } else {
                s_Instance = CreateInstance<LoggingSettings>();
                s_Instance.Save();
            }
#else
            s_Instance = Resources.Load<LoggingSettings>("Vecerdi.Logging/LoggingSettings");
#endif
            s_Instance.UpdateCategories();
            return s_Instance;
        }

        public void TransformAllCategoryNames() {
            foreach (LogCategory logCategory in this.m_LogCategories) {
                logCategory.CategoryName = this.TransformCategoryName(logCategory.OriginalCategoryName);
            }

            this.Save();
        }

        public string TransformCategoryName(string categoryName) {
            if (this.m_CategoryNameTransforms.HasFlag(CategoryNameTransform.AllUppercase)) {
                categoryName = categoryName.ToUpperInvariant();
            } else if (this.m_CategoryNameTransforms.HasFlag(CategoryNameTransform.AllLowercase)) {
                categoryName = categoryName.ToLowerInvariant();
            }

            if (this.m_CategoryNameTransforms.HasFlag(CategoryNameTransform.ReplaceSpacesWithUnderscores)) {
                categoryName = categoryName.Replace(' ', '_');
            } else if (this.m_CategoryNameTransforms.HasFlag(CategoryNameTransform.ReplaceSpacesWithDashes)) {
                categoryName = categoryName.Replace(' ', '-');
            } else if (this.m_CategoryNameTransforms.HasFlag(CategoryNameTransform.ReplaceSpacesWithDots)) {
                categoryName = categoryName.Replace(' ', '.');
            } else if (this.m_CategoryNameTransforms.HasFlag(CategoryNameTransform.ReplaceSpacesWithSlashes)) {
                categoryName = categoryName.Replace(' ', '/');
            }

            return categoryName;
        }
    }

    [Flags]
    public enum CategoryNameTransform {
        // None = 0,
        AllUppercase = 1 << 0,
        AllLowercase = 1 << 1,
        ReplaceSpacesWithUnderscores = 1 << 2,
        ReplaceSpacesWithDashes = 1 << 3,
        ReplaceSpacesWithDots = 1 << 4,
        ReplaceSpacesWithSlashes = 1 << 5,
    }

    [Serializable]
    public class LogCategory {
        [SerializeField] private string m_CategoryName;
        [SerializeField] private string m_OriginalCategoryName;
        [SerializeField] private LogLevel m_LogLevel;

        public string CategoryName {
            get => this.m_CategoryName;
            set => this.m_CategoryName = value;
        }
        public string OriginalCategoryName => this.m_OriginalCategoryName;
        public LogLevel LogLevel => this.m_LogLevel;

        public LogCategory(string categoryName, string originalCategoryName, LogLevel logLevel) {
            this.m_CategoryName = categoryName;
            this.m_OriginalCategoryName = originalCategoryName;
            this.m_LogLevel = logLevel;
        }
    }
}