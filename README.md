# Figma to Unity UGUI

Unity Package Manager package for importing a Figma frame into a Unity UGUI hierarchy.

This repository is structured as a root UPM package, so Unity can install it directly from a Git URL without `?path=...`.

## Install

In Unity:

1. Open `Window > Package Manager`.
2. Click `+`.
3. Choose `Add package from git URL...`.
4. Paste this repository URL.

You can also add it to a Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.mano.figma-to-ugui": "https://github.com/YOUR_ORG/figma-to-unity-UGUI.git"
  }
}
```

For local development, choose `Add package from disk...` and select this repository's root `package.json`.

## Use

1. Open `Tools/Figma/Import to UGUI`.
2. Paste a Figma personal access token.
3. Paste a Figma file URL, design URL, or file key.
4. Optionally paste a Figma node id or full node URL.
5. Select a target `RectTransform`, or let the tool create/use a `Canvas`.
6. Click `Import Figma UI`.

If Odin Inspector is installed, `Tools/Figma/Import to UGUI (Odin)` is also available. Both windows run the same importer.

Generated PNG sprites are written to `Assets/FigmaToUGUI/Generated` in the consuming Unity project. The package code stays reusable and clean.

## Current scope

- Figma frames, groups, components, and instances become `RectTransform` GameObjects.
- Solid fills become UGUI `Image` colors.
- Text nodes become UGUI `Text` components.
- Text can optionally import as TextMeshPro when TextMeshPro is installed.
- Figma Auto Layout frames can become Unity `HorizontalLayoutGroup` or `VerticalLayoutGroup`.
- Figma Constraints can become RectTransform anchors for left/right/center/stretch behavior.
- Figma `SCALE` constraints can become proportional anchors for responsive resizing.
- Imported root frames can stretch to their selected parent or Canvas.
- Import profiles can map Figma font families to Unity fonts or TMP font assets.
- Import profiles can replace matching Figma layers with Unity prefabs.
- Import profiles can apply nine-slice borders to generated sprites by layer-name rule.
- Imported nodes can store metadata for replacing a previous import from the same Figma node.
- Image fills, vectors, lines, stars, polygons, boolean operations, and complex shapes can be rendered as PNG sprites through Figma's image endpoint.
- Generated sprites can be reused from cache on repeated imports.
- Vector-only groups can be collapsed into one generated sprite to reduce import time.

Future upgrades can add design tokens/variables, richer component overrides, and prototype interaction import.
