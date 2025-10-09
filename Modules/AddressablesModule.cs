using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using TheOne.Extensions;
using TheOne.Tool.Core;
using TheOne.Tool.Optimization.Texture.MenuItems;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TheOne.UITemplate.Editor.Optimization.Modules
{
    /// <summary>
    /// Addressables Module - Consolidates group management and build-in screen preload analysis.
    /// Combines functionality from AddressableGroupOdin and BuildInScreenFinderOdin.
    /// </summary>
    public class AddressablesModule : OptimizationModuleBase
    {
        public override string ModuleName => "Addressables Management";
        public override string ModuleIcon => "📦";

        private static string NotBuildinScreenAssetGroupName = "NotBuildInScreenAsset";

        #region Tab: Summary

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [Title("Addressables Statistics", TitleAlignment = TitleAlignments.Centered)]
        [ShowInInspector, DisplayAsString, LabelText("Total Groups")]
        private string TotalGroups => $"📦 {this.groups.Count:N0} groups";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Remote Groups")]
        [GUIColor(0.6f, 0.8f, 1f)]
        private string RemoteGroups => $"☁️ {this.groups.Count(g => g.IsRemotely):N0} remote";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Build-In Scenes")]
        [GUIColor(0.8f, 1f, 0.6f)]
        private string BuildInScenes => $"🎮 {this.sceneToDependencyAsset.Count:N0} preload scenes";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Statistics")]
        [ShowInInspector, DisplayAsString, LabelText("Misplaced Assets")]
        [GUIColor(1f, 0.6f, 0.6f)]
        private string MisplacedAssets => $"⚠️ {this.GetTotalMisplacedAssets():N0} need regroup";

        [TabGroup("Tabs", "Summary")]
        [BoxGroup("Tabs/Summary/Issues")]
        [Title("Quick Actions", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Addressables management helps organize assets into optimal groups for on-demand loading.", InfoMessageType.Info)]
        [PropertySpace(10)]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Analyze Groups", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void AnalyzeGroupsSummary()
        {
            this.GetAllGroups();
        }

        [TabGroup("Tabs", "Summary")]
        [HorizontalGroup("Tabs/Summary/Issues/Buttons")]
        [Button("Analyze Preload", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void AnalyzePreloadSummary()
        {
            this.AnalyzeBuildInScene();
        }

        private int GetTotalMisplacedAssets()
        {
            return this.buildInSceneAssetsThatNotInRightGroup.Sum(kp => kp.Value.Count) + this.notBuildInSceneAssetsThatNotInRightGroup.Count;
        }

        #endregion

        #region Tab: Groups

        [TabGroup("Tabs", "Groups")]
        [ShowInInspector]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 50, AlwaysExpanded = true)]
        [Title("Addressable Groups", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Shows all addressable groups with their build and load paths.", InfoMessageType.Info)]
        private List<AddressableGroupInfo> groups = new();

        [TabGroup("Tabs", "Groups")]
        [ButtonGroup("Tabs/Groups/Actions")]
        [Button(ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        public void GetAllGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressablesModule] Addressable Asset Settings not found.");
                return;
            }

            var remoteBuildProfileData = settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kRemoteBuildPath);

            this.groups = settings.groups.Select(group =>
            {
                var bundledAssetGroupSchema = group.GetSchema<BundledAssetGroupSchema>();
                if (bundledAssetGroupSchema == null) return null;
                return new AddressableGroupInfo
                {
                    Group = group,
                    Schema = bundledAssetGroupSchema,
                    IsRemotely = bundledAssetGroupSchema.BuildPath.Id.Equals(remoteBuildProfileData.Id),
                };
            }).Where(groupInfo => groupInfo != null).ToList();

            Debug.Log($"[AddressablesModule] Loaded {this.groups.Count} addressable groups.");
        }

        #endregion

        #region Tab: Preload (Build-in Screens)

        [TabGroup("Tabs", "Preload")]
        [ShowInInspector]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 30)]
        [Title("Dependencies Asset", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Shows all assets required by build-in scenes (preloaded at startup).", InfoMessageType.Info)]
        private Dictionary<SceneAsset, HashSet<Object>> sceneToDependencyAsset = new();

        [TabGroup("Tabs", "Preload")]
        [ShowInInspector]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 30)]
        [Title("Not In Right Atlas Texture", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("⚠️ These textures should be moved to the correct atlas for their scene.", InfoMessageType.Warning, VisibleIf = "@notInRightAtlasTexture.Count > 0")]
        private Dictionary<SceneAsset, HashSet<Texture>> notInRightAtlasTexture = new();

        [TabGroup("Tabs", "Preload")]
        [ShowInInspector]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 30)]
        [Title("Build-In Scenes Assets Not In Right Group", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("⚠️ These assets are used by build-in scenes but are in the wrong addressable group.", InfoMessageType.Warning, VisibleIf = "@buildInSceneAssetsThatNotInRightGroup.Count > 0")]
        private Dictionary<SceneAsset, HashSet<Object>> buildInSceneAssetsThatNotInRightGroup = new();

        [TabGroup("Tabs", "Preload")]
        [ShowInInspector]
        [TableList(ShowPaging = true, NumberOfItemsPerPage = 30)]
        [Title("Not Build-In Scenes Assets Not In Right Group", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("These assets are in build-in groups but not actually used by build-in scenes.", InfoMessageType.Info, VisibleIf = "@notBuildInSceneAssetsThatNotInRightGroup.Count > 0")]
        private HashSet<Object> notBuildInSceneAssetsThatNotInRightGroup = new();

        [TabGroup("Tabs", "Preload")]
        [PropertySpace(10)]
        [ButtonGroup("Tabs/Preload/Actions")]
        [Button(ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void AnalyzeBuildInScene()
        {
            this.FindActiveScreensAndAddressableReferences();
        }

        [TabGroup("Tabs", "Preload")]
        [ButtonGroup("Tabs/Preload/Actions")]
        [GUIColor(0.4f, 1f, 0.7f)]
        [Button(ButtonSizes.Large)]
        private void RegroupBuildInScreenAddressable()
        {
            this.MoveWrongAtlasTexture();
            this.ReGroup();
        }

        [TabGroup("Tabs", "Preload")]
        [ButtonGroup("Tabs/Preload/Actions")]
        [Button("Select Misplaced Assets", ButtonSizes.Medium)]
        [GUIColor(1f, 0.9f, 0.6f)]
        private void SelectMisplacedAssets()
        {
            var assets = this.buildInSceneAssetsThatNotInRightGroup.SelectMany(kp => kp.Value)
                .Concat(this.notBuildInSceneAssetsThatNotInRightGroup)
                .Where(a => a != null)
                .ToArray();

            if (assets.Length == 0)
            {
                Debug.LogWarning("[AddressablesModule] No misplaced assets found.");
                return;
            }

            Selection.objects = assets;
            Debug.Log($"[AddressablesModule] Selected {assets.Length} misplaced assets.");
        }

        private void FindActiveScreensAndAddressableReferences()
        {
            (this.sceneToDependencyAsset, this.buildInSceneAssetsThatNotInRightGroup, this.notBuildInSceneAssetsThatNotInRightGroup) = AnalyzeProject();
            this.notInRightAtlasTexture = AnalyzeAtlases(this.sceneToDependencyAsset);

            Debug.Log($"[AddressablesModule] Analyzed {this.sceneToDependencyAsset.Count} build-in scenes. " +
                      $"Found {this.buildInSceneAssetsThatNotInRightGroup.Sum(kp => kp.Value.Count)} misplaced assets.");
        }

        private static (Dictionary<SceneAsset, HashSet<Object>>, Dictionary<SceneAsset, HashSet<Object>>, HashSet<Object>) AnalyzeProject()
        {
            var activeScenes = FindActiveScreens().ToList();
            var sceneToDependencyAssets = activeScenes.ToDictionary(scene => scene,
                scene =>
                {
                    var hashSet = AssetSearcher.GetAllDependencies<Object>(scene).ToHashSet();
                    return hashSet;
                });

            var countedAssets = new HashSet<Object>();
            var buildInSceneAssetsThatNotInRightGroup = sceneToDependencyAssets.ToDictionary(kp => kp.Key,
                kp =>
                {
                    var hashSet = kp.Value.Where(asset => IsAssetInNotRightBuildInGroup(asset, kp.Key))
                        .ToHashSet();
                    hashSet.RemoveRange(countedAssets);
                    countedAssets.AddRange(kp.Value);
                    return hashSet;
                });

            var allAssetsInGroup = activeScenes.SelectMany(scene => AssetSearcher.GetAllAssetsInGroup(GetSceneAssetGroupName(scene))).ToHashSet();
            var dependencyAssets = sceneToDependencyAssets.SelectMany(kp => kp.Value).ToHashSet();
            var notBuildInSceneAssetsThatNotInRightGroup = allAssetsInGroup.Where(asset => !dependencyAssets.Contains(asset))
                .ToHashSet();

            return (sceneToDependencyAssets, buildInSceneAssetsThatNotInRightGroup, notBuildInSceneAssetsThatNotInRightGroup);
        }

        private static bool IsAssetInNotRightBuildInGroup(Object asset, SceneAsset sceneAsset)
        {
            return AssetSearcher.IsAssetAddressable(asset, out var group) && !group.name.Equals(GetSceneAssetGroupName(sceneAsset));
        }

        private static Dictionary<SceneAsset, HashSet<Texture>> AnalyzeAtlases(Dictionary<SceneAsset, HashSet<Object>> sceneToDependencyAsset)
        {
            var result = new Dictionary<SceneAsset, HashSet<Texture>>();

            var countedTextures = new HashSet<Texture>();
            foreach (var (scene, assets) in sceneToDependencyAsset)
            {
                result.Add(scene, new());
                var textures = assets.OfType<Texture>().ToHashSet();
                textures.RemoveRange(countedTextures);
                foreach (var texture in textures)
                {
                    var assetPath = AssetDatabase.GetAssetPath(texture);
                    var fileName = Path.GetFileName(assetPath);
                    if (!assetPath.Equals($"{GetTextureBuildInPath(scene)}/{fileName}")) result[scene].Add(texture);
                }
                countedTextures.AddRange(textures);
            }

            return result;
        }

        private static IEnumerable<SceneAsset> FindActiveScreens()
        {
            return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
        }

        private static string GetSceneAssetGroupName(SceneAsset sceneAsset)
        {
            return $"BuildInScreenAsset_{sceneAsset.name}";
        }

        private static string GetTextureBuildInPath(SceneAsset sceneAsset)
        {
            return $"Assets/Sprites/BuildInUI/{sceneAsset.name}";
        }

        private void MoveWrongAtlasTexture()
        {
            foreach (var (scene, textures) in this.notInRightAtlasTexture)
            {
                if (textures.Count == 0) continue;
                var folderPath = GetTextureBuildInPath(scene);
                if (AssetSearcher.CreateFolderIfNotExist(folderPath))
                {
                    AssetDatabase.Refresh();
                    CreateAtlasFromFolders.CreateAtlasForFolder(folderPath, AssetDatabase.LoadAssetAtPath<Object>(folderPath));
                    AssetDatabase.Refresh();
                }
                foreach (var texture in textures) AssetSearcher.MoveToNewFolder(texture, folderPath);
            }
            AssetDatabase.Refresh();
        }

        private void ReGroup()
        {
            if (this.buildInSceneAssetsThatNotInRightGroup.Sum(kpv => kpv.Value.Count) == 0 && this.notBuildInSceneAssetsThatNotInRightGroup.Count == 0)
            {
                EditorUtility.DisplayDialog("Optimizing BuildIn Scene Assets!!", "There are no assets to reorganize.", "OK");
                return;
            }

            var userConfirmed = EditorUtility.DisplayDialog("Optimizing BuildIn Scene Assets!!", "This will reorganize assets into the correct Addressable Asset Groups based on their usage. Do you want to proceed?", "Ok");
            if (!userConfirmed) return;

            var totalAssets = this.buildInSceneAssetsThatNotInRightGroup.Count + this.notBuildInSceneAssetsThatNotInRightGroup.Count;
            var processedAssets = 0;

            EditorUtility.DisplayProgressBar("Optimizing BuildIn Scene Assets!!", "Please wait...", 0f);

            var movedAssets = new List<string>();

            foreach (var (scene, values) in this.buildInSceneAssetsThatNotInRightGroup)
            foreach (var asset in values)
            {
                AssetSearcher.MoveAssetToGroup(asset, GetSceneAssetGroupName(scene));
                processedAssets++;
                movedAssets.Add(AssetDatabase.GetAssetPath(asset));
                EditorUtility.DisplayProgressBar("ReGrouping Assets", $"Processing {AssetDatabase.GetAssetPath(asset)}", processedAssets / (float)totalAssets);
            }

            foreach (var asset in this.notBuildInSceneAssetsThatNotInRightGroup)
            {
                AssetSearcher.MoveAssetToGroup(asset, NotBuildinScreenAssetGroupName);
                processedAssets++;
                movedAssets.Add(AssetDatabase.GetAssetPath(asset));
                EditorUtility.DisplayProgressBar("ReGrouping Assets", $"Processing {AssetDatabase.GetAssetPath(asset)}", processedAssets / (float)totalAssets);
            }

            EditorUtility.ClearProgressBar();

            var movedAssetsSummary = string.Join("\n", movedAssets);
            EditorUtility.DisplayDialog("Optimizing BuildIn Scene Assets Summary", $"ReGrouping Complete. Moved Assets:\n{movedAssetsSummary}", "OK");

            this.FindActiveScreensAndAddressableReferences();
        }

        #endregion

        #region Tab: Analysis

        [TabGroup("Tabs", "Analysis")]
        [ShowInInspector]
        [ReadOnly, GUIColor(1, 1, 1)]
        [Title("Group Statistics", TitleAlignment = TitleAlignments.Centered)]
        private string GroupStats => this.GenerateGroupStats();

        [TabGroup("Tabs", "Analysis")]
        [ShowInInspector]
        [ReadOnly, GUIColor(1, 1, 1)]
        [Title("Preload Statistics", TitleAlignment = TitleAlignments.Centered)]
        private string PreloadStats => this.GeneratePreloadStats();

        private string GenerateGroupStats()
        {
            if (this.groups == null || this.groups.Count == 0)
                return "Click 'GetAllGroups' to analyze addressable groups.";

            var totalGroups = this.groups.Count;
            var remoteGroups = this.groups.Count(g => g.IsRemotely);
            var localGroups = totalGroups - remoteGroups;

            return $"Total Groups: {totalGroups}\n" +
                   $"Remote Groups: {remoteGroups}\n" +
                   $"Local Groups: {localGroups}";
        }

        private string GeneratePreloadStats()
        {
            if (this.sceneToDependencyAsset == null || this.sceneToDependencyAsset.Count == 0)
                return "Click 'AnalyzeBuildInScene' to analyze preload screens.";

            var totalScenes = this.sceneToDependencyAsset.Count;
            var totalDependencies = this.sceneToDependencyAsset.Sum(kp => kp.Value.Count);
            var misplacedAssets = this.buildInSceneAssetsThatNotInRightGroup?.Sum(kp => kp.Value.Count) ?? 0;
            var wrongAtlasTextures = this.notInRightAtlasTexture?.Sum(kp => kp.Value.Count) ?? 0;

            return $"Build-In Scenes: {totalScenes}\n" +
                   $"Total Dependencies: {totalDependencies}\n" +
                   $"Misplaced Assets: {misplacedAssets}\n" +
                   $"Wrong Atlas Textures: {wrongAtlasTextures}";
        }

        #endregion

        #region Module Implementation

        protected override void OnAnalyze()
        {
            this.GetAllGroups();
            this.AnalyzeBuildInScene();
        }

        protected override void OnRefresh()
        {
            this.GetAllGroups();
            if (this.sceneToDependencyAsset != null && this.sceneToDependencyAsset.Count > 0)
            {
                this.AnalyzeBuildInScene();
            }
        }

        protected override void OnClear()
        {
            this.groups = new();
            this.sceneToDependencyAsset = new();
            this.notInRightAtlasTexture = new();
            this.buildInSceneAssetsThatNotInRightGroup = new();
            this.notBuildInSceneAssetsThatNotInRightGroup = new();
        }

        public override void OnActivated()
        {
            base.OnActivated();
            // Auto-refresh groups if empty
            if (this.groups == null || this.groups.Count == 0)
            {
                this.GetAllGroups();
            }
        }

        #endregion

        #region Data Models

        /// <summary>
        /// Represents an addressable group with its schema and deployment configuration.
        /// Reused from AddressableGroupOdin.
        /// </summary>
        private class AddressableGroupInfo
        {
            public AddressableAssetGroup Group;
            public BundledAssetGroupSchema Schema;
            [OnValueChanged("OnChangeLoadType")] public bool IsRemotely;

            private void OnChangeLoadType()
            {
                this.Schema.BuildPath.SetVariableByName(AddressableAssetSettingsDefaultObject.Settings, this.IsRemotely ? AddressableAssetSettings.kRemoteBuildPath : AddressableAssetSettings.kLocalBuildPath);
                this.Schema.LoadPath.SetVariableByName(AddressableAssetSettingsDefaultObject.Settings,
                    this.IsRemotely ? AddressableAssetSettings.kRemoteLoadPath : AddressableAssetSettings.kLocalLoadPath);
            }
        }

        #endregion
    }
}
