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

The packaged [docs browser source](../Example/NowDocsExample.cs) includes a
custom-material example. It uses
[`DocsFrostRectangleUGUI.shader`](../Assets/Shaders/DocsFrostRectangleUGUI.shader)
through `SetCanvasMaterial(...)`.

SDF scenes use a different, versioned material contract. Do not apply the
rectangle vertex-layout recipe below to `NowSdfBuilder.SetMaterial(...)`; use
the shared final-shading hook and complete examples in
[SDF Shapes > Custom SDF Materials](SDF.md#custom-sdf-materials).

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
| UV2 | `TEXCOORD2` | `NowCornerRadius.packed` as `(topRight, bottomRight, topLeft, bottomLeft)` |
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

## XR Single-Pass Instanced

Caller-provided rectangle shaders own their vertex entry point, so they do not
automatically inherit NowUI's stereo setup. To route geometry to both eye
slices under XR single-pass instanced rendering, mirror the packaged rectangle
shaders:

```hlsl
#pragma multi_compile_instancing

struct appdata
{
    float4 vertex : POSITION;
    // Custom vertex streams...
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 vertex : SV_POSITION;
    // Custom interpolators...
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert(appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    // Populate the remaining output fields...
    return o;
}
```

Call `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)` at the start of the fragment
function when it reads `unity_StereoEyeIndex` or samples a stereo screen-space
texture. The packaged shaders keep this setup in place for per-eye fragment
work.

Keep `#pragma multi_compile_instancing` alongside this vertex plumbing. Custom
vertex/fragment shaders need it to generate the instanced variant used by XR
single-pass-instanced rendering; the macros alone do not create that variant.

## Shader Mask Opt-In

The mask rect in these vertex layouts is the legacy rectangular clip. Packaged
NowUI shaders additionally evaluate ambient analytic and texture-backed masks
created with `Now.Mask(NowMaskShape)`, `Now.Mask(NowMaskTexture)`, or an SDF
scene's `BeginMask()`, but caller-provided custom materials must opt in. Reading
UV6 or UGUI UV2 alone does not apply rounded, circular, elliptical, capsule,
feathered, or texture-coverage boundaries.

Follow the corresponding packaged shader, or the complete
[`DocsFrostRectangleUGUI.shader`](../Assets/Shaders/DocsFrostRectangleUGUI.shader)
example. The essential pieces are:

```hlsl
Properties
{
    [HideInInspector] _NowUIMaskCount ("Now UI Mask Count", Float) = 0
    [HideInInspector] _NowUITextureMaskCount ("Now UI Texture Mask Count", Float) = 0
    [HideInInspector] _NowUITextureMask0 ("Now UI Texture Mask 0", 2D) = "black" {}
    [HideInInspector] _NowUITextureMask1 ("Now UI Texture Mask 1", 2D) = "black" {}
}

// In the program block:
#include "Packages/com.blenminer.nowui/Assets/Shaders/NowUIMask.cginc"

// In the fragment function, after reconstructing the top-left-origin NowUI
// position used by the packaged shader:
col.a *= NowUIMaskCoverage(uiPosition); // straight-alpha output
```

Multiply the whole output color for a premultiplied-alpha shader. The hidden
count properties are how NowUI detects support before binding active mask data;
texture support also requires both hidden sampler properties. The include
declares the remaining arrays and coverage helpers. A shader may intentionally
support analytic masks only by declaring `_NowUIMaskCount` and calling
`NowUIAnalyticMaskCoverage`, but texture and SDF masks then do not affect it.
See [Masks](Masks.md) for coverage and nesting behavior.

## Notes

Custom materials create their own batches. Consecutive rectangles with the same
material and canvas material can batch together; switching either material
starts a new batch.

Sliced sprites still emit nine quads. Radius, outline, and blur are geometry
data, but whether they affect the final pixels depends on the custom shader.
