#ifndef NOWUI_TEXT_GRADIENT_INCLUDED
#define NOWUI_TEXT_GRADIENT_INCLUDED

#include "NowUIColorSpace.cginc"

sampler2D _NowGradientRampTexture;

inline float NowUITextGradientApplySpread(float t, float spread)
{
    if (spread < 0.5)
        return saturate(t);

    if (spread < 1.5)
        return frac(t);

    return 1.0 - abs(frac(t * 0.5) * 2.0 - 1.0);
}

inline float NowUITextGradientFlags(float encodedRamp)
{
    return floor(frac(encodedRamp) * 256.0);
}

inline float NowUITextGradientPosition(
    float2 uiPosition,
    float4 payload,
    float flags)
{
    float kind = fmod(flags, 4.0);

    // Linear repetitions are baked into the affine coefficients by the CPU.
    if (kind < 0.5)
        return dot(uiPosition, payload.xy) + payload.z;

    if (kind < 1.5)
    {
        float circle = fmod(floor(flags / 16.0), 2.0);
        float2 radii = circle > 0.5 ? payload.zz : payload.zw;
        return length((uiPosition - payload.xy) / max(abs(radii), 0.0001));
    }

    float2 delta = uiPosition - payload.xy;
    // CSS convention: zero points up and positive turns rotate clockwise in
    // NowUI's positive-y-down coordinate system.
    float turns = atan2(delta.x, -delta.y) / 6.28318530718;
    return frac(turns - payload.z) * payload.w;
}

inline float4 NowUITextGradientSample(
    float2 uiPosition,
    float4 payload,
    float encodedRamp)
{
    float row = floor(encodedRamp);
    float flags = NowUITextGradientFlags(encodedRamp);
    float spread = fmod(floor(flags / 4.0), 4.0);
    float fixedMode = fmod(floor(flags / 32.0), 2.0);
    float t = NowUITextGradientApplySpread(
        NowUITextGradientPosition(uiPosition, payload, flags),
        spread);

    float rampIndex = fixedMode > 0.5
        ? floor(t * 255.0 + 0.5)
        : t * 255.0;
    float2 rampUV = float2(
        (rampIndex + 0.5) / 256.0,
        (row + 0.5) / 256.0);
    float4 ramp = tex2D(_NowGradientRampTexture, rampUV);
    ramp.rgb = NowUIColorToWorkingSpace(ramp.rgb);
    return ramp;
}

#endif
