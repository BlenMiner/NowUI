#ifndef NOWUI_COLOR_SPACE_INCLUDED
#define NOWUI_COLOR_SPACE_INCLUDED

// NowUI's public Color values and theme palettes are authored as display/sRGB
// UI colors, matching Unity's color picker and CSS-style palette values. Shader
// math must happen in the project's working color space so a linear project
// does not brighten those values when the render target is presented.
inline float3 NowUIColorToWorkingSpace(float3 color)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return color;
#else
    // UnityCG's general cubic approximation loses several display-code values
    // in the dark UI range. This is UnityUI.cginc's piecewise approximation,
    // whose round trip stays within half of one 8-bit sRGB step.
    half3 value = (half3)color;
    half3 low = 0.0849710h * value - 0.000163029h;
    half3 high =
        value * (value * (value * 0.265885h + 0.736584h) - 0.00980184h) +
        0.00319697h;
    const half3 split = (half3)0.0725490h;
    return (value < split) ? low : high;
#endif
}

inline float4 NowUIColorToWorkingSpace(float4 color)
{
    return float4(NowUIColorToWorkingSpace(color.rgb), color.a);
}

#endif
