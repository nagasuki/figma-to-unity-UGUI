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

## Profiles

Create a `FigmaToUGUIProfile` asset from the importer window or from `Assets > Create > Figma To UGUI > Import Profile`.

Profiles can configure:

- Legacy Unity font mappings.
- TextMeshPro font asset mappings.
- Layer-name to prefab mappings.
- Layer-name to nine-slice sprite border rules.
- Default toggles for Auto Layout, constraints, and re-import metadata.
