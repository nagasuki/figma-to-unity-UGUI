# Changelog

## 0.5.0 - 2026-06-08

- Added Sync wording in the editor window and docs.
- Added batch sync for multiple explicit Figma node IDs in one run.
- Added optional full-file sync for every top-level frame, with per-frame progress messages.
- Added initial edit-preserving re-sync behavior for user-added children under generated nodes.
- Added RectMask2D import for Figma nodes with clipped content.
- Added rule-based inference for UGUI Button, Toggle, InputField, and ScrollRect components.
- Added optional prefab creation for synced root frames.
- Added an implementation roadmap for the larger product feature set.

## 0.4.0

- Added responsive root stretching so imported screens can fill the selected parent or Canvas.
- Added Figma `SCALE` constraint conversion to proportional RectTransform anchors.
- Added importer option for stretching the imported root to its parent.

## 0.3.2

- Added generated sprite cache reuse to skip Figma render/download work on repeated imports.
- Added vector-only subtree collapsing to reduce the number of generated sprite requests for complex icon/group layers.
- Added importer window toggles for sprite cache reuse and vector group collapsing.

## 0.3.1

- Improved import speed by downloading generated sprites with limited concurrency.
- Improved import speed by batching generated PNG asset import and importer setting updates.
- Removed an unnecessary early `AssetDatabase.Refresh` during import setup.

## 0.3.0

- Added Unity Editor progress UI with `EditorUtility.DisplayCancelableProgressBar`.
- Added import cancellation support for Figma requests, sprite downloads, and hierarchy generation.
- Disabled importer controls while an import is running.

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
