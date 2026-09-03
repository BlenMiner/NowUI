// Bakes a signed distance field from an image's alpha channel with jump
// flooding over marching-squares contour segments. Three passes run through
// Graphics.Blit:
//   0 Seed:    extract the alpha-threshold contour segment of every texel cell.
//   1 Flood:   propagate the nearest segment at a shrinking jump step.
//   2 Resolve: measure each texel against its nearest segment and sign it.
// Measuring against sub-texel contour segments instead of texel centers keeps
// the field's gradient smooth near the edge, which emboss and antialiasing
// sample directly. The field is padded around the source rect so exterior
// effects have room.
Shader "Hidden/NowUI/SDF Image Field"
{
    Properties
    {
        _MainTex ("Flood Input", 2D) = "black" {}
        _SourceTex ("Source Image", 2D) = "white" {}
        _SourceUv ("Source UV Rect", Vector) = (0, 0, 1, 1)
        _FieldParams ("Sprite Width, Sprite Height, Padding, Threshold", Vector) = (1, 1, 0, 0.5)
        _FieldTexels ("Field Width, Field Height, 1/W, 1/H", Vector) = (1, 1, 1, 1)
        _Step ("Jump Step", Float) = 1
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _SourceTex;
        float4 _SourceUv;
        float4 _FieldParams;
        float4 _FieldTexels;
        float _Step;

        #define NOW_SDF_FIELD_NO_SEGMENT float4(-1.0, -1.0, -1.0, -1.0)
        #define NOW_SDF_FIELD_FAR (1000000.0)
        #define NOW_SDF_FIELD_MAX_DISTANCE (30000.0)

        // Texel centers sit at integer + 0.5 in field texel space.
        float2 FieldTexelCenter(float2 uv)
        {
            return floor(uv * _FieldTexels.xy) + 0.5;
        }

        float SourceAlpha(float2 texelCenter)
        {
            float2 spriteTexel = texelCenter - _FieldParams.z;
            float2 spriteSize = max(_FieldParams.xy, 1.0);

            if (spriteTexel.x < 0.0 || spriteTexel.y < 0.0 ||
                spriteTexel.x > spriteSize.x || spriteTexel.y > spriteSize.y)
            {
                return 0.0;
            }

            float2 sourceUv = _SourceUv.xy + spriteTexel / spriteSize * _SourceUv.zw;
            return tex2Dlod(_SourceTex, float4(sourceUv, 0.0, 0.0)).a;
        }

        float Threshold()
        {
            return clamp(_FieldParams.w, 0.0001, 0.9999);
        }

        bool IsInside(float alpha)
        {
            return alpha >= Threshold();
        }

        // Linear crossing of the threshold between two texel centers.
        float2 Crossing(float2 a, float alphaA, float2 b, float alphaB)
        {
            float t = saturate((Threshold() - alphaA) / (alphaB - alphaA));
            return lerp(a, b, t);
        }

        float SegmentDistance(float2 p, float4 segment)
        {
            float2 a = segment.xy;
            float2 ab = segment.zw - a;
            float lengthSquared = dot(ab, ab);
            float t = lengthSquared > 0.0 ? saturate(dot(p - a, ab) / lengthSquared) : 0.0;
            return length(p - (a + ab * t));
        }

        void Push(float2 crossing, inout float2 p0, inout float2 p1, inout int count)
        {
            if (count == 0)
                p0 = crossing;
            else if (count == 1)
                p1 = crossing;

            ++count;
        }

        // Marching squares on the cell whose corners are this texel center and
        // its right, top, and top-right neighbors. Corners beyond the field or
        // the sprite rect read as transparent.
        float4 SeedFragment(v2f_img i) : SV_Target
        {
            float2 c00 = FieldTexelCenter(i.uv);
            float2 c10 = c00 + float2(1.0, 0.0);
            float2 c01 = c00 + float2(0.0, 1.0);
            float2 c11 = c00 + float2(1.0, 1.0);
            float a00 = SourceAlpha(c00);
            float a10 = SourceAlpha(c10);
            float a01 = SourceAlpha(c01);
            float a11 = SourceAlpha(c11);
            bool b00 = IsInside(a00);
            bool b10 = IsInside(a10);
            bool b01 = IsInside(a01);
            bool b11 = IsInside(a11);

            if (b00 == b10 && b00 == b01 && b00 == b11)
                return NOW_SDF_FIELD_NO_SEGMENT;

            float2 p0 = 0.0;
            float2 p1 = 0.0;
            int count = 0;

            if (b00 != b10)
                Push(Crossing(c00, a00, c10, a10), p0, p1, count);
            if (b10 != b11)
                Push(Crossing(c10, a10, c11, a11), p0, p1, count);
            if (b01 != b11)
                Push(Crossing(c01, a01, c11, a11), p0, p1, count);
            if (b00 != b01)
                Push(Crossing(c00, a00, c01, a01), p0, p1, count);

            // Two crossings form the cell's contour segment. A saddle has four;
            // its second short segment lies within one texel of the first and
            // is dropped, which only perturbs the field locally.
            return float4(p0, p1);
        }

        void Consider(float2 texel, float4 candidate, inout float4 best, inout float bestDistance)
        {
            if (candidate.x < 0.0)
                return;

            float distance = SegmentDistance(texel, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        float4 FloodFragment(v2f_img i) : SV_Target
        {
            float2 texel = FieldTexelCenter(i.uv);
            float4 current = tex2Dlod(_MainTex, float4(i.uv, 0.0, 0.0));
            float4 best = NOW_SDF_FIELD_NO_SEGMENT;
            float bestDistance = NOW_SDF_FIELD_FAR;
            Consider(texel, current, best, bestDistance);

            [unroll]
            for (int y = -1; y <= 1; ++y)
            {
                [unroll]
                for (int x = -1; x <= 1; ++x)
                {
                    if (x == 0 && y == 0)
                        continue;

                    float2 neighbor = texel + float2(x, y) * _Step;

                    if (neighbor.x < 0.0 || neighbor.y < 0.0 ||
                        neighbor.x > _FieldTexels.x || neighbor.y > _FieldTexels.y)
                    {
                        continue;
                    }

                    float4 candidate = tex2Dlod(_MainTex, float4(neighbor * _FieldTexels.zw, 0.0, 0.0));
                    Consider(texel, candidate, best, bestDistance);
                }
            }

            return best;
        }

        float4 ResolveFragment(v2f_img i) : SV_Target
        {
            float2 texel = FieldTexelCenter(i.uv);
            float4 segment = tex2Dlod(_MainTex, float4(i.uv, 0.0, 0.0));
            bool inside = IsInside(SourceAlpha(texel));
            float distance = segment.x < 0.0
                ? NOW_SDF_FIELD_MAX_DISTANCE
                : min(SegmentDistance(texel, segment), NOW_SDF_FIELD_MAX_DISTANCE);
            return float4(inside ? -distance : distance, 0.0, 0.0, 1.0);
        }

        // Dilated sprite color for the sprite rect (no padding). Texels inside
        // the silhouette keep their own pixels. Texels outside take the color
        // just inside the nearest contour point with full alpha, so smooth
        // fillets and morph bridges that reach past the pixels inherit the edge
        // color instead of sampling transparency. Blit target: sprite-sized.
        float4 DilateFragment(v2f_img i) : SV_Target
        {
            float2 spriteSize = max(_FieldParams.xy, 1.0);
            float2 spriteTexel = floor(i.uv * spriteSize) + 0.5;
            float2 fieldTexel = spriteTexel + _FieldParams.z;
            float4 own = tex2Dlod(_SourceTex, float4(_SourceUv.xy + spriteTexel / spriteSize * _SourceUv.zw, 0.0, 0.0));
            float4 segment = tex2Dlod(_MainTex, float4(fieldTexel * _FieldTexels.zw, 0.0, 0.0));

            if (segment.x < 0.0)
                return float4(own.rgb, IsInside(own.a) ? own.a : 1.0);

            float2 a = segment.xy;
            float2 ab = segment.zw - a;
            float lengthSquared = dot(ab, ab);
            float t = lengthSquared > 0.0 ? saturate(dot(fieldTexel - a, ab) / lengthSquared) : 0.0;
            float2 nearest = a + ab * t;
            float2 toward = nearest - fieldTexel;

            if (IsInside(own.a))
            {
                // The field owns the edge: antialiased texels within a texel of
                // the contour become opaque so their alpha ramp cannot ghost
                // through fillets, while interior translucency is preserved.
                float edgeAlpha = lerp(1.0, own.a, saturate(length(toward) - 1.0));
                return float4(own.rgb, edgeAlpha);
            }
            float towardLength = max(length(toward), 0.0001);
            float2 insidePoint = nearest + toward / towardLength * 0.75;
            float2 insideSprite = clamp(insidePoint - _FieldParams.z, 0.5, spriteSize - 0.5);
            float4 edge = tex2Dlod(_SourceTex, float4(_SourceUv.xy + insideSprite / spriteSize * _SourceUv.zw, 0.0, 0.0));
            return float4(edge.rgb, 1.0);
        }

        // Copies the _SourceUv region of _SourceTex into the _StampRect texel
        // rect of the target atlas. The blit covers the whole atlas; fragments
        // outside the rect discard so existing entries are preserved.
        float4 _StampRect;

        float4 StampFragment(v2f_img i) : SV_Target
        {
            float2 texel = floor(i.uv * _FieldTexels.xy);
            float2 local = texel - _StampRect.xy;

            if (local.x < 0.0 || local.y < 0.0 ||
                local.x >= _StampRect.z || local.y >= _StampRect.w)
            {
                discard;
            }

            float2 sourceUv = _SourceUv.xy + (local + 0.5) / max(_StampRect.zw, 1.0) * _SourceUv.zw;
            return tex2Dlod(_SourceTex, float4(sourceUv, 0.0, 0.0));
        }
        ENDCG

        Pass
        {
            Name "Seed"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment SeedFragment
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "Flood"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FloodFragment
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "Resolve"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment ResolveFragment
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "Stamp"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment StampFragment
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "Dilate"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment DilateFragment
            #pragma target 3.0
            ENDCG
        }
    }

    Fallback Off
}
