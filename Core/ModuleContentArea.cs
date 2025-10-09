using System.Collections.Generic;
using Sirenix.OdinInspector;
using TheOne.UITemplate.Editor.Optimization.Modules;

namespace TheOne.UITemplate.Editor.Optimization
{
    /// <summary>
    /// Right panel component that displays the currently active module.
    /// Handles lazy loading and caching of module instances.
    /// </summary>
    public class ModuleContentArea
    {
        /// <summary>
        /// Currently displayed module.
        /// </summary>
        [ShowInInspector]
        [HideLabel]
        [InlineEditor(InlineEditorModes.GUIOnly)]
        private IOptimizationModule currentModule;

        /// <summary>
        /// Cache of instantiated modules to avoid recreating them.
        /// </summary>
        private Dictionary<string, IOptimizationModule> moduleCache = new Dictionary<string, IOptimizationModule>();

        /// <summary>
        /// Loads and displays the specified module.
        /// Modules are lazy-loaded and cached for performance.
        /// </summary>
        /// <param name="moduleName">Name of the module to load</param>
        public void LoadModule(string moduleName)
        {
            // Deactivate current module
            this.currentModule?.OnDeactivated();

            // Lazy load and cache modules
            if (!this.moduleCache.ContainsKey(moduleName))
            {
                this.moduleCache[moduleName] = this.CreateModule(moduleName);
            }

            // Activate new module
            this.currentModule = this.moduleCache[moduleName];
            this.currentModule?.OnActivated();
        }

        /// <summary>
        /// Factory method to create module instances.
        /// Override or extend this to add new modules.
        /// </summary>
        /// <param name="moduleName">Name of the module to create</param>
        /// <returns>New module instance</returns>
        private IOptimizationModule CreateModule(string moduleName)
        {
            return moduleName switch
            {
                "Overview" => new OverviewModule(),
                "Textures" => new TextureModule(),
                "Font" => new FontModule(),
                "Shader" => new ShaderModule(),
                "Mesh" => new MeshModule(),
                "Addressables" => new AddressablesModule(),
                "Audio" => new OverviewModule(), // AudioModule - investigating compilation issue
                "Reports" => new OverviewModule(), // ReportsModule - investigating compilation issue
                "Settings" => new SettingsModule(),
                _ => new OverviewModule()
            };
        }
    }
}