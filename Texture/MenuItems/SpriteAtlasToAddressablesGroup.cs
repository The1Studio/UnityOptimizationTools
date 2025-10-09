#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Đầu tiên Analyze Duplicate Assets
/// Sau đó, chọn các SpriteAtlas chuột phải vào menu "TheOne/Add Atlas Packables To Group"
/// </summary>
public static class SpriteAtlasToAddressablesGroup
{
    // Chỉ xử lý những texture đã là Addressable (mọi group) -> không tạo mới, không đổi tên
    private const bool ONLY_FROM_EXISTING_ADDRESSABLES = true;

    [MenuItem("Assets/TheOne/Add Atlas Packables To Group", true)]
    private static bool Validate() =>
        Selection.objects != null && Array.Exists(Selection.objects, o => o is SpriteAtlas);

    [MenuItem("Assets/TheOne/Add Atlas Packables To Group")]
    private static void AddSelectedAtlases()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (!settings)
        {
            Debug.LogError("Addressables Settings not found.");
            return;
        }

        // Lấy toàn bộ GUID đang là Addressable trong mọi group (để lọc nếu cần)
        HashSet<string> allAddressableGuids = null;
        if (ONLY_FROM_EXISTING_ADDRESSABLES)
        {
            allAddressableGuids = new HashSet<string>();
            foreach (var g in settings.groups)
            {
                if (!g) continue;
                foreach (var e in g.entries)
                    if (e != null && !string.IsNullOrEmpty(e.guid))
                        allAddressableGuids.Add(e.guid);
            }
        }

        var atlases = new List<SpriteAtlas>();
        foreach (var o in Selection.objects)
            if (o is SpriteAtlas sa)
                atlases.Add(sa);
        if (atlases.Count == 0)
        {
            Debug.LogWarning("No SpriteAtlas selected.");
            return;
        }

        try
        {
            AssetDatabase.StartAssetEditing();
            for (var i = 0; i < atlases.Count; i++)
            {
                var atlas = atlases[i];
                EditorUtility.DisplayProgressBar("Addressables",
                    $"Processing {atlas.name} ({i + 1}/{atlases.Count})", (float)i / atlases.Count);
                ProcessOneAtlas(settings, atlas, allAddressableGuids);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"Processed {atlases.Count} Sprite Atlases.");
    }

    private static void ProcessOneAtlas(AddressableAssetSettings settings, SpriteAtlas atlas, HashSet<string> allAddressableGuids)
    {
        // Thu thập Texture2D thật sự có Sprite (tránh ảnh rác)
        var texPaths = new HashSet<string>();
        foreach (var p in atlas.GetPackables())
        {
            if (!p) continue;
            var path = AssetDatabase.GetAssetPath(p);

            if (p is DefaultAsset && AssetDatabase.IsValidFolder(path))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { path }))
                {
                    var pth  = AssetDatabase.GUIDToAssetPath(guid);
                    var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(pth);
                    if (subs != null && Array.Exists(subs, a => a is Sprite)) texPaths.Add(pth);
                }
            }
            else if (p is Texture2D)
            {
                var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                if (subs != null && Array.Exists(subs, a => a is Sprite)) texPaths.Add(path);
            }
            else if (p is Sprite sp && sp.texture)
            {
                var tPath = AssetDatabase.GetAssetPath(sp.texture);
                if (!string.IsNullOrEmpty(tPath)) texPaths.Add(tPath);
            }
        }

        if (texPaths.Count == 0)
        {
            Debug.LogWarning($"[{atlas.name}] No Texture2D with sprites found.");
            return;
        }

        // (Tuỳ chọn) chỉ lấy những texture đã là Addressable (mọi group)
        if (ONLY_FROM_EXISTING_ADDRESSABLES && allAddressableGuids != null)
        {
            texPaths.RemoveWhere(p =>
            {
                var g = AssetDatabase.AssetPathToGUID(p);
                return string.IsNullOrEmpty(g) || !allAddressableGuids.Contains(g);
            });

            if (texPaths.Count == 0)
            {
                Debug.Log($"[{atlas.name}] No textures qualify (not present in any Addressables groups).");
                return;
            }
        }

        // Group đích cho atlas
        var groupName = $"UI_Atlas_{atlas.name}";
        var group     = settings.FindGroup(groupName);
        if (!group)
        {
            group = settings.CreateGroup(
                groupName,
                false, false, true,
                new List<AddressableAssetGroupSchema>(),
                new Type[] { typeof(BundledAssetGroupSchema) }
            );

            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BundleMode  = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        }

        var movedOrCreated = 0;
        foreach (var path in texPaths)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) continue;

            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                // Giữ nguyên address hiện có
                settings.MoveEntry(entry, group);
                movedOrCreated++;
            }
            else if (!ONLY_FROM_EXISTING_ADDRESSABLES)
            {
                // Tạo entry mới nhưng KHÔNG set address => giữ mặc định (thường là AssetPath)
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry != null) movedOrCreated++;
            }
        }

        Debug.Log($"[{atlas.name}] Moved/Created {movedOrCreated} entries into group '{groupName}' (addresses preserved).");
    }
}
#endif