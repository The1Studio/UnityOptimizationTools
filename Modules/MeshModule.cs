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
    /// Mesh Optimization Module - Wraps AddressableMeshFinderOdin logic.
    /// Uses AssetAnalysisService to avoid code duplication.
    /// Provides compression analysis and bulk optimization features.
    /// </summary>
    public class MeshModule : OptimizationModuleBase
    {
        public override string ModuleName => "Mesh Optimization";
        public override string ModuleIcon => "🎨";

        private readonly AssetAnalysisService analysisService = new AssetAnalysisService();

        // Cached mesh data organized by compression level
        private Dictionary<ModelImporterMeshCompression, List<MeshInfo>> meshesByCompression;

        #region Compression Tab

        [TabGroup("Compression")]
        [TitleGroup("Compression/Off Compression Meshes", "Meshes with compression disabled - highest quality but largest file size")]
        [ShowInInspector]
        [TableList(ShowIndexLabels = true, AlwaysExpanded = true)]
        [PropertyOrder(1)]
        private List<MeshInfo> OffCompressionMeshes => this.meshesByCompression?[ModelImporterMeshCompression.Off] ?? new List<MeshInfo>();

        [TabGroup("Compression")]
        [TitleGroup("Compression/Off Compression Meshes")]
        [Button("Compress All to High", ButtonSizes.Medium)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [PropertyOrder(2)]
        [EnableIf("@OffCompressionMeshes.Count > 0")]
        private void CompressAllOffToHigh()
        {
            this.CompressMeshes(this.OffCompressionMeshes, ModelImporterMeshCompression.High, "Compressing Off → High");
        }

        [TabGroup("Compression")]
        [TitleGroup("Compression/Low Compression Meshes", "Meshes with low compression - moderate quality and size")]
        [ShowInInspector]
        [TableList(ShowIndexLabels = true, AlwaysExpanded = true)]
        [PropertyOrder(3)]
        private List<MeshInfo> LowCompressionMeshes => this.meshesByCompression?[ModelImporterMeshCompression.Low] ?? new List<MeshInfo>();

        [TabGroup("Compression")]
        [TitleGroup("Compression/Low Compression Meshes")]
        [Button("Compress to High", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 0.4f)]
        [PropertyOrder(4)]
        [EnableIf("@LowCompressionMeshes.Count > 0")]
        private void CompressAllLowToHigh()
        {
            this.CompressMeshes(this.LowCompressionMeshes, ModelImporterMeshCompression.High, "Compressing Low → High");
        }

        [TabGroup("Compression")]
        [TitleGroup("Compression/Medium Compression Meshes", "Meshes with medium compression - balanced quality and size")]
        [ShowInInspector]
        [TableList(ShowIndexLabels = true, AlwaysExpanded = true)]
        [PropertyOrder(5)]
        private List<MeshInfo> MediumCompressionMeshes => this.meshesByCompression?[ModelImporterMeshCompression.Medium] ?? new List<MeshInfo>();

        [TabGroup("Compression")]
        [TitleGroup("Compression/Medium Compression Meshes")]
        [Button("Compress to High", ButtonSizes.Medium)]
        [GUIColor(0.5f, 0.8f, 0.5f)]
        [PropertyOrder(6)]
        [EnableIf("@MediumCompressionMeshes.Count > 0")]
        private void CompressAllMediumToHigh()
        {
            this.CompressMeshes(this.MediumCompressionMeshes, ModelImporterMeshCompression.High, "Compressing Medium → High");
        }

        [TabGroup("Compression")]
        [TitleGroup("Compression/High Compression Meshes", "Meshes with high compression - smallest file size")]
        [ShowInInspector]
        [TableList(ShowIndexLabels = true, AlwaysExpanded = true)]
        [PropertyOrder(7)]
        private List<MeshInfo> HighCompressionMeshes => this.meshesByCompression?[ModelImporterMeshCompression.High] ?? new List<MeshInfo>();

        #endregion

        #region Settings Tab

        [TabGroup("Settings")]
        [TitleGroup("Settings/Mesh Import Settings", "Configure mesh import optimization settings")]
        [ShowInInspector]
        [LabelText("Target Compression Level")]
        [EnumToggleButtons]
        [PropertyOrder(1)]
        private ModelImporterMeshCompression targetCompression = ModelImporterMeshCompression.High;

        [TabGroup("Settings")]
        [TitleGroup("Settings/Mesh Import Settings")]
        [ShowInInspector]
        [LabelText("Target Animation Compression")]
        [EnumToggleButtons]
        [PropertyOrder(2)]
        private ModelImporterAnimationCompression targetAnimationCompression = ModelImporterAnimationCompression.Optimal;

        [TabGroup("Settings")]
        [TitleGroup("Settings/Bulk Operations", "Apply settings to multiple meshes at once")]
        [Button("Apply Target Settings to All Off Compression", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 1f)]
        [PropertyOrder(3)]
        [EnableIf("@OffCompressionMeshes.Count > 0")]
        private void ApplySettingsToOffCompression()
        {
            var meshes = this.OffCompressionMeshes;
            this.CompressMeshes(meshes, this.targetCompression, "Applying settings to Off compression meshes", this.targetAnimationCompression);
        }

        [TabGroup("Settings")]
        [TitleGroup("Settings/Bulk Operations")]
        [Button("Apply Target Settings to All Non-High Compression", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 1f)]
        [PropertyOrder(4)]
        [EnableIf("@(OffCompressionMeshes.Count + LowCompressionMeshes.Count + MediumCompressionMeshes.Count) > 0")]
        private void ApplySettingsToNonHighCompression()
        {
            var meshes = new List<MeshInfo>();
            meshes.AddRange(this.OffCompressionMeshes);
            meshes.AddRange(this.LowCompressionMeshes);
            meshes.AddRange(this.MediumCompressionMeshes);
            this.CompressMeshes(meshes, this.targetCompression, "Applying settings to all non-High compression meshes", this.targetAnimationCompression);
        }

        #endregion

        #region Analysis Tab

        [TabGroup("Analysis")]
        [TitleGroup("Analysis/Mesh Statistics", "Overview of mesh compression distribution")]
        [ShowInInspector]
        [PropertyOrder(1)]
        [ReadOnly, GUIColor(1, 1, 1)]
        [LabelText("Total Meshes")]
        private string TotalMeshCount =>
            this.meshesByCompression != null
            ? $"🎨 {this.meshesByCompression.Values.Sum(list => list.Count):N0} meshes"
            : "Click Analyze";

        [TabGroup("Analysis")]
        [TitleGroup("Analysis/Mesh Statistics")]
        [ShowInInspector]
        [PropertyOrder(2)]
        [ReadOnly]
        [LabelText("Off Compression")]
        [GUIColor(1f, 0.5f, 0.5f)]
        private string OffCount =>
            this.meshesByCompression != null
            ? $"⚠️ {this.OffCompressionMeshes.Count:N0} meshes"
            : "-";

        [TabGroup("Analysis")]
        [TitleGroup("Analysis/Mesh Statistics")]
        [ShowInInspector]
        [PropertyOrder(3)]
        [ReadOnly]
        [LabelText("Low Compression")]
        [GUIColor(1f, 0.8f, 0.5f)]
        private string LowCount =>
            this.meshesByCompression != null
            ? $"⚡ {this.LowCompressionMeshes.Count:N0} meshes"
            : "-";

        [TabGroup("Analysis")]
        [TitleGroup("Analysis/Mesh Statistics")]
        [ShowInInspector]
        [PropertyOrder(4)]
        [ReadOnly]
        [LabelText("Medium Compression")]
        [GUIColor(0.8f, 1f, 0.5f)]
        private string MediumCount =>
            this.meshesByCompression != null
            ? $"✓ {this.MediumCompressionMeshes.Count:N0} meshes"
            : "-";

        [TabGroup("Analysis")]
        [TitleGroup("Analysis/Mesh Statistics")]
        [ShowInInspector]
        [PropertyOrder(5)]
        [ReadOnly]
        [LabelText("High Compression")]
        [GUIColor(0.5f, 1f, 0.5f)]
        private string HighCount =>
            this.meshesByCompression != null
            ? $"✓✓ {this.HighCompressionMeshes.Count:N0} meshes"
            : "-";

        [TabGroup("Analysis")]
        [TitleGroup("Analysis/Optimization Recommendations")]
        [InfoBox("$GetRecommendationMessage", InfoMessageType.Info, VisibleIf = "@meshesByCompression != null")]
        [HideInInspector]
        public bool HasRecommendations => this.meshesByCompression != null;

        private string GetRecommendationMessage()
        {
            if (this.meshesByCompression == null) return "";

            var offCount         = this.OffCompressionMeshes.Count;
            var lowCount         = this.LowCompressionMeshes.Count;
            var mediumCount      = this.MediumCompressionMeshes.Count;
            var totalOptimizable = offCount + lowCount + mediumCount;

            if (totalOptimizable == 0)
            {
                return "✅ All meshes are using High compression. No optimization needed!";
            }

            var message = $"📊 Optimization Opportunities Found: {totalOptimizable} meshes\n\n";

            if (offCount > 0)
                message += $"• {offCount} meshes with compression OFF (highest priority)\n";
            if (lowCount > 0)
                message += $"• {lowCount} meshes with LOW compression\n";
            if (mediumCount > 0)
                message += $"• {mediumCount} meshes with MEDIUM compression\n";

            message += "\nRecommendation: Use 'Apply Target Settings to All Non-High Compression' in Settings tab.";

            return message.TrimEnd();
        }

        #endregion

        #region Module Implementation

        protected override void OnAnalyze()
        {
            EditorUtility.DisplayProgressBar("Mesh Analysis", "Analyzing meshes in Addressables...", 0f);
            try
            {
                // Use AssetAnalysisService - NO CODE DUPLICATION
                this.meshesByCompression = this.analysisService.GetMeshesByCompression(forceRefresh: true);

                var totalMeshes = this.meshesByCompression.Values.Sum(list => list.Count);
                var offCount    = this.OffCompressionMeshes.Count;

                Debug.Log($"[MeshModule] Analysis complete. Found {totalMeshes} meshes, {offCount} need compression.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        protected override void OnRefresh()
        {
            this.OnAnalyze(); // Same as analyze for this module
        }

        protected override void OnClear()
        {
            this.meshesByCompression = null;
            this.analysisService.ClearCache();
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-analyze if no data
            if (this.meshesByCompression == null)
            {
                this.OnAnalyze();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Compresses a list of meshes to the target compression level.
        /// REUSES logic from AddressableMeshFinderOdin.CompressAllToHigh()
        /// </summary>
        private void CompressMeshes(
            List<MeshInfo> meshes,
            ModelImporterMeshCompression targetMeshCompression,
            string progressTitle,
            ModelImporterAnimationCompression? targetAnimCompression = null)
        {
            if (meshes == null || meshes.Count == 0)
            {
                Debug.LogWarning("[MeshModule] No meshes to compress.");
                return;
            }

            var totalSteps = meshes.Count;
            var currentStep = 0;

            try
            {
                foreach (var meshInfo in meshes)
                {
                    EditorUtility.DisplayProgressBar(progressTitle, $"Processing {meshInfo.Mesh.name}", currentStep / (float)totalSteps);

                    meshInfo.ModelImporter.meshCompression = targetMeshCompression;

                    if (targetAnimCompression.HasValue)
                    {
                        meshInfo.ModelImporter.animationCompression = targetAnimCompression.Value;
                    }

                    meshInfo.ModelImporter.SaveAndReimport();
                    currentStep++;
                }

                Debug.Log($"[MeshModule] Compressed {totalSteps} meshes to {targetMeshCompression}.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                // Refresh data after changes
                this.OnRefresh();
            }
        }

        #endregion
    }
}
