# OverviewModule Architecture

## Overview Dashboard Layout

```
┌─────────────────────────────────────────────────────────────────────┐
│                      📊 Overview Dashboard                           │
├─────────────────────────────────────────────────────────────────────┤
│                        Project Summary                               │
│ ┌─────────────────┬─────────────────┬─────────────────┐            │
│ │   🖼 Textures   │   🔊 Audio      │   🎨 Meshes     │            │
│ │   2,134 assets  │   456 assets    │   89 assets     │            │
│ │ ⚠️ 23 uncomp.   │ ⚠️ 12 wrong comp│   5,234 verts   │            │
│ │ [View Details]  │ [View Details]  │ [View Details]  │            │
│ └─────────────────┴─────────────────┴─────────────────┘            │
│                                                                      │
│ ┌─────────────────┬─────────────────┬─────────────────┐            │
│ │  🌈 Shaders     │   🔤 Fonts      │  📦 Addressables│            │
│ │   45 shaders    │   12 fonts      │   3,456 assets  │            │
│ │   128 variants  │   8 w/ atlases  │   24 groups     │            │
│ │ [View Details]  │ [View Details]  │ [View Details]  │            │
│ └─────────────────┴─────────────────┴─────────────────┘            │
├─────────────────────────────────────────────────────────────────────┤
│                       Recent Activity                                │
│ ┌─────────────────────────────────────────────────────────────┐    │
│ │ 🕒 Last Refresh: 14:23:45                                   │    │
│ │                                                              │    │
│ │ ⚠️ Found 47 optimization opportunities:                     │    │
│ │                                                              │    │
│ │   • 23 uncompressed textures                                │    │
│ │   • 12 wrong compression audio                              │    │
│ │   • 12 non-POT textures                                     │    │
│ └─────────────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────────────┤
│                       Quick Actions                                  │
│  [🔍 Analyze All]    [📄 Export Report]    [⚙️ Settings]          │
└─────────────────────────────────────────────────────────────────────┘
```

## Data Flow

```
┌──────────────────┐
│  User Opens Hub  │
│                  │
└────────┬─────────┘
         │
         ▼
┌──────────────────────┐
│ OverviewModule       │
│ .OnActivated()       │◄──── Auto-refresh if cache > 5 min
└────────┬─────────────┘
         │
         ▼
┌──────────────────────┐
│ RefreshStats()       │
│  (< 100ms target)    │
└────────┬─────────────┘
         │
         ├─────────────────────────────────────────────────┐
         │                                                  │
         ▼                                                  ▼
┌────────────────────┐                           ┌──────────────────┐
│ AssetDatabase      │                           │  Addressables    │
│ Lightweight Queries│                           │  Settings API    │
├────────────────────┤                           ├──────────────────┤
│ • FindAssets()     │                           │ • Group count    │
│ • TextureImporter  │                           │ • Entry count    │
│ • AudioImporter    │                           └──────────────────┘
│ • Load<Mesh>()     │
│ • Basic checks     │
└────────┬───────────┘
         │
         ▼
┌──────────────────────┐
│  ProjectStats        │
│  (Cached in memory)  │
├──────────────────────┤
│ • TextureCount       │
│ • AudioCount         │
│ • MeshCount          │
│ • TotalVertices      │
│ • UncompressedCount  │
│ • WrongCompressionC  │
│ • TotalIssues        │
└────────┬─────────────┘
         │
         ▼
┌──────────────────────┐
│  Dashboard UI        │
│  (Odin Inspector)    │
└──────────────────────┘
```

## Navigation Flow

