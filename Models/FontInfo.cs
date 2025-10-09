namespace TheOne.Tool.Optimization.Models
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Data model for font information used in optimization analysis.
    /// </summary>
    [System.Serializable]
    public class FontInfo
    {
        public Font Font;
        public TrueTypeFontImporter FontImporter;
        public List<Object> Objects;
        public string CustomSet => this.FontImporter.customCharacters;
    }
}
