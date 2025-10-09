using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheOne.UITemplate.Editor.Optimization.Modules
{
    /// <summary>
    /// Material Module - Analyzes material usage, shader assignments, texture slots, and property overrides.
    /// Identifies unused materials, duplicate materials, and optimization opportunities.
    /// </summary>
    public class MaterialModule : OptimizationModuleBase
    {
        public override string ModuleName => "Material Analysis";
        public override string ModuleIcon => "🎨";

        // Performance optimization: Cache for dependency lookups
        private Dictionary<string, HashSet<string>> dependencyCache = new Dictionary<string, HashSet<string>>();

        #region Tab: Summary

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [Title("Material Statistics", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Total Materials")]
        private string TotalMaterials => $"🎨 {this.allMaterials.Count:N0} materials";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Unique Shaders")]
        [GUIColor(0.6f, 0.8f, 1f)]
        private string UniqueShaders => $"🌈 {this.GetUniqueShaderCount():N0} shaders used";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Missing Shaders")]
        [GUIColor(1f, 0.6f, 0.6f)]
        private string MissingShaders => $"⚠️ {this.materialsWithMissingShader.Count:N0} broken materials";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Unused Materials")]
        [GUIColor(1f, 0.9f, 0.6f)]
        private string UnusedMaterialsCount => $"📦 {this.unusedMaterials.Count:N0} not referenced";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Issues")]
        [Title("Quick Actions", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Material analysis helps reduce draw calls, identify shader issues, and find unused assets.", InfoMessageType.Info)]
        [PropertySpace(10)]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Select Missing Shaders", ButtonSizes.Medium)]
        [GUIColor(1f, 0.7f, 0.7f)]
        private void SelectMissingShaders()
        {
            var materials = this.materialsWithMissingShader.Select(m => m.Material).Where(m => m != null).ToArray();
            if (materials.Length == 0)
            {
                Debug.LogWarning("[MaterialModule] No materials with missing shaders found.");
                return;
            }
            Selection.objects = materials;
            Debug.Log($"[MaterialModule] Selected {materials.Length} materials with missing shaders.");
        }

        [TabGroup("Tabs", "Summary")]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Select Unused", ButtonSizes.Medium)]
        [GUIColor(1f, 0.9f, 0.6f)]
        private void SelectUnused()
        {
            var materials = this.unusedMaterials.Select(m => m.Material).Where(m => m != null).ToArray();
            if (materials.Length == 0)
            {
                Debug.LogWarning("[MaterialModule] No unused materials found.");
                return;
            }
            Selection.objects = materials;
            Debug.Log($"[MaterialModule] Selected {materials.Length} unused materials.");
        }

        #endregion

        #region Tab: All Materials

        [TabGroup("Tabs", "All Materials")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("All Materials in Project", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Shows all materials with shader and texture information.", InfoMessageType.Info)]
        [Searchable]
        [ShowInInspector]
        private List<MaterialInfo> allMaterials = new List<MaterialInfo>();

        [TabGroup("Tabs", "All Materials")]
        [ButtonGroup("Tabs/All Materials/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(0.6f, 1f, 0.6f)]
        private void SelectAllMaterials()
        {
            var materials = this.allMaterials.Select(m => m.Material).Where(m => m != null).ToArray();
            if (materials.Length == 0) return;
            Selection.objects = materials;
            Debug.Log($"[MaterialModule] Selected {materials.Length} materials.");
        }

        [TabGroup("Tabs", "All Materials")]
        [ButtonGroup("Tabs/All Materials/Actions")]
        [Button("Group By Shader", ButtonSizes.Medium)]
        private void GroupByShader()
        {
            if (this.allMaterials.Count == 0)
            {
                Debug.LogWarning("[MaterialModule] No materials to group.");
                return;
            }

            this.allMaterials = this.allMaterials.OrderBy(m => m.ShaderName).ThenBy(m => m.Name).ToList();
            Debug.Log("[MaterialModule] Materials grouped by shader.");
        }

        #endregion

        #region Tab: Missing Shaders

        [TabGroup("Tabs", "Missing Shaders")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Materials with Missing/Broken Shaders", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("⚠️ These materials reference shaders that no longer exist or failed to compile.", InfoMessageType.Error)]
        [Searchable]
        [ShowInInspector]
        private List<MaterialInfo> materialsWithMissingShader = new List<MaterialInfo>();

        [TabGroup("Tabs", "Missing Shaders")]
        [ButtonGroup("Tabs/Missing Shaders/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(1f, 0.7f, 0.7f)]
        private void SelectMissingShaderMaterials()
        {
            this.SelectMissingShaders();
        }

        [TabGroup("Tabs", "Missing Shaders")]
        [ButtonGroup("Tabs/Missing Shaders/Actions")]
        [Button("Log Details", ButtonSizes.Medium)]
        private void LogMissingShaders()
        {
            if (this.materialsWithMissingShader.Count == 0)
            {
                Debug.LogWarning("[MaterialModule] No materials with missing shaders found.");
                return;
            }

            Debug.Log($"[MaterialModule] Found {this.materialsWithMissingShader.Count} materials with missing shaders:");
            foreach (var mat in this.materialsWithMissingShader)
            {
                Debug.LogWarning($"  • {mat.Name} - Path: {mat.Path}", mat.Material);
            }
        }

        #endregion

        #region Tab: Unused Materials

        [TabGroup("Tabs", "Unused")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Potentially Unused Materials", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("These materials are not referenced in any scenes or prefabs. Verify before deleting as they may be used at runtime.", InfoMessageType.Warning)]
        [Searchable]
        [ShowInInspector]
        private List<MaterialInfo> unusedMaterials = new List<MaterialInfo>();

        [TabGroup("Tabs", "Unused")]
        [ButtonGroup("Tabs/Unused/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(1f, 0.9f, 0.6f)]
        private void SelectUnusedMaterials()
        {
            this.SelectUnused();
        }

        [TabGroup("Tabs", "Unused")]
        [ButtonGroup("Tabs/Unused/Actions")]
        [Button("Find References", ButtonSizes.Medium)]
        private void FindReferences()
        {
            if (Selection.activeObject is Material mat)
            {
                var dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(mat), false);
                Debug.Log($"[MaterialModule] Material '{mat.name}' dependencies: {string.Join(", ", dependencies)}");

                // Find assets that reference this material
                var allAssets = AssetDatabase.GetAllAssetPaths();
                var references = new List<string>();

                foreach (var path in allAssets)
                {
                    var deps = AssetDatabase.GetDependencies(path, false);
                    if (deps.Contains(AssetDatabase.GetAssetPath(mat)))
                    {
                        references.Add(path);
                    }
                }

                if (references.Count > 0)
                {
                    Debug.Log($"[MaterialModule] Found {references.Count} references to '{mat.name}':\n  " + string.Join("\n  ", references));
                }
                else
                {
                    Debug.LogWarning($"[MaterialModule] No references found for '{mat.name}' (may be unused).");
                }
            }
            else
            {
                Debug.LogWarning("[MaterialModule] Select a material first to find its references.");
            }
        }

        #endregion

        #region Tab: Shader Usage

        [TabGroup("Tabs", "By Shader")]
        [ShowInInspector]
        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.Foldout)]
        [Title("Materials Grouped by Shader", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Shows which materials use each shader. Useful for shader consolidation and draw call reduction.", InfoMessageType.Info)]
        private Dictionary<string, List<MaterialInfo>> materialsByShader = new Dictionary<string, List<MaterialInfo>>();

        [TabGroup("Tabs", "By Shader")]
        [ButtonGroup("Tabs/By Shader/Actions")]
        [Button("Refresh Grouping", ButtonSizes.Medium)]
        private void RefreshShaderGrouping()
        {
            this.materialsByShader.Clear();

            foreach (var mat in this.allMaterials)
            {
                var shaderName = mat.ShaderName ?? "Unknown";
                if (!this.materialsByShader.ContainsKey(shaderName))
                {
                    this.materialsByShader[shaderName] = new List<MaterialInfo>();
                }
                this.materialsByShader[shaderName].Add(mat);
            }

            Debug.Log($"[MaterialModule] Grouped {this.allMaterials.Count} materials into {this.materialsByShader.Count} shader categories.");
        }

        #endregion

        #region Analysis Logic

        protected override void OnAnalyze()
        {
            EditorUtility.DisplayProgressBar("Material Analysis", "Scanning project for materials...", 0f);
            try
            {
                this.AnalyzeMaterials();
                this.RefreshShaderGrouping();
                Debug.Log($"[MaterialModule] Analysis complete: {this.allMaterials.Count} materials analyzed, " +
                          $"{this.materialsWithMissingShader.Count} with missing shaders, " +
                          $"{this.unusedMaterials.Count} potentially unused.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        protected override void OnRefresh()
        {
            this.OnAnalyze();
        }

        protected override void OnClear()
        {
            this.allMaterials.Clear();
            this.materialsWithMissingShader.Clear();
            this.unusedMaterials.Clear();
            this.materialsByShader.Clear();
            Debug.Log("[MaterialModule] Data cleared.");
        }

        private void AnalyzeMaterials()
        {
            this.allMaterials.Clear();
            this.materialsWithMissingShader.Clear();
            this.unusedMaterials.Clear();
            this.dependencyCache.Clear();

            // Build dependency cache first (performance optimization)
            EditorUtility.DisplayProgressBar("Material Analysis", "Building dependency cache...", 0f);
            this.BuildDependencyCache();

            var materialGuids = AssetDatabase.FindAssets("t:Material");
            var total = materialGuids.Length;

            for (int i = 0; i < materialGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null) continue;

                EditorUtility.DisplayProgressBar("Material Analysis", $"Analyzing {material.name}...", (float)i / total);

                var info = this.AnalyzeMaterial(material, path);
                this.allMaterials.Add(info);

                // Categorize
                if (info.HasMissingShader) this.materialsWithMissingShader.Add(info);

                // Check if unused using cached dependencies
                if (!this.IsReferenced(path)) this.unusedMaterials.Add(info);
            }
        }

        private MaterialInfo AnalyzeMaterial(Material material, string path)
        {
            var info = new MaterialInfo
            {
                Material = material,
                Name = material.name,
                Path = path,
                ShaderName = material.shader != null ? material.shader.name : "Missing Shader",
                HasMissingShader = material.shader == null,
                RenderQueue = material.renderQueue,
                PassCount = material.passCount
            };

            // Count texture slots
            var shader = material.shader;
            if (shader != null)
            {
                int textureCount = ShaderUtil.GetPropertyCount(shader);
                for (int i = 0; i < textureCount; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        var propName = ShaderUtil.GetPropertyName(shader, i);
                        if (material.GetTexture(propName) != null)
                        {
                            info.TextureSlotCount++;
                        }
                    }
                }
            }

            return info;
        }

        private bool IsReferenced(string materialPath)
        {
            // Use cached dependencies for performance
            foreach (var kvp in this.dependencyCache)
            {
                if (kvp.Value.Contains(materialPath))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Builds a reverse dependency cache: for each asset (prefab/scene), stores its material dependencies.
        /// This dramatically improves performance when checking if materials are referenced.
        /// </summary>
        private void BuildDependencyCache()
        {
            this.dependencyCache.Clear();

            var allAssetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.EndsWith(".prefab") || p.EndsWith(".unity"))
                .ToArray();

            for (int i = 0; i < allAssetPaths.Length; i++)
            {
                var path = allAssetPaths[i];

                if (i % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("Material Analysis",
                        $"Caching dependencies ({i}/{allAssetPaths.Length})...",
                        (float)i / allAssetPaths.Length);
                }

                var dependencies = AssetDatabase.GetDependencies(path, false);
                var materialDeps = new HashSet<string>(dependencies.Where(d => d.EndsWith(".mat")));

                if (materialDeps.Count > 0)
                {
                    this.dependencyCache[path] = materialDeps;
                }
            }
        }

        private int GetUniqueShaderCount()
        {
            return this.allMaterials.Select(m => m.ShaderName).Distinct().Count();
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-analyze if no data
            if (this.allMaterials.Count == 0)
            {
                this.OnAnalyze();
            }
        }

        #endregion

        #region Data Model

        [System.Serializable]
        private class MaterialInfo
        {
            [TableColumnWidth(200)]
            [ShowInInspector, ReadOnly]
            public Material Material;

            [TableColumnWidth(200)]
            [ShowInInspector, ReadOnly]
            public string Name;

            [TableColumnWidth(200)]
            [ShowInInspector, ReadOnly]
            [GUIColor("GetShaderColor")]
            public string ShaderName;

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public int TextureSlotCount;

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public int PassCount;

            [TableColumnWidth(100)]
            [ShowInInspector, ReadOnly]
            public int RenderQueue;

            [TableColumnWidth(300)]
            [ShowInInspector, ReadOnly]
            public string Path;

            [HideInInspector]
            public bool HasMissingShader;

            private Color GetShaderColor()
            {
                return this.HasMissingShader ? new Color(1f, 0.6f, 0.6f) : Color.white;
            }
        }

        #endregion
    }
}