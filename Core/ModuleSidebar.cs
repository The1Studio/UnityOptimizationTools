using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace TheOne.UITemplate.Editor.Optimization
{
    using Sirenix.Utilities.Editor;
    using UnityEngine;

    /// <summary>
    /// Left sidebar component for module navigation and search.
    /// Displays a filterable list of available optimization modules.
    /// </summary>
    public class ModuleSidebar
    {
        /// <summary>
        /// Event fired when a module is selected.
        /// </summary>
        public event Action<string> OnModuleSelected;

        /// <summary>
        /// Currently selected module name.
        /// </summary>
        public string SelectedModule { get; private set; } = "Overview";

        /// <summary>
        /// List of all available modules.
        /// </summary>
        private List<ModuleInfo> modules = new List<ModuleInfo>
        {
            new ModuleInfo("Overview", "📊", "Dashboard and quick stats"),
            new ModuleInfo("Textures", "🖼", "Texture optimization"),
            new ModuleInfo("Addressables", "📦", "Addressable groups"),
            new ModuleInfo("Audio", "🔊", "Audio compression"),
            new ModuleInfo("Mesh", "🎨", "Mesh optimization"),
            new ModuleInfo("Shader", "🌈", "Shader analysis"),
            new ModuleInfo("Font", "🔤", "Font optimization"),
            new ModuleInfo("Reports", "📄", "Export reports"),
            new ModuleInfo("Settings", "⚙", "Global settings"),
        };

        /// <summary>
        /// Filtered list of modules based on search input.
        /// </summary>
        private List<ModuleInfo> FilteredModules =>
            string.IsNullOrEmpty(this.searchFilter)
                ? this.modules
                : this.modules.Where(m => m.Name.ToLower().Contains(this.searchFilter.ToLower())).ToList();

        /// <summary>
        /// Search filter text.
        /// </summary>
        [HideInInspector]
        private string searchFilter = "";

        /// <summary>
        /// Draws the module list with clickable buttons.
        /// </summary>
        [OnInspectorGUI]
        private void DrawModuleList()
        {
            // Draw search bar with better styling
            SirenixEditorGUI.BeginBox();
            UnityEditor.EditorGUILayout.LabelField("🔍 Search Modules", UnityEditor.EditorStyles.boldLabel);
            this.searchFilter = SirenixEditorGUI.ToolbarSearchField(this.searchFilter);
            SirenixEditorGUI.EndBox();

            UnityEditor.EditorGUILayout.Space(10);

            // Draw module list
            foreach (var module in this.FilteredModules)
            {
                var isSelected = module.Name == this.SelectedModule;

                // Use colored box for selected module
                var originalColor = UnityEngine.GUI.backgroundColor;
                if (isSelected)
                {
                    UnityEngine.GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                }

                SirenixEditorGUI.BeginBox();
                using (new UnityEditor.EditorGUILayout.HorizontalScope())
                {
                    // Icon with larger size
                    var iconStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label);
                    iconStyle.fontSize = 20;
                    iconStyle.alignment = UnityEngine.TextAnchor.MiddleCenter;
                    UnityEngine.GUILayout.Label(module.Icon, iconStyle, UnityEngine.GUILayout.Width(40), UnityEngine.GUILayout.Height(30));

                    // Module name
                    UnityEngine.GUILayout.Label(module.Name, isSelected ? UnityEditor.EditorStyles.boldLabel : UnityEditor.EditorStyles.label);

                    UnityEngine.GUILayout.FlexibleSpace();

                    // Arrow button
                    if (UnityEngine.GUILayout.Button("→", UnityEngine.GUILayout.Width(30), UnityEngine.GUILayout.Height(25)))
                    {
                        this.SelectModule(module.Name);
                    }
                }

                // Show module description on hover
                var rect = UnityEngine.GUILayoutUtility.GetLastRect();
                UnityEditor.EditorGUI.LabelField(rect, "", module.Description);

                SirenixEditorGUI.EndBox();

                UnityEngine.GUI.backgroundColor = originalColor;

                UnityEditor.EditorGUILayout.Space(2);
            }
        }

        /// <summary>
        /// Programmatically selects a module by name.
        /// </summary>
        /// <param name="moduleName">Name of the module to select</param>
        public void SelectModule(string moduleName)
        {
            this.SelectedModule = moduleName;
            this.OnModuleSelected?.Invoke(moduleName);
        }
    }
}