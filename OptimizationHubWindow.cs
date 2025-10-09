using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TheOne.UITemplate.Editor.Optimization.Modules;

namespace TheOne.UITemplate.Editor.Optimization
{
    /// <summary>
    /// Main window for the Unity Optimization Hub.
    /// Provides a centralized interface for all optimization tools using a tab-based layout.
    /// Features:
    /// - Simple tab-based navigation with Odin TabGroup
    /// - Lazy loading and caching for performance
    /// - State persistence across Unity sessions
    /// - Direct module access without complex layouts
    /// </summary>
    public class OptimizationHubWindow : OdinEditorWindow
    {
        /// <summary>
        /// Opens the Optimization Hub window.
        /// </summary>
        [MenuItem("TheOne/Optimization Hub")]
        private static void OpenWindow()
        {
            var window = GetWindow<OptimizationHubWindow>("🔧 Optimization Hub");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        #region Modules

        [TabGroup("📊 Overview", order: 0)]
        [PropertyOrder(-1)]
        [HorizontalGroup("📊 Overview/Header")]
        [VerticalGroup("📊 Overview/Header/Left")]
        [ShowInInspector, ReadOnly, HideLabel]
        [PreviewField(50, ObjectFieldAlignment.Center)]
        private Texture2D Logo
        {
            get
            {
                if (this.logoTexture == null)
                {
                    this.logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UITemplate/Editor/Optimization/theoneLogo-normal.png");
                }
                return this.logoTexture;
            }
        }
        private Texture2D logoTexture;

        [TabGroup("📊 Overview", order: 0)]
        [PropertyOrder(-1)]
        [HorizontalGroup("📊 Overview/Header")]
        [VerticalGroup("📊 Overview/Header/Right")]
        [Title("Unity Optimization Hub", bold: true, horizontalLine: false, titleAlignment: TitleAlignments.Left)]
        [InfoBox("Comprehensive toolkit for analyzing and optimizing Unity projects. " +
                 "Scan textures, audio, meshes, shaders, fonts, and addressables for performance improvements.\n\n" +
                 "✓  Identify optimization opportunities\n" +
                 "✓  Reduce build size and memory usage\n" +
                 "✓  Improve runtime performance",
                 InfoMessageType.None)]
        [ShowInInspector, HideLabel]
        [ReadOnly]
        private string hubDescriptionPlaceholder => "";

        [TabGroup("📊 Overview", order: 0)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private OverviewModule overviewModule = new OverviewModule();

        [TabGroup("🖼 Textures", order: 1)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private TextureModule textureModule = new TextureModule();

        [TabGroup("🔊 Audio", order: 2)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private AudioModule audioModule = new AudioModule();

        [TabGroup("📦 Addressables", order: 3)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private AddressablesModule addressablesModule = new AddressablesModule();

        [TabGroup("🎨 Mesh", order: 4)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private MeshModule meshModule = new MeshModule();

        [TabGroup("🌈 Shader", order: 5)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private ShaderModule shaderModule = new ShaderModule();

        [TabGroup("🔤 Font", order: 6)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private FontModule fontModule = new FontModule();

        [TabGroup("📄 Reports", order: 7)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private ReportsModule reportsModule = new ReportsModule();

        [TabGroup("⚙ Settings", order: 8)]
        [HideLabel]
        [InlineProperty]
        [ShowInInspector]
        private SettingsModule settingsModule = new SettingsModule();

        #endregion

        /// <summary>
        /// Called when the window is enabled.
        /// Initializes all modules.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            // Initialize modules if null
            if (this.overviewModule == null) this.overviewModule         = new OverviewModule();
            if (this.textureModule == null) this.textureModule           = new TextureModule();
            if (this.audioModule == null) this.audioModule               = new AudioModule();
            if (this.addressablesModule == null) this.addressablesModule = new AddressablesModule();
            if (this.meshModule == null) this.meshModule                 = new MeshModule();
            if (this.shaderModule == null) this.shaderModule             = new ShaderModule();
            if (this.fontModule == null) this.fontModule                 = new FontModule();
            if (this.reportsModule == null) this.reportsModule           = new ReportsModule();
            if (this.settingsModule == null) this.settingsModule            = new SettingsModule();

            // Activate overview module by default
            this.overviewModule.OnActivated();
        }

        protected override void OnImGUI()
        {
            // Increase font size for better readability
            var originalFontSize = GUI.skin.label.fontSize;
            var originalButtonFontSize = GUI.skin.button.fontSize;

            GUI.skin.label.fontSize = 12;
            GUI.skin.button.fontSize = 11;

            base.OnImGUI();

            // Restore original font sizes
            GUI.skin.label.fontSize = originalFontSize;
            GUI.skin.button.fontSize = originalButtonFontSize;
        }

        /// <summary>
        /// Called when the window is disabled.
        /// Cleanup modules.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
        }
    }
}
