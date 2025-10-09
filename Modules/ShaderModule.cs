using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using TheOne.Tool.Optimization.Models;
using TheOne.UITemplate.Editor.Optimization.Services;
using UnityEditor;
using UnityEngine;

namespace TheOne.UITemplate.Editor.Optimization.Modules
{
    /// <summary>
    /// Shader Module - Wraps existing ShaderModule logic.
    /// Provides shader variant analysis, material tracking, and shader replacement tools.
    /// NO CODE DUPLICATION - reuses all logic from ShaderModule.
    /// </summary>
    public class ShaderModule : OptimizationModuleBase
    {
        public override string ModuleName => "Shader Analysis";
        public override string ModuleIcon => "🌈";

        // Use service instead of window reference - NO REFLECTION
        private readonly ShaderAnalysisService analysisService = new ShaderAnalysisService();

        #region Tab: Summary

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [Title("Shader Statistics", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Total Shaders")]
        private string TotalShadersDisplay => $"🌈 {this.cachedShaderInfos.Count:N0} shaders";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Total Materials")]
        [GUIColor(0.6f, 0.8f, 1f)]
        private string TotalMaterialsDisplay => $"📦 {this.GetTotalMaterialCount():N0} materials";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Built-in Shaders")]
        [GUIColor(1f, 0.9f, 0.6f)]
        private string BuiltInShadersDisplay => $"⚠️ {this.GetBuiltInShaderCount():N0} legacy shaders";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Issues")]
        [Title("Quick Actions", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Shader analysis helps identify shader complexity, variant counts, and material usage patterns.", InfoMessageType.Info)]
        [PropertySpace(10)]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Analyze Shaders", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void AnalyzeShadersSummary()
        {
            this.RefreshShaderData();
        }

        // Removed: OpenShaderWindowSummary - functionality integrated into this module

        private int GetBuiltInShaderCount()
        {
            return this.cachedShaderInfos.Count(s => s.OriginalShader != null &&
                (s.OriginalShader.name.StartsWith("Standard") ||
                 s.OriginalShader.name.StartsWith("Legacy") ||
                 s.OriginalShader.name.StartsWith("Mobile")));
        }

        #endregion

        #region Tab Groups

        [TabGroup("Tabs", "Variants")]
        [Title("Shader Variants", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("$GetVariantsSummary", InfoMessageType.Info)]
        [PropertySpace(10)]
        [ShowInInspector]
        [HideLabel]
        private string variantsInfoPlaceholder => "";

        [TabGroup("Tabs", "Variants")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20, AlwaysExpanded = true, DrawScrollView = true)]
        [ShowInInspector]
        [HideLabel]
        private List<ShaderMaterialInfo> cachedShaderInfos = new List<ShaderMaterialInfo>();

        [TabGroup("Tabs", "Analysis")]
        [TitleGroup("Tabs/Analysis/Shader Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Total Shaders")]
        private string TotalShaders => $"🌈 {this.cachedShaderInfos.Count:N0} shaders";

        [TabGroup("Tabs", "Analysis")]
        [TitleGroup("Tabs/Analysis/Shader Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Total Materials")]
        private string TotalMaterials => $"📦 {this.GetTotalMaterialCount():N0} materials";

        [TabGroup("Tabs", "Analysis")]
        [TitleGroup("Tabs/Analysis/Shader Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Unique Shaders")]
        private string UniqueShaders => $"✨ {this.cachedShaderInfos.Count:N0} unique shaders";

        [TabGroup("Tabs", "Analysis")]
        [TitleGroup("Tabs/Analysis/Optimization Suggestions")]
        [InfoBox("$GetOptimizationSuggestions", InfoMessageType.Warning, VisibleIf = "HasSuggestions")]
        [HideInInspector]
        public bool HasSuggestions => this.cachedShaderInfos.Count > 0;

        [TabGroup("Tabs", "Tools")]
        [TitleGroup("Tabs/Tools/Shader Replacement")]
        [InfoBox("Use the table in the 'Variants' tab to replace shaders. " +
                 "Select a replacement shader, choose materials, and click 'Replace Shader'.", InfoMessageType.Info)]
        [ShowInInspector, HideLabel, ReadOnly]
        private string ShaderReplacementInfo => "";

        [TabGroup("Tabs", "Tools")]
        [TitleGroup("Tabs/Tools/Quick Actions")]
        [Button("Find Unused Shaders", ButtonSizes.Medium)]
        [GUIColor(1f, 0.8f, 0.5f)]
        private void FindUnusedShaders()
        {
            EditorUtility.DisplayProgressBar("Finding Unused Shaders", "Scanning project...", 0.5f);

            try
            {
                // Get all shaders in the project
                var allShaderGuids = AssetDatabase.FindAssets("t:Shader");
                var allShaders = new HashSet<Shader>();

                foreach (var guid in allShaderGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (shader != null)
                    {
                        allShaders.Add(shader);
                    }
                }

                // Get all used shaders from cached analysis
                var usedShaders = new HashSet<Shader>();
                foreach (var shaderInfo in this.cachedShaderInfos)
                {
                    if (shaderInfo.OriginalShader != null)
                    {
                        usedShaders.Add(shaderInfo.OriginalShader);
                    }
                }

                // Find unused shaders
                var unusedShaders = allShaders.Except(usedShaders).ToList();

                // Exclude built-in Unity shaders
                unusedShaders = unusedShaders.Where(s =>
                    !s.name.StartsWith("Hidden/") &&
                    !s.name.StartsWith("Legacy Shaders/") &&
                    !s.name.StartsWith("Standard") &&
                    AssetDatabase.GetAssetPath(s).StartsWith("Assets/")
                ).ToList();

                EditorUtility.ClearProgressBar();

                if (unusedShaders.Count == 0)
                {
                    EditorUtility.DisplayDialog("Unused Shaders",
                        "No unused shaders found!\n\nAll project shaders are being used by materials.",
                        "OK");
                }
                else
                {
                    var message = $"Found {unusedShaders.Count} unused shaders.\n\n" +
                                  $"These shaders are not referenced by any materials in your project.\n\n" +
                                  $"Select them in the Project window?";

                    if (EditorUtility.DisplayDialog("Unused Shaders Found", message, "Yes, Select", "Cancel"))
                    {
                        Selection.objects = unusedShaders.ToArray();
                    }

                    Debug.Log($"[ShaderModule] Found {unusedShaders.Count} unused shaders:\n" +
                              string.Join("\n", unusedShaders.Select(s => s.name)));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [TabGroup("Tabs", "Tools")]
        [TitleGroup("Tabs/Tools/Quick Actions")]
        [Button("Analyze Shader Complexity", ButtonSizes.Medium)]
        [GUIColor(0.8f, 0.7f, 1f)]
        private void AnalyzeComplexity()
        {
            if (this.cachedShaderInfos == null || this.cachedShaderInfos.Count == 0)
            {
                EditorUtility.DisplayDialog("No Data",
                    "Please analyze shaders first (click Analyze or Refresh button).",
                    "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Analyzing Shader Complexity", "Processing shaders...", 0.5f);

            try
            {
                var complexityData = new List<ShaderComplexityInfo>();

                foreach (var shaderInfo in this.cachedShaderInfos)
                {
                    if (shaderInfo.OriginalShader == null) continue;

                    var shader = shaderInfo.OriginalShader;
                    var path = AssetDatabase.GetAssetPath(shader);

                    // Skip built-in shaders
                    if (!path.StartsWith("Assets/")) continue;

                    var info = new ShaderComplexityInfo
                    {
                        ShaderName = shader.name,
                        Shader = shader,
                        PassCount = shader.passCount,
                        RenderQueue = shader.renderQueue,
                        MaterialCount = shaderInfo.GetMaterialCount()
                    };

                    // Analyze shader file for keywords and complexity indicators
                    if (System.IO.File.Exists(path))
                    {
                        var shaderCode = System.IO.File.ReadAllText(path);

                        // Count common complexity indicators
                        info.HasAlphaBlending = shaderCode.Contains("Blend ") || shaderCode.Contains("BlendOp");
                        info.HasTransparency = shaderCode.Contains("Queue") && shaderCode.Contains("Transparent");
                        info.UsesGrabPass = shaderCode.Contains("GrabPass");
                        info.LineCount = shaderCode.Split('\n').Length;

                        // Count shader properties
                        var propertiesMatch = System.Text.RegularExpressions.Regex.Matches(shaderCode, @"Properties\s*\{([^}]*)\}");
                        if (propertiesMatch.Count > 0)
                        {
                            var propertiesBlock = propertiesMatch[0].Groups[1].Value;
                            info.PropertyCount = System.Text.RegularExpressions.Regex.Matches(propertiesBlock, @"\w+\s*\(""[^""]*"",").Count;
                        }

                        // Estimate complexity score (0-100)
                        info.ComplexityScore = this.CalculateComplexityScore(info);
                    }

                    complexityData.Add(info);
                }

                EditorUtility.ClearProgressBar();

                // Sort by complexity (highest first)
                complexityData = complexityData.OrderByDescending(c => c.ComplexityScore).ToList();

                // Display results
                var highComplexity = complexityData.Where(c => c.ComplexityScore > 70).ToList();
                var mediumComplexity = complexityData.Where(c => c.ComplexityScore > 40 && c.ComplexityScore <= 70).ToList();
                var lowComplexity = complexityData.Where(c => c.ComplexityScore <= 40).ToList();

                var message = $"Shader Complexity Analysis Complete!\n\n" +
                              $"Total Shaders Analyzed: {complexityData.Count}\n\n" +
                              $"🔴 High Complexity (>70): {highComplexity.Count}\n" +
                              $"🟡 Medium Complexity (40-70): {mediumComplexity.Count}\n" +
                              $"🟢 Low Complexity (<40): {lowComplexity.Count}\n\n";

                if (highComplexity.Count > 0)
                {
                    message += $"Most complex shader:\n" +
                               $"  • {highComplexity[0].ShaderName}\n" +
                               $"  • Score: {highComplexity[0].ComplexityScore:F0}\n" +
                               $"  • {highComplexity[0].PassCount} passes, {highComplexity[0].LineCount} lines\n";
                }

                EditorUtility.DisplayDialog("Shader Complexity Analysis", message, "OK");

                // Log detailed results
                Debug.Log("[ShaderModule] Shader Complexity Analysis:\n" +
                          "=== HIGH COMPLEXITY ===\n" +
                          string.Join("\n", highComplexity.Take(5).Select(c =>
                              $"  {c.ShaderName}: {c.ComplexityScore:F0} (Passes: {c.PassCount}, Lines: {c.LineCount})")) +
                          "\n\n=== MEDIUM COMPLEXITY ===\n" +
                          string.Join("\n", mediumComplexity.Take(5).Select(c =>
                              $"  {c.ShaderName}: {c.ComplexityScore:F0} (Passes: {c.PassCount}, Lines: {c.LineCount})"))
                );
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private float CalculateComplexityScore(ShaderComplexityInfo info)
        {
            float score = 0;

            // Pass count contributes significantly
            score += info.PassCount * 15f;

            // Line count (normalized)
            score += (info.LineCount / 100f) * 10f;

            // Property count
            score += info.PropertyCount * 2f;

            // Special features add complexity
            if (info.HasAlphaBlending) score += 10f;
            if (info.HasTransparency) score += 15f;
            if (info.UsesGrabPass) score += 25f; // GrabPass is expensive

            // Material count (more materials = more important to optimize)
            score += Mathf.Log(info.MaterialCount + 1) * 5f;

            return Mathf.Clamp(score, 0, 100);
        }

        private class ShaderComplexityInfo
        {
            public string ShaderName;
            public Shader Shader;
            public int PassCount;
            public int RenderQueue;
            public int MaterialCount;
            public int LineCount;
            public int PropertyCount;
            public bool HasAlphaBlending;
            public bool HasTransparency;
            public bool UsesGrabPass;
            public float ComplexityScore;
        }

        #endregion

        #region Module Implementation

        protected override void OnAnalyze()
        {
            EditorUtility.DisplayProgressBar("Shader Analysis", "Analyzing shaders...", 0.5f);
            try
            {
                this.RefreshShaderData();
                Debug.Log($"[ShaderModule] Analysis complete. Found {this.cachedShaderInfos.Count} unique shaders with {this.GetTotalMaterialCount()} materials.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        protected override void OnRefresh()
        {
            this.RefreshShaderData();
        }

        protected override void OnClear()
        {
            this.cachedShaderInfos.Clear();
            Debug.Log("[ShaderModule] Shader data cleared.");
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-refresh if no data
            if (this.cachedShaderInfos == null || this.cachedShaderInfos.Count == 0)
            {
                this.RefreshShaderData();
            }
        }

        public override void OnDeactivated()
        {
            base.OnDeactivated();
            // Data stays cached for reuse
        }

        #endregion

        #region Data Management (Using Service)

        /// <summary>
        /// Refreshes shader data using ShaderAnalysisService.
        /// NO CODE DUPLICATION - uses shared service.
        /// </summary>
        private void RefreshShaderData()
        {
            this.cachedShaderInfos = this.analysisService.FindAllShadersAndMaterials();
        }

        #endregion

        #region Helper Methods

        private string GetVariantsSummary()
        {
            if (this.cachedShaderInfos == null || this.cachedShaderInfos.Count == 0)
            {
                return "No shader data available. Click 'Refresh' to scan the project.";
            }

            return $"Found {this.cachedShaderInfos.Count} unique shaders used across {this.GetTotalMaterialCount()} materials in your project.";
        }

        private int GetTotalMaterialCount()
        {
            if (this.cachedShaderInfos == null) return 0;

            int count = 0;
            foreach (var shaderInfo in this.cachedShaderInfos)
            {
                count += shaderInfo.GetMaterialCount();
            }
            return count;
        }

        private string GetOptimizationSuggestions()
        {
            var suggestions = new List<string>();

            if (this.cachedShaderInfos.Count > 50)
            {
                suggestions.Add($"• You have {this.cachedShaderInfos.Count} unique shaders. Consider consolidating similar shaders to reduce build size.");
            }

            if (this.GetTotalMaterialCount() > 200)
            {
                suggestions.Add($"• You have {this.GetTotalMaterialCount()} materials. Consider material sharing to reduce draw calls.");
            }

            // Check for built-in shaders (Standard, etc.)
            int builtInCount = 0;
            foreach (var shaderInfo in this.cachedShaderInfos)
            {
                if (shaderInfo.OriginalShader != null &&
                    (shaderInfo.OriginalShader.name.StartsWith("Standard") ||
                     shaderInfo.OriginalShader.name.StartsWith("Legacy")))
                {
                    builtInCount++;
                }
            }

            if (builtInCount > 0)
            {
                suggestions.Add($"• Found {builtInCount} built-in shaders. Consider replacing with optimized custom shaders for mobile.");
            }

            if (suggestions.Count == 0)
            {
                return "Shader usage looks good! No major optimization suggestions at this time.";
            }

            return "Optimization Opportunities:\n\n" + string.Join("\n", suggestions);
        }

        #endregion
    }
}