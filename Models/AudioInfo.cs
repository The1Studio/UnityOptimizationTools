namespace TheOne.Tool.Optimization.Models
{
    using Sirenix.OdinInspector;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Data model for audio clip information used in optimization analysis.
    /// </summary>
    [System.Serializable]
    public class AudioInfo
    {
        [HideLabel]
        [PreviewField(90, ObjectFieldAlignment.Left)]
        [HorizontalGroup("row2", 90)]
        [VerticalGroup("row2/left")]
        [ShowInInspector]
        public AudioClip AudioPreview { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        public AudioClip Audio { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        public AudioImporter Importer { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        public bool ForceToMono { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        public bool Normalize { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        public bool LoadInBackground { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        public AudioClipLoadType LoadType { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        public bool PreloadAudioData { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        public AudioCompressionFormat CompressionFormat { get; set; }

        [VerticalGroup("row2/right")]
        [ShowInInspector]
        [PropertyRange(0, 1)]
        public float Quality { get; set; }
    }
}
