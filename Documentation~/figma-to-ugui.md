# Figma to Unity UGUI

## Package Layout

This package follows the common Unity Package Manager repository layout:

```text
package.json
Runtime/
Editor/
Documentation~/
CHANGELOG.md
LICENSE.md
README.md
```

## Import Workflow

1. Open `Tools/Figma/Import to UGUI`.
2. Paste a Figma personal access token.
3. Paste a Figma file URL, design URL, or file key.
4. Optionally paste a Figma node id or a full node URL.
5. Select a target `RectTransform`, or let the importer create/use a `Canvas`.
6. Click `Import Figma UI`.

Projects with Odin Inspector installed can also open `Tools/Figma/Import to UGUI (Odin)`.

## Profiles

Create a `FigmaToUGUIProfile` asset from the importer window or from `Assets > Create > Figma To UGUI > Import Profile`.

Profiles can configure:

- Legacy Unity font mappings.
- TextMeshPro font asset mappings.
- Layer-name to prefab mappings.
- Layer-name to nine-slice sprite border rules.
- Default toggles for Auto Layout, constraints, and re-import metadata.

## Speed Options

The importer has two speed-focused options in the import window:

- `Reuse generated sprite cache` skips Figma render/download work when a generated PNG already exists for the same Figma node and render scale.
- `Collapse vector-only groups into one generated sprite` reduces request count by rendering complex visual-only groups as one sprite instead of rendering every vector child separately.

## Responsive Import

Enable these options in the import window for responsive UI:

- `Convert Figma Constraints to RectTransform anchors` maps Figma left, right, center, stretch, and scale constraints to Unity anchors.
- `Stretch imported root to parent/canvas` makes the imported root frame fill its selected parent or Canvas.

For best results, set Figma constraints on important child layers before importing.
