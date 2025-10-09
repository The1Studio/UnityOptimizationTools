namespace TheOne.Tool.Optimization
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Auto-optimization script for project initialization.
    /// Disabled: BuildInScreenFinderOdin.AutoOptimize() has been migrated to Optimization Hub.
    /// </summary>
    // [InitializeOnLoad]
    public static class ProjectAutoOptimizationScript
    {
        // static ProjectAutoOptimizationScript()
        // {
        //     if (EditorUtility.scriptCompilationFailed || EditorApplication.isCompiling)
        //     {
        //         Debug.LogWarning("Skipping migration due to compilation errors or isCompiling.");
        //         return;
        //     }
        //
        //     if (!SessionState.GetBool("AutoOptimizeRan", false))
        //     {
        //         SessionState.SetBool("AutoOptimizeRan", true);
        //         // BuildInScreenFinderOdin.AutoOptimize(); // Migrated to Optimization Hub
        //     }
        // }
    }
}