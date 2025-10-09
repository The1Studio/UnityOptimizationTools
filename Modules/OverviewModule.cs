using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace TheOne.UITemplate.Editor.Optimization
{
    /// <summary>
    /// Overview Dashboard Module - Comprehensive project optimization overview.
    /// Provides project-wide statistics and health checks.
    /// Uses lightweight AssetDatabase queries for fast loading (target: < 200ms).
    /// </summary>
    public class OverviewModule : OptimizationModuleBase
    {
        public override string ModuleName => "Overview Dashboard";
        public override string ModuleIcon => "📊";

        // Cached statistics - updated on Refresh/Analyze
        private ProjectStats cachedStats;

        // Performance optimization: Cache for directory sizes
        private Dictionary<string, (long size, DateTime timestamp)> directorySizeCache = new Dictionary<string, (long, DateTime)>();
        private const int CacheValidityMinutes = 5; // Cache valid for 5 minutes
        private List<HealthCheckItem> healthChecks = new List<HealthCheckItem>();
        private DateTime lastRefreshTime;

        #region Project Statistics Section

        [TitleGroup("Project Statistics")]
        [InfoBox("Comprehensive project asset statistics and size breakdown", InfoMessageType.Info)]
        [PropertyOrder(1)]

        [BoxGroup("Project Statistics/Assets")]
        [HorizontalGroup("Project Statistics/Assets/Row1", Width = 0.33f)]
        [VerticalGroup("Project Statistics/Assets/Row1/Col1")]
        [LabelText("Total Assets"), DisplayAsString]
        [ShowInInspector]
        private string TotalAssetsDisplay =>
            this.cachedStats != null
            ? $"📦 {this.cachedStats.TotalAssets:N0}"
            : "Click Analyze";

        [VerticalGroup("Project Statistics/Assets/Row1/Col1")]
        [LabelText("Project Size"), DisplayAsString]
        [ShowInInspector]
        private string ProjectSizeDisplay =>
            this.cachedStats != null
            ? $"💾 {this.FormatBytes(this.cachedStats.TotalProjectSize)}"
            : "-";

        [HorizontalGroup("Project Statistics/Assets/Row1", Width = 0.33f)]
        [VerticalGroup("Project Statistics/Assets/Row1/Col2")]
        [LabelText("Textures"), DisplayAsString]
        [ShowInInspector]
        private string TexturesDisplay =>
            this.cachedStats != null
            ? $"🖼️ {this.cachedStats.TextureCount:N0} ({this.FormatBytes(this.cachedStats.TextureSize)})"
            : "-";

        [VerticalGroup("Project Statistics/Assets/Row1/Col2")]
        [LabelText("Audio Clips"), DisplayAsString]
        [ShowInInspector]
        private string AudioDisplay =>
            this.cachedStats != null
            ? $"🔊 {this.cachedStats.AudioCount:N0} ({this.FormatBytes(this.cachedStats.AudioSize)})"
            : "-";

        [HorizontalGroup("Project Statistics/Assets/Row1", Width = 0.33f)]
        [VerticalGroup("Project Statistics/Assets/Row1/Col3")]
        [LabelText("Prefabs"), DisplayAsString]
        [ShowInInspector]
        private string PrefabsDisplay =>
            this.cachedStats != null
            ? $"🎮 {this.cachedStats.PrefabCount:N0}"
            : "-";

        [VerticalGroup("Project Statistics/Assets/Row1/Col3")]
        [LabelText("Materials"), DisplayAsString]
        [ShowInInspector]
        private string MaterialsDisplay =>
            this.cachedStats != null
            ? $"🎨 {this.cachedStats.MaterialCount:N0}"
            : "-";

        [BoxGroup("Project Statistics/Assets")]
        [HorizontalGroup("Project Statistics/Assets/Row2", Width = 0.33f)]
        [VerticalGroup("Project Statistics/Assets/Row2/Col1")]
        [LabelText("Meshes"), DisplayAsString]
        [ShowInInspector]
        private string MeshesDisplay =>
            this.cachedStats != null
            ? $"🔷 {this.cachedStats.MeshCount:N0} ({this.cachedStats.TotalVertices:N0} verts)"
            : "-";

        [HorizontalGroup("Project Statistics/Assets/Row2", Width = 0.33f)]
        [VerticalGroup("Project Statistics/Assets/Row2/Col2")]
        [LabelText("Shaders"), DisplayAsString]
        [ShowInInspector]
        private string ShadersDisplay =>
            this.cachedStats != null
            ? $"🌈 {this.cachedStats.ShaderCount:N0} ({this.cachedStats.ShaderVariantCount:N0} variants)"
            : "-";

        [HorizontalGroup("Project Statistics/Assets/Row2", Width = 0.33f)]
        [VerticalGroup("Project Statistics/Assets/Row2/Col3")]
        [LabelText("Animations"), DisplayAsString]
        [ShowInInspector]
        private string AnimationsDisplay =>
            this.cachedStats != null
            ? $"🎬 {this.cachedStats.AnimationCount:N0}"
            : "-";

        [BoxGroup("Project Statistics/Addressables")]
        [HorizontalGroup("Project Statistics/Addressables/Row", Width = 0.5f)]
        [VerticalGroup("Project Statistics/Addressables/Row/Col1")]
        [LabelText("Addressable Assets"), DisplayAsString]
        [ShowInInspector]
        private string AddressablesDisplay =>
            this.cachedStats != null
            ? $"📦 {this.cachedStats.AddressableAssets:N0}"
            : "-";

        [HorizontalGroup("Project Statistics/Addressables/Row", Width = 0.5f)]
        [VerticalGroup("Project Statistics/Addressables/Row/Col2")]
        [LabelText("Groups"), DisplayAsString]
        [ShowInInspector]
        private string GroupsDisplay =>
            this.cachedStats != null
            ? $"📁 {this.cachedStats.AddressableGroups:N0}"
            : "-";

        [BoxGroup("Project Statistics/Addressables")]
        [LabelText("Estimated Build Size"), DisplayAsString]
        [ShowInInspector]
        [PropertySpace(0, 10)]
        private string BuildSizeEstimate =>
            this.cachedStats != null
            ? $"📊 {this.FormatBytes(this.cachedStats.EstimatedBuildSize)} (compressed)"
            : "-";

        #endregion

        #region Quick Health Check Section

        [TitleGroup("Quick Health Check")]
        [InfoBox("Automated scan for common optimization issues", InfoMessageType.Info)]
        [PropertyOrder(2)]

        [BoxGroup("Quick Health Check/Status")]
        [ShowInInspector]
        [LabelText("Overall Health Score")]
        [ProgressBar(0, 100, ColorGetter = "GetHealthScoreColor")]
        private float HealthScore => this.CalculateHealthScore();

        [BoxGroup("Quick Health Check/Status")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Status")]
        [GUIColor("GetHealthStatusColor")]
        private string HealthStatus => this.GetHealthStatusText();

        [BoxGroup("Quick Health Check/Issues")]
        [TableList(AlwaysExpanded = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
        [ShowInInspector]
        [HideLabel]
        private List<HealthCheckItem> HealthCheckItems => this.healthChecks;

        #endregion

        // Quick Actions section removed - use tabs for navigation

        #region Recent Activity

        [TitleGroup("Recent Activity")]
        [PropertyOrder(4)]

        [BoxGroup("Recent Activity/Info")]
        [ShowInInspector]
        [ReadOnly, GUIColor(1, 1, 1)]
        [LabelText("Last Refresh")]
        private string LastRefresh =>
            this.lastRefreshTime != DateTime.MinValue
            ? $"🕒 {this.lastRefreshTime:yyyy-MM-dd HH:mm:ss}"
            : "Not analyzed yet - Click 'Analyze All Modules'";

        [BoxGroup("Recent Activity/Info")]
        [ShowInInspector]
        [ReadOnly, GUIColor(1, 1, 1)]
        [LabelText("Analysis Duration")]
        private string AnalysisDuration =>
            this.cachedStats != null && this.cachedStats.AnalysisDurationMs > 0
            ? $"⏱️ {this.cachedStats.AnalysisDurationMs:F0}ms"
            : "-";

        [BoxGroup("Recent Activity/Summary")]
        [InfoBox("$GetTopIssuesMessage", InfoMessageType.Warning, VisibleIf = "HasIssues")]
        [PropertySpace(10)]
        [HideInInspector]
        public bool HasIssues => this.cachedStats != null && this.cachedStats.TotalIssues > 0;

        private string GetTopIssuesMessage()
        {
            if (this.cachedStats == null) return "";

            var issues = new[]
            {
                (Count: this.cachedStats.UncompressedTextures, Name: "uncompressed textures", Severity: "High"),
                (Count: this.cachedStats.WrongCompressionAudio, Name: "wrong compression audio", Severity: "High"),
                (Count: this.cachedStats.NonPOTTextures, Name: "non-POT textures", Severity: "Medium"),
                (Count: this.cachedStats.LargeMeshes, Name: "high-poly meshes (>10k verts)", Severity: "Medium"),
                (Count: this.cachedStats.UnusedShaderKeywords, Name: "unused shader keywords", Severity: "Low")
            }.Where(i => i.Count > 0)
             .OrderByDescending(i => i.Count)
             .Take(5);

            var message = $"⚠️ Found {this.cachedStats.TotalIssues:N0} optimization opportunities:\n\n";
            foreach (var issue in issues)
            {
                var icon = issue.Severity == "High" ? "🔴" : issue.Severity == "Medium" ? "🟡" : "🟢";
                message += $"{icon} {issue.Count:N0} {issue.Name}\n";
            }
            return message.TrimEnd();
        }

        #endregion

        #region Module Implementation

        protected override void OnAnalyze()
        {
            this.AnalyzeAllModules();
        }

        protected override void OnRefresh()
        {
            this.RefreshStats();
            this.RunHealthChecks();
        }

        protected override void OnClear()
        {
            this.cachedStats = null;
            this.healthChecks.Clear();
            this.directorySizeCache.Clear(); // Clear directory size cache
            this.lastRefreshTime = DateTime.MinValue;
            Debug.Log("[OverviewModule] Cache cleared.");
        }

        private void AnalyzeAllModules()
        {
            EditorUtility.DisplayProgressBar("Overview", "Analyzing entire project...", 0f);
            try
            {
                this.RefreshStats();
                this.RunHealthChecks();
                Debug.Log($"[Overview] Full analysis complete. Found {this.cachedStats.TotalIssues} issues across {this.cachedStats.TotalAssets:N0} assets.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-refresh if cache is stale (> 5 minutes) or never loaded
            if (this.cachedStats == null || (DateTime.Now - this.lastRefreshTime).TotalMinutes > 5)
            {
                this.RefreshStats();
                this.RunHealthChecks();
            }
        }

        #endregion

        #region Data Aggregation

        /// <summary>
        /// Refreshes project statistics using lightweight AssetDatabase queries.
        /// Target: < 200ms execution time.
        /// </summary>
        private void RefreshStats()
        {
            var startTime = DateTime.Now;
            this.cachedStats = new ProjectStats();

            // Count all asset types
            this.cachedStats.TextureCount  = AssetDatabase.FindAssets("t:Texture2D").Length;
            this.cachedStats.AudioCount    = AssetDatabase.FindAssets("t:AudioClip").Length;
            this.cachedStats.PrefabCount   = AssetDatabase.FindAssets("t:Prefab").Length;
            this.cachedStats.MaterialCount = AssetDatabase.FindAssets("t:Material").Length;
            this.cachedStats.MeshCount     = AssetDatabase.FindAssets("t:Mesh").Length;
            this.cachedStats.ShaderCount   = AssetDatabase.FindAssets("t:Shader").Length;
            this.cachedStats.AnimationCount   = AssetDatabase.FindAssets("t:AnimationClip").Length;

            // Calculate sizes (lightweight approach)
            this.cachedStats.TextureSize = this.CalculateAssetSize("t:Texture2D");
            this.cachedStats.AudioSize   = this.CalculateAssetSize("t:AudioClip");

            // Check for optimization issues (lightweight)
            this.AnalyzeTextureIssues();
            this.AnalyzeAudioIssues();
            this.AnalyzeMeshIssues();

            // Addressables stats
            try
            {
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    this.cachedStats.AddressableGroups = settings.groups.Count(g => g != null && !g.ReadOnly);
                    this.cachedStats.AddressableAssets = settings.groups
                        .Where(g => g != null)
                        .Sum(g => g.entries.Count);
                }
            }
            catch
            {
                this.cachedStats.AddressableGroups = 0;
                this.cachedStats.AddressableAssets    = 0;
            }

            // Calculate totals
            this.cachedStats.TotalAssets = this.cachedStats.TextureCount + this.cachedStats.AudioCount + this.cachedStats.PrefabCount + this.cachedStats.MaterialCount + this.cachedStats.MeshCount + this.cachedStats.ShaderCount + this.cachedStats.AnimationCount;

            this.cachedStats.TotalProjectSize   = this.CalculateTotalProjectSize();
            this.cachedStats.EstimatedBuildSize = (long)(this.cachedStats.TotalProjectSize * 0.3f); // Rough estimate: 30% after compression

            this.cachedStats.TotalIssues = this.cachedStats.UncompressedTextures + this.cachedStats.WrongCompressionAudio + this.cachedStats.NonPOTTextures + this.cachedStats.LargeMeshes + this.cachedStats.UnusedShaderKeywords;

            this.cachedStats.AnalysisDurationMs = (DateTime.Now - startTime).TotalMilliseconds;
            this.lastRefreshTime                   = DateTime.Now;

            Debug.Log($"[Overview] Stats refreshed in {this.cachedStats.AnalysisDurationMs:F1}ms - " +
                     $"{this.cachedStats.TotalAssets:N0} assets, {this.cachedStats.TotalIssues:N0} issues");
        }

        /// <summary>
        /// Analyzes textures for common optimization issues.
        /// </summary>
        private void AnalyzeTextureIssues()
        {
            var textures = AssetDatabase.FindAssets("t:Texture2D")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            // Uncompressed textures
            this.cachedStats.UncompressedTextures = textures.Count(path =>
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                return importer != null && importer.textureCompression == TextureImporterCompression.Uncompressed;
            });

            // Non-POT textures (sample check - full analysis in TextureModule)
            this.cachedStats.NonPOTTextures = textures.Take(100).Count(path =>
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) return false;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) return false;
                return !this.IsPowerOfTwo(texture.width) || !this.IsPowerOfTwo(texture.height);
            });
        }

        /// <summary>
        /// Analyzes audio clips for compression issues.
        /// </summary>
        private void AnalyzeAudioIssues()
        {
            var audioClips = AssetDatabase.FindAssets("t:AudioClip")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            this.cachedStats.WrongCompressionAudio = audioClips.Count(path =>
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                return importer != null &&
                       importer.defaultSampleSettings.compressionFormat != AudioCompressionFormat.Vorbis;
            });
        }

        /// <summary>
        /// Analyzes meshes for high poly counts.
        /// </summary>
        private void AnalyzeMeshIssues()
        {
            // Sample check (full analysis in MeshModule)
            var meshPaths = AssetDatabase.FindAssets("t:Mesh")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Take(50)
                .ToList();

            foreach (var path in meshPaths)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null)
                {
                    this.cachedStats.TotalVertices += mesh.vertexCount;
                    if (mesh.vertexCount > 10000)
                    {
                        this.cachedStats.LargeMeshes++;
                    }
                }
            }
        }

        /// <summary>
        /// Runs comprehensive health checks across all optimization areas.
        /// </summary>
        private void RunHealthChecks()
        {
            this.healthChecks.Clear();

            if (this.cachedStats == null) return;

            // Texture health checks
            if (this.cachedStats.UncompressedTextures > 0)
            {
                this.healthChecks.Add(new HealthCheckItem
                {
                    Category       = "Textures",
                    Issue          = $"{this.cachedStats.UncompressedTextures:N0} uncompressed textures",
                    Severity       = Severity.High,
                    Recommendation = "Enable compression to reduce build size",
                    Module         = "Textures"
                });
            }

            if (this.cachedStats.NonPOTTextures > 0)
            {
                this.healthChecks.Add(new HealthCheckItem
                {
                    Category       = "Textures",
                    Issue          = $"{this.cachedStats.NonPOTTextures:N0}+ non-power-of-two textures",
                    Severity       = Severity.Medium,
                    Recommendation = "Use POT sizes for better mobile performance",
                    Module         = "Textures"
                });
            }

            // Audio health checks
            if (this.cachedStats.WrongCompressionAudio > 0)
            {
                this.healthChecks.Add(new HealthCheckItem
                {
                    Category       = "Audio",
                    Issue          = $"{this.cachedStats.WrongCompressionAudio:N0} audio clips with wrong compression",
                    Severity       = Severity.High,
                    Recommendation = "Use Vorbis compression for mobile",
                    Module         = "Audio"
                });
            }

            // Mesh health checks
            if (this.cachedStats.LargeMeshes > 0)
            {
                this.healthChecks.Add(new HealthCheckItem
                {
                    Category       = "Meshes",
                    Issue          = $"{this.cachedStats.LargeMeshes:N0} high-poly meshes (>10k verts)",
                    Severity       = Severity.Medium,
                    Recommendation = "Optimize mesh vertex count for mobile",
                    Module         = "Mesh"
                });
            }

            // Addressables health checks
            if (this.cachedStats.AddressableAssets == 0)
            {
                this.healthChecks.Add(new HealthCheckItem
                {
                    Category = "Addressables",
                    Issue = "No addressable assets configured",
                    Severity = Severity.Low,
                    Recommendation = "Use Addressables for dynamic content loading",
                    Module = "Addressables"
                });
            }

            // Project size check
            if (this.cachedStats.TotalProjectSize > 500 * 1024 * 1024) // > 500MB
            {
                this.healthChecks.Add(new HealthCheckItem
                {
                    Category       = "Project",
                    Issue          = $"Large project size: {this.FormatBytes(this.cachedStats.TotalProjectSize)}",
                    Severity       = Severity.Medium,
                    Recommendation = "Review asset optimization across all modules",
                    Module         = "Overview"
                });
            }

            // Add success message if no issues
            if (this.healthChecks.Count == 0)
            {
                this.healthChecks.Add(new HealthCheckItem
                {
                    Category = "Status",
                    Issue = "No critical issues found",
                    Severity = Severity.Success,
                    Recommendation = "Project is well optimized!",
                    Module = "Overview"
                });
            }

            Debug.Log($"[Overview] Health check complete. Found {this.healthChecks.Count(h => h.Severity != Severity.Success)} issues.");
        }

        /// <summary>
        /// Exports a comprehensive project optimization report.
        /// </summary>
        private void ExportProjectReport()
        {
            if (this.cachedStats == null)
            {
                Debug.LogWarning("[Overview] No data to export. Run analysis first.");
                return;
            }

            var reportPath = $"OptimizationReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var fullPath = Path.Combine(Application.dataPath, "..", reportPath);

            using (var writer = new StreamWriter(fullPath))
            {
                writer.WriteLine("═══════════════════════════════════════════════════════════");
                writer.WriteLine("         UNITY PROJECT OPTIMIZATION REPORT");
                writer.WriteLine("═══════════════════════════════════════════════════════════");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"Project: {Application.productName}");
                writer.WriteLine($"Unity Version: {Application.unityVersion}");
                writer.WriteLine();

                writer.WriteLine("PROJECT STATISTICS");
                writer.WriteLine("───────────────────────────────────────────────────────────");
                writer.WriteLine($"Total Assets:        {this.cachedStats.TotalAssets:N0}");
                writer.WriteLine($"Project Size:        {this.FormatBytes(this.cachedStats.TotalProjectSize)}");
                writer.WriteLine($"Build Size Estimate: {this.FormatBytes(this.cachedStats.EstimatedBuildSize)}");
                writer.WriteLine();

                writer.WriteLine("ASSET BREAKDOWN");
                writer.WriteLine("───────────────────────────────────────────────────────────");
                writer.WriteLine($"Textures:     {this.cachedStats.TextureCount:N0,8} ({this.FormatBytes(this.cachedStats.TextureSize)})");
                writer.WriteLine($"Audio Clips:  {this.cachedStats.AudioCount:N0,8} ({this.FormatBytes(this.cachedStats.AudioSize)})");
                writer.WriteLine($"Prefabs:      {this.cachedStats.PrefabCount:N0,8}");
                writer.WriteLine($"Materials:    {this.cachedStats.MaterialCount:N0,8}");
                writer.WriteLine($"Meshes:       {this.cachedStats.MeshCount:N0,8} ({this.cachedStats.TotalVertices:N0} total verts)");
                writer.WriteLine($"Shaders:      {this.cachedStats.ShaderCount:N0,8}");
                writer.WriteLine($"Animations:   {this.cachedStats.AnimationCount:N0,8}");
                writer.WriteLine();

                writer.WriteLine("ADDRESSABLES");
                writer.WriteLine("───────────────────────────────────────────────────────────");
                writer.WriteLine($"Groups:       {this.cachedStats.AddressableGroups:N0}");
                writer.WriteLine($"Assets:       {this.cachedStats.AddressableAssets:N0}");
                writer.WriteLine();

                writer.WriteLine("OPTIMIZATION ISSUES");
                writer.WriteLine("───────────────────────────────────────────────────────────");
                writer.WriteLine($"Total Issues Found: {this.cachedStats.TotalIssues:N0}");
                writer.WriteLine();

                foreach (var check in this.healthChecks.OrderByDescending(h => h.Severity))
                {
                    var severityIcon = check.Severity == Severity.High ? "[!!!]" :
                                      check.Severity == Severity.Medium ? "[!!]" :
                                      check.Severity == Severity.Low ? "[!]" : "[OK]";

                    writer.WriteLine($"{severityIcon} {check.Category}: {check.Issue}");
                    writer.WriteLine($"     → {check.Recommendation}");
                    writer.WriteLine();
                }

                writer.WriteLine("═══════════════════════════════════════════════════════════");
                writer.WriteLine("End of Report");
                writer.WriteLine("═══════════════════════════════════════════════════════════");
            }

            Debug.Log($"[Overview] Report exported to: {fullPath}");
            EditorUtility.RevealInFinder(fullPath);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculates total size of assets of a specific type.
        /// </summary>
        private long CalculateAssetSize(string assetType)
        {
            var paths = AssetDatabase.FindAssets(assetType)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Take(200); // Sample for performance

            long totalSize = 0;
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(Application.dataPath, "..", path);
                if (File.Exists(fullPath))
                {
                    totalSize += new FileInfo(fullPath).Length;
                }
            }

            return totalSize;
        }

        /// <summary>
        /// Calculates total project size.
        /// </summary>
        private long CalculateTotalProjectSize()
        {
            var assetsPath = Application.dataPath;
            if (!Directory.Exists(assetsPath)) return 0;

            var dirInfo = new DirectoryInfo(assetsPath);
            return this.GetDirectorySize(dirInfo);
        }

        /// <summary>
        /// Recursively calculates directory size.
        /// </summary>
        private long GetDirectorySize(DirectoryInfo directory)
        {
            var path = directory.FullName;
            
            // Check cache first
            if (this.directorySizeCache.TryGetValue(path, out var cached))
            {
                // Cache is valid for CacheValidityMinutes
                if ((DateTime.Now - cached.timestamp).TotalMinutes < CacheValidityMinutes)
                {
                    return cached.size;
                }
            }

            long size = 0;
            try
            {
                // Add file sizes
                var files = directory.GetFiles();
                foreach (var file in files)
                {
                    size += file.Length;
                }

                // Add subdirectory sizes
                var dirs = directory.GetDirectories();
                foreach (var dir in dirs)
                {
                    size += this.GetDirectorySize(dir);
                }
                
                // Cache the result
                this.directorySizeCache[path] = (size, DateTime.Now);
            }
            catch
            {
                // Skip directories that can't be accessed
            }
            return size;
        }

        /// <summary>
        /// Formats byte count to human-readable string.
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Checks if a number is a power of two.
        /// </summary>
        private bool IsPowerOfTwo(int n)
        {
            return n > 0 && (n & (n - 1)) == 0;
        }

        /// <summary>
        /// Calculates overall health score (0-100).
        /// </summary>
        private float CalculateHealthScore()
        {
            if (this.cachedStats == null) return 0f;

            var totalAssets = Math.Max(this.cachedStats.TotalAssets, 1);
            var issueRatio  = (float)this.cachedStats.TotalIssues / totalAssets;
            var score       = 100f - (issueRatio * 100f);

            return Mathf.Clamp(score, 0f, 100f);
        }

        /// <summary>
        /// Gets color for health score progress bar.
        /// </summary>
        private Color GetHealthScoreColor()
        {
            var score = this.HealthScore;
            if (score >= 80f) return new Color(0.3f, 1f, 0.3f); // Green
            if (score >= 60f) return new Color(1f, 0.9f, 0.3f); // Yellow
            return new Color(1f, 0.3f, 0.3f); // Red
        }

        /// <summary>
        /// Gets health status text based on score.
        /// </summary>
        private string GetHealthStatusText()
        {
            if (this.cachedStats == null) return "Not Analyzed";

            var score = this.HealthScore;
            if (score >= 90f) return "✅ Excellent";
            if (score >= 80f) return "🟢 Good";
            if (score >= 60f) return "🟡 Fair";
            if (score >= 40f) return "🟠 Needs Attention";
            return "🔴 Critical";
        }

        /// <summary>
        /// Gets GUI color for health status.
        /// </summary>
        private Color GetHealthStatusColor()
        {
            if (this.cachedStats == null) return Color.white;

            var score = this.HealthScore;
            if (score >= 80f) return new Color(0.5f, 1f, 0.5f);
            if (score >= 60f) return new Color(1f, 1f, 0.5f);
            return new Color(1f, 0.5f, 0.5f);
        }

        /// <summary>
        /// Switches to a different module by name.
        /// </summary>
        private void SwitchToModule(string moduleName)
        {
            Debug.Log($"[Overview] Requesting switch to {moduleName} module");
            // Note: Actual navigation handled by OptimizationHubWindow
            // This is a placeholder for UI integration
        }

        #endregion

        #region Data Models

        /// <summary>
        /// Comprehensive project statistics container.
        /// </summary>
        private class ProjectStats
        {
            // Asset counts
            public int TextureCount;
            public int AudioCount;
            public int PrefabCount;
            public int MaterialCount;
            public int MeshCount;
            public int ShaderCount;
            public int AnimationCount;

            // Asset sizes
            public long TextureSize;
            public long AudioSize;
            public long TotalProjectSize;
            public long EstimatedBuildSize;

            // Mesh details
            public int TotalVertices;

            // Shader details
            public int ShaderVariantCount;

            // Addressables
            public int AddressableGroups;
            public int AddressableAssets;

            // Issues
            public int UncompressedTextures;
            public int NonPOTTextures;
            public int WrongCompressionAudio;
            public int LargeMeshes;
            public int UnusedShaderKeywords;

            // Aggregates
            public int TotalAssets;
            public int TotalIssues;
            public double AnalysisDurationMs;
        }

        /// <summary>
        /// Health check result item.
        /// </summary>
        [Serializable]
        private class HealthCheckItem
        {
            [TableColumnWidth(100)]
            [GUIColor("GetSeverityColor")]
            public string Category;

            [TableColumnWidth(250)]
            public string Issue;

            [TableColumnWidth(80)]
            [GUIColor("GetSeverityColor")]
            public Severity Severity;

            [TableColumnWidth(300)]
            public string Recommendation;

            [TableColumnWidth(100)]
            [Button("Go")]
            public string Module;

            private Color GetSeverityColor()
            {
                switch (this.Severity)
                {
                    case Severity.High: return new Color(1f, 0.3f, 0.3f);
                    case Severity.Medium: return new Color(1f, 0.8f, 0.3f);
                    case Severity.Low: return new Color(0.8f, 0.8f, 0.8f);
                    case Severity.Success: return new Color(0.3f, 1f, 0.3f);
                    default: return Color.white;
                }
            }
        }

        /// <summary>
        /// Issue severity levels.
        /// </summary>
        private enum Severity
        {
            Success,
            Low,
            Medium,
            High
        }

        #endregion
    }
}