```
┌─────────────────┐
│ Overview        │
│ Dashboard       │
└────────┬────────┘
         │
         │ User clicks "View Details"
         │
         ├───────────┬───────────┬───────────┬───────────┐
         │           │           │           │           │
         ▼           ▼           ▼           ▼           ▼
    ┌────────┐  ┌───────┐  ┌──────┐  ┌────────┐  ┌────────┐
    │Textures│  │ Audio │  │ Mesh │  │Shaders │  │ Fonts  │
    │ Module │  │Module │  │Module│  │ Module │  │ Module │
    └────────┘  └───────┘  └──────┘  └────────┘  └────────┘
         │           │           │           │           │
         │           │           │           │           │
         └───────────┴───────────┴───────────┴───────────┘
                            │
                  Full detailed analysis
                  (TextureFinderOdin, etc.)
```

## Performance Strategy

### ✅ What OverviewModule DOES
- Simple `AssetDatabase.FindAssets()` queries
- Basic importer property checks (compression, format)
- Count aggregation (textures, audio, meshes, etc.)
- Lightweight heuristics (POT check, vertex sum)
- **Target: < 100ms total execution time**

### ❌ What OverviewModule DOES NOT DO
- Full texture analysis (leave to TextureFinderOdin)
- Audio waveform analysis (leave to AddressableAudioFinderOdin)
- Mesh optimization calculations (leave to AddressableMeshFinderOdin)
- Shader variant collection (too expensive)
- File size calculations (AssetDatabase doesn't provide, needs FileInfo)

## Caching Strategy

```
First Load:
  └─> RefreshStats() ──> Cache ProjectStats ──> lastRefreshTime = Now

User switches to another module:
  └─> OnDeactivated() ──> Keep cache in memory

User returns to Overview:
  └─> OnActivated() ──> Check cache age
        │
        ├─> If < 5 minutes: Use cached stats (instant load)
        └─> If > 5 minutes: RefreshStats() (background update)

User clicks "Analyze All":
  └─> Force RefreshStats() ──> Update cache ──> Show progress bar
```

## Integration Points

### 1. Module Switching (TODO)
Currently logs to console. Needs integration with `OptimizationHubWindow`:

```csharp
// Current (placeholder):
private void SwitchToModule(string moduleName)
{
    Debug.Log($"Switching to {moduleName}");
}

// Future (requires parent window reference):
private void SwitchToModule(string moduleName)
{
    parentWindow.moduleSidebar.SelectModule(moduleName);
}
```

### 2. Issue Tracking
Track optimization opportunities over time:

```csharp
// Future enhancement: Historical tracking
private class IssueHistory
{
    public DateTime Timestamp;
    public int UncompressedTextures;
    public int WrongCompressionAudio;
    // ... other metrics
}
```

### 3. Quick Fixes
One-click fixes for common issues:

```csharp
// Future enhancement: Quick fix actions
[Button("Fix All Uncompressed")]
private void FixUncompressedTextures()
{
    // Apply TextureImporterCompression.Compressed to all
}
```

## File Structure

```
Assets/UITemplate/Editor/Optimization/
├── Core/
│   ├── IOptimizationModule.cs          (Interface)
│   ├── OptimizationModuleBase.cs       (Base class)
│   ├── ModuleSidebar.cs                (Left navigation)
│   └── ModuleContentArea.cs            (Right content)
│
├── Modules/
│   ├── OverviewModule.cs              ✅ NEW: Dashboard implementation
│   ├── README.md                       ✅ NEW: Module documentation
│   └── ARCHITECTURE.md                 ✅ NEW: This file
│
├── Texture/
│   └── TextureFinderOdin.cs           (Heavy analysis - NOT used by Overview)
├── Audio/
│   └── AddressableAudioFinderOdin.cs  (Heavy analysis - NOT used by Overview)
└── ...
```

## Design Principles Summary

1. **Separation of Concerns**: Overview aggregates, specialized modules analyze
2. **Performance First**: < 100ms load time via lightweight queries only
3. **Navigation Hub**: Quick access to detailed modules via "View Details" buttons
4. **Smart Caching**: Auto-refresh only when stale (> 5 minutes)
5. **Issue Prioritization**: Show top 3 issues in warning box
6. **Future-Proof**: Designed for easy extension (trends, quick fixes, etc.)
