# Custom Materials

`NowRectangle` can draw with caller-provided materials. Use this for effects
that belong to a single rectangle, such as frost, refraction-style overlays,
scanlines, branded fills, or a project-specific UGUI shader.

```csharp
Now.Rectangle(rect)
    .SetTexture(frostNoise)
    .SetMaterial(frostMaterial)
    .Draw();
```

If the rectangle is rendered inside `NowGraphic`, pass a UGUI-compatible
material as the second argument:

```csharp
Now.Rectangle(rect)
    .SetTexture(frostNoise)
    .SetMaterial(frostMaterial, frostUGUIMaterial)
    .Draw();
```

For UGUI-only effects, keep the normal renderer on the built-in material and
override only the canvas material:

```csharp
Now.Rectangle(rect)
    .SetTexture(frostNoise)
    .SetCanvasMaterial(frostUGUIMaterial)
    .Draw();
```

The docs browser includes a live example in
`Assets/Scenes/DocsScene.unity`: open the **Custom material demo** page. It
uses `Assets/NowUI/Assets/Shaders/DocsFrostRectangleUGUI.shader` through
`SetCanvasMaterial(...)`.

## Material Lifetime

NowUI does not take ownership of materials passed to `SetMaterial` or
`SetCanvasMaterial`. Keep those assets or runtime material instances alive for
as long as the rectangle can draw.

When `SetTexture(...)` is combined with a custom material, NowUI creates and
caches an internal material instance for the material + texture pair, assigns
the texture as `_MainTex`, and keeps the source material untouched.

## Shader Inputs

Normal render paths receive the same streams as `NowUI/UI Rectangle`:

| Stream | Semantic | Contents |
| --- | --- | --- |
| Position | `POSITION` | Quad vertex in NowUI space |
| UV0 | `TEXCOORD0` | Texture UV |
| UV1 | `TEXCOORD1` | Rect as `(x, y, width, height)` |
| UV2 | `TEXCOORD2` | Radius as `(topLeft, bottomLeft, bottomRight, topRight)` |
| UV3 | `TEXCOORD3` | Vertex color |
| UV4 | `TEXCOORD4` | Outline color |
| UV5 | `TEXCOORD5` | Extra data: blur in `x`, outline width in `y` |
| UV6 | `TEXCOORD6` | NowUI mask rect |
| UV7 | `TEXCOORD7` | Raw 0..1 quad UV |

UGUI render paths use the compact canvas layout from `NowUI/UI Rectangle UGUI`:

| Stream | Semantic | Contents |
| --- | --- | --- |
| Position | `POSITION` | Canvas vertex position |
| Color | `COLOR` | Vertex color |
| UV0 | `TEXCOORD0` | Texture UV in `xy`, top-right radius in `z` |
| UV1 | `TEXCOORD1` | Rect as `(x, y, width, height)` |
| UV2 | `TEXCOORD2` | NowUI mask rect |
| UV3 | `TEXCOORD3` | Extra data: blur in `x`, outline width in `y` |
| Normal | `NORMAL` | First three radius components |
| Tangent | `TANGENT` | Outline color |

UGUI shaders should also include the usual Unity UI stencil, color mask, clip
rect, softness, and alpha clip properties if they need to work under `Mask`,
`RectMask2D`, or material modifiers.

## Notes

Custom materials create their own batches. Consecutive rectangles with the same
material and canvas material can batch together; switching either material
starts a new batch.

Sliced sprites still emit nine quads. Radius, outline, and blur are geometry
data, but whether they affect the final pixels depends on the custom shader.
