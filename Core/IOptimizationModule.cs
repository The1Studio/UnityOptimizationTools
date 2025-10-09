using System;

namespace TheOne.UITemplate.Editor.Optimization
{
    /// <summary>
    /// Interface for all optimization modules in the Optimization Hub.
    /// Modules implement this interface to provide consistent lifecycle hooks and behavior.
    /// </summary>
    public interface IOptimizationModule
    {
        /// <summary>
        /// Display name of the module shown in the UI.
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Icon emoji or symbol representing this module.
        /// </summary>
        string ModuleIcon { get; }

        /// <summary>
        /// Called when the module becomes active and is displayed.
        /// Use this to initialize or refresh module state.
        /// </summary>
        void OnActivated();

        /// <summary>
        /// Called when the module is deactivated (user switches to another module).
        /// Use this to clean up temporary state.
        /// </summary>
        void OnDeactivated();

        /// <summary>
        /// Refreshes the module data from the current project state.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Clears all cached data and resets the module to initial state.
        /// </summary>
        void Clear();
    }
}
