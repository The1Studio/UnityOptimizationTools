namespace TheOne.UITemplate.Editor.Optimization.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using TheOne.Extensions;
    using TheOne.Tool.Core;
    using TheOne.Tool.Optimization.Models;
    using TheOne.Tool.Optimization.Models;
    using TheOne.Tool.Optimization.Models;
    using TheOne.Tool.Optimization.Texture;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.U2D;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Service that consolidates common asset analysis logic from all optimization tools.
    /// This service EXTRACTS and REUSES existing logic - it does NOT duplicate code.
    /// </summary>
    public class AssetAnalysisService
    {
        private readonly AssetCache cache = new AssetCache();
        private readonly ProgressTracker progressTracker = new ProgressTracker();

        #region Texture Analysis (from TextureFinderOdin)

        /// <summary>
        /// Get all textures with their metadata. Uses caching to avoid redundant analysis.
        /// EXTRACTS logic from TextureFinderOdin.GetTextureInfos()
        /// </summary>
        public List<TextureInfo> GetAllTextureInfos(bool forceRefresh = false)
        {
            const string cacheKey = "AllTextureInfos";

            if (!forceRefresh && this.cache.IsValid(cacheKey))
                return this.cache.Get<List<TextureInfo>>(cacheKey);

            var textures = AssetSearcher.GetAllAssetInAddressable<Texture>().Keys.ToList();
            var result = this.CreateTextureInfoList(textures);

            this.cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        /// <summary>
        /// Find textures not in atlas and not POT (Power of Two) size.
        /// REUSES logic from TextureFinderOdin.FindTexturesInAssets()
        /// </summary>
        public List<TextureInfo> FindTexturesNotInAtlas(bool forceRefresh = false)
        {
            const string cacheKey = "TexturesNotInAtlas";

            if (!forceRefresh && this.cache.IsValid(cacheKey))
                return this.cache.Get<List<TextureInfo>>(cacheKey);

            var allTextures = this.GetAllTextureInfos(forceRefresh);
            var atlasTextureSet = this.GetAtlasTextureSet();

            var result = allTextures
                .Where(info => !atlasTextureSet.Contains(info.Texture)
                    && info.TextureImporter.textureType == TextureImporterType.Sprite
                    && !this.IsSquarePowerOfTwo(info.TextureSize))
                .ToList();

            this.cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        /// <summary>
        /// Find uncompressed textures.
        /// EXTRACTS logic from TextureFinderOdin.FindTexturesInAssets()
        /// </summary>
        public List<TextureInfo> FindUncompressedTextures(bool forceRefresh = false)
        {
            const string cacheKey = "UncompressedTextures";

            if (!forceRefresh && this.cache.IsValid(cacheKey))
                return this.cache.Get<List<TextureInfo>>(cacheKey);

            var allTextures = this.GetAllTextureInfos(forceRefresh);
            var result = allTextures
                .Where(info => info.CompressionType == TextureImporterCompression.Uncompressed)
                .ToList();

            this.cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        /// <summary>
        /// Find textures with mipmap enabled.
        /// EXTRACTS logic from TextureFinderOdin.FindTexturesInAssets()
        /// </summary>
        public List<TextureInfo> FindTexturesWithMipMap(bool forceRefresh = false)
        {
            const string cacheKey = "TexturesWithMipMap";

            if (!forceRefresh && this.cache.IsValid(cacheKey))
                return this.cache.Get<List<TextureInfo>>(cacheKey);

            var allTextures = this.GetAllTextureInfos(forceRefresh);
            var result = allTextures
                .Where(info => info.GenerateMipMapEnabled)
                .ToList();

            this.cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        /// <summary>
        /// Helper: Create TextureInfo list from Texture list.
        /// CALLS existing logic from TextureFinderOdin (no duplication)
        /// </summary>
        private List<TextureInfo> CreateTextureInfoList(List<Texture> textures)
        {
            return textures.Select(texture =>
            {
                var path = AssetDatabase.GetAssetPath(texture);
                var fileInfo = new FileInfo(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) return null;

                return new TextureInfo
                {
                    Texture = texture,
                    TextureImporter = importer,
                    FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                    TextureSize = this.GetTextureSizeAccordingToMaxSize(texture, importer),
                    ReadWriteEnabled = importer.isReadable,
                    GenerateMipMapEnabled = importer.mipmapEnabled,
                    Path = path,
                };
            }).Where(info => info != null).ToList();
        }

        /// <summary>
        /// Helper: Get texture size according to max size setting.
        /// REUSES logic from TextureFinderOdin.GetTextureSizeAccordingToMaxSize()
        /// </summary>
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

        /// <summary>
        /// Helper: Get all textures in atlases.
        /// REUSES logic from TextureFinderOdin.FindTexturesInAssets()
        /// </summary>
        private HashSet<Texture> GetAtlasTextureSet()
        {
            var atlases = AssetSearcher.GetAllAssetsOfType<SpriteAtlas>();
            var atlasTextureSet = new HashSet<Texture>();

            foreach (var spriteAtlas in atlases)
            {
                var texturesInAtlas = AssetSearcher.GetAllDependencies<Texture>(spriteAtlas);
                atlasTextureSet.AddRange(texturesInAtlas);
            }

            return atlasTextureSet;
        }

        /// <summary>
        /// Helper: Check if texture size is Power of Two.
        /// REUSES logic from TextureFinderOdin.IsSquarePowerOfTwo()
        /// </summary>
        private bool IsSquarePowerOfTwo(Vector2 textureSize)
        {
            var width = (int)textureSize.x;
            var height = (int)textureSize.y;

            var widthIsPOT = (width & (width - 1)) == 0 && width > 0;
            var heightIsPOT = (height & (height - 1)) == 0 && height > 0;

            return widthIsPOT && heightIsPOT;
        }

        #endregion

        #region Audio Analysis (from AddressableAudioFinderOdin)

        /// <summary>
        /// Get all audio clips with their metadata.
        /// EXTRACTS logic from AddressableAudioFinderOdin.FindAudiosInAddressables()
        /// </summary>
        public Dictionary<AudioClip, AudioInfo> GetAllAudioInfos(bool forceRefresh = false)
        {
            const string cacheKey = "AllAudioInfos";

            if (!forceRefresh && this.cache.IsValid(cacheKey))
                return this.cache.Get<Dictionary<AudioClip, AudioInfo>>(cacheKey);

            var result = new Dictionary<AudioClip, AudioInfo>();
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null) return result;

            var totalSteps = settings.groups.Sum(group => group.entries.Count);
            this.progressTracker.Start("Analyzing Audio", totalSteps);

            foreach (var group in settings.groups)
            foreach (var entry in group.entries)
            {
                this.progressTracker.Increment();

                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                var dependencies = AssetDatabase.GetDependencies(path, true);

                foreach (var depPath in dependencies)
                {
                    var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(depPath);
                    if (audioClip == null || result.ContainsKey(audioClip)) continue;

                    var importer = AssetImporter.GetAtPath(depPath) as AudioImporter;
                    if (importer == null) continue;

                    var serializedObject = new SerializedObject(importer);
                    var normalize = serializedObject.FindProperty("m_Normalize").boolValue;
                    var audioSetting = importer.defaultSampleSettings;

                    result[audioClip] = new AudioInfo
                    {
                        AudioPreview = audioClip,
                        Audio = audioClip,
                        Importer = importer,
                        ForceToMono = importer.forceToMono,
                        Normalize = normalize,
                        LoadInBackground = importer.loadInBackground,
                        LoadType = audioSetting.loadType,
                        PreloadAudioData = audioSetting.preloadAudioData,
                        CompressionFormat = audioSetting.compressionFormat,
                        Quality = audioSetting.quality,
                    };
                }
            }

            this.progressTracker.Complete();
            this.cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        /// <summary>
        /// Find audio clips with wrong compression settings.
        /// REUSES logic from AddressableAudioFinderOdin.IsWrongCompression()
        /// </summary>
        public List<AudioInfo> FindAudioWithWrongCompression(float targetQuality = 0.2f, int longAudioLengthThreshold = 60, bool forceRefresh = false)
        {
            var allAudioInfos = this.GetAllAudioInfos(forceRefresh);

            return allAudioInfos.Values
                .Where(info => this.IsWrongAudioCompression(info, targetQuality, longAudioLengthThreshold))
                .ToList();
        }

        /// <summary>
        /// Helper: Check if audio has wrong compression settings.
        /// REUSES logic from AddressableAudioFinderOdin.IsWrongCompression()
        /// </summary>
        private bool IsWrongAudioCompression(AudioInfo info, float targetQuality, int longAudioLengthThreshold)
        {
            var isLongSound = info.Audio.length >= longAudioLengthThreshold;
            var expectedLoadType = isLongSound ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            var expectedPreloadData = !isLongSound;

            return !info.ForceToMono
                || info.LoadType != expectedLoadType
                || info.PreloadAudioData != expectedPreloadData
                || info.CompressionFormat != AudioCompressionFormat.Vorbis
                || Math.Abs(info.Quality - targetQuality) > 0.0001f
                || info.Normalize;
        }

        #endregion

        #region Mesh Analysis (from AddressableMeshFinderOdin)

        /// <summary>
        /// Get all meshes with their metadata.
        /// EXTRACTS logic from AddressableMeshFinderOdin.FindMeshesInAddressables()
        /// </summary>
        public Dictionary<ModelImporterMeshCompression, List<MeshInfo>> GetMeshesByCompression(bool forceRefresh = false)
        {
            const string cacheKey = "MeshesByCompression";

            if (!forceRefresh && this.cache.IsValid(cacheKey))
                return this.cache.Get<Dictionary<ModelImporterMeshCompression, List<MeshInfo>>>(cacheKey);

            var result = new Dictionary<ModelImporterMeshCompression, List<MeshInfo>>
            {
                { ModelImporterMeshCompression.Off, new List<MeshInfo>() },
                { ModelImporterMeshCompression.Low, new List<MeshInfo>() },
                { ModelImporterMeshCompression.Medium, new List<MeshInfo>() },
                { ModelImporterMeshCompression.High, new List<MeshInfo>() },
            };

            var allMeshInAddressable = AssetSearcher.GetAllAssetInAddressable<Mesh>();

            foreach (var keyValuePair in allMeshInAddressable.Where(kvp => kvp.Key))
            {
                var mesh = keyValuePair.Key;
                var assetPath = AssetDatabase.GetAssetPath(mesh);
                var modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (modelImporter == null) continue;

                var meshInfo = new MeshInfo
                {
                    Mesh = mesh,
                    Objects = keyValuePair.Value.ToList(),
                    ModelImporter = modelImporter,
                };

                result[modelImporter.meshCompression].Add(meshInfo);
            }

            this.cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        #endregion

        #region Font Analysis (from FontFinderOdin)

        /// <summary>
        /// Get all fonts categorized by compression status.
        /// EXTRACTS logic from FontFinderOdin.FindFontsInAddressable()
        /// </summary>
        public (List<FontInfo> compressed, List<FontInfo> uncompressed) GetFontsByCompression(bool forceRefresh = false)
        {
            const string cacheKey = "FontsByCompression";

            if (!forceRefresh && this.cache.IsValid(cacheKey))
                return this.cache.Get<(List<FontInfo>, List<FontInfo>)>(cacheKey);

            var compressed = new List<FontInfo>();
            var uncompressed = new List<FontInfo>();

            var fonts = AssetSearcher.GetAllAssetInAddressable<Font>();

            foreach (var keyValuePair in fonts)
            {
                var font = keyValuePair.Key;
                var path = AssetDatabase.GetAssetPath(font);
                var fontImporter = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                if (fontImporter == null) continue;

                var fontInfo = new FontInfo
                {
                    Font = font,
                    FontImporter = fontImporter,
                    Objects = keyValuePair.Value.ToList(),
                };

                if (fontImporter.fontTextureCase == FontTextureCase.CustomSet)
                    compressed.Add(fontInfo);
                else
                    uncompressed.Add(fontInfo);
            }

            var result = (compressed, uncompressed);
            this.cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        #endregion

        #region Shader Analysis (from ShaderListOdinWindow)

        /// <summary>
        /// Get all dependencies for an addressable asset path.
        /// REUSES logic from ShaderListOdinWindow.GetAllDependencies()
        /// </summary>
        public List<string> GetAllDependencies(string assetPath)
        {
            return new List<string>(AssetDatabase.GetDependencies(assetPath, true));
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Clear all cached analysis results.
        /// </summary>
        public void ClearCache()
        {
            this.cache.Clear();
        }

        /// <summary>
        /// Get cache statistics for debugging.
        /// </summary>
        public (int count, List<string> keys) GetCacheStats()
        {
            return this.cache.GetStats();
        }

        #endregion
    }
}
