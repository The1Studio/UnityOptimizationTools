using System.Collections.Generic;
using System.Linq;
using TheOne.Tool.Optimization.Models;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheOne.UITemplate.Editor.Optimization.Services
{
    /// <summary>
    /// Service for analyzing shaders and materials across the project.
    /// Extracted from ShaderListOdinWindow to avoid code duplication and reflection usage.
    /// </summary>
    public class ShaderAnalysisService
    {
        /// <summary>
        /// Analyzes all shaders and materials in build settings scenes and addressables.
        /// Returns a list of ShaderMaterialInfo with all materials grouped by shader.
        /// </summary>
        public List<ShaderMaterialInfo> FindAllShadersAndMaterials()
        {
            var shaderDict = new Dictionary<string, ShaderMaterialInfo>();

            // Store the original scene path so we can return to it later.
            var originalScenePath = SceneManager.GetActiveScene().path;

            var scenes = EditorBuildSettings.scenes;
            var totalSteps = scenes.Length + 1; // +1 for addressables processing.
            var currentStep = 0;

            // Get all scenes from the build settings:
            foreach (var sceneInBuild in scenes)
            {
                currentStep++;
                EditorUtility.DisplayProgressBar("Analyzing Shaders and Materials",
                    $"Processing Scene {currentStep} out of {totalSteps}",
                    currentStep / (float)totalSteps);

                if (!sceneInBuild.enabled) continue;

                if (string.IsNullOrEmpty(sceneInBuild.path))
                {
                    Debug.LogWarning("[ShaderAnalysisService] Invalid scene path in build settings.");
                    continue;
                }

                EditorSceneManager.OpenScene(sceneInBuild.path, OpenSceneMode.Single);

                var renderers = Object.FindObjectsOfType<Renderer>();
                foreach (var renderer in renderers)
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (!mat || !mat.shader) continue;

                    if (!shaderDict.ContainsKey(mat.shader.name))
                    {
                        shaderDict[mat.shader.name] = new ShaderMaterialInfo { OriginalShader = mat.shader };
                    }

                    shaderDict[mat.shader.name].AddUniqueMaterial(mat, renderer.gameObject);
                }
            }

            // Handle addressable:
            EditorUtility.DisplayProgressBar("Analyzing Shaders and Materials", "Processing Addressables", currentStep / (float)totalSteps);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings)
            {
                foreach (var group in settings.groups)
                foreach (var entry in group.entries)
                {
                    var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    var mainObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    var dependencies = this.GetAllDependencies(path);
                    foreach (var mat in dependencies.Select(depPath => AssetDatabase.LoadAssetAtPath<Material>(depPath)).Where(mat => mat))
                    {
                        if (!mat.shader) continue;

                        if (!shaderDict.ContainsKey(mat.shader.name))
                        {
                            shaderDict[mat.shader.name] = new ShaderMaterialInfo { OriginalShader = mat.shader };
                        }

                        var info = shaderDict[mat.shader.name];
                        info.AddUniqueMaterial(mat, mainObject); // Assume the mainObject is using the material.
                    }
                }
            }

            // Return to the original scene:
            if (!string.IsNullOrEmpty(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath);
            }
            else
            {
                Debug.LogWarning("[ShaderAnalysisService] Original scene path is null or empty. Cannot revert to the original scene.");
            }

            // Clear progress bar after completion:
            EditorUtility.ClearProgressBar();

            return new List<ShaderMaterialInfo>(shaderDict.Values);
        }

        private List<string> GetAllDependencies(string assetPath)
        {
            return new List<string>(AssetDatabase.GetDependencies(assetPath, true));
        }
    }
}
