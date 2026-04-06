#ifndef INVERT_HULL_OUTLINE_INCLUDED
#define INVERT_HULL_OUTLINE_INCLUDED

// ============================================================================
// InvertHullOutline.hlsl - Clip-Space Invert Hull Outline
// CleanRenderPipeline v2
// ============================================================================
// KEY CHANGE vs v1: Extrusion in CLIP SPACE, not world space.
// This fixes baked/scaled meshes having inconsistent outline thickness.
// Outline width is specified in screen-space pixels regardless of
// object scale, distance, or bake state.
//
// Global Properties (set from C# via Shader.SetGlobalXxx):
//   float  _Global_OutlineWidth       (master width multiplier, 0 = off)
//   float  _Global_OutlineDistFade    (fade start distance)
//   float  _Global_OutlineDistMax     (fade end distance, fully culled)
//   half4  _Global_OutlineColor       (fallback color if per-mat not set)
//
// Per-Material Properties (in CBUFFER):
//   half4  _OutlineColor
//   float  _OutlineWidth              (per-object width, multiplied with global)
//   float  _OutlineZOffset
// ============================================================================

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Global properties (set by OutlineGlobalSettings.cs)
float _Global_OutlineWidth;
float _Global_OutlineDistFade;
float _Global_OutlineDistMax;
float _Global_OutlineNearClip;

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
#if defined(_OUTLINE_SMOOTH_NORMAL)
    float4 tangentOS  : TANGENT;
#endif
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS  : SV_POSITION;
    float2 uv          : TEXCOORD0;
    half   fadeFactor   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ----------------------------------------------------------------
// Clip-space normal extrusion
// Transforms the normal to clip space and offsets the projected
// position, giving screen-pixel-constant width regardless of
// object scale / world size / bake transform.
// ----------------------------------------------------------------
float4 ExtrudeClipSpaceOutline(float4 posCS, float3 normalOS, float4 positionOS, float widthPixels)
{
    // Get normal in clip space via two-point method:
    // Project (pos) and (pos + normal * tiny), difference = clip-space normal direction
    float3 extrudeOS = positionOS.xyz + normalOS * 0.001;
    float4 extrudeCS = TransformWorldToHClip(TransformObjectToWorld(extrudeOS));

    // Clip-space offset direction (xy only)
    float2 dir = extrudeCS.xy / extrudeCS.w - posCS.xy / posCS.w;
    float len = length(dir);
    if (len > 0.00001)
        dir /= len;

    // Convert pixel width to clip-space units
    // _ScreenParams.xy = (width, height) of render target
    float2 pixelToClip = float2(2.0 / _ScreenParams.x, 2.0 / _ScreenParams.y);
    float2 offset = dir * widthPixels * pixelToClip;

    // Scale offset by w to get proper perspective
    posCS.xy += offset * posCS.w;

    return posCS;
}

// ----------------------------------------------------------------
// Distance-based fade — runs PER-VERTEX, uses actual vertex world pos.
// This is the CORRECT approach for baked meshes: each vertex knows
// its own world position regardless of how the mesh was merged/baked.
// bounds.center is meaningless for large baked meshes, but each
// vertex's positionWS is always accurate.
//
// Returns 0..1. Multiplied into width so fade=0 → no outline.
// ----------------------------------------------------------------
half ComputeDistanceFade(float3 positionWS)
{
    float dist = distance(GetCameraPositionWS(), positionWS);
    float fade = 1.0;

    // VR near-clip: fade out vertices very close to camera (comfort)
    if (_Global_OutlineNearClip > 0.0)
    {
        float halfNear = _Global_OutlineNearClip * 0.5;
        fade *= saturate((dist - halfNear) / max(halfNear, 0.001));
    }

    // Far fade: if global fade distance is 0, skip (always visible)
    if (_Global_OutlineDistFade > 0.0)
    {
        float range = max(_Global_OutlineDistMax - _Global_OutlineDistFade, 0.01);
        fade *= 1.0 - saturate((dist - _Global_OutlineDistFade) / range);
    }

    return (half)fade;
}

// ----------------------------------------------------------------
// Main vertex function
// ----------------------------------------------------------------
OutlineVaryings InvertHullOutlineVert(
    OutlineAttributes input,
    float perMatWidth, float zOffset)
{
    OutlineVaryings o = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    // Final width = per-material * global multiplier
    float finalWidth = perMatWidth * _Global_OutlineWidth;

    // World position for distance fade
    float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
    half fade = ComputeDistanceFade(posWS);
    finalWidth *= fade;

    // If width is zero, collapse vertex (GPU will cull degenerate triangles)
    if (finalWidth <= 0.0)
    {
        o.positionCS = float4(0, 0, -2, 1); // behind near plane = culled
        o.fadeFactor = 0.0h;
        o.uv = input.uv;
        return o;
    }

    // Base clip-space position
    float4 posCS = TransformWorldToHClip(posWS);

    // Choose extrusion normal
    float3 extrudeNormal = input.normalOS;
#if defined(_OUTLINE_SMOOTH_NORMAL)
    extrudeNormal = input.tangentOS.xyz;
#endif

    // Clip-space extrusion (scale-independent!)
    o.positionCS = ExtrudeClipSpaceOutline(posCS, extrudeNormal, input.positionOS, finalWidth);

    // Z-offset to push outline behind the mesh surface
    #if UNITY_REVERSED_Z
        o.positionCS.z -= zOffset * o.positionCS.w * 0.0001;
    #else
        o.positionCS.z += zOffset * o.positionCS.w * 0.0001;
    #endif

    o.uv = input.uv;
    o.fadeFactor = fade;

    return o;
}

#endif // INVERT_HULL_OUTLINE_INCLUDED
