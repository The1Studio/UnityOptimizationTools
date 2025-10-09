namespace TheOne.Tool.Optimization.Models
{
    using System.Collections.Generic;
    using System.Linq;
    using Sirenix.OdinInspector;
    using Sirenix.Utilities.Editor;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Data model for shader and material information used in optimization analysis.
    /// </summary>
    [System.Serializable]
    public class ShaderMaterialInfo
    {
        [TableColumnWidth(200, Resizable = true)]
        [LabelText("Original Shader")]
        [ReadOnly]
        public Shader OriginalShader;

        [TableColumnWidth(200, Resizable = true)]
        [LabelText("Replacement Shader")]
        [ValueDropdown("GetAllShadersForDropdown")]
        public Shader ReplacementShader;

        [TableColumnWidth(80, Resizable = false)]
        [LabelText("Materials")]
        [ShowInInspector, DisplayAsString]
        [ReadOnly]
        private string MaterialCountDisplay => $"📦 {this.GetMaterialCount()}";

        [TableColumnWidth(120, Resizable = false)]
        [LabelText("Actions")]
        [HorizontalGroup("Actions", Width = 120)]
        [Button("Replace", ButtonSizes.Small)]
        [EnableIf("CanReplaceShader")]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        private void ReplaceButtonInTable()
        {
            this.ReplaceShaderForThis();
        }

        [FoldoutGroup("Details", Expanded = false)]
        [ShowInInspector]
        [HideLabel]
        [TableList(
            AlwaysExpanded = true,
            DrawScrollView = true,
            ShowIndexLabels = false,
            IsReadOnly = false,
            ShowPaging = true,
            NumberOfItemsPerPage = 20
        )]
        [Title("Materials Using This Shader", TitleAlignment = TitleAlignments.Centered, Bold = true)]
        [InfoBox("Select materials to replace by checking 'Should Replace' column", InfoMessageType.Info)]
        public List<MaterialInfo> Materials = new();

        private ValueDropdownList<Shader> GetAllShadersForDropdown()
        {
            // Return all shaders in project
            var shaders = new ValueDropdownList<Shader>();
            var guids = AssetDatabase.FindAssets("t:Shader");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader != null)
                {
                    shaders.Add(shader.name, shader);
                }
            }
            return shaders;
        }

        public bool CanReplaceShader()
        {
            return this.ReplacementShader != null && this.ReplacementShader != this.OriginalShader;
        }

        private bool ContainsMaterial(Material material)
        {
            return this.Materials.Exists(m => m.Material == material);
        }

        public void AddUniqueMaterial(Material material, GameObject obj)
        {
            var existingInfo = this.Materials.FirstOrDefault(m => m.Material == material);

            if (existingInfo == null)
            {
                var newInfo = new MaterialInfo { Material = material };
                newInfo.UsingObjects.Add(obj);
                this.Materials.Add(newInfo);
            }
            else
            {
                if (!existingInfo.UsingObjects.Contains(obj))
                {
                    existingInfo.UsingObjects.Add(obj);
                }
            }
        }

        [FoldoutGroup("Details")]
        [ButtonGroup("Details/Action")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(0.6f, 0.8f, 1f)]
        public void SelectAllMaterials()
        {
            foreach (var materialInfo in this.Materials)
            {
                materialInfo.ShouldReplace = true;
            }
        }

        [FoldoutGroup("Details")]
        [ButtonGroup("Details/Action")]
        [Button("Deselect All", ButtonSizes.Medium)]
        [GUIColor(1f, 0.6f, 0.6f)]
        public void DeselectAllMaterials()
        {
            foreach (var materialInfo in this.Materials)
            {
                materialInfo.ShouldReplace = false;
            }
        }

        public void ReplaceShaderForThis()
        {
            if (this.ReplacementShader != null)
            {
                foreach (var materialInfo in this.Materials)
                {
                    if (materialInfo.ShouldReplace)
                    {
                        materialInfo.Material.shader = this.ReplacementShader;
                    }
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"[ShaderMaterialInfo] Replaced shader for {this.Materials.Count(m => m.ShouldReplace)} materials.");
            }
        }

        /// <summary>
        /// Gets the total number of materials using this shader.
        /// </summary>
        public int GetMaterialCount()
        {
            return this.Materials?.Count ?? 0;
        }
    }

    /// <summary>
    /// Information about a material and its usage.
    /// </summary>
    [System.Serializable]
    public class MaterialInfo
    {
        [TableColumnWidth(250, Resizable = true)]
        [PreviewField(50f, Sirenix.OdinInspector.ObjectFieldAlignment.Left)]
        [HorizontalGroup("Row")]
        [VerticalGroup("Row/Left")]
        [LabelText("Material")]
        [ReadOnly]
        public Material Material;

        [TableColumnWidth(100, Resizable = false)]
        [VerticalGroup("Row/Center")]
        [LabelText("Usage Count")]
        [ShowInInspector, DisplayAsString]
        [ReadOnly]
        private string UsageCount => $"📦 {this.UsingObjects?.Count ?? 0} objects";

        [HideInInspector]
        public List<GameObject> UsingObjects = new();

        [HideInInspector]
        public bool isImportedAsset;

        [TableColumnWidth(80, Resizable = false)]
        [VerticalGroup("Row/Right")]
        [LabelText("Replace")]
        [ToggleLeft]
        public bool ShouldReplace
        {
            get
            {
                // If the asset is imported (like materials in FBX), always return false.
                if (this.isImportedAsset) return false;
                return this._shouldReplace;
            }
            set
            {
                if (!this.isImportedAsset)
                {
                    this._shouldReplace = value;
                }
            }
        }

        private bool _shouldReplace = true;

        public MaterialInfo()
        {
            // Check if the material is part of an imported asset.
            if (this.Material != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(this.Material);
                var importer = AssetImporter.GetAtPath(assetPath);
                this.isImportedAsset = importer != null && importer.assetPath != assetPath;
            }
        }
    }
}
