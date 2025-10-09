using System;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace TheOne.UITemplate.Editor.Optimization
{
    /// <summary>
    /// Settings Module - Centralized configuration for all optimization modules.
    /// Provides global preferences with persistent storage via EditorPrefs.
    /// Settings organized into General, Performance, and Advanced tabs.
    /// </summary>
    public class SettingsModule : OptimizationModuleBase
    {
        public override string ModuleName => "Settings";
        public override string ModuleIcon => "⚙️";

        // EditorPrefs key prefix
        private const string PREF_PREFIX = "OptHub_Setting_";

        #region General Tab

        [TabGroup("Tabs", "General")]
        [FolderPath(AbsolutePath = false)]
        [LabelText("Default Export Directory")]
        [Tooltip("Default directory for exporting reports and analysis results")]
        [OnValueChanged(nameof(SaveDefaultExportPath))]
        [ShowInInspector]
        private string DefaultExportPath
        {
            get => EditorPrefs.GetString(PREF_PREFIX + "ExportPath", "OptimizationReports");
            set => EditorPrefs.SetString(PREF_PREFIX + "ExportPath", value);
        }

        [TabGroup("Tabs", "General")]
        [LabelText("Show Confirmation Dialogs")]
        [Tooltip("Display confirmation dialogs for destructive operations (clear, delete, etc.)")]
        [OnValueChanged(nameof(SaveShowConfirmations))]
        [ShowInInspector]
        private bool ShowConfirmationDialogs
        {
            get => EditorPrefs.GetBool(PREF_PREFIX + "ShowConfirmations", true);
            set => EditorPrefs.SetBool(PREF_PREFIX + "ShowConfirmations", value);
        }

        [TabGroup("Tabs", "General")]
        [LabelText("Auto-Refresh on Focus")]
        [Tooltip("Automatically refresh module data when the window gains focus")]
        [OnValueChanged(nameof(SaveAutoRefresh))]
        [ShowInInspector]
        private bool AutoRefreshOnFocus
        {
            get => EditorPrefs.GetBool(PREF_PREFIX + "AutoRefresh", false);
            set => EditorPrefs.SetBool(PREF_PREFIX + "AutoRefresh", value);
        }

        [TabGroup("Tabs", "General")]
        [LabelText("Theme")]
        [EnumToggleButtons]
        [Tooltip("UI color theme preference (Light/Dark/Auto follows Unity Editor theme)")]
        [OnValueChanged(nameof(SaveTheme))]
        [ShowInInspector]
        private ThemeMode Theme
        {
            get => (ThemeMode)EditorPrefs.GetInt(PREF_PREFIX + "Theme", (int)ThemeMode.Auto);
            set => EditorPrefs.SetInt(PREF_PREFIX + "Theme", (int)value);
        }

        [TabGroup("Tabs", "General")]
        [LabelText("Auto-Refresh Interval (minutes)")]
        [Range(1, 60)]
        [Tooltip("Minutes before cached data is considered stale and requires refresh")]
        [OnValueChanged(nameof(SaveAutoRefreshInterval))]
        [ShowInInspector]
        private int AutoRefreshInterval
        {
            get => EditorPrefs.GetInt(PREF_PREFIX + "RefreshInterval", 5);
            set => EditorPrefs.SetInt(PREF_PREFIX + "RefreshInterval", value);
        }

        #endregion

        #region Performance Tab

        [TabGroup("Tabs", "Performance")]
        [LabelText("Cache Duration (minutes)")]
        [Range(1, 30)]
        [Tooltip("How long to keep analysis results in memory before requiring re-analysis")]
        [OnValueChanged(nameof(SaveCacheDuration))]
        [ShowInInspector]
        private int CacheDuration
        {
            get => EditorPrefs.GetInt(PREF_PREFIX + "CacheDuration", 5);
            set => EditorPrefs.SetInt(PREF_PREFIX + "CacheDuration", value);
        }

        [TabGroup("Tabs", "Performance")]
        [LabelText("Enable Parallel Analysis")]
        [Tooltip("Use multiple threads for faster analysis (may increase memory usage)")]
        [OnValueChanged(nameof(SaveParallelAnalysis))]
        [ShowInInspector]
        private bool EnableParallelAnalysis
        {
            get => EditorPrefs.GetBool(PREF_PREFIX + "ParallelAnalysis", true);
            set => EditorPrefs.SetBool(PREF_PREFIX + "ParallelAnalysis", value);
        }

        [TabGroup("Tabs", "Performance")]
        [LabelText("Max Items Per Page")]
        [Range(100, 10000)]
        [Tooltip("Maximum number of items to display per table page (higher values may impact performance)")]
        [OnValueChanged(nameof(SaveMaxItemsPerPage))]
        [ShowInInspector]
        private int MaxItemsPerPage
        {
            get => EditorPrefs.GetInt(PREF_PREFIX + "MaxItemsPerPage", 1000);
            set => EditorPrefs.SetInt(PREF_PREFIX + "MaxItemsPerPage", value);
        }

        [TabGroup("Tabs", "Performance")]
        [LabelText("Enable Progress Bars")]
        [Tooltip("Show progress bars during long operations (may slow down analysis slightly)")]
        [OnValueChanged(nameof(SaveEnableProgressBars))]
        [ShowInInspector]
        private bool EnableProgressBars
        {
            get => EditorPrefs.GetBool(PREF_PREFIX + "ProgressBars", true);
            set => EditorPrefs.SetBool(PREF_PREFIX + "ProgressBars", value);
        }

        [TabGroup("Tabs", "Performance")]
        [InfoBox("Performance Impact:\n\n" +
                 "• Parallel Analysis: Faster analysis but uses more CPU and RAM\n" +
                 "• Cache Duration: Longer duration reduces re-analysis frequency\n" +
                 "• Max Items Per Page: Higher values improve overview but may lag UI\n" +
                 "• Progress Bars: Visual feedback with minimal performance overhead",
            InfoMessageType.Info)]
        [HideLabel]
        [ShowInInspector]
        private string PerformanceInfo => "";

        #endregion

        #region Advanced Tab

        [TabGroup("Tabs", "Advanced")]
        [LabelText("Debug Mode")]
        [Tooltip("Enable detailed logging and timing information in the Console")]
        [OnValueChanged(nameof(SaveDebugMode))]
        [ShowInInspector]
        private bool DebugMode
        {
            get => EditorPrefs.GetBool(PREF_PREFIX + "DebugMode", false);
            set => EditorPrefs.SetBool(PREF_PREFIX + "DebugMode", value);
        }

        [TabGroup("Tabs", "Advanced")]
        [LabelText("Enable Experimental Features")]
        [Tooltip("Enable experimental and unstable features (use at your own risk)")]
        [OnValueChanged(nameof(SaveExperimentalFeatures))]
        [ShowInInspector]
        private bool EnableExperimentalFeatures
        {
            get => EditorPrefs.GetBool(PREF_PREFIX + "ExperimentalFeatures", false);
            set => EditorPrefs.SetBool(PREF_PREFIX + "ExperimentalFeatures", value);
        }

        [TabGroup("Tabs", "Advanced")]
        [HorizontalGroup("Tabs/Advanced/Actions")]
        [Button("Reset All Settings", ButtonSizes.Large)]
        [GUIColor(1f, 0.7f, 0.4f)]
        private void ResetAllSettings()
        {
            if (this.ShowConfirmationDialogs)
            {
                if (!EditorUtility.DisplayDialog("Reset Settings",
                    "Are you sure you want to reset all settings to default values?",
                    "Reset", "Cancel"))
                {
                    return;
                }
            }

            // Delete all EditorPrefs with our prefix
            this.DeleteAllPreferences();

            // Log action
            Debug.Log("[SettingsModule] All settings reset to defaults.");
        }

        [TabGroup("Tabs", "Advanced")]
        [HorizontalGroup("Tabs/Advanced/Actions")]
        [Button("Clear All Caches", ButtonSizes.Large)]
        [GUIColor(1f, 0.5f, 0.5f)]
        private void ClearAllCaches()
        {
            if (this.ShowConfirmationDialogs)
            {
                if (!EditorUtility.DisplayDialog("Clear Caches",
                    "Are you sure you want to clear all optimization caches?\nThis will trigger re-analysis on next module load.",
                    "Clear", "Cancel"))
                {
                    return;
                }
            }

            // Clear EditorPrefs cache entries
            var cacheKeys = new[]
            {
                "OptHub_Cache_Overview",
                "OptHub_Cache_Font",
                "OptHub_Cache_Shader",
                "OptHub_Cache_Mesh",
                "OptHub_Cache_Audio",
                "OptHub_Cache_Addressables"
            };

            foreach (var key in cacheKeys)
            {
                EditorPrefs.DeleteKey(key);
            }

            Debug.Log("[SettingsModule] All caches cleared.");
        }

        [TabGroup("Tabs", "Advanced")]
        [InfoBox("$GetVersionInfo", InfoMessageType.None)]
        [HideLabel]
        [ShowInInspector]
        private string VersionInfo => "";

        private string GetVersionInfo()
        {
            return $"Optimization Hub v1.0.0\n" +
                   $"Unity {Application.unityVersion}\n" +
                   $"Editor Build: {Application.buildGUID}\n" +
                   $"Platform: {Application.platform}";
        }

        #endregion

        #region Save Methods (OnValueChanged callbacks)

        private void SaveDefaultExportPath()
        {
            // Already saved via property setter
            if (this.DebugMode) Debug.Log($"[Settings] Export path updated: {this.DefaultExportPath}");
        }

        private void SaveShowConfirmations()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Show confirmations: {this.ShowConfirmationDialogs}");
        }

        private void SaveAutoRefresh()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Auto-refresh: {this.AutoRefreshOnFocus}");
        }

        private void SaveTheme()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Theme changed: {this.Theme}");
        }

        private void SaveAutoRefreshInterval()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Refresh interval: {this.AutoRefreshInterval} minutes");
        }

        private void SaveCacheDuration()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Cache duration: {this.CacheDuration} minutes");
        }

        private void SaveParallelAnalysis()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Parallel analysis: {this.EnableParallelAnalysis}");
        }

        private void SaveMaxItemsPerPage()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Max items per page: {this.MaxItemsPerPage}");
        }

        private void SaveEnableProgressBars()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Progress bars: {this.EnableProgressBars}");
        }

        private void SaveDebugMode()
        {
            Debug.Log($"[Settings] Debug mode: {this.DebugMode}");
        }

        private void SaveExperimentalFeatures()
        {
            if (this.DebugMode) Debug.Log($"[Settings] Experimental features: {this.EnableExperimentalFeatures}");
        }

        #endregion

        #region Public API for Other Modules

        /// <summary>
        /// Gets the configured export directory path.
        /// </summary>
        public static string GetExportPath() => EditorPrefs.GetString(PREF_PREFIX + "ExportPath", "OptimizationReports");

        /// <summary>
        /// Gets whether confirmation dialogs should be shown.
        /// </summary>
        public static bool GetShowConfirmations() => EditorPrefs.GetBool(PREF_PREFIX + "ShowConfirmations", true);

        /// <summary>
        /// Gets the cache duration in minutes.
        /// </summary>
        public static int GetCacheDuration() => EditorPrefs.GetInt(PREF_PREFIX + "CacheDuration", 5);

        /// <summary>
        /// Gets whether parallel analysis is enabled.
        /// </summary>
        public static bool GetParallelAnalysisEnabled() => EditorPrefs.GetBool(PREF_PREFIX + "ParallelAnalysis", true);

        /// <summary>
        /// Gets the maximum items per page for tables.
        /// </summary>
        public static int GetMaxItemsPerPage() => EditorPrefs.GetInt(PREF_PREFIX + "MaxItemsPerPage", 1000);

        /// <summary>
        /// Gets whether progress bars are enabled.
        /// </summary>
        public static bool GetProgressBarsEnabled() => EditorPrefs.GetBool(PREF_PREFIX + "ProgressBars", true);

        /// <summary>
        /// Gets whether debug mode is enabled.
        /// </summary>
        public static bool GetDebugMode() => EditorPrefs.GetBool(PREF_PREFIX + "DebugMode", false);

        /// <summary>
        /// Gets whether experimental features are enabled.
        /// </summary>
        public static bool GetExperimentalFeaturesEnabled() => EditorPrefs.GetBool(PREF_PREFIX + "ExperimentalFeatures", false);

        /// <summary>
        /// Gets the auto-refresh interval in minutes.
        /// </summary>
        public static int GetAutoRefreshInterval() => EditorPrefs.GetInt(PREF_PREFIX + "RefreshInterval", 5);

        /// <summary>
        /// Gets whether auto-refresh on focus is enabled.
        /// </summary>
        public static bool GetAutoRefreshOnFocus() => EditorPrefs.GetBool(PREF_PREFIX + "AutoRefresh", false);

        #endregion

        #region Module Implementation

        protected override void OnAnalyze()
        {
            // Settings module doesn't perform analysis
            Debug.Log("[SettingsModule] Settings don't require analysis. All settings are persisted automatically.");
        }

        protected override void OnRefresh()
        {
            // Force reload from EditorPrefs (in case changed externally)
            Debug.Log("[SettingsModule] Settings refreshed from EditorPrefs.");
        }

        protected override void OnClear()
        {
            // Clear functionality handled by "Clear All Caches" button
            Debug.Log("[SettingsModule] Use 'Clear All Caches' or 'Reset All Settings' buttons.");
        }

        public override void OnActivated()
        {
            base.OnActivated();
            if (this.DebugMode)
            {
                Debug.Log("[SettingsModule] Activated - Loading settings from EditorPrefs");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Deletes all optimization hub preferences.
        /// </summary>
        private void DeleteAllPreferences()
        {
            var allKeys = new[]
            {
                "ExportPath",
                "ShowConfirmations",
                "AutoRefresh",
                "Theme",
                "RefreshInterval",
                "CacheDuration",
                "ParallelAnalysis",
                "MaxItemsPerPage",
                "ProgressBars",
                "DebugMode",
                "ExperimentalFeatures"
            };

            foreach (var key in allKeys)
            {
                EditorPrefs.DeleteKey(PREF_PREFIX + key);
            }
        }

        #endregion

        #region Enums

        /// <summary>
        /// UI theme modes for the optimization hub.
        /// </summary>
        public enum ThemeMode
        {
            Light,
            Dark,
            Auto
        }

        #endregion
    }
}
