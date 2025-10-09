using System;
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
    /// Audio Optimization Module - Manages audio compression settings and optimization.
    /// Wraps existing AddressableAudioFinderOdin logic without duplication.
    /// Uses AssetAnalysisService for data retrieval and AudioInfo models from AddressableAudioFinderOdin.
    /// </summary>
    public class AudioModule : OptimizationModuleBase
    {
        public override string ModuleName => "Audio Optimization";
        public override string ModuleIcon => "🔊";

        private readonly AssetAnalysisService analysisService = new AssetAnalysisService();

        // Settings for audio compression
        [Serializable]
        private class AudioCompressionSettings
        {
            [Range(0, 1f)]
            [Tooltip("Vorbis quality (0-1). Lower = smaller file, lower quality. Recommended: 0.2")]
            public float quality = 0.2f;

            [Tooltip("Audio length threshold in seconds. Clips longer than this use CompressedInMemory, shorter use DecompressOnLoad")]
            public int longAudioLengthThreshold = 60;
        }

        #region Summary Dashboard

        [TabGroup("Tabs", "Issues")]
        [BoxGroup("Tabs/Issues/Dashboard", ShowLabel = false)]
        [PropertyOrder(-100)]
        [ShowInInspector]
        [HideLabel]
        [ReadOnly, GUIColor(1, 1, 1)]
        
        private string SummaryStats => this.GetSummaryStats();

        private string GetSummaryStats()
        {
            var allAudio = this.analysisService.GetAllAudioInfos(forceRefresh: false);

            if (allAudio.Count == 0)
                return "Click 'Analyze' button below to scan all audio clips in the project.";

            var total               = allAudio.Count;
            var totalSizeMB         = allAudio.Values.Sum(info => this.GetAudioSize(info)) / (1024f * 1024f);
            var issueCount          = this.wrongCompressionAudioList.Count;
            var optimizedCount      = total - issueCount;
            var optimizedPercentage = total > 0 ? (optimizedCount * 100.0f / total) : 0;

            var stats = $"AUDIO SUMMARY\n" +
                       $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                       $"Total Audio Clips: {total:N0}\n" +
                       $"Total Size: {totalSizeMB:F2} MB\n" +
                       $"Optimized: {optimizedCount:N0} ({optimizedPercentage:F1}%)\n" +
                       $"Issues Found: {issueCount:N0}";

            return stats;
        }

        [TabGroup("Tabs", "Issues")]
        [BoxGroup("Tabs/Issues/Dashboard", ShowLabel = false)]
        [PropertyOrder(-99)]
        [InfoBox("$GetDashboardMessage", "$GetDashboardMessageType")]
        [HideInInspector]
        public bool ShowDashboard = true;

        private string GetDashboardMessage()
        {
            var allAudio = this.analysisService.GetAllAudioInfos(forceRefresh: false);
            if (allAudio.Count == 0) return "";

            if (this.wrongCompressionAudioList.Count == 0)
            {
                return "All audio clips are optimally configured!";
            }

            var mono        = this.wrongCompressionAudioList.Count(a => !a.ForceToMono);
            var compression = this.wrongCompressionAudioList.Count(a => a.CompressionFormat != AudioCompressionFormat.Vorbis);
            var normalized  = this.wrongCompressionAudioList.Count(a => a.Normalize);
            var loadType = this.wrongCompressionAudioList.Count(a =>
            {
                var isLongSound      = a.Audio.length >= this.compressionSettings.longAudioLengthThreshold;
                var expectedLoadType = isLongSound ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                return a.LoadType != expectedLoadType;
            });

            var message                  = $"Found {this.wrongCompressionAudioList.Count} clips with issues:\n";
            if (mono > 0) message        += $"• {mono} not forced to mono (50% size waste)\n";
            if (compression > 0) message += $"• {compression} not using Vorbis compression\n";
            if (normalized > 0) message  += $"• {normalized} have normalization enabled\n";
            if (loadType > 0) message    += $"• {loadType} incorrect load type configuration";

            return message.TrimEnd();
        }

        private InfoMessageType GetDashboardMessageType()
        {
            if (this.wrongCompressionAudioList.Count == 0) return InfoMessageType.Info;
            if (this.wrongCompressionAudioList.Count > 50) return InfoMessageType.Error;
            if (this.wrongCompressionAudioList.Count > 10) return InfoMessageType.Warning;
            return InfoMessageType.Info;
        }

        #endregion

        #region Issues Tab

        [TabGroup("Tabs", "Issues")]
        [BoxGroup("Tabs/Issues/List")]
        [Title("Audio Clips with Issues", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector]
        [ListDrawerSettings(NumberOfItemsPerPage = 10, ShowPaging = true)]
        [TableList(AlwaysExpanded = true)]
        [PropertyOrder(0)]
        [InfoBox("No issues found! All audio clips are properly configured.", InfoMessageType.Info, VisibleIf = "@wrongCompressionAudioList.Count == 0")]
        private List<AudioInfo> wrongCompressionAudioList = new List<AudioInfo>();

        public bool HasIssues => this.wrongCompressionAudioList.Count > 0;

        [TabGroup("Tabs", "Issues")]
        [BoxGroup("Tabs/Issues/Actions")]
        [PropertyOrder(1)]
        [PropertySpace(SpaceBefore = 10)]
        [ButtonGroup("Tabs/Issues/Actions/Buttons")]
        [Button("Fix All Issues", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [EnableIf("HasIssues")]
        private void CompressAndFixAll()
        {
            if (this.wrongCompressionAudioList.Count == 0)
            {
                Debug.LogWarning("[AudioModule] No audio issues to fix. Run Analyze first.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Fix All Audio Issues",
                $"This will apply optimal compression settings to {this.wrongCompressionAudioList.Count} audio clips.\n\n" +
                $"Settings to apply:\n" +
                $"• Force to Mono: Enabled\n" +
                $"• Compression: Vorbis (quality: {this.compressionSettings.quality})\n" +
                $"• Normalization: Disabled\n" +
                $"• Load Type: Auto (based on duration)\n\n" +
                $"This operation cannot be undone easily. Continue?",
                "Fix All", "Cancel"))
            {
                return;
            }

            var totalSteps   = this.wrongCompressionAudioList.Count;
            var currentStep  = 0;
            var successCount = 0;
            var failCount    = 0;

            try
            {
                foreach (var audioInfo in this.wrongCompressionAudioList)
                {
                    EditorUtility.DisplayProgressBar("Fixing Audio Compression",
                        $"Processing {audioInfo.Audio.name}... ({currentStep + 1}/{totalSteps})",
                        currentStep / (float)totalSteps);

                    try
                    {
                        this.ApplyOptimalSettings(audioInfo);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AudioModule] Failed to fix {audioInfo.Audio.name}: {ex.Message}");
                        failCount++;
                    }

                    currentStep++;
                }

                Debug.Log($"[AudioModule] Optimization complete: {successCount} succeeded, {failCount} failed.");

                // Refresh after fixing
                this.OnAnalyze();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [TabGroup("Tabs", "Issues")]
        [BoxGroup("Tabs/Issues/Actions")]
        [ButtonGroup("Tabs/Issues/Actions/Buttons")]
        [Button("Select All Issues", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.7f, 0.3f)]
        [EnableIf("HasIssues")]
        private void SelectAllIssues()
        {
            var objects = this.wrongCompressionAudioList.Select(info => info.Audio as UnityEngine.Object).ToArray();
            Selection.objects = objects;
            Debug.Log($"[AudioModule] Selected {objects.Length} audio clips with issues in Project window.");
        }

        [TabGroup("Tabs", "Issues")]
        [BoxGroup("Tabs/Issues/Actions")]
        [ButtonGroup("Tabs/Issues/Actions/Buttons")]
        [Button("Fix Selected Only", ButtonSizes.Medium)]
        [GUIColor(0.5f, 0.7f, 0.9f)]
        private void FixSelectedClips()
        {
            var selectedClips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
            if (selectedClips.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select audio clips in the Project window first.", "OK");
                return;
            }

            var selectedIssues = this.wrongCompressionAudioList
                .Where(info => selectedClips.Contains(info.Audio))
                .ToList();

            if (selectedIssues.Count == 0)
            {
                EditorUtility.DisplayDialog("No Issues Found",
                    $"None of the {selectedClips.Length} selected audio clips have optimization issues.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Fix Selected Audio",
                $"Apply optimal settings to {selectedIssues.Count} selected audio clips?",
                "Fix", "Cancel"))
            {
                return;
            }

            var currentStep = 0;
            try
            {
                foreach (var audioInfo in selectedIssues)
                {
                    EditorUtility.DisplayProgressBar("Fixing Selected Audio",
                        $"Processing {audioInfo.Audio.name}...",
                        currentStep / (float)selectedIssues.Count);

                    this.ApplyOptimalSettings(audioInfo);
                    currentStep++;
                }

                Debug.Log($"[AudioModule] Fixed {selectedIssues.Count} selected audio clips.");
                this.OnAnalyze();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #endregion

        #region Settings Tab

        [TabGroup("Settings")]
        [BoxGroup("Settings/Presets", ShowLabel = false)]
        [PropertyOrder(-1)]
        [InfoBox("Choose a quality preset or customize settings below", InfoMessageType.Info)]
        [HideInInspector]
        public bool ShowPresetsInfo = true;

        [TabGroup("Settings")]
        [BoxGroup("Settings/Presets", ShowLabel = false)]
        [PropertyOrder(0)]
        [ButtonGroup("Settings/Presets/Buttons")]
        [Button("Low Quality (Smaller Size)", ButtonSizes.Large)]
        [GUIColor(0.9f, 0.5f, 0.5f)]
        private void ApplyLowQualityPreset()
        {
            this.compressionSettings.quality               = 0.1f;
            this.compressionSettings.longAudioLengthThreshold = 30;
            Debug.Log("[AudioModule] Applied Low Quality preset (Quality: 0.1, Threshold: 30s)");
        }

        [TabGroup("Settings")]
        [BoxGroup("Settings/Presets", ShowLabel = false)]
        [ButtonGroup("Settings/Presets/Buttons")]
        [Button("Balanced (Recommended)", ButtonSizes.Large)]
        [GUIColor(0.5f, 0.9f, 0.5f)]
        private void ApplyBalancedPreset()
        {
            this.compressionSettings.quality               = 0.2f;
            this.compressionSettings.longAudioLengthThreshold = 60;
            Debug.Log("[AudioModule] Applied Balanced preset (Quality: 0.2, Threshold: 60s)");
        }

        [TabGroup("Settings")]
        [BoxGroup("Settings/Presets", ShowLabel = false)]
        [ButtonGroup("Settings/Presets/Buttons")]
        [Button("High Quality (Larger Size)", ButtonSizes.Large)]
        [GUIColor(0.5f, 0.7f, 0.9f)]
        private void ApplyHighQualityPreset()
        {
            this.compressionSettings.quality               = 0.5f;
            this.compressionSettings.longAudioLengthThreshold = 90;
            Debug.Log("[AudioModule] Applied High Quality preset (Quality: 0.5, Threshold: 90s)");
        }

        [TabGroup("Settings")]
        [BoxGroup("Settings/Custom")]
        [Title("Custom Compression Settings", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector]
        [HideReferenceObjectPicker]
        [PropertyOrder(1)]
        private AudioCompressionSettings compressionSettings = new AudioCompressionSettings();

        [TabGroup("Settings")]
        [BoxGroup("Settings/Custom")]
        [PropertyOrder(2)]
        [ShowInInspector]
        [ReadOnly, GUIColor(1, 1, 1)]
        [HideLabel]
        
        private string SettingsImpact => this.GetSettingsImpact();

        private string GetSettingsImpact()
        {
            var allAudio = this.analysisService.GetAllAudioInfos(forceRefresh: false);
            if (allAudio.Count == 0) return "";

            var longClips  = allAudio.Count(pair => pair.Key.length >= this.compressionSettings.longAudioLengthThreshold);
            var shortClips = allAudio.Count - longClips;

            return $"ESTIMATED IMPACT WITH CURRENT SETTINGS:\n" +
                   $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                   $"Short clips (<{this.compressionSettings.longAudioLengthThreshold}s): {shortClips} → DecompressOnLoad\n" +
                   $"Long clips (≥{this.compressionSettings.longAudioLengthThreshold}s): {longClips} → CompressedInMemory\n" +
                   $"Vorbis Quality: {this.compressionSettings.quality:P0} (0% = smallest, 100% = highest quality)";
        }

        [TabGroup("Settings")]
        [BoxGroup("Settings/Recommendations")]
        [PropertyOrder(3)]
        [InfoBox("RECOMMENDED SETTINGS:\n\n" +
                 "• Quality: 0.2 (20%) - Good balance of size and quality\n" +
                 "• Long Audio Threshold: 60 seconds\n" +
                 "• Force to Mono: Enabled (saves 50% space for stereo audio)\n" +
                 "• Compression: Vorbis (best mobile compression)\n" +
                 "• Normalization: Disabled (prevents audio artifacts)\n\n" +
                 "LOAD TYPE STRATEGY:\n" +
                 "• Short clips: DecompressOnLoad (faster playback, more memory)\n" +
                 "• Long clips: CompressedInMemory (less memory, slight CPU cost)",
                 InfoMessageType.Info)]
        [HideInInspector]
        public bool ShowRecommendations = true;

        #endregion

        #region Analysis Tab

        [TabGroup("Analysis")]
        [BoxGroup("Analysis/Stats")]
        [Title("Detailed Analysis Results", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector]
        [ReadOnly, GUIColor(1, 1, 1)]
        [HideLabel]
        [PropertyOrder(0)]
        private string AnalysisStats => this.GetAnalysisStats();

        private string GetAnalysisStats()
        {
            var allAudio = this.analysisService.GetAllAudioInfos(forceRefresh: false);

            if (allAudio.Count == 0)
                return "Click 'Analyze' button in the toolbar to scan all audio clips in Addressables.";

            var total       = allAudio.Count;
            var totalSize   = allAudio.Values.Sum(info => this.GetAudioSize(info));
            var totalSizeMB = totalSize / (1024f * 1024f);

            var correct    = total - this.wrongCompressionAudioList.Count;
            var percentage = total > 0 ? (correct * 100.0f / total) : 0;

            // Breakdown by compression format
            var vorbis = allAudio.Values.Count(info => info.CompressionFormat == AudioCompressionFormat.Vorbis);
            var pcm = allAudio.Values.Count(info => info.CompressionFormat == AudioCompressionFormat.PCM);
            var adpcm = allAudio.Values.Count(info => info.CompressionFormat == AudioCompressionFormat.ADPCM);
            var other = total - vorbis - pcm - adpcm;

            // Breakdown by load type
            var decompressOnLoad = allAudio.Values.Count(info => info.LoadType == AudioClipLoadType.DecompressOnLoad);
            var compressedInMemory = allAudio.Values.Count(info => info.LoadType == AudioClipLoadType.CompressedInMemory);
            var streaming = allAudio.Values.Count(info => info.LoadType == AudioClipLoadType.Streaming);

            // Mono vs Stereo
            var mono = allAudio.Values.Count(info => info.ForceToMono);
            var stereo = total - mono;

            return $"ANALYSIS RESULTS\n" +
                   $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                   $"Total Audio Clips: {total:N0}\n" +
                   $"Total Size: {totalSizeMB:F2} MB ({totalSize:N0} bytes)\n" +
                   $"Correctly Configured: {correct:N0} ({percentage:F1}%)\n" +
                   $"Needs Optimization: {this.wrongCompressionAudioList.Count:N0}\n\n" +
                   $"COMPRESSION FORMAT BREAKDOWN:\n" +
                   $"• Vorbis: {vorbis:N0} ({this.GetPercentage(vorbis, total)})\n" +
                   $"• PCM: {pcm:N0} ({this.GetPercentage(pcm, total)})\n" +
                   $"• ADPCM: {adpcm:N0} ({this.GetPercentage(adpcm, total)})\n" +
                   $"• Other: {other:N0} ({this.GetPercentage(other, total)})\n\n" +
                   $"LOAD TYPE BREAKDOWN:\n" +
                   $"• DecompressOnLoad: {decompressOnLoad:N0} ({this.GetPercentage(decompressOnLoad, total)})\n" +
                   $"• CompressedInMemory: {compressedInMemory:N0} ({this.GetPercentage(compressedInMemory, total)})\n" +
                   $"• Streaming: {streaming:N0} ({this.GetPercentage(streaming, total)})\n\n" +
                   $"CHANNEL CONFIGURATION:\n" +
                   $"• Mono: {mono:N0} ({this.GetPercentage(mono, total)})\n" +
                   $"• Stereo: {stereo:N0} ({this.GetPercentage(stereo, total)})";
        }

        private string GetPercentage(int count, int total)
        {
            if (total == 0) return "0%";
            return $"{count * 100.0f / total:F1}%";
        }

        private long GetAudioSize(AudioInfo info)
        {
            // Rough estimation based on compression format and channels
            var samples = info.Audio.samples;
            var channels = info.Audio.channels;
            var frequency = info.Audio.frequency;

            if (info.CompressionFormat == AudioCompressionFormat.PCM)
            {
                return samples * channels * 2; // 16-bit PCM
            }
            else if (info.CompressionFormat == AudioCompressionFormat.Vorbis)
            {
                // Vorbis compression ratio estimate
                var uncompressedSize = samples * channels * 2;
                return (long)(uncompressedSize * 0.1f); // Rough 10:1 compression
            }
            else if (info.CompressionFormat == AudioCompressionFormat.ADPCM)
            {
                return samples * channels / 2; // 4-bit ADPCM
            }

            return samples * channels * 2; // Default fallback
        }

        #endregion

        #region Duplicates Tab

        [TabGroup("Duplicates")]
        [BoxGroup("Duplicates/Info", ShowLabel = false)]
        [PropertyOrder(-1)]
        [InfoBox("Duplicate audio clips waste storage space and memory. Use this tool to find identical audio files.\n\n" +
                 "WARNING: Sample-by-sample comparison can take several minutes for large projects.",
                 InfoMessageType.Warning)]
        [HideInInspector]
        public bool ShowDuplicatesInfo = true;

        [TabGroup("Duplicates")]
        [BoxGroup("Duplicates/Summary", ShowLabel = false)]
        [PropertyOrder(0)]
        [ShowInInspector]
        [ReadOnly, GUIColor(1, 1, 1)]
        [HideLabel]
        
        [ShowIf("@DuplicateAudios.Count > 0")]
        private string DuplicatesSummary => this.GetDuplicatesSummary();

        private string GetDuplicatesSummary()
        {
            if (this.DuplicateAudios.Count == 0) return "";

            var totalDuplicates = this.DuplicateAudios.Sum(set => set.Count - 1);
            var allAudio        = this.analysisService.GetAllAudioInfos(forceRefresh: false);

            // Estimate size savings
            long potentialSavings = 0;
            foreach (var duplicateSet in this.DuplicateAudios)
            {
                var firstClip = duplicateSet.First();
                if (allAudio.TryGetValue(firstClip, out var info))
                {
                    var clipSize = this.GetAudioSize(info);
                    potentialSavings += clipSize * (duplicateSet.Count - 1); // Keep one, remove rest
                }
            }

            var savingsMB = potentialSavings / (1024f * 1024f);

            return $"DUPLICATE DETECTION RESULTS\n" +
                   $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                   $"Duplicate Groups Found: {this.DuplicateAudios.Count:N0}\n" +
                   $"Redundant Clips: {totalDuplicates:N0}\n" +
                   $"Potential Size Savings: {savingsMB:F2} MB";
        }

        [TabGroup("Duplicates")]
        [BoxGroup("Duplicates/Results")]
        [Title("Duplicate Audio Groups", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector]
        [ListDrawerSettings(NumberOfItemsPerPage = 5, ShowPaging = true)]
        [PropertyOrder(1)]
        [InfoBox("No duplicates found. All audio clips are unique!", InfoMessageType.Info, VisibleIf = "@DuplicateAudios.Count == 0")]
        private List<HashSet<AudioClip>> DuplicateAudios = new List<HashSet<AudioClip>>();

        [TabGroup("Duplicates")]
        [BoxGroup("Duplicates/Actions")]
        [PropertyOrder(2)]
        [PropertySpace(SpaceBefore = 10)]
        [ButtonGroup("Duplicates/Actions/Buttons")]
        [Button("Check for Duplicates", ButtonSizes.Large)]
        [GUIColor(1, 0.9f, 0.5f)]
        private void CheckDuplicateAudios()
        {
            // Get all audio clips if not already loaded
            var allAudio = this.analysisService.GetAllAudioInfos(forceRefresh: false);
            if (allAudio.Count == 0)
            {
                Debug.LogWarning("[AudioModule] No audio clips found. Running analysis first...");
                this.OnAnalyze();
                allAudio = this.analysisService.GetAllAudioInfos(forceRefresh: false);
            }

            this.DuplicateAudios.Clear();
            var audioClips = allAudio.Keys.ToList();

            if (audioClips.Count <= 1)
            {
                EditorUtility.DisplayDialog("Not Enough Audio",
                    "Need at least 2 audio clips to check for duplicates.", "OK");
                return;
            }

            var totalComparisons = (audioClips.Count * (audioClips.Count - 1)) / 2;
            var currentComparison = 0;

            try
            {
                for (var i = 0; i < audioClips.Count - 1; i++)
                {
                    for (var j = i + 1; j < audioClips.Count; j++)
                    {
                        currentComparison++;

                        if (EditorUtility.DisplayCancelableProgressBar("Checking for Duplicates",
                            $"Comparing {audioClips[i].name} vs {audioClips[j].name}... ({currentComparison}/{totalComparisons})",
                            currentComparison / (float)totalComparisons))
                        {
                            Debug.Log("[AudioModule] Duplicate check cancelled by user.");
                            return;
                        }

                        if (!this.AreAudioClipsEqual(audioClips[i], audioClips[j])) continue;

                        // Found a duplicate
                        if (this.DuplicateAudios.All(x => !x.Contains(audioClips[i])))
                        {
                            this.DuplicateAudios.Add(new HashSet<AudioClip> { audioClips[i], audioClips[j] });
                        }
                        else
                        {
                            this.DuplicateAudios.First(x => x.Contains(audioClips[i])).Add(audioClips[j]);
                        }

                        Debug.Log($"[AudioModule] Found duplicate: {audioClips[i].name} == {audioClips[j].name}");
                    }
                }

                if (this.DuplicateAudios.Count == 0)
                {
                    EditorUtility.DisplayDialog("No Duplicates Found",
                        $"Checked {audioClips.Count} audio clips. No duplicates detected!", "OK");
                }
                else
                {
                    var totalDuplicates = this.DuplicateAudios.Sum(set => set.Count - 1);
                    EditorUtility.DisplayDialog("Duplicates Found",
                        $"Found {this.DuplicateAudios.Count} groups of duplicates with {totalDuplicates} redundant clips.", "OK");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [TabGroup("Duplicates")]
        [BoxGroup("Duplicates/Actions")]
        [ButtonGroup("Duplicates/Actions/Buttons")]
        [Button("Select All Duplicates", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.7f, 0.3f)]
        [ShowIf("@DuplicateAudios.Count > 0")]
        private void SelectAllDuplicates()
        {
            var allDuplicates = this.DuplicateAudios.SelectMany(set => set).Cast<UnityEngine.Object>().ToArray();
            Selection.objects = allDuplicates;
            Debug.Log($"[AudioModule] Selected {allDuplicates.Length} duplicate audio clips in Project window.");
        }

        [TabGroup("Duplicates")]
        [BoxGroup("Duplicates/Actions")]
        [ButtonGroup("Duplicates/Actions/Buttons")]
        [Button("Keep One, Delete Rest", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.3f, 0.3f)]
        [ShowIf("@DuplicateAudios.Count > 0")]
        private void DeleteDuplicates()
        {
            var totalToDelete = this.DuplicateAudios.Sum(set => set.Count - 1);

            if (!EditorUtility.DisplayDialog("Delete Duplicate Audio Files",
                $"This will DELETE {totalToDelete} duplicate audio files permanently!\n\n" +
                $"For each duplicate group, the first file will be kept and all others will be deleted.\n\n" +
                $"THIS CANNOT BE UNDONE. Make sure you have a backup!\n\n" +
                $"Continue?",
                "Delete", "Cancel"))
            {
                return;
            }

            var deletedCount = 0;
            var failedCount = 0;

            try
            {
                foreach (var duplicateSet in this.DuplicateAudios)
                {
                    var clips = duplicateSet.ToList();
                    var keepClip = clips[0];

                    for (int i = 1; i < clips.Count; i++)
                    {
                        var clipToDelete = clips[i];
                        var path = AssetDatabase.GetAssetPath(clipToDelete);

                        EditorUtility.DisplayProgressBar("Deleting Duplicates",
                            $"Deleting {clipToDelete.name}...",
                            deletedCount / (float)totalToDelete);

                        if (AssetDatabase.DeleteAsset(path))
                        {
                            deletedCount++;
                            Debug.Log($"[AudioModule] Deleted duplicate: {clipToDelete.name} (kept: {keepClip.name})");
                        }
                        else
                        {
                            failedCount++;
                            Debug.LogError($"[AudioModule] Failed to delete: {path}");
                        }
                    }
                }

                AssetDatabase.Refresh();
                this.DuplicateAudios.Clear();

                EditorUtility.DisplayDialog("Deletion Complete",
                    $"Deleted {deletedCount} duplicate files.\n" +
                    (failedCount > 0 ? $"{failedCount} files failed to delete." : ""),
                    "OK");

                // Refresh analysis
                this.OnAnalyze();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Compares two audio clips sample-by-sample to determine if they are identical.
        /// REUSED from AddressableAudioFinderOdin.AreAudioClipsEqual()
        /// </summary>
        private bool AreAudioClipsEqual(AudioClip clipA, AudioClip clipB)
        {
            if (clipA.samples != clipB.samples || clipA.channels != clipB.channels || clipA.frequency != clipB.frequency)
            {
                // AudioClips have different settings
                return false;
            }

            var dataA = new float[clipA.samples * clipA.channels];
            var dataB = new float[clipB.samples * clipB.channels];

            clipA.GetData(dataA, 0);
            clipB.GetData(dataB, 0);

            // Compare audio data sample by sample
            for (var i = 0; i < dataA.Length; i++)
            {
                if (Math.Abs(dataA[i] - dataB[i]) > 0.0001f) // Use small epsilon for float comparison
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Applies optimal compression settings to an audio clip.
        /// REUSES logic from AddressableAudioFinderOdin.CompressAndFixAll()
        /// </summary>
        private void ApplyOptimalSettings(AudioInfo audioInfo)
        {
            var importer = audioInfo.Importer;
            var audioClip = audioInfo.Audio;
            var audioSetting = importer.defaultSampleSettings;
            var isLongSound = audioClip.length >= this.compressionSettings.longAudioLengthThreshold;

            // Apply optimal settings
            importer.forceToMono           = true;
            importer.loadInBackground      = true;
            audioSetting.loadType          = isLongSound ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            audioSetting.preloadAudioData  = !isLongSound;
            audioSetting.compressionFormat = AudioCompressionFormat.Vorbis;
            audioSetting.quality           = this.compressionSettings.quality;

            // Disable normalization to prevent audio artifacts
            var serializedObject = new SerializedObject(importer);
            var normalize = serializedObject.FindProperty("m_Normalize");
            normalize.boolValue = false;
            serializedObject.ApplyModifiedProperties();

            // Save and reimport
            importer.defaultSampleSettings = audioSetting;
            importer.SaveAndReimport();
        }

        #endregion

        #region Module Implementation

        protected override void OnAnalyze()
        {
            EditorUtility.DisplayProgressBar("Audio Analysis", "Scanning all audio clips in Addressables...", 0f);

            try
            {
                // Use AssetAnalysisService to find audio with wrong compression
                var issues = this.analysisService.FindAudioWithWrongCompression(
                    targetQuality: this.compressionSettings.quality,
                    longAudioLengthThreshold: this.compressionSettings.longAudioLengthThreshold,
                    forceRefresh: true
                );

                this.wrongCompressionAudioList = issues;

                if (this.wrongCompressionAudioList.Count > 0)
                {
                    Debug.Log($"[AudioModule] Analysis complete. Found {this.wrongCompressionAudioList.Count} audio clips with issues.");
                }
                else
                {
                    Debug.Log("[AudioModule] Analysis complete. All audio clips are optimally configured!");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        protected override void OnRefresh()
        {
            // Refresh using cached data (faster than full analysis)
            var issues = this.analysisService.FindAudioWithWrongCompression(
                targetQuality: this.compressionSettings.quality,
                longAudioLengthThreshold: this.compressionSettings.longAudioLengthThreshold,
                forceRefresh: false
            );

            this.wrongCompressionAudioList = issues;
            Debug.Log($"[AudioModule] Refreshed. {this.wrongCompressionAudioList.Count} issues found.");
        }

        protected override void OnClear()
        {
            this.wrongCompressionAudioList.Clear();
            this.DuplicateAudios.Clear();
            this.analysisService.ClearCache();
            Debug.Log("[AudioModule] Data cleared.");
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-refresh if we have no data
            if (this.wrongCompressionAudioList.Count == 0)
            {
                var allAudio = this.analysisService.GetAllAudioInfos(forceRefresh: false);
                if (allAudio.Count > 0)
                {
                    this.OnRefresh();
                }
            }
        }

        #endregion
    }
}
