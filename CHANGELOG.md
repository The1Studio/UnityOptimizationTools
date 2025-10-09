# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2025-01-10

### Fixed
- Updated dependencies to match assembly definition references
  - Added `com.theone.extensions`
  - Added `com.theone.tool.core`
  - Added `com.unity.nuget.newtonsoft-json`
  - Removed `com.theone.template.editor` (replaced by above packages)

## [1.0.0] - 2025-01-10

### Added
- Initial release of Unity Optimization Tools
- Optimization Hub window with modular architecture
- Overview Module with project-wide health checks
- Texture Module with TinyPNG compression and atlas generation
- Shader Module with variant analysis and material tracking
- Mesh Module for 3D mesh optimization
- Audio Module for audio clip compression
- Font Module for font asset optimization
- Material Module for material instance consolidation
- Addressables Module for asset bundle analysis
- Animation Module for animation clip optimization
- Reports Module for build size reports
- Settings Module for tool configuration
- TinyPNG API integration for texture compression
- Sprite atlas generation from folder structures
- Shader replacement tools
- Material usage tracking
- Asset cache system for performance
- Progress tracking for long operations
- Odin Inspector integration for rich UI

### Features
- Table-based views with pagination
- Search and filter capabilities
- Visual previews for assets
- Color-coded health indicators
- Expandable detail views
- Inline editing and actions
- Export to CSV/JSON
- Build report integration
- Custom module support

[1.0.1]: https://github.com/The1Studio/UnityOptimizationTools/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/The1Studio/UnityOptimizationTools/releases/tag/v1.0.0
