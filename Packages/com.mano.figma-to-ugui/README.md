# Figma to Unity UGUI

UPM package for importing a Figma frame into a Unity UGUI hierarchy.

## Install

Use one of these options:

- Copy `Packages/com.mano.figma-to-ugui` into another Unity project's `Packages` folder.
- Add this repository through Unity Package Manager using a Git URL.
- Add a local package from Unity Package Manager and pick `Packages/com.mano.figma-to-ugui/package.json`.

For Git installs, use a URL with `?path=/Packages/com.mano.figma-to-ugui`.

## Use

1. Open `Tools/Figma/Import to UGUI`.
2. Paste a Figma personal access token.
3. Paste a Figma file URL, design URL, or file key.
4. Optionally paste a Figma node id or a full node URL.
5. Select a target `RectTransform`, or let the importer create/use a `Canvas`.
6. Click `Import Figma UI`.

Generated sprite assets are written to the Unity project under `Assets/FigmaToUGUI/Generated` by default, because package folders should stay read-only/reusable.

## Current mapping

- Figma frames, groups, components, and instances become `RectTransform` GameObjects.
- Solid fills become UGUI `Image` colors.
- Text nodes become UGUI `Text` components.
- Text can optionally import as TextMeshPro when TextMeshPro is installed.
- Figma Auto Layout frames can become Unity `HorizontalLayoutGroup` or `VerticalLayoutGroup`.
- Figma Constraints can become RectTransform anchors for left/right/center/stretch behavior.
- Import profiles can map Figma font families to Unity fonts or TMP font assets.
- Import profiles can replace matching Figma layers with Unity prefabs.
- Import profiles can apply nine-slice borders to generated sprites by layer-name rule.
- Imported nodes can store metadata for replacing a previous import from the same Figma node.
- Image fills, vectors, lines, stars, polygons, boolean operations, and complex shapes can be rendered as PNG sprites through Figma's image endpoint.

## Design tips

- Treat each Unity screen or panel as a top-level Figma frame.
- Keep layer names readable; names become Unity GameObject names.
- Use solid rectangles for panels/buttons when you want editable UGUI objects.
- Use image/vector layers when a baked sprite is acceptable.
