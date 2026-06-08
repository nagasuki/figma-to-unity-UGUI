# Feature Roadmap

This package aims to become a one-click Figma to Unity UGUI sync pipeline using only the official Figma API. The current implementation already covers the baseline importer, and the remaining work is grouped below so features can land without claiming unsupported behavior early.

## Available Now

- One-window Figma URL/key import with a personal access token.
- One selected frame/node import into a UGUI hierarchy.
- Multiple explicit Figma node IDs in one sync run.
- Full-file batch sync for every top-level frame.
- One scene root per synced frame.
- Canvas creation or selected `RectTransform` parenting.
- RectTransform anchors for Figma Min, Max, Center, Stretch, and Scale constraints.
- Auto Layout to `HorizontalLayoutGroup` and `VerticalLayoutGroup`.
- Solid fills to UGUI `Image`.
- Figma clipped frames to UGUI `RectMask2D`.
- Text to legacy UGUI `Text`, with optional TextMeshPro via reflection.
- Rule-based `Button`, `Toggle`, `InputField`, and `ScrollRect` component inference from layer names.
- Optional prefab creation for synced root frames.
- Generated PNG sprites for image fills, vectors, boolean operations, and complex shapes.
- Sprite cache reuse and vector-only subtree collapsing.
- Import profiles for font mappings, prefab mappings, and nine-slice sprite borders.
- Import metadata and replace-existing re-import from the same Figma node.
- Re-sync preservation for user-added children under generated nodes.
- Optional Odin editor window when Odin Inspector is present.

## Phase 1: Sync UX And Batch Frames

- Rename the primary action to Sync across UI and docs.
- Preserve current Unity and Odin editor-window parity.

## Phase 2: Edit-Preserving Re-Sync

- Track stable Figma node IDs on every generated object.
- Diff incoming Figma data against the existing generated hierarchy.
- Update generated properties while preserving user-added components.
- Preserve user-added children that cannot be matched back to generated nodes.
- Add conflict reporting for renamed, deleted, or type-changed nodes.
- Add reusable sprite and prefab asset lookup before creating new assets.

## Phase 3: Rich Visual Fidelity

- Convert vector icons to real UGUI meshes where Figma geometry is available.
- Add SDF rounded rectangles for scalable corners.
- Add SDF gradients for linear and radial fills where practical.
- Add richer mask handling for non-rectangular clips.
- Add drop shadows, inner shadows, and blur effects.
- Keep PNG fallback for unsupported or expensive visuals.

## Phase 4: Text And Fonts

- Make TextMeshPro the preferred text path when installed.
- Use Unity's default TMP font when no profile font is configured.
- Map inline bold, italic, underline, and strikethrough to TMP rich text.
- Add automatic font-family matching from installed Unity/TMP assets.
- Add optional Google Fonts download and TMP atlas baking.
- Keep the package free of analytics, telemetry, and third-party runtime SDKs.

## Phase 5: Components And Workflow

- Add configurable component inference rules beyond the built-in name tokens.
- Generate prefabs from synced component subtrees.
- Add code generation hooks for strongly named UI references.
- Add Unity Localization support.
- Add optional I2 Localization support when I2 is present.
- Add an interactive demo scene for Built-in Render Pipeline and URP.

## Phase 6: Live Pipeline And Offline Import

- Add a bundled Figma plugin for one-click push metadata/export.
- Support FigJam inputs where the data can be mapped to UGUI.
- Add offline ZIP import for exported Figma payloads and image assets.
- Keep online sync limited to the official Figma API.
- Validate Unity 2021.3 through Unity 6 compatibility.
