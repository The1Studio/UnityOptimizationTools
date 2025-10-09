namespace TheOne.Tool.Optimization.Models
{
    using Sirenix.OdinInspector;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Data model for texture information used in optimization analysis.
    /// </summary>
    [System.Serializable]
    public class TextureInfo
    {
        [HideLabel]
        [PreviewField(90, ObjectFieldAlignment.Left)]
        [HorizontalGroup("group", 90)]
        [VerticalGroup("group/left")]
        [ShowInInspector]
        public Texture Texture { get; set; }

        [VerticalGroup("group/right")]
        [ShowInInspector]
        public string Name => this.Texture.name;

        [VerticalGroup("group/right")]
        [ShowInInspector]
        [LabelText("File Size (Byte)")]
        public long FileSize { get; set; }

        [VerticalGroup("group/right")]
        [ShowInInspector]
        [LabelText("TinyPNG Compressed File Size (Byte)")]
        public long TinyPNGCompressedFileSize { get; set; }

        [VerticalGroup("group/right")]
        [ShowInInspector]
        public Vector2 TextureSize { get; set; }

        public TextureImporter TextureImporter { get; set; }

        [VerticalGroup("group/right")]
        [ShowInInspector]
        public bool ReadWriteEnabled { get; set; }

        [VerticalGroup("group/right")]
        [ShowInInspector]
        public bool GenerateMipMapEnabled { get; set; }

        [VerticalGroup("group/right")]
        [ShowInInspector]
        public int MaxTextureSize => this.TextureImporter.maxTextureSize;

        [VerticalGroup("group/right")]
        [ShowInInspector]
        public TextureImporterCompression CompressionType => this.TextureImporter.textureCompression;

        [VerticalGroup("group/right")]
        [ShowInInspector]
        public bool UseCrunchCompression => this.TextureImporter.crunchedCompression;

        public string Path { get; set; }
    }
}
