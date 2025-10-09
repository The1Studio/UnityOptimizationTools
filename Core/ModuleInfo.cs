using System;
using Sirenix.OdinInspector;

namespace TheOne.UITemplate.Editor.Optimization
{
    /// <summary>
    /// Metadata for an optimization module displayed in the sidebar.
    /// Contains display information and status counters.
    /// </summary>
    [Serializable]
    public class ModuleInfo
    {
        /// <summary>
        /// Icon emoji or symbol for the module.
        /// </summary>
        [HorizontalGroup]
        [LabelWidth(30)]
        [ReadOnly]
        public string Icon;

        /// <summary>
        /// Display name of the module.
        /// </summary>
        [HorizontalGroup]
        [HideLabel]
        [ReadOnly]
        public string Name;

        /// <summary>
        /// Module description text.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Number of issues found by this module.
        /// </summary>
        public int IssueCount { get; set; }

        /// <summary>
        /// Number of assets analyzed by this module.
        /// </summary>
        public int AssetCount { get; set; }

        /// <summary>
        /// Creates a new ModuleInfo instance.
        /// </summary>
        /// <param name="name">Module name</param>
        /// <param name="icon">Module icon</param>
        /// <param name="description">Module description</param>
        public ModuleInfo(string name, string icon, string description)
        {
            this.Name     = name;
            this.Icon     = icon;
            this.Description = description;
        }
    }
}