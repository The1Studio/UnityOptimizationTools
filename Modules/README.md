# Optimization Modules

This directory contains individual optimization modules for the Optimization Hub.

## OverviewModule

**Purpose**: Provides a high-level dashboard showing summary statistics from all optimization areas.

### Design Principles

1. **Aggregate, Don't Duplicate**: The Overview module shows counts and simple statistics without duplicating the heavy analysis logic from specialized tools.

2. **Lightweight Queries**: Uses `AssetDatabase.FindAssets()` and basic importer checks to gather counts quickly (target: < 100ms).

3. **Navigation Hub**: Provides "View Details" buttons to jump to specific optimization modules for detailed analysis.

### Dashboard Layout

The dashboard is organized into three sections:

#### 1. Project Summary (6 Cards)
- **Textures**: Count, uncompressed count, navigation button
- **Audio**: Count, wrong compression count, navigation button
- **Meshes**: Count, total vertices, navigation button
- **Shaders**: Count, variant count, navigation button
- **Fonts**: Count, atlas font count, navigation button
- **Addressables**: Asset count, group count, navigation button

#### 2. Recent Activity
- Last refresh timestamp
- Top 3 issues warning box with counts

#### 3. Quick Actions
- **Analyze All**: Refreshes all statistics
- **Export Report**: Navigate to Reports module
- **Settings**: Navigate to Settings module

### Data Collection Strategy

The module uses lightweight AssetDatabase queries:

```csharp
// ✅ CORRECT - Simple count queries
var textureCount = AssetDatabase.FindAssets("t:Texture2D").Length;

// ✅ CORRECT - Basic importer checks
var uncompressed = textures
    .Select(path => AssetImporter.GetAtPath(path) as TextureImporter)
    .Count(imp => imp != null && imp.textureCompression == TextureImporterCompression.Uncompressed);

// ❌ WRONG - Don't duplicate heavy analysis
// Don't recreate TextureFinderOdin's full analysis logic here
```

### Performance Targets

- **Initial Load**: < 100ms
- **Refresh**: < 200ms (acceptable since it's user-triggered)
- **Memory**: < 5MB for cached statistics

### Caching Strategy

Statistics are cached and auto-refresh under these conditions:
- On first activation (if no cache exists)
- If cache is older than 5 minutes
- When user clicks "Analyze All" or "Refresh" button

### Future Enhancements

1. **Module Switching**: Currently logs to console; needs integration with `OptimizationHubWindow` to actually switch modules
2. **Trend Tracking**: Store historical stats to show improvement over time
3. **Issue Prioritization**: Score issues by impact (file size × occurrence count)
4. **Quick Fixes**: One-click fixes for common issues directly from dashboard

## Adding New Modules

When adding new optimization modules:

1. Create module class in this directory
2. Inherit from `OptimizationModuleBase`
3. Update `ModuleContentArea.CreateModule()` factory method
4. Update `ModuleSidebar.modules` list
5. Add summary card to OverviewModule if relevant

Example:
```csharp
public class MyNewModule : OptimizationModuleBase
{
    public override string ModuleName => "My Feature";
    public override string ModuleIcon => "🎯";

    protected override void OnAnalyze() { /* Heavy analysis */ }
    protected override void OnRefresh() { /* Reload data */ }
    protected override void OnClear() { /* Reset state */ }
}
```
