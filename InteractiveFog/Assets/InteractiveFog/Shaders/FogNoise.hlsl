#ifndef INTERACTIVE_FOG_NOISE_INCLUDED
#define INTERACTIVE_FOG_NOISE_INCLUDED

// Cheap hash-based value noise, shared by the fluid compute shader and the raymarch shader.

float h21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float vn2(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(h21(i), h21(i + float2(1, 0)), f.x),
                lerp(h21(i + float2(0, 1)), h21(i + float2(1, 1)), f.x), f.y);
}

float fbm2(float2 p)
{
    float s = 0.0, a = 0.5;
    [unroll] for (int i = 0; i < 5; i++) { s += a * vn2(p); p = p * 2.03 + 17.1; a *= 0.5; }
    return s;
}

float h31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float vn3(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(h31(i), h31(i + float3(1, 0, 0)), f.x), lerp(h31(i + float3(0, 1, 0)), h31(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(h31(i + float3(0, 0, 1)), h31(i + float3(1, 0, 1)), f.x), lerp(h31(i + float3(0, 1, 1)), h31(i + float3(1, 1, 1)), f.x), f.y),
        f.z);
}

#endif
