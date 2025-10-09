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
    /// Font Optimization Module - Analyzes and optimizes font assets.
    /// Wraps existing FontFinderOdin logic, reuses AssetAnalysisService.
    /// NO CODE DUPLICATION - References existing models and services.
    /// </summary>
    public class FontModule : OptimizationModuleBase
    {
        public override string ModuleName => "Font Optimization";
        public override string ModuleIcon => "🔤";

        private readonly AssetAnalysisService analysisService = new AssetAnalysisService();

        // Cached font data from analysis
        private List<FontInfo> compressedFonts = new List<FontInfo>();
        private List<FontInfo> uncompressedFonts = new List<FontInfo>();

        #region Tab: Summary

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [Title("Font Statistics", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Total Fonts")]
        private string TotalFontsDisplay => $"🔤 {this.compressedFonts.Count + this.uncompressedFonts.Count:N0} fonts";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Compressed Fonts")]
        [GUIColor(0.6f, 1f, 0.6f)]
        private string CompressedDisplay => $"✅ {this.compressedFonts.Count:N0} optimized";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Uncompressed Fonts")]
        [GUIColor(1f, 0.6f, 0.6f)]
        private string UncompressedDisplay => $"⚠️ {this.uncompressedFonts.Count:N0} need optimization";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Compression Rate")]
        [ProgressBar(0, 100, ColorGetter = "GetCompressionRateColor")]
        private double CompressionRateSummary => this.CompressionRate;

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Issues")]
        [Title("Quick Actions", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Font optimization reduces memory by including only used characters in the atlas.", InfoMessageType.Info)]
        [PropertySpace(10)]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Analyze Fonts", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void AnalyzeFontsSummary()
        {
            this.OnAnalyze();
        }

        [TabGroup("Tabs", "Summary")]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Select Uncompressed", ButtonSizes.Medium)]
        [GUIColor(1f, 0.7f, 0.7f)]
        private void SelectUncompressedFonts()
        {
            var fonts = this.uncompressedFonts.Select(f => f.Font).Where(f => f != null).ToArray();
            if (fonts.Length == 0)
            {
                Debug.LogWarning("[FontModule] No uncompressed fonts found.");
                return;
            }
            Selection.objects = fonts;
            Debug.Log($"[FontModule] Selected {fonts.Length} uncompressed fonts.");
        }

        private Color GetCompressionRateColor()
        {
            return this.GetCompressionColor(this.CompressionRate);
        }

        #endregion

        #region Fonts Tab

        [TabGroup("Tabs", "Fonts")]
        [ShowInInspector]
        [TableList(ShowIndexLabels = true, ShowPaging = true, NumberOfItemsPerPage = 50)]
        [Title("Compressed Fonts (CustomSet)", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("These fonts use custom character sets for optimal atlas size.", InfoMessageType.Info)]
        private List<FontInfo> CompressedFonts => this.compressedFonts;

        [TabGroup("Tabs", "Fonts")]
        [ShowInInspector]
        [TableList(ShowIndexLabels = true, ShowPaging = true, NumberOfItemsPerPage = 50)]
        [Title("Non-Compressed Fonts", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("⚠️ These fonts use dynamic/full unicode, which increases memory usage.", InfoMessageType.Warning)]
        [GUIColor(1f, 0.8f, 0.6f)]
        private List<FontInfo> UncompressedFonts => this.uncompressedFonts;

        [TabGroup("Tabs", "Fonts")]
        [ButtonGroup("Tabs/Fonts/Actions")]
        [Button("Select All Compressed", ButtonSizes.Medium)]
        [GUIColor(0.6f, 1f, 0.6f)]
        private void SelectCompressedFonts()
        {
            var fonts = this.compressedFonts.Select(f => f.Font).Where(f => f != null).ToArray();
            if (fonts.Length == 0) return;
            Selection.objects = fonts;
            Debug.Log($"[FontModule] Selected {fonts.Length} compressed fonts.");
        }

        [TabGroup("Tabs", "Fonts")]
        [ButtonGroup("Tabs/Fonts/Actions")]
        [Button("Select All Uncompressed", ButtonSizes.Medium)]
        [GUIColor(1f, 0.7f, 0.7f)]
        private void SelectUncompressedInTab()
        {
            this.SelectUncompressedFonts();
        }

        #endregion

        #region Atlas Tab

        [TabGroup("Atlas")]
        [Title("Font Atlas Information", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Total Fonts")]
        private string TotalFontCount => $"📊 {this.compressedFonts.Count + this.uncompressedFonts.Count:N0} fonts";

        [TabGroup("Atlas")]
        [ShowInInspector, DisplayAsString, LabelText("Compressed")]
        [GUIColor(0.6f, 1f, 0.6f)]
        private string CompressedCount => $"✅ {this.compressedFonts.Count:N0} using CustomSet";

        [TabGroup("Atlas")]
        [ShowInInspector, DisplayAsString, LabelText("Uncompressed")]
        [GUIColor(1f, 0.6f, 0.6f)]
        private string UncompressedCount => $"⚠️ {this.uncompressedFonts.Count:N0} need optimization";

        [TabGroup("Atlas")]
        [InfoBox("Font atlas optimization reduces memory by including only used characters. " +
                 "Set fontTextureCase to CustomSet and define customCharacters.",
                 InfoMessageType.Info)]
        [PropertySpace(10)]
        [ShowInInspector, DisplayAsString, HideLabel]
        private string AtlasInfo => "ℹ️ CustomSet atlases are significantly smaller than dynamic fonts.";

        #endregion

        #region Analysis Tab

        [TabGroup("Analysis")]
        [Title("Font Analysis Summary", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Compression Rate")]
        [ProgressBar(0, 100, ColorGetter = "GetCompressionColor")]
        private double CompressionRate
        {
            get
            {
                var total = this.compressedFonts.Count + this.uncompressedFonts.Count;
                if (total == 0) return 0;
                return (this.compressedFonts.Count / (double)total) * 100.0;
            }
        }

        [TabGroup("Analysis")]
        [ShowInInspector, DisplayAsString, LabelText("Total Font References")]
        private string TotalReferences => $"🔗 {this.GetTotalReferences():N0} objects use these fonts";

        [TabGroup("Analysis")]
        [PropertySpace(20)]
        [Title("Character Set Analysis", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector]
        [TableList]
        private List<CharacterSetInfo> CharacterSetBreakdown => this.GetCharacterSetBreakdown();

        private Color GetCompressionColor(double value)
        {
            if (value >= 80) return new Color(0.6f, 1f, 0.6f);
            if (value >= 50) return new Color(1f, 1f, 0.6f);
            return new Color(1f, 0.6f, 0.6f);
        }

        private int GetTotalReferences()
        {
            return this.compressedFonts.Sum(f => f.Objects?.Count ?? 0) + this.uncompressedFonts.Sum(f => f.Objects?.Count ?? 0);
        }

        private List<CharacterSetInfo> GetCharacterSetBreakdown()
        {
            var breakdown = new List<CharacterSetInfo>();

            // Group fonts by FontTextureCase
            var allFonts = this.compressedFonts.Concat(this.uncompressedFonts).ToList();
            var grouped  = allFonts.GroupBy(f => f.FontImporter.fontTextureCase);

            foreach (var group in grouped)
            {
                breakdown.Add(new CharacterSetInfo
                {
                    CaseType = group.Key.ToString(),
                    Count = group.Count(),
                    TotalReferences = group.Sum(f => f.Objects?.Count ?? 0)
                });
            }

            return breakdown.OrderByDescending(c => c.Count).ToList();
        }

        #endregion

        #region Module Implementation

        protected override void OnAnalyze()
        {
            EditorUtility.DisplayProgressBar("Font Analysis", "Analyzing fonts in addressables...", 0.5f);
            try
            {
                // Use AssetAnalysisService - NO CODE DUPLICATION
                var (compressed, uncompressed) = this.analysisService.GetFontsByCompression(forceRefresh: true);

                this.compressedFonts = compressed;
                this.uncompressedFonts  = uncompressed;

                Debug.Log($"[FontModule] Analysis complete: {this.compressedFonts.Count} compressed, {this.uncompressedFonts.Count} uncompressed");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        protected override void OnRefresh()
        {
            // Refresh from cache (don't force refresh unless Analyze is called)
            var (compressed, uncompressed) = this.analysisService.GetFontsByCompression(forceRefresh: false);

            this.compressedFonts = compressed;
            this.uncompressedFonts  = uncompressed;

            Debug.Log($"[FontModule] Refreshed from cache: {this.compressedFonts.Count} compressed, {this.uncompressedFonts.Count} uncompressed");
        }

        protected override void OnClear()
        {
            this.compressedFonts.Clear();
            this.uncompressedFonts.Clear();
            this.analysisService.ClearCache();
            Debug.Log("[FontModule] Cache cleared");
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-refresh if no data is loaded
            if (this.compressedFonts.Count == 0 && this.uncompressedFonts.Count == 0)
            {
                this.OnRefresh();
            }
        }

        #endregion

        #region Data Models

        /// <summary>
        /// Character set breakdown for analysis tab.
        /// </summary>
        private class CharacterSetInfo
        {
            [TableColumnWidth(150)]
            public string CaseType { get; set; }

            [TableColumnWidth(80)]
            public int Count { get; set; }

            [TableColumnWidth(120)]
            public int TotalReferences { get; set; }
        }

        #endregion
    }
}