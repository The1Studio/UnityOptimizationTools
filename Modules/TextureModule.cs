using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using TheOne.Extensions;
using TheOne.Tool.Core;
using TheOne.Tool.Optimization.Texture;
using TheOne.Tool.Optimization.Models;
using TheOne.UITemplate.Editor.Optimization.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace TheOne.UITemplate.Editor.Optimization.Modules
{
    /// <summary>
    /// Texture optimization module.
    /// Analyzes texture usage, compression, mipmaps, atlas integration, and provides bulk operations.
    /// </summary>
    public class TextureModule : OptimizationModuleBase
    {
        public override string ModuleName => "Textures";
        public override string ModuleIcon => "🖼";

        #region Summary Dashboard

        private bool HasCriticalIssues => this.isAnalyzed && (this.notCompressedTexture.Count + this.compressedAndNotCrunchTexture.Count) > 50;
        private bool HasWarningIssues  => this.isAnalyzed && (this.notCompressedTexture.Count + this.compressedAndNotCrunchTexture.Count) > 20 && !this.HasCriticalIssues;
        private bool HasNoIssues       => this.isAnalyzed && !this.HasCriticalIssues && !this.HasWarningIssues;

        [TabGroup("Tabs", "Overview")]
        [PropertyOrder(-100)]
        [ShowInInspector]
        [HideLabel]
        [InfoBox("$SummaryText", InfoMessageType.Error, VisibleIf = "HasCriticalIssues")]
        [InfoBox("$SummaryText", InfoMessageType.Warning, VisibleIf = "HasWarningIssues")]
        [InfoBox("$SummaryText", InfoMessageType.Info, VisibleIf = "HasNoIssues")]
        [InfoBox("Click 'Analyze' to scan all textures in the project.", InfoMessageType.Info, VisibleIf = "@!isAnalyzed")]
        private string summaryDashboard => "";

        private string SummaryText
        {
            get
            {
                if (!this.isAnalyzed)
                    return "";

                var totalTextures       = this.allTextures.Count;
                var totalMemory         = this.CalculateTotalMemory();
                var compressedCount     = this.allTextures.Count(t => t.CompressionType != TextureImporterCompression.Uncompressed);
                var mipmapCount         = this.generatedMipMap.Count;
                var atlasCount          = this.allTextures.Count - this.notInAtlasTexture.Count;
                var optimizedPercentage = totalTextures > 0 ? (compressedCount * 100f / totalTextures) : 0;

                var issuesCount = this.notCompressedTexture.Count + this.compressedAndNotCrunchTexture.Count + this.generatedMipMap.Count;

                return $"TEXTURE ANALYSIS SUMMARY\n\n" +
                       $"Total Textures: {totalTextures}\n" +
                       $"Estimated Memory: {this.FormatBytes(totalMemory)}\n" +
                       $"Optimization Rate: {optimizedPercentage:F1}% ({compressedCount}/{totalTextures})\n\n" +
                       $"STATISTICS\n" +
                       $"- Compressed: {compressedCount}\n" +
                       $"- With Mipmaps: {mipmapCount}\n" +
                       $"- In Atlas: {atlasCount}\n\n" +
                       $"ISSUES DETECTED: {issuesCount}\n" +
                       $"- Not Compressed: {this.notCompressedTexture.Count}\n" +
                       $"- Compressed but Not Crunched: {this.compressedAndNotCrunchTexture.Count}\n" +
                       $"- Unnecessary Mipmaps: {this.generatedMipMap.Count}";
            }
        }

        private long CalculateTotalMemory()
        {
            return this.allTextures.Sum(t =>
            {
                var width = (int)t.TextureSize.x;
                var height = (int)t.TextureSize.y;
                var bytesPerPixel = t.CompressionType == TextureImporterCompression.Uncompressed ? 4 : 1;
                return (long)(width * height * bytesPerPixel);
            });
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region Sorting Settings

        private enum SortingBy
        {
            FileSize,
            TotalPixel,
        }

        private enum SortingOrder
        {
            Ascending,
            Descending,
        }

        [TabGroup("Tabs", "Settings")]
        [EnumToggleButtons]
        [OnValueChanged(nameof(OnSortingChanged))]
        [LabelText("Sort By")]
        private SortingBy sortingBy = SortingBy.TotalPixel;

        [TabGroup("Tabs", "Settings")]
        [EnumToggleButtons]
        [OnValueChanged(nameof(OnSortingChanged))]
        [LabelText("Order")]
        private SortingOrder sortingOrder = SortingOrder.Descending;

        private void OnSortingChanged()
        {
            if (this.isAnalyzed)
            {
                this.SortAllLists();
            }
        }

        #endregion

        #region Data Lists - Overview Tab

        [TabGroup("Tabs", "Overview")]
        [FoldoutGroup("Tabs/Overview/All Textures", expanded: true)]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(1)]
        [ShowInInspector]
        private List<TextureInfo> allTextures = new();

        #endregion

        #region Data Lists - Issues Tab

        [TabGroup("Tabs", "Issues")]
        [FoldoutGroup("Tabs/Issues/Compression Issues", expanded: true)]
        [Title("Not Compressed Textures", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("These textures are uncompressed and should be compressed to save memory.", InfoMessageType.Warning, VisibleIf = "@notCompressedTexture.Count > 0")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(1)]
        [ShowInInspector]
        private List<TextureInfo> notCompressedTexture = new();

        [TabGroup("Tabs", "Issues")]
        [FoldoutGroup("Tabs/Issues/Compression Issues", expanded: true)]
        [Title("Compressed but Not Crunched", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Enable crunch compression on these textures to reduce build size.", InfoMessageType.Info, VisibleIf = "@compressedAndNotCrunchTexture.Count > 0")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(2)]
        [ShowInInspector]
        private List<TextureInfo> compressedAndNotCrunchTexture = new();

        [TabGroup("Tabs", "Issues")]
        [FoldoutGroup("Tabs/Issues/Mipmap Issues", expanded: true)]
        [Title("Textures with Mipmaps Generated", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("UI textures typically don't need mipmaps. Disable to save memory.", InfoMessageType.Warning, VisibleIf = "@generatedMipMap.Count > 0")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(3)]
        [ShowInInspector]
        private List<TextureInfo> generatedMipMap = new();

        [TabGroup("Tabs", "Issues")]
        [FoldoutGroup("Tabs/Issues/Atlas Issues", expanded: true)]
        [Title("Not In Atlas & Not POT Size", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Non-power-of-two sprites should be in sprite atlases for better performance.", InfoMessageType.Info, VisibleIf = "@notInAtlasTexture.Count > 0")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(4)]
        [ShowInInspector]
        private List<TextureInfo> notInAtlasTexture = new();

        [TabGroup("Tabs", "Issues")]
        [FoldoutGroup("Tabs/Issues/Atlas Issues", expanded: true)]
        [Title("Not Compressed and Not In Atlas", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("These textures should either be compressed or added to sprite atlas.", InfoMessageType.Warning, VisibleIf = "@notCompressedAndNotInAtlasTexture.Count > 0")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(5)]
        [ShowInInspector]
        private List<TextureInfo> notCompressedAndNotInAtlasTexture = new();

        #endregion

        #region Data Lists - Analysis Tab

        [TabGroup("Tabs", "Analysis")]
        [FoldoutGroup("Tabs/Analysis/3D Models", expanded: true)]
        [Title("3D Model Textures", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Textures used by 3D models (MeshRenderer/SkinnedMeshRenderer).", InfoMessageType.Info)]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(1)]
        [ShowInInspector]
        private List<TextureInfo> modelTextures = new();

        [TabGroup("Tabs", "Analysis")]
        [FoldoutGroup("Tabs/Analysis/3D Models", expanded: true)]
        [Title("All Textures (Not In Models)", TitleAlignment = TitleAlignments.Centered)]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(2)]
        [ShowInInspector]
        private List<TextureInfo> allTexturesNotInModels = new();

        [TabGroup("Tabs", "Analysis")]
        [FoldoutGroup("Tabs/Analysis/Sprite Atlas", expanded: true)]
        [Title("Duplicated Atlas Textures", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("These textures appear in multiple sprite atlases - consider consolidating.", InfoMessageType.Warning, VisibleIf = "@duplicatedAtlasTexture.Count > 0")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(3)]
        [ShowInInspector]
        private List<TextureInfo> duplicatedAtlasTexture = new();

        [TabGroup("Tabs", "Analysis")]
        [FoldoutGroup("Tabs/Analysis/Sprite Atlas", expanded: true)]
        [Title("Don't Use Atlas Textures", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Textures in sprite atlas but not referenced in addressables.", InfoMessageType.Info, VisibleIf = "@dontUseAtlasTexture.Count > 0")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 20)]
        [PropertyOrder(4)]
        [ShowInInspector]
        private List<TextureInfo> dontUseAtlasTexture = new();

        #endregion

        #region Bulk Actions

        [TabGroup("Tabs", "Quick Actions")]
        [PropertySpace(10)]
        [TitleGroup("Tabs/Quick Actions/Quick Fixes")]
        [InfoBox("Bulk optimization operations to fix common issues. Always backup before applying.", InfoMessageType.Warning)]
        [ButtonGroup("Tabs/Quick Actions/Quick Fixes/Buttons")]
        [Button(ButtonSizes.Large, Name = "Fix All Compression Issues")]
        [GUIColor(0.3f, 0.9f, 0.3f)]
        private void FixAllCompressionIssues()
        {
            if (!EditorUtility.DisplayDialog(
                "Fix All Compression Issues",
                $"This will compress {this.notCompressedTexture.Count} uncompressed textures and enable crunch compression on {this.compressedAndNotCrunchTexture.Count} textures.\n\nThis operation cannot be undone. Continue?",
                "Yes, Fix Issues",
                "Cancel"))
            {
                return;
            }

            int fixedCount = 0;
            var texturesToFix = this.notCompressedTexture.Concat(this.compressedAndNotCrunchTexture).ToList();

            try
            {
                for (int i = 0; i < texturesToFix.Count; i++)
                {
                    var textureInfo = texturesToFix[i];

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Fixing Compression Issues",
                        $"Processing {textureInfo.Name}... ({i + 1}/{texturesToFix.Count})",
                        (float)i / texturesToFix.Count))
                    {
                        break;
                    }

                    var importer = textureInfo.TextureImporter;
                    if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Compressed;
                    }
                    importer.crunchedCompression = true;
                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"Fixed compression on {fixedCount} textures");
            AssetDatabase.Refresh();
            this.Refresh();
        }

        [TabGroup("Tabs", "Quick Actions")]
        [ButtonGroup("Tabs/Quick Actions/Quick Fixes/Buttons")]
        [Button(ButtonSizes.Large, Name = "Disable All Mipmaps")]
        [GUIColor(0.9f, 0.9f, 0.3f)]
        private void DisableAllMipmaps()
        {
            if (!EditorUtility.DisplayDialog(
                "Disable All Mipmaps",
                $"This will disable mipmaps on {this.generatedMipMap.Count} textures.\n\nThis is recommended for UI sprites but not for 3D textures. Continue?",
                "Yes, Disable Mipmaps",
                "Cancel"))
            {
                return;
            }

            int fixedCount = 0;

            try
            {
                for (int i = 0; i < this.generatedMipMap.Count; i++)
                {
                    var textureInfo = this.generatedMipMap[i];

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Disabling Mipmaps",
                        $"Processing {textureInfo.Name}... ({i + 1}/{this.generatedMipMap.Count})",
                        (float)i / this.generatedMipMap.Count))
                    {
                        break;
                    }

                    var importer = textureInfo.TextureImporter;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"Disabled mipmaps on {fixedCount} textures");
            AssetDatabase.Refresh();
            this.Refresh();
        }

        [TabGroup("Tabs", "Quick Actions")]
        [PropertySpace(20)]
        [TitleGroup("Tabs/Quick Actions/Selection Actions")]
        [InfoBox("Select textures in the project window for manual inspection.")]
        [ButtonGroup("Tabs/Quick Actions/Selection Actions/Issues")]
        [Button(ButtonSizes.Medium, Name = "Select Not Compressed")]
        [GUIColor(1f, 0.8f, 0.5f)]
        private void SelectAllNotCompressedTextures()
        {
            Selection.objects = this.notCompressedTexture.Select(info => info.Texture).ToArray();
            Debug.Log($"Selected {this.notCompressedTexture.Count} uncompressed textures");
        }

        [TabGroup("Tabs", "Quick Actions")]
        [ButtonGroup("Tabs/Quick Actions/Selection Actions/Issues")]
        [Button(ButtonSizes.Medium, Name = "Select Not Crunched")]
        [GUIColor(1f, 0.8f, 0.5f)]
        private void SelectAllCompressedAndNotCrunchedTextures()
        {
            Selection.objects = this.compressedAndNotCrunchTexture.Select(info => info.Texture).ToArray();
            Debug.Log($"Selected {this.compressedAndNotCrunchTexture.Count} compressed but not crunched textures");
        }

        [TabGroup("Tabs", "Quick Actions")]
        [ButtonGroup("Tabs/Quick Actions/Selection Actions/Issues")]
        [Button(ButtonSizes.Medium, Name = "Select With Mipmaps")]
        [GUIColor(1f, 0.8f, 0.5f)]
        private void SelectAllGeneratedMipMapTextures()
        {
            Selection.objects = this.generatedMipMap.Select(info => info.Texture).ToArray();
            Debug.Log($"Selected {this.generatedMipMap.Count} textures with mipmaps");
        }

        [TabGroup("Tabs", "Quick Actions")]
        [PropertySpace(10)]
        [ButtonGroup("Tabs/Quick Actions/Selection Actions/Categories")]
        [Button(ButtonSizes.Medium, Name = "Select All Textures")]
        [GUIColor(0.5f, 0.8f, 0.9f)]
        private void SelectAllTextures()
        {
            Selection.objects = this.allTextures.Select(info => info.Texture).ToArray();
            Debug.Log($"Selected {this.allTextures.Count} textures");
        }

        [TabGroup("Tabs", "Quick Actions")]
        [ButtonGroup("Tabs/Quick Actions/Selection Actions/Categories")]
        [Button(ButtonSizes.Medium, Name = "Select Model Textures")]
        [GUIColor(0.5f, 0.8f, 0.9f)]
        private void SelectAllModelTextures()
        {
            Selection.objects = this.modelTextures.Select(info => info.Texture).ToArray();
            Debug.Log($"Selected {this.modelTextures.Count} model textures");
        }

        [TabGroup("Tabs", "Quick Actions")]
        [ButtonGroup("Tabs/Quick Actions/Selection Actions/Categories")]
        [Button(ButtonSizes.Medium, Name = "Select Unused Atlas")]
        [GUIColor(0.5f, 0.8f, 0.9f)]
        private void SelectAllDontUseAtlasTextures()
        {
            Selection.objects = this.dontUseAtlasTexture.Select(info => info.Texture).ToArray();
            Debug.Log($"Selected {this.dontUseAtlasTexture.Count} unused atlas textures");
        }

        [TabGroup("Tabs", "Quick Actions")]
        [PropertySpace(20)]
        [TitleGroup("Tabs/Quick Actions/Export")]
        [InfoBox("Export texture analysis data to ScriptableObject for runtime access or external tools.")]
        [Button(ButtonSizes.Large, Name = "Create TextureInfo Data Asset")]
        [GUIColor(0.7f, 0.7f, 1)]
        private void CreateTextureInfoDataAsset()
        {
            this.GenerateTextureInfoDataAsset();
        }

        #endregion

        #region Module Interface Implementation

        private bool isAnalyzed = false;

        public override void OnActivated()
        {
            // Module activated - data loaded lazily
        }

        public override void OnDeactivated()
        {
            // Module deactivated - keep data cached
        }

        protected override void OnAnalyze()
        {
            this.AnalyzeTextures();
        }

        protected override void OnRefresh()
        {
            this.AnalyzeTextures();
        }

        protected override void OnClear()
        {
            this.allTextures.Clear();
            this.generatedMipMap.Clear();
            this.compressedAndNotCrunchTexture.Clear();
            this.notCompressedTexture.Clear();
            this.modelTextures.Clear();
            this.allTexturesNotInModels.Clear();
            this.notInAtlasTexture.Clear();
            this.duplicatedAtlasTexture.Clear();
            this.dontUseAtlasTexture.Clear();
            this.notCompressedAndNotInAtlasTexture.Clear();
            this.isAnalyzed = false;
        }

        #endregion

        #region Analysis Logic

        private void AnalyzeTextures()
        {
            this.Clear();

            var allAddressableTextures   = AssetSearcher.GetAllAssetInAddressable<Texture>().Keys.ToList();
            var textureInfos             = this.GetTextureInfos(allAddressableTextures);
            var allAddressableTextureSet = allAddressableTextures.ToHashSet();

            // Analyze 3D Model textures
            var meshRenderers = AssetSearcher.GetAllAssetInAddressable<MeshRenderer>().Keys.ToList();
            var skinnedMeshRenderers = AssetSearcher.GetAllAssetInAddressable<SkinnedMeshRenderer>().Keys.ToList();
            var modelTextureSet = new HashSet<Texture>();

            foreach (var meshRenderer in meshRenderers)
            {
                modelTextureSet.AddRange(AssetSearcher.GetAllDependencies<Texture>(meshRenderer));
            }

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                modelTextureSet.AddRange(AssetSearcher.GetAllDependencies<Texture>(skinnedMeshRenderer));
            }

            // Analyze Atlas textures
            var atlases = AssetSearcher.GetAllAssetsOfType<SpriteAtlas>();
            var atlasTextureSet = new HashSet<Texture>();
            var duplicatedAtlasTextureSet = new HashSet<Texture>();

            foreach (var spriteAtlas in atlases)
            {
                var textureInAtlases = AssetSearcher.GetAllDependencies<Texture>(spriteAtlas);
                foreach (var textureInAtlas in textureInAtlases)
                {
                    if (!atlasTextureSet.Add(textureInAtlas))
                    {
                        duplicatedAtlasTextureSet.Add(textureInAtlas);
                    }
                }
            }

            // Find unused atlas textures
            var dontUseAtlasTextureSet = new HashSet<Texture>(atlasTextureSet);
            dontUseAtlasTextureSet.RemoveRange(allAddressableTextureSet);
            this.dontUseAtlasTexture = this.GetTextureInfos(dontUseAtlasTextureSet.ToList());

            // TODO: Remove UITemplate filtering after migrating features to separate packages
            // This temporary workaround filters UITemplate assets to avoid false positives during the migration phase.
            // Once all features are properly separated into com.theone.tool.* packages, this filter should be removed.
            this.dontUseAtlasTexture = this.dontUseAtlasTexture.Where(info => !info.Path.Contains("UITemplate")).ToList();

            // Categorize textures
            foreach (var textureInfo in textureInfos)
            {
                this.allTextures.Add(textureInfo);

                if (textureInfo.GenerateMipMapEnabled)
                {
                    this.generatedMipMap.Add(textureInfo);
                }

                if (textureInfo.TextureImporter.textureCompression != TextureImporterCompression.Uncompressed &&
                    !textureInfo.TextureImporter.crunchedCompression)
                {
                    this.compressedAndNotCrunchTexture.Add(textureInfo);
                }

                if (textureInfo.CompressionType == TextureImporterCompression.Uncompressed)
                {
                    this.notCompressedTexture.Add(textureInfo);
                }

                if (modelTextureSet.Contains(textureInfo.Texture))
                {
                    this.modelTextures.Add(textureInfo);
                    continue;
                }

                this.allTexturesNotInModels.Add(textureInfo);

                if (!atlasTextureSet.Contains(textureInfo.Texture) &&
                    textureInfo.TextureImporter.textureType == TextureImporterType.Sprite &&
                    !this.IsSquarePowerOfTwo(textureInfo.TextureSize))
                {
                    this.notInAtlasTexture.Add(textureInfo);
                }

                if (duplicatedAtlasTextureSet.Contains(textureInfo.Texture))
                {
                    this.duplicatedAtlasTexture.Add(textureInfo);
                }

                if (textureInfo.CompressionType == TextureImporterCompression.Uncompressed &&
                    !atlasTextureSet.Contains(textureInfo.Texture))
                {
                    this.notCompressedAndNotInAtlasTexture.Add(textureInfo);
                }
            }

            this.isAnalyzed = true;
            Debug.Log($"Texture analysis complete: {this.allTextures.Count} total textures analyzed");
        }

        private List<TextureInfo> GetTextureInfos(List<Texture> textures)
        {
            var textureInfos = textures.Select(texture =>
            {
                var path = AssetDatabase.GetAssetPath(texture);
                var fileInfo = new FileInfo(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null) return null;

                return new TextureInfo
                {
                    Texture               = texture,
                    TextureImporter       = importer,
                    FileSize              = fileInfo.Exists ? fileInfo.Length : 0,
                    TextureSize           = this.GetTextureSizeAccordingToMaxSize(texture, importer),
                    ReadWriteEnabled      = importer.isReadable,
                    GenerateMipMapEnabled = importer.mipmapEnabled,
                    Path                  = path,
                };
            }).Where(textureInfo => textureInfo != null).ToList();

            this.Sort(textureInfos);
            return textureInfos;
        }

        private void SortAllLists()
        {
            this.Sort(this.allTextures);
            this.Sort(this.generatedMipMap);
            this.Sort(this.compressedAndNotCrunchTexture);
            this.Sort(this.notCompressedTexture);
            this.Sort(this.modelTextures);
            this.Sort(this.allTexturesNotInModels);
            this.Sort(this.notInAtlasTexture);
            this.Sort(this.duplicatedAtlasTexture);
            this.Sort(this.dontUseAtlasTexture);
            this.Sort(this.notCompressedAndNotInAtlasTexture);
        }

        private void Sort(List<TextureInfo> textureInfos)
        {
            textureInfos.Sort((a, b) =>
            {
                return this.sortingBy switch
                {
                    SortingBy.FileSize => this.sortingOrder == SortingOrder.Ascending
                        ? a.FileSize.CompareTo(b.FileSize)
                        : b.FileSize.CompareTo(a.FileSize),
                    SortingBy.TotalPixel => (int)(this.sortingOrder == SortingOrder.Ascending
                        ? a.TextureSize.x * a.TextureSize.y - b.TextureSize.x * b.TextureSize.y
                        : b.TextureSize.x * b.TextureSize.y - a.TextureSize.x * a.TextureSize.y),
                    _ => throw new ArgumentOutOfRangeException(),
                };
            });
        }

        private Vector2 GetTextureSizeAccordingToMaxSize(Texture texture, TextureImporter importer)
        {
            if (importer != null)
            {
                var maxSize = importer.maxTextureSize;
                var aspectRatio = (float)texture.width / texture.height;

                if (texture.width > maxSize || texture.height > maxSize)
                {
                    if (texture.width > texture.height)
                        return new Vector2(maxSize, maxSize / aspectRatio);
                    else
                        return new Vector2(maxSize * aspectRatio, maxSize);
                }
            }

            return new Vector2(texture.width, texture.height);
        }

        private bool IsSquarePowerOfTwo(Vector2 textureSize)
        {
            var width = (int)textureSize.x;
            var height = (int)textureSize.y;

            // Check if both width and height are power of two
            var widthIsPOT = (width & (width - 1)) == 0 && width > 0;
            var heightIsPOT = (height & (height - 1)) == 0 && height > 0;

            return widthIsPOT && heightIsPOT;
        }

        #endregion

        #region ScriptableObject Export

        private void GenerateTextureInfoDataAsset()
        {
            var path = "Assets/OptimizationData/TextureInfoData.asset";
            var textureInfoData = AssetDatabase.LoadAssetAtPath<TextureInfoData>(path);

            if (textureInfoData == null)
            {
                textureInfoData = ScriptableObject.CreateInstance<TextureInfoData>();
                var directory = Path.GetDirectoryName(path);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                AssetDatabase.CreateAsset(textureInfoData, path);
            }

            textureInfoData.textureInfos = this.GetTextureInfos(
                AssetSearcher.GetAllAssetInAddressable<Texture>().Keys.ToList()
            );

            EditorUtility.SetDirty(textureInfoData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = textureInfoData;

            Debug.Log($"TextureInfoData ScriptableObject {(textureInfoData ? "updated" : "created")} at {path}");
        }

        #endregion
    }
}