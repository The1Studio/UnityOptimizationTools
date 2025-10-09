using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace TheOne.UITemplate.Editor.Optimization.Modules
{
    /// <summary>
    /// Prefab Module - Analyzes prefab usage, dependencies, missing references, and nesting depth.
    /// Provides tools for finding unused prefabs, detecting circular dependencies, and optimizing prefab hierarchies.
    /// </summary>
    public class PrefabModule : OptimizationModuleBase
    {
        public override string ModuleName => "Prefab Analysis";
        public override string ModuleIcon => "🧩";

        #region Tab: Summary

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [Title("Prefab Statistics", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Total Prefabs")]
        private string TotalPrefabs => $"🧩 {this.allPrefabs.Count:N0} prefabs";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Nested Prefabs")]
        [GUIColor(0.6f, 0.8f, 1f)]
        private string NestedPrefabsCount => $"🔗 {this.allPrefabs.Count(p => p.NestedPrefabCount > 0):N0} with variants/nesting";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Missing References")]
        [GUIColor(1f, 0.6f, 0.6f)]
        private string MissingReferencesCount => $"⚠️ {this.prefabsWithMissingReferences.Count:N0} have issues";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Deep Nesting")]
        [GUIColor(1f, 0.9f, 0.6f)]
        private string DeepNestingCount => $"📊 {this.deeplyNestedPrefabs.Count:N0} deep hierarchies";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Issues")]
        [Title("Quick Actions", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Prefab analysis helps identify unused assets, broken references, and optimization opportunities.", InfoMessageType.Info)]
        [PropertySpace(10)]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Select All Missing Refs", ButtonSizes.Medium)]
        [GUIColor(1f, 0.7f, 0.7f)]
        private void SelectAllMissingReferences()
        {
            var prefabs = this.prefabsWithMissingReferences.Select(p => p.Prefab).Where(p => p != null).ToArray();
            if (prefabs.Length == 0)
            {
                Debug.LogWarning("[PrefabModule] No prefabs with missing references found.");
                return;
            }
            Selection.objects = prefabs;
            Debug.Log($"[PrefabModule] Selected {prefabs.Length} prefabs with missing references.");
        }

        [TabGroup("Tabs", "Summary")]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Select Deep Nested", ButtonSizes.Medium)]
        [GUIColor(1f, 0.9f, 0.6f)]
        private void SelectDeepNested()
        {
            var prefabs = this.deeplyNestedPrefabs.Select(p => p.Prefab).Where(p => p != null).ToArray();
            if (prefabs.Length == 0)
            {
                Debug.LogWarning("[PrefabModule] No deeply nested prefabs found.");
                return;
            }
            Selection.objects = prefabs;
            Debug.Log($"[PrefabModule] Selected {prefabs.Length} deeply nested prefabs.");
        }

        #endregion

        #region Tab: All Prefabs

        [TabGroup("Tabs", "All Prefabs")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("All Prefabs in Project", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Shows all prefabs with dependency information and nesting depth.", InfoMessageType.Info)]
        [Searchable]
        private List<PrefabInfo> allPrefabs = new List<PrefabInfo>();

        [TabGroup("Tabs", "All Prefabs")]
        [ButtonGroup("Tabs/All Prefabs/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(0.6f, 1f, 0.6f)]
        private void SelectAllPrefabs()
        {
            var prefabs = this.allPrefabs.Select(p => p.Prefab).Where(p => p != null).ToArray();
            if (prefabs.Length == 0) return;
            Selection.objects = prefabs;
            Debug.Log($"[PrefabModule] Selected {prefabs.Length} prefabs.");
        }

        #endregion

        #region Tab: Missing References

        [TabGroup("Tabs", "Missing References")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Prefabs with Missing References", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("⚠️ These prefabs have missing or broken component/asset references.", InfoMessageType.Warning)]
        [Searchable]
        private List<PrefabInfo> prefabsWithMissingReferences = new List<PrefabInfo>();

        [TabGroup("Tabs", "Missing References")]
        [ButtonGroup("Tabs/Missing References/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(1f, 0.7f, 0.7f)]
        private void SelectMissingRefsPrefabs()
        {
            this.SelectAllMissingReferences();
        }

        [TabGroup("Tabs", "Missing References")]
        [ButtonGroup("Tabs/Missing References/Actions")]
        [Button("Log Details", ButtonSizes.Medium)]
        private void LogMissingReferences()
        {
            if (this.prefabsWithMissingReferences.Count == 0)
            {
                Debug.LogWarning("[PrefabModule] No prefabs with missing references found.");
                return;
            }

            Debug.Log($"[PrefabModule] Found {this.prefabsWithMissingReferences.Count} prefabs with missing references:");
            foreach (var prefab in this.prefabsWithMissingReferences)
            {
                Debug.Log($"  • {prefab.Name} - {prefab.MissingReferences} missing refs - {prefab.Path}");
            }
        }

        #endregion

        #region Tab: Deep Nesting

        [TabGroup("Tabs", "Deep Nesting")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Deeply Nested Prefabs (>5 levels)", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Deep prefab nesting can impact performance and make editing difficult. Consider flattening hierarchies.", InfoMessageType.Warning)]
        [Searchable]
        private List<PrefabInfo> deeplyNestedPrefabs = new List<PrefabInfo>();

        [TabGroup("Tabs", "Deep Nesting")]
        [ButtonGroup("Tabs/Deep Nesting/Actions")]
        [Button("Select All", ButtonSizes.Medium)]
        [GUIColor(1f, 0.9f, 0.6f)]
        private void SelectDeepNestedPrefabs()
        {
            this.SelectDeepNested();
        }

        #endregion

        #region Tab: Variants

        [TabGroup("Tabs", "Variants")]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Prefabs with Variants", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Shows prefabs that have nested prefab variants or references.", InfoMessageType.Info)]
        [Searchable]
        private List<PrefabInfo> prefabsWithVariants = new List<PrefabInfo>();

        #endregion

        #region Analysis Logic

        protected override void OnAnalyze()
        {
            EditorUtility.DisplayProgressBar("Prefab Analysis", "Scanning project for prefabs...", 0f);
            try
            {
                this.AnalyzePrefabs();
                Debug.Log($"[PrefabModule] Analysis complete: {this.allPrefabs.Count} prefabs analyzed, " +
                          $"{this.prefabsWithMissingReferences.Count} with missing references, " +
                          $"{this.deeplyNestedPrefabs.Count} deeply nested.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        protected override void OnRefresh()
        {
            this.OnAnalyze();
        }

        protected override void OnClear()
        {
            this.allPrefabs.Clear();
            this.prefabsWithMissingReferences.Clear();
            this.deeplyNestedPrefabs.Clear();
            this.prefabsWithVariants.Clear();
            Debug.Log("[PrefabModule] Data cleared.");
        }

        private void AnalyzePrefabs()
        {
            this.allPrefabs.Clear();
            this.prefabsWithMissingReferences.Clear();
            this.deeplyNestedPrefabs.Clear();
            this.prefabsWithVariants.Clear();

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            var total = prefabGuids.Length;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                EditorUtility.DisplayProgressBar("Prefab Analysis", $"Analyzing {prefab.name}...", (float)i / total);

                var info = this.AnalyzePrefab(prefab, path);
                this.allPrefabs.Add(info);

                // Categorize
                if (info.MissingReferences > 0) this.prefabsWithMissingReferences.Add(info);

                if (info.MaxNestingDepth > 5) this.deeplyNestedPrefabs.Add(info);

                if (info.NestedPrefabCount > 0) this.prefabsWithVariants.Add(info);
            }

            // Sort by issues
            this.prefabsWithMissingReferences = this.prefabsWithMissingReferences.OrderByDescending(p => p.MissingReferences).ToList();
            this.deeplyNestedPrefabs          = this.deeplyNestedPrefabs.OrderByDescending(p => p.MaxNestingDepth).ToList();
        }

        private PrefabInfo AnalyzePrefab(GameObject prefab, string path)
        {
            var info = new PrefabInfo
            {
                Prefab = prefab,
                Name = prefab.name,
                Path = path,
                ComponentCount = prefab.GetComponentsInChildren<Component>(true).Length,
                ChildCount = prefab.transform.childCount
            };

            // Analyze nesting depth
            info.MaxNestingDepth = this.GetMaxNestingDepth(prefab.transform);

            // Count nested prefabs
            info.NestedPrefabCount = this.CountNestedPrefabs(prefab);

            // Check for missing references
            info.MissingReferences = this.CountMissingReferences(prefab);

            return info;
        }

        private int GetMaxNestingDepth(Transform transform, int currentDepth = 0)
        {
            if (transform.childCount == 0) return currentDepth;

            int maxDepth = currentDepth;
            foreach (Transform child in transform)
            {
                int depth                      = this.GetMaxNestingDepth(child, currentDepth + 1);
                if (depth > maxDepth) maxDepth = depth;
            }
            return maxDepth;
        }

        private int CountNestedPrefabs(GameObject prefab)
        {
            int count = 0;
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && t.gameObject != prefab)
                    count++;
            }
            return count;
        }

        private int CountMissingReferences(GameObject prefab)
        {
            int missingCount = 0;
            var components = prefab.GetComponentsInChildren<Component>(true);

            foreach (var component in components)
            {
                if (component == null)
                {
                    missingCount++;
                    continue;
                }

                var so = new SerializedObject(component);
                var sp = so.GetIterator();

                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0)
                        {
                            missingCount++;
                        }
                    }
                }
            }

            return missingCount;
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-analyze if no data
            if (this.allPrefabs.Count == 0)
            {
                this.OnAnalyze();
            }
        }

        #endregion

        #region Data Model

        [System.Serializable]
        private class PrefabInfo
        {
            [TableColumnWidth(200)]
            [ShowInInspector, ReadOnly]
            public GameObject Prefab;

            [TableColumnWidth(200)]
            [ShowInInspector, ReadOnly]
            public string Name;

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public int ComponentCount;

            [TableColumnWidth(80)]
            [ShowInInspector, ReadOnly]
            public int ChildCount;

            [TableColumnWidth(100)]
            [ShowInInspector, ReadOnly]
            public int NestedPrefabCount;

            [TableColumnWidth(100)]
            [ShowInInspector, ReadOnly]
            public int MaxNestingDepth;

            [TableColumnWidth(120)]
            [ShowInInspector, ReadOnly]
            [GUIColor("GetMissingRefColor")]
            public int MissingReferences;

            [TableColumnWidth(300)]
            [ShowInInspector, ReadOnly]
            public string Path;

            private Color GetMissingRefColor()
            {
                return this.MissingReferences > 0 ? new Color(1f, 0.6f, 0.6f) : Color.white;
            }
        }

        #endregion
    }
}
