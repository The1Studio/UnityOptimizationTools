using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using TheOne.UITemplate.Editor.Optimization.Services;
using UnityEditor;
using UnityEngine;

namespace TheOne.UITemplate.Editor.Optimization.Modules
{
    /// <summary>
    /// Reports Module - Export optimization analysis results to CSV/JSON for documentation and tracking.
    /// Reuses data from existing modules via AssetAnalysisService without re-analyzing.
    /// Supports multiple export formats and maintains export history.
    /// </summary>
    public class ReportsModule : OptimizationModuleBase
    {
        public override string ModuleName => "Reports & Export";
        public override string ModuleIcon => "📄";

        private readonly AssetAnalysisService analysisService = new AssetAnalysisService();

        // Export settings
        private string exportPath = "Assets/OptimizationReports";
        private ExportFormat exportFormat = ExportFormat.CSV;
        private ReportTemplate reportTemplate = ReportTemplate.Summary;
        private bool autoOpenAfterExport = true;
        private bool includeTimestampInFilename = true;
        private string customReportHeader = "Unity Optimization Report";

        // Export history
        private List<ExportHistoryEntry> exportHistory = new List<ExportHistoryEntry>();
        private const int MaxHistoryEntries = 10;

        #region Tab: Summary

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [Title("Export Statistics", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Total Exports")]
        private string TotalExportsDisplay => $"📄 {this.exportHistory.Count:N0} reports generated";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Export Format")]
        [GUIColor(0.6f, 0.8f, 1f)]
        private string FormatDisplay => $"📋 {this.exportFormat}";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Export Location")]
        private string LocationDisplay => $"📁 {this.exportPath}";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Issues")]
        [Title("Quick Export", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Export optimization analysis results to various formats for documentation and tracking.", InfoMessageType.Info)]
        [PropertySpace(10)]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Export All Modules", ButtonSizes.Large)]
        [GUIColor(0.4f, 1f, 0.7f)]
        private void QuickExportAll()
        {
            this.ExportAllModules();
        }

        [TabGroup("Tabs", "Summary")]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Open Export Folder", ButtonSizes.Large)]
        [GUIColor(0.6f, 0.8f, 1f)]
        private void OpenFolderSummary()
        {
            this.OpenExportFolder();
        }

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/LastExport")]
        [InfoBox("$GetLastExportInfo", InfoMessageType.Info, VisibleIf = "HasExportHistory")]
        [PropertyOrder(10)]
        [HideInInspector]
        public bool HasExportHistorySummary => this.exportHistory.Count > 0;

        #endregion

        #region Tab 1: Export

        [TabGroup("Tabs", "Export")]
        [Title("Export Settings", TitleAlignment = TitleAlignments.Centered)]
        [FolderPath(AbsolutePath = false, RequireExistingPath = false)]
        [LabelText("Export Path")]
        [ShowInInspector]
        private string ExportPath
        {
            get => this.exportPath;
            set => this.exportPath = value;
        }

        [TabGroup("Tabs", "Export")]
        [EnumToggleButtons]
        [LabelText("Export Format")]
        [ShowInInspector]
        private ExportFormat SelectedExportFormat
        {
            get => this.exportFormat;
            set => this.exportFormat = value;
        }

        [TabGroup("Tabs", "Export")]
        [EnumToggleButtons]
        [LabelText("Report Template")]
        [ShowInInspector]
        private ReportTemplate SelectedReportTemplate
        {
            get => this.reportTemplate;
            set => this.reportTemplate = value;
        }

        [TabGroup("Tabs", "Export")]
        [Title("Export Actions", TitleAlignment = TitleAlignments.Centered)]
        [PropertySpace(10)]
        [Button(ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void ExportAllModules()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Exporting Report", "Gathering data from all modules...", 0f);

                var reportData = this.GatherAllModuleData();
                var filePath   = this.GenerateFilePath("AllModules");

                EditorUtility.DisplayProgressBar("Exporting Report", "Writing report file...", 0.5f);

                switch (this.exportFormat)
                {
                    case ExportFormat.CSV:
                        this.WriteCSVReport(filePath, reportData);
                        break;
                    case ExportFormat.JSON:
                        this.WriteJSONReport(filePath, reportData);
                        break;
                    case ExportFormat.Markdown:
                        this.WriteMarkdownReport(filePath, reportData);
                        break;
                    case ExportFormat.HTML:
                        this.WriteHTMLReport(filePath, reportData);
                        break;
                }

                this.AddToHistory(filePath);

                Debug.Log($"[ReportsModule] Successfully exported all modules to: {filePath}");

                if (this.autoOpenAfterExport)
                    EditorUtility.RevealInFinder(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ReportsModule] Export failed: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ExportCurrentModule()
        {
            try
            {
                // Get the active OptimizationHubWindow
                var window = EditorWindow.GetWindow<OptimizationHubWindow>(false, "🔧 Optimization Hub", false);
                if (window == null)
                {
                    EditorUtility.DisplayDialog("Error", "Optimization Hub window not found. Please open the Optimization Hub first.", "OK");
                    return;
                }

                // Prompt user to select which module to export
                var moduleOptions = new[]
                {
                    "Textures", "Audio", "Meshes", "Fonts", "Shaders", 
                    "Addressables", "Overview", "All Modules"
                };

                var selectedIndex = EditorUtility.DisplayDialogComplex(
                    "Export Current Module",
                    "Which module would you like to export?",
                    "Textures",
                    "Audio",
                    "Cancel"
                );

                if (selectedIndex == 2) return; // Cancel

                string moduleName;
                switch (selectedIndex)
                {
                    case 0: moduleName = "Textures"; break;
                    case 1: moduleName = "Audio"; break;
                    default: moduleName = "AllModules"; break;
                }

                EditorUtility.DisplayProgressBar("Exporting Module", $"Exporting {moduleName}...", 0.5f);

                var reportData = this.GatherModuleData(moduleName);
                var filePath   = this.GenerateFilePath(moduleName);

                switch (this.exportFormat)
                {
                    case ExportFormat.CSV:
                        this.WriteCSVReport(filePath, reportData);
                        break;
                    case ExportFormat.JSON:
                        this.WriteJSONReport(filePath, reportData);
                        break;
                    case ExportFormat.Markdown:
                        this.WriteMarkdownReport(filePath, reportData);
                        break;
                    case ExportFormat.HTML:
                        this.WriteHTMLReport(filePath, reportData);
                        break;
                }

                this.AddToHistory(filePath);

                Debug.Log($"[ReportsModule] Successfully exported {moduleName} to: {filePath}");

                if (this.autoOpenAfterExport)
                    EditorUtility.RevealInFinder(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ReportsModule] Export failed: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [TabGroup("Tabs", "Export")]
        [Title("Last Export Info", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("$GetLastExportInfo", InfoMessageType.Info, VisibleIf = "HasExportHistory")]
        [PropertyOrder(10)]
        [HideInInspector]
        public bool HasExportHistory => this.exportHistory.Count > 0;

        private string GetLastExportInfo()
        {
            if (this.exportHistory.Count == 0) return "No exports yet.";

            var last     = this.exportHistory[0];
            var fileSize = File.Exists(last.FilePath) ? new FileInfo(last.FilePath).Length / 1024f : 0;

            return $"Last Export:\n" +
                   $"• Time: {last.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                   $"• Path: {last.FilePath}\n" +
                   $"• Size: {fileSize:F2} KB";
        }

        #endregion

        #region Tab 2: History

        [TabGroup("Tabs", "History")]
        [Title("Recent Exports", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector]
        [TableList(AlwaysExpanded = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
        private List<ExportHistoryEntry> ExportHistory => this.exportHistory;

        [TabGroup("Tabs", "History")]
        [HorizontalGroup("Tabs/History/Actions")]
        [Button(ButtonSizes.Medium)]
        [EnableIf("HasExportHistory")]
        private void OpenExportFolder()
        {
            if (!Directory.Exists(this.exportPath))
            {
                Debug.LogWarning($"[ReportsModule] Export folder does not exist: {this.exportPath}");
                return;
            }

            EditorUtility.RevealInFinder(this.exportPath);
        }

        [TabGroup("Tabs", "History")]
        [HorizontalGroup("Tabs/History/Actions")]
        [Button(ButtonSizes.Medium)]
        [EnableIf("HasExportHistory")]
        private void ClearHistory()
        {
            this.exportHistory.Clear();
            Debug.Log("[ReportsModule] Export history cleared.");
        }

        #endregion

        #region Tab 3: Settings

        [TabGroup("Tabs", "Settings")]
        [Title("Export Settings", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector]
        [LabelText("Auto-open after export")]
        private bool AutoOpenAfterExport
        {
            get => this.autoOpenAfterExport;
            set => this.autoOpenAfterExport = value;
        }

        [TabGroup("Tabs", "Settings")]
        [ShowInInspector]
        [LabelText("Include timestamps in filename")]
        private bool IncludeTimestampInFilename
        {
            get => this.includeTimestampInFilename;
            set => this.includeTimestampInFilename = value;
        }

        [TabGroup("Tabs", "Settings")]
        [ShowInInspector]
        [LabelText("Custom report header")]
        [MultiLineProperty(3)]
        private string CustomReportHeader
        {
            get => this.customReportHeader;
            set => this.customReportHeader = value;
        }

        [TabGroup("Tabs", "Settings")]
        [InfoBox("Template Descriptions:\n\n" +
                 "• Summary: Overview statistics only (fast, compact)\n" +
                 "• Detailed: Full asset lists with all properties\n" +
                 "• Custom: User-defined data selection (future feature)",
                 InfoMessageType.Info)]
        [PropertyOrder(10)]
        [HideInInspector]
        public bool ShowTemplateInfo = true;

        #endregion

        #region Module Implementation

        protected override void OnAnalyze()
        {
            // Reports module doesn't analyze - it exports existing data
            Debug.Log("[ReportsModule] Use 'Export All Modules' to generate reports from existing analysis data.");
        }

        protected override void OnRefresh()
        {
            // Refresh export history file info
            foreach (var entry in this.exportHistory)
            {
                if (File.Exists(entry.FilePath))
                {
                    var fileInfo = new FileInfo(entry.FilePath);
                    entry.FileSize = fileInfo.Length;
                }
            }
            Debug.Log("[ReportsModule] Export history refreshed.");
        }

        protected override void OnClear()
        {
            this.exportHistory.Clear();
            Debug.Log("[ReportsModule] Data cleared.");
        }

        #endregion

        #region Data Gathering

        /// <summary>
        /// Gathers analysis data from all modules using AssetAnalysisService.
        /// Does NOT re-analyze - reuses cached data.
        /// </summary>
        private ReportData GatherAllModuleData()
        {
            var data = new ReportData
            {
                GeneratedAt  = DateTime.Now,
                ReportHeader = this.customReportHeader,
                Template     = this.reportTemplate
            };

            // Textures
            var textureInfos = this.analysisService.GetAllTextureInfos(forceRefresh: false);
            data.TotalTextures        = textureInfos.Count;
            data.UncompressedTextures = this.analysisService.FindUncompressedTextures(forceRefresh: false).Count;
            data.TexturesWithMipMap   = this.analysisService.FindTexturesWithMipMap(forceRefresh: false).Count;
            data.TexturesNotInAtlas   = this.analysisService.FindTexturesNotInAtlas(forceRefresh: false).Count;

            if (this.reportTemplate == ReportTemplate.Detailed)
            {
                data.TextureDetails = textureInfos.Select(t => new TextureDetail
                {
                    Name = t.Texture.name,
                    Path = t.Path,
                    Size = $"{t.TextureSize.x}x{t.TextureSize.y}",
                    FileSize = t.FileSize,
                    Compression = t.CompressionType.ToString(),
                    MipMap = t.GenerateMipMapEnabled,
                    ReadWrite = t.ReadWriteEnabled
                }).ToList();
            }

            // Audio
            var audioInfos = this.analysisService.GetAllAudioInfos(forceRefresh: false);
            data.TotalAudioClips       = audioInfos.Count;
            data.WrongCompressionAudio = this.analysisService.FindAudioWithWrongCompression(forceRefresh: false).Count;

            if (this.reportTemplate == ReportTemplate.Detailed)
            {
                data.AudioDetails = audioInfos.Values.Select(a => new AudioDetail
                {
                    Name = a.Audio.name,
                    Length = a.Audio.length,
                    Channels = a.Audio.channels,
                    ForceToMono = a.ForceToMono,
                    CompressionFormat = a.CompressionFormat.ToString(),
                    Quality = a.Quality,
                    LoadType = a.LoadType.ToString()
                }).ToList();
            }

            // Meshes
            var meshesByCompression = this.analysisService.GetMeshesByCompression(forceRefresh: false);
            data.TotalMeshes = meshesByCompression.Values.Sum(list => list.Count);
            data.UncompressedMeshes = meshesByCompression[ModelImporterMeshCompression.Off].Count;

            if (this.reportTemplate == ReportTemplate.Detailed)
            {
                data.MeshDetails = meshesByCompression.Values.SelectMany(list => list).Select(m => new MeshDetail
                {
                    Name = m.Mesh.name,
                    VertexCount = m.Mesh.vertexCount,
                    TriangleCount = m.Mesh.triangles.Length / 3,
                    Compression = m.ModelImporter.meshCompression.ToString()
                }).ToList();
            }

            // Fonts
            var (compressedFonts, uncompressedFonts) = this.analysisService.GetFontsByCompression(forceRefresh: false);
            data.TotalFonts                          = compressedFonts.Count + uncompressedFonts.Count;
            data.CompressedFonts                     = compressedFonts.Count;
            data.UncompressedFonts                   = uncompressedFonts.Count;

            if (this.reportTemplate == ReportTemplate.Detailed)
            {
                data.FontDetails = compressedFonts.Concat(uncompressedFonts).Select(f => new FontDetail
                {
                    Name = f.Font.name,
                    FontCase = f.FontImporter.fontTextureCase.ToString(),
                    IsCompressed = f.FontImporter.fontTextureCase == FontTextureCase.CustomSet
                }).ToList();
            }

            // Addressables
            try
            {
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    data.AddressableGroups = settings.groups.Count(g => g != null && !g.ReadOnly);
                    data.AddressableAssets = settings.groups.Where(g => g != null).Sum(g => g.entries.Count);
                }
            }
            catch
            {
                data.AddressableGroups = 0;
                data.AddressableAssets = 0;
            }

            return data;
        }

        /// <summary>
        /// Gathers data for a specific module.
        /// </summary>
        private ReportData GatherModuleData(string moduleName)
        {
            var data = new ReportData
            {
                GeneratedAt  = DateTime.Now,
                ReportHeader = $"{this.customReportHeader} - {moduleName}",
                Template     = this.reportTemplate
            };

            switch (moduleName)
            {
                case "Textures":
                    var textureInfos = this.analysisService.GetAllTextureInfos(forceRefresh: false);
                    data.TotalTextures        = textureInfos.Count;
                    data.UncompressedTextures = this.analysisService.FindUncompressedTextures(forceRefresh: false).Count;
                    data.TexturesWithMipMap   = this.analysisService.FindTexturesWithMipMap(forceRefresh: false).Count;
                    data.TexturesNotInAtlas   = this.analysisService.FindTexturesNotInAtlas(forceRefresh: false).Count;

                    if (this.reportTemplate == ReportTemplate.Detailed)
                    {
                        data.TextureDetails = textureInfos.Select(t => new TextureDetail
                        {
                            Name = t.Texture.name,
                            Path = t.Path,
                            Size = $"{t.TextureSize.x}x{t.TextureSize.y}",
                            FileSize = t.FileSize,
                            Compression = t.CompressionType.ToString(),
                            MipMap = t.GenerateMipMapEnabled,
                            ReadWrite = t.ReadWriteEnabled
                        }).ToList();
                    }
                    break;

                case "Audio":
                    var audioInfos = this.analysisService.GetAllAudioInfos(forceRefresh: false);
                    data.TotalAudioClips       = audioInfos.Count;
                    data.WrongCompressionAudio = this.analysisService.FindAudioWithWrongCompression(forceRefresh: false).Count;

                    if (this.reportTemplate == ReportTemplate.Detailed)
                    {
                        data.AudioDetails = audioInfos.Values.Select(a => new AudioDetail
                        {
                            Name = a.Audio.name,
                            Length = a.Audio.length,
                            Channels = a.Audio.channels,
                            ForceToMono = a.ForceToMono,
                            CompressionFormat = a.CompressionFormat.ToString(),
                            Quality = a.Quality,
                            LoadType = a.LoadType.ToString()
                        }).ToList();
                    }
                    break;

                case "Meshes":
                    var meshesByCompression = this.analysisService.GetMeshesByCompression(forceRefresh: false);
                    data.TotalMeshes = meshesByCompression.Values.Sum(list => list.Count);
                    data.UncompressedMeshes = meshesByCompression[ModelImporterMeshCompression.Off].Count;

                    if (this.reportTemplate == ReportTemplate.Detailed)
                    {
                        data.MeshDetails = meshesByCompression.Values.SelectMany(list => list).Select(m => new MeshDetail
                        {
                            Name = m.Mesh.name,
                            VertexCount = m.Mesh.vertexCount,
                            TriangleCount = m.Mesh.triangles.Length / 3,
                            Compression = m.ModelImporter.meshCompression.ToString()
                        }).ToList();
                    }
                    break;

                case "Fonts":
                    var (compressedFonts, uncompressedFonts) = this.analysisService.GetFontsByCompression(forceRefresh: false);
                    data.TotalFonts                          = compressedFonts.Count + uncompressedFonts.Count;
                    data.CompressedFonts                     = compressedFonts.Count;
                    data.UncompressedFonts                   = uncompressedFonts.Count;

                    if (this.reportTemplate == ReportTemplate.Detailed)
                    {
                        data.FontDetails = compressedFonts.Concat(uncompressedFonts).Select(f => new FontDetail
                        {
                            Name = f.Font.name,
                            FontCase = f.FontImporter.fontTextureCase.ToString(),
                            IsCompressed = f.FontImporter.fontTextureCase == FontTextureCase.CustomSet
                        }).ToList();
                    }
                    break;

                default:
                    // Fall back to all modules
                    return this.GatherAllModuleData();
            }

            return data;
        }

        #endregion

        #region File Writing

        private string GenerateFilePath(string moduleName)
        {
            if (!Directory.Exists(this.exportPath))
                Directory.CreateDirectory(this.exportPath);

            var fileName = $"OptimizationReport_{moduleName}";
            if (this.includeTimestampInFilename)
                fileName += $"_{DateTime.Now:yyyyMMdd_HHmmss}";

            var extension = this.exportFormat switch
            {
                ExportFormat.CSV => ".csv",
                ExportFormat.JSON => ".json",
                ExportFormat.Markdown => ".md",
                ExportFormat.HTML => ".html",
                _ => ".txt"
            };
            return Path.Combine(this.exportPath, fileName + extension);
        }

        private void WriteCSVReport(string filePath, ReportData data)
        {
            var csv = new StringBuilder();

            // Header
            csv.AppendLine($"# {data.ReportHeader}");
            csv.AppendLine($"# Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"# Template: {data.Template}");
            csv.AppendLine();

            // Summary
            csv.AppendLine("# SUMMARY");
            csv.AppendLine("Category,Total,Issues");
            csv.AppendLine($"Textures,{data.TotalTextures},{data.UncompressedTextures} uncompressed");
            csv.AppendLine($"Audio,{data.TotalAudioClips},{data.WrongCompressionAudio} wrong compression");
            csv.AppendLine($"Meshes,{data.TotalMeshes},{data.UncompressedMeshes} uncompressed");
            csv.AppendLine($"Fonts,{data.TotalFonts},{data.UncompressedFonts} uncompressed");
            csv.AppendLine($"Addressables,{data.AddressableAssets},{data.AddressableGroups} groups");
            csv.AppendLine();

            // Detailed sections
            if (data.Template == ReportTemplate.Detailed)
            {
                // Textures
                if (data.TextureDetails != null && data.TextureDetails.Count > 0)
                {
                    csv.AppendLine("# TEXTURE DETAILS");
                    csv.AppendLine("Name,Path,Size,FileSize,Compression,MipMap,ReadWrite");
                    foreach (var tex in data.TextureDetails)
                    {
                        csv.AppendLine($"\"{tex.Name}\",\"{tex.Path}\",{tex.Size},{tex.FileSize},{tex.Compression},{tex.MipMap},{tex.ReadWrite}");
                    }
                    csv.AppendLine();
                }

                // Audio
                if (data.AudioDetails != null && data.AudioDetails.Count > 0)
                {
                    csv.AppendLine("# AUDIO DETAILS");
                    csv.AppendLine("Name,Length,Channels,ForceToMono,Compression,Quality,LoadType");
                    foreach (var audio in data.AudioDetails)
                    {
                        csv.AppendLine($"\"{audio.Name}\",{audio.Length:F2},{audio.Channels},{audio.ForceToMono},{audio.CompressionFormat},{audio.Quality:F2},{audio.LoadType}");
                    }
                    csv.AppendLine();
                }

                // Meshes
                if (data.MeshDetails != null && data.MeshDetails.Count > 0)
                {
                    csv.AppendLine("# MESH DETAILS");
                    csv.AppendLine("Name,VertexCount,TriangleCount,Compression");
                    foreach (var mesh in data.MeshDetails)
                    {
                        csv.AppendLine($"\"{mesh.Name}\",{mesh.VertexCount},{mesh.TriangleCount},{mesh.Compression}");
                    }
                    csv.AppendLine();
                }

                // Fonts
                if (data.FontDetails != null && data.FontDetails.Count > 0)
                {
                    csv.AppendLine("# FONT DETAILS");
                    csv.AppendLine("Name,FontCase,IsCompressed");
                    foreach (var font in data.FontDetails)
                    {
                        csv.AppendLine($"\"{font.Name}\",{font.FontCase},{font.IsCompressed}");
                    }
                    csv.AppendLine();
                }
            }

            File.WriteAllText(filePath, csv.ToString());
        }

        private void WriteJSONReport(string filePath, ReportData data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private void WriteMarkdownReport(string filePath, ReportData data)
        {
            var md = new StringBuilder();

            // Header
            md.AppendLine($"# {data.ReportHeader}");
            md.AppendLine();
            md.AppendLine($"**Generated:** {data.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            md.AppendLine($"**Template:** {data.Template}");
            md.AppendLine();
            md.AppendLine("---");
            md.AppendLine();

            // Summary
            md.AppendLine("## Summary");
            md.AppendLine();
            md.AppendLine("| Category | Total | Issues |");
            md.AppendLine("|----------|------:|-------:|");
            md.AppendLine($"| Textures | {data.TotalTextures:N0} | {data.UncompressedTextures} uncompressed |");
            md.AppendLine($"| Audio | {data.TotalAudioClips:N0} | {data.WrongCompressionAudio} wrong compression |");
            md.AppendLine($"| Meshes | {data.TotalMeshes:N0} | {data.UncompressedMeshes} uncompressed |");
            md.AppendLine($"| Fonts | {data.TotalFonts:N0} | {data.UncompressedFonts} uncompressed |");
            md.AppendLine($"| Addressables | {data.AddressableAssets:N0} | {data.AddressableGroups} groups |");
            md.AppendLine();

            // Detailed sections (if requested)
            if (data.Template == ReportTemplate.Detailed)
            {
                if (data.TextureDetails != null && data.TextureDetails.Count > 0)
                {
                    md.AppendLine("## Texture Details");
                    md.AppendLine();
                    md.AppendLine("| Name | Size | File Size | Compression | MipMap |");
                    md.AppendLine("|------|------|-----------|-------------|--------|");
                    foreach (var tex in data.TextureDetails.Take(100)) // Limit for readability
                    {
                        md.AppendLine($"| {tex.Name} | {tex.Size} | {tex.FileSize:N0} | {tex.Compression} | {tex.MipMap} |");
                    }
                    md.AppendLine();
                }
            }

            md.AppendLine("---");
            md.AppendLine("*Generated by Unity Optimization Hub*");

            File.WriteAllText(filePath, md.ToString());
        }

        private void WriteHTMLReport(string filePath, ReportData data)
        {
            var html = new StringBuilder();

            // HTML Header
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='UTF-8'>");
            html.AppendLine($"    <title>{data.ReportHeader}</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }");
            html.AppendLine("        h1 { color: #333; border-bottom: 3px solid #4CAF50; padding-bottom: 10px; }");
            html.AppendLine("        h2 { color: #555; margin-top: 30px; }");
            html.AppendLine("        table { border-collapse: collapse; width: 100%; margin: 20px 0; background-color: white; }");
            html.AppendLine("        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            html.AppendLine("        th { background-color: #4CAF50; color: white; }");
            html.AppendLine("        tr:nth-child(even) { background-color: #f2f2f2; }");
            html.AppendLine("        .meta { color: #666; font-size: 0.9em; }");
            html.AppendLine("        .issue { color: #d32f2f; font-weight: bold; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // Content
            html.AppendLine($"    <h1>{data.ReportHeader}</h1>");
            html.AppendLine($"    <p class='meta'>Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss} | Template: {data.Template}</p>");

            html.AppendLine("    <h2>Summary</h2>");
            html.AppendLine("    <table>");
            html.AppendLine("        <tr><th>Category</th><th>Total</th><th>Issues</th></tr>");
            html.AppendLine($"        <tr><td>Textures</td><td>{data.TotalTextures:N0}</td><td class='issue'>{data.UncompressedTextures} uncompressed</td></tr>");
            html.AppendLine($"        <tr><td>Audio</td><td>{data.TotalAudioClips:N0}</td><td class='issue'>{data.WrongCompressionAudio} wrong compression</td></tr>");
            html.AppendLine($"        <tr><td>Meshes</td><td>{data.TotalMeshes:N0}</td><td class='issue'>{data.UncompressedMeshes} uncompressed</td></tr>");
            html.AppendLine($"        <tr><td>Fonts</td><td>{data.TotalFonts:N0}</td><td class='issue'>{data.UncompressedFonts} uncompressed</td></tr>");
            html.AppendLine($"        <tr><td>Addressables</td><td>{data.AddressableAssets:N0}</td><td>{data.AddressableGroups} groups</td></tr>");
            html.AppendLine("    </table>");

            if (data.Template == ReportTemplate.Detailed && data.TextureDetails != null && data.TextureDetails.Count > 0)
            {
                html.AppendLine("    <h2>Texture Details</h2>");
                html.AppendLine("    <table>");
                html.AppendLine("        <tr><th>Name</th><th>Size</th><th>File Size</th><th>Compression</th><th>MipMap</th></tr>");
                foreach (var tex in data.TextureDetails.Take(100))
                {
                    html.AppendLine($"        <tr><td>{tex.Name}</td><td>{tex.Size}</td><td>{tex.FileSize:N0}</td><td>{tex.Compression}</td><td>{tex.MipMap}</td></tr>");
                }
                html.AppendLine("    </table>");
            }

            html.AppendLine("    <hr>");
            html.AppendLine("    <p style='text-align:center; color:#888;'><em>Generated by Unity Optimization Hub</em></p>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText(filePath, html.ToString());
        }

        #endregion

        #region History Management

        private void AddToHistory(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var entry = new ExportHistoryEntry
            {
                Timestamp = DateTime.Now,
                FilePath = filePath,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                Format = this.exportFormat.ToString()
            };

            this.exportHistory.Insert(0, entry);

            // Keep only last N entries
            if (this.exportHistory.Count > MaxHistoryEntries) this.exportHistory = this.exportHistory.Take(MaxHistoryEntries).ToList();
        }

        #endregion

        #region Data Models

        [Serializable]
        private enum ExportFormat
        {
            CSV,
            JSON,
            Markdown,
            HTML
        }

        [Serializable]
        private enum ReportTemplate
        {
            Summary,
            Detailed,
            Custom
        }

        [Serializable]
        private class ReportData
        {
            public DateTime GeneratedAt { get; set; }
            public string ReportHeader { get; set; }
            public ReportTemplate Template { get; set; }

            // Summary stats
            public int TotalTextures { get; set; }
            public int UncompressedTextures { get; set; }
            public int TexturesWithMipMap { get; set; }
            public int TexturesNotInAtlas { get; set; }

            public int TotalAudioClips { get; set; }
            public int WrongCompressionAudio { get; set; }

            public int TotalMeshes { get; set; }
            public int UncompressedMeshes { get; set; }

            public int TotalFonts { get; set; }
            public int CompressedFonts { get; set; }
            public int UncompressedFonts { get; set; }

            public int AddressableGroups { get; set; }
            public int AddressableAssets { get; set; }

            // Detailed lists (only populated if template is Detailed)
            public List<TextureDetail> TextureDetails { get; set; }
            public List<AudioDetail> AudioDetails { get; set; }
            public List<MeshDetail> MeshDetails { get; set; }
            public List<FontDetail> FontDetails { get; set; }
        }

        [Serializable]
        private class TextureDetail
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Size { get; set; }
            public long FileSize { get; set; }
            public string Compression { get; set; }
            public bool MipMap { get; set; }
            public bool ReadWrite { get; set; }
        }

        [Serializable]
        private class AudioDetail
        {
            public string Name { get; set; }
            public float Length { get; set; }
            public int Channels { get; set; }
            public bool ForceToMono { get; set; }
            public string CompressionFormat { get; set; }
            public float Quality { get; set; }
            public string LoadType { get; set; }
        }

        [Serializable]
        private class MeshDetail
        {
            public string Name { get; set; }
            public int VertexCount { get; set; }
            public int TriangleCount { get; set; }
            public string Compression { get; set; }
        }

        [Serializable]
        private class FontDetail
        {
            public string Name { get; set; }
            public string FontCase { get; set; }
            public bool IsCompressed { get; set; }
        }

        [Serializable]
        private class ExportHistoryEntry
        {
            [TableColumnWidth(150)]
            [ShowInInspector, ReadOnly]
            public DateTime Timestamp { get; set; }

            [TableColumnWidth(300)]
            [ShowInInspector, ReadOnly]
            public string FilePath { get; set; }

            [TableColumnWidth(100)]
            [ShowInInspector, ReadOnly]
            [LabelText("Size (KB)")]
            public string FileSizeKB => (this.FileSize / 1024f).ToString("F2");

            [HideInInspector]
            public long FileSize { get; set; }

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public string Format { get; set; }
        }

        #endregion
    }
}
