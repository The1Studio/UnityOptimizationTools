namespace TheOne.Tool.Optimization.Models
{
    using System.Collections.Generic;
    using Sirenix.OdinInspector;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Data model for mesh information used in optimization analysis.
    /// </summary>
    [System.Serializable]
    public class MeshInfo
    {
        [InlineProperty]
        [ShowInInspector]
        public Mesh Mesh { get; set; }

        [InlineProperty]
        [ShowInInspector]
        public List<Object> Objects { get; set; }

        [InlineProperty]
        [ShowInInspector]
        public ModelImporter ModelImporter { get; set; }

        [InlineProperty]
        [ShowInInspector]
        public ModelImporterMeshCompression MeshCompression => this.ModelImporter.meshCompression;

        [InlineProperty]
        [ShowInInspector]
        public ModelImporterAnimationCompression AnimationCompression => this.ModelImporter.animationCompression;

        /// <summary>
        /// Gets specific information from ModelImporter.
        /// </summary>
        public string GetImporterInfo()
        {
            return this.ModelImporter != null ? this.ModelImporter.assetPath : "No Importer";
        }
    }
}
