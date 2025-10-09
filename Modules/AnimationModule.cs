using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace TheOne.UITemplate.Editor.Optimization.Modules
{
    /// <summary>
    /// Animation Module - Analyzes animation clips, keyframe density, file sizes, and compression.
    /// Identifies inefficient animations, unused curves, and provides optimization suggestions.
    /// </summary>
    public class AnimationModule : OptimizationModuleBase
    {
        public override string ModuleName => "Animation Analysis";
        public override string ModuleIcon => "🎬";

        #region Tab: Summary

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [Title("Animation Statistics", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Total Clips")]
        private string TotalClips => $"🎬 {this.allAnimations.Count:N0} animations";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Total Duration")]
        [GUIColor(0.6f, 0.8f, 1f)]
        private string TotalDuration => $"⏱️ {this.GetTotalDuration():F2} seconds";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Dense Keyframes")]
        [GUIColor(1f, 0.9f, 0.6f)]
        private string DenseKeyframesCount => $"📊 {this.denseKeyframeAnimations.Count:N0} need optimization";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Uncompressed")]
        [GUIColor(1f, 0.6f, 0.6f)]
        private string UncompressedCount => $"⚠️ {this.uncompressedAnimations.Count:N0} without compression";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Issues")]
        [Title("Quick Actions", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Animation analysis helps identify large clips, redundant keyframes, and compression issues.", InfoMessageType.Info)]
        [PropertySpace(10)]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Select Dense Keyframes", ButtonSizes.Medium)]
        [GUIColor(1f, 0.9f, 0.6f)]
        private void SelectDenseKeyframes()
        {
            var clips = this.denseKeyframeAnimations.Select(a => a.Clip).Where(c => c != null).ToArray();
            if (clips.Length == 0)
            {
                Debug.LogWarning("[AnimationModule] No animations with dense keyframes found.");
                return;
            }
            Selection.objects = clips;
            Debug.Log($"[AnimationModule] Selected {clips.Length} animations with dense keyframes.");
        }

        [TabGroup("Tabs", "Summary")]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Select Uncompressed", ButtonSizes.Medium)]
        [GUIColor(1f, 0.7f, 0.7f)]
        private void SelectUncompressed()
        {
            var clips = this.uncompressedAnimations.Select(a => a.Clip).Where(c => c != null).ToArray();
            if (clips.Length == 0)
            {
                Debug.LogWarning("[AnimationModule] No uncompressed animations found.");
                return;
            }
            Selection.objects = clips;
            Debug.Log($"[AnimationModule] Selected {clips.Length} uncompressed animations.");
        }

        #endregion

        #region Tab: All Animations

        [TabGroup("Tabs", "All Animations")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("All Animation Clips in Project", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Shows all animation clips with keyframe counts, duration, and compression info.", InfoMessageType.Info)]
        [Searchable]
        private List<AnimationInfo> allAnimations = new List<AnimationInfo>();

        [TabGroup("Tabs", "All Animations")]
        [ButtonGroup("Tabs/All Animations/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(0.6f, 1f, 0.6f)]
        private void SelectAllAnimations()
        {
            var clips = this.allAnimations.Select(a => a.Clip).Where(c => c != null).ToArray();
            if (clips.Length == 0) return;
            Selection.objects = clips;
            Debug.Log($"[AnimationModule] Selected {clips.Length} animation clips.");
        }

        [TabGroup("Tabs", "All Animations")]
        [ButtonGroup("Tabs/All Animations/Actions")]
        [Button("Sort by Keyframes", ButtonSizes.Medium)]
        private void SortByKeyframes()
        {
            this.allAnimations = this.allAnimations.OrderByDescending(a => a.TotalKeyframes).ToList();
            Debug.Log("[AnimationModule] Sorted by keyframe count.");
        }

        [TabGroup("Tabs", "All Animations")]
        [ButtonGroup("Tabs/All Animations/Actions")]
        [Button("Sort by Duration", ButtonSizes.Medium)]
        private void SortByDuration()
        {
            this.allAnimations = this.allAnimations.OrderByDescending(a => a.Length).ToList();
            Debug.Log("[AnimationModule] Sorted by duration.");
        }

        #endregion

        #region Tab: Dense Keyframes

        [TabGroup("Tabs", "Dense Keyframes")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Animations with Dense Keyframes (>30 keys/sec)", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("These animations have very dense keyframes which may be redundant. Consider keyframe reduction or resampling.", InfoMessageType.Warning)]
        [Searchable]
        private List<AnimationInfo> denseKeyframeAnimations = new List<AnimationInfo>();

        [TabGroup("Tabs", "Dense Keyframes")]
        [ButtonGroup("Tabs/Dense Keyframes/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(1f, 0.9f, 0.6f)]
        private void SelectDenseAnimations()
        {
            this.SelectDenseKeyframes();
        }

        [TabGroup("Tabs", "Dense Keyframes")]
        [ButtonGroup("Tabs/Dense Keyframes/Actions")]
        [Button("Log Details", ButtonSizes.Medium)]
        private void LogDenseKeyframes()
        {
            if (this.denseKeyframeAnimations.Count == 0)
            {
                Debug.LogWarning("[AnimationModule] No animations with dense keyframes found.");
                return;
            }

            Debug.Log($"[AnimationModule] Found {this.denseKeyframeAnimations.Count} animations with dense keyframes:");
            foreach (var anim in this.denseKeyframeAnimations)
            {
                Debug.Log($"  • {anim.Name} - {anim.KeyframeRate:F1} keys/sec - {anim.TotalKeyframes} total keys");
            }
        }

        #endregion

        #region Tab: Uncompressed

        [TabGroup("Tabs", "Uncompressed")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Uncompressed Animations", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("⚠️ These animations are not compressed, which increases build size. Enable keyframe reduction or compression.", InfoMessageType.Warning)]
        [Searchable]
        private List<AnimationInfo> uncompressedAnimations = new List<AnimationInfo>();

        [TabGroup("Tabs", "Uncompressed")]
        [ButtonGroup("Tabs/Uncompressed/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(1f, 0.7f, 0.7f)]
        private void SelectUncompressedAnimations()
        {
            this.SelectUncompressed();
        }

        [TabGroup("Tabs", "Uncompressed")]
        [ButtonGroup("Tabs/Uncompressed/Actions")]
        [Button("Enable Compression (Optimal)", ButtonSizes.Medium)]
        [GUIColor(0.4f, 1f, 0.7f)]
        private void EnableCompressionOnAll()
        {
            if (this.uncompressedAnimations.Count == 0)
            {
                Debug.LogWarning("[AnimationModule] No uncompressed animations to optimize.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Enable Animation Compression",
                $"This will enable Optimal compression on {this.uncompressedAnimations.Count} animations.\n\nThis reduces build size with minimal quality loss. Continue?",
                "Yes, Enable Compression",
                "Cancel"))
            {
                return;
            }

            int count = 0;
            try
            {
                for (int i = 0; i < this.uncompressedAnimations.Count; i++)
                {
                    var anim = this.uncompressedAnimations[i];
                    EditorUtility.DisplayProgressBar("Enabling Compression", $"Processing {anim.Name}...", (float)i / this.uncompressedAnimations.Count);

                    var path = AssetDatabase.GetAssetPath(anim.Clip);
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;

                    if (importer != null)
                    {
                        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[AnimationModule] Enabled compression on {count} animations.");
            AssetDatabase.Refresh();
            this.OnRefresh();
        }

        #endregion

        #region Tab: Large Clips

        [TabGroup("Tabs", "Large Clips")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Large Animation Clips (>10 seconds)", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Long animation clips may benefit from splitting into smaller segments for better memory management.", InfoMessageType.Info)]
        [Searchable]
        private List<AnimationInfo> largeAnimations = new List<AnimationInfo>();

        #endregion

        #region Analysis Logic

        protected override void OnAnalyze()
        {
            EditorUtility.DisplayProgressBar("Animation Analysis", "Scanning project for animations...", 0f);
            try
            {
                this.AnalyzeAnimations();
                Debug.Log($"[AnimationModule] Analysis complete: {this.allAnimations.Count} animations analyzed, " +
                          $"{this.denseKeyframeAnimations.Count} with dense keyframes, " +
                          $"{this.uncompressedAnimations.Count} uncompressed.");
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
            this.allAnimations.Clear();
            this.denseKeyframeAnimations.Clear();
            this.uncompressedAnimations.Clear();
            this.largeAnimations.Clear();
            Debug.Log("[AnimationModule] Data cleared.");
        }

        private void AnalyzeAnimations()
        {
            this.allAnimations.Clear();
            this.denseKeyframeAnimations.Clear();
            this.uncompressedAnimations.Clear();
            this.largeAnimations.Clear();

            var clipGuids = AssetDatabase.FindAssets("t:AnimationClip");
            var total = clipGuids.Length;

            for (int i = 0; i < clipGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

                if (clip == null) continue;

                // Skip Unity's built-in clips
                if (path.StartsWith("Packages/") || path.StartsWith("Assets/Plugins/")) continue;

                EditorUtility.DisplayProgressBar("Animation Analysis", $"Analyzing {clip.name}...", (float)i / total);

                var info = this.AnalyzeClip(clip, path);
                this.allAnimations.Add(info);

                // Categorize
                if (info.KeyframeRate > 30f) this.denseKeyframeAnimations.Add(info);

                if (!info.IsCompressed) this.uncompressedAnimations.Add(info);

                if (info.Length > 10f) this.largeAnimations.Add(info);
            }

            // Sort by issues
            this.denseKeyframeAnimations = this.denseKeyframeAnimations.OrderByDescending(a => a.KeyframeRate).ToList();
            this.uncompressedAnimations  = this.uncompressedAnimations.OrderByDescending(a => a.TotalKeyframes).ToList();
            this.largeAnimations         = this.largeAnimations.OrderByDescending(a => a.Length).ToList();
        }

        private AnimationInfo AnalyzeClip(AnimationClip clip, string path)
        {
            var info = new AnimationInfo
            {
                Clip = clip,
                Name = clip.name,
                Path = path,
                Length = clip.length,
                FrameRate = clip.frameRate,
                IsLooping = clip.isLooping
            };

            // Count keyframes from all curves
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int totalKeyframes = 0;

            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null)
                {
                    totalKeyframes += curve.keys.Length;
                }
            }

            info.CurveCount = bindings.Length;
            info.TotalKeyframes = totalKeyframes;
            info.KeyframeRate = clip.length > 0 ? totalKeyframes / clip.length : 0;

            // Check compression (from ModelImporter if imported from FBX)
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                info.IsCompressed = importer.animationCompression != ModelImporterAnimationCompression.Off;
                info.CompressionType = importer.animationCompression.ToString();
            }
            else
            {
                info.IsCompressed = true; // Native .anim files are always compressed
                info.CompressionType = "Native";
            }

            return info;
        }

        private float GetTotalDuration()
        {
            return this.allAnimations.Sum(a => a.Length);
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-analyze if no data
            if (this.allAnimations.Count == 0)
            {
                this.OnAnalyze();
            }
        }

        #endregion

        #region Data Model

        [System.Serializable]
        private class AnimationInfo
        {
            [TableColumnWidth(200)]
            [ShowInInspector, ReadOnly]
            public AnimationClip Clip;

            [TableColumnWidth(200)]
            [ShowInInspector, ReadOnly]
            public string Name;

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public float Length;

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public float FrameRate;

            [TableColumnWidth(100)]
            [ShowInInspector, ReadOnly]
            public int TotalKeyframes;

            [TableColumnWidth(100)]
            [ShowInInspector, ReadOnly]
            [GUIColor("GetKeyframeRateColor")]
            public float KeyframeRate;

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public int CurveCount;

            [TableColumnWidth(100)]
            [ShowInInspector, ReadOnly]
            [GUIColor("GetCompressionColor")]
            public string CompressionType;

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public bool IsLooping;

            [TableColumnWidth(300)]
            [ShowInInspector, ReadOnly]
            public string Path;

            [HideInInspector]
            public bool IsCompressed;

            private Color GetKeyframeRateColor()
            {
                if (this.KeyframeRate > 60f) return new Color(1f, 0.4f, 0.4f); // Red - very dense
                if (this.KeyframeRate > 30f) return new Color(1f, 0.9f, 0.6f);    // Yellow - dense
                return Color.white;
            }

            private Color GetCompressionColor()
            {
                return this.IsCompressed ? Color.white : new Color(1f, 0.6f, 0.6f);
            }
        }

        #endregion
    }
}
