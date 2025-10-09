using Sirenix.OdinInspector;

namespace TheOne.UITemplate.Editor.Optimization
{
    /// <summary>
    /// Base class for all optimization modules providing common UI elements and lifecycle hooks.
    /// Modules inherit from this class to get standard toolbar buttons and structured layout.
    /// </summary>
    public abstract class OptimizationModuleBase : IOptimizationModule
    {
        /// <summary>
        /// Display name of the module shown in the UI.
        /// </summary>
        public abstract string ModuleName { get; }

        /// <summary>
        /// Icon emoji or symbol representing this module.
        /// </summary>
        public abstract string ModuleIcon { get; }

        /// <summary>
        /// Analyze button - triggers module-specific analysis.
        /// </summary>
        [PropertyOrder(-10)]
        [HorizontalGroup("Toolbar")]
        [Button(ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
        public void Analyze()
        {
            this.OnAnalyze();
        }

        /// <summary>
        /// Refresh button - reloads module data.
        /// </summary>
        [PropertyOrder(-10)]
        [HorizontalGroup("Toolbar")]
        [Button(ButtonSizes.Medium)]
        public void Refresh()
        {
            this.OnRefresh();
        }

        /// <summary>
        /// Clear button - resets module state.
        /// </summary>
        [PropertyOrder(-10)]
        [HorizontalGroup("Toolbar")]
        [Button(ButtonSizes.Medium)]
        public void Clear()
        {
            this.OnClear();
        }

        /// <summary>
        /// Implement module-specific analysis logic.
        /// </summary>
        protected abstract void OnAnalyze();

        /// <summary>
        /// Implement module-specific refresh logic.
        /// </summary>
        protected abstract void OnRefresh();

        /// <summary>
        /// Implement module-specific clear logic.
        /// </summary>
        protected abstract void OnClear();

        /// <summary>
        /// Called when the module becomes active.
        /// Override to implement activation logic.
        /// </summary>
        public virtual void OnActivated() { }

        /// <summary>
        /// Called when the module is deactivated.
        /// Override to implement deactivation logic.
        /// </summary>
        public virtual void OnDeactivated() { }
    }
}
