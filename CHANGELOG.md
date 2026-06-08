# Changelog

## 0.2.1

- Reduced Figma image export batch size.
- Added automatic retry by splitting image export batches after Figma render timeouts.
- Added per-node lower-scale retry and skip behavior so one large rendered sprite does not fail the whole import.

## 0.2.0

- Added root UPM package structure for direct Git URL installs.
- Added Unity `.meta` files for immutable Git package imports.
- Added import profiles for font, prefab, and nine-slice mappings.
- Added optional TextMeshPro import through reflection.
- Added Figma Auto Layout to Unity layout group conversion.
- Added Figma Constraints to RectTransform anchor conversion.
- Added import metadata and replace-existing re-import behavior.

## 0.1.0

- Added first Figma REST API importer.
- Added UGUI hierarchy generation with `RectTransform`, `Image`, and `Text`.
- Added PNG sprite generation for image fills and complex visual nodes.
