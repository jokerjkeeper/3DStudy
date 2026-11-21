#ifndef NOGGI_BANGS_PROJECTION_INCLUDED
#define NOGGI_BANGS_PROJECTION_INCLUDED

TEXTURE2D(_BangsDepth);
SAMPLER(sampler_BangsDepth);
float4x4 _BangsVP;
float4 _BangsOrigin, _BangsDirection, _BangsParams;
float4 _BangsFaceCenter, _BangsReceiverUp;
float _BangsSoftness;

half3 ApplyBangsProjection(half3 color, float3 positionWS)
{
    if (_BangsParams.w < 0.5)
        return color;

    float faceHeight = dot(positionWS - _BangsFaceCenter.xyz, _BangsReceiverUp.xyz);
    half faceFade = smoothstep(_BangsReceiverUp.w, _BangsReceiverUp.w + .022, faceHeight);
    if (faceFade <= 0)
        return color;

    float2 uv = mul(_BangsVP, float4(positionWS, 1)).xy * 0.5 + 0.5;
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1 - uv.y;
    #endif
    float receiverDepth = dot(positionWS - _BangsOrigin.xyz, _BangsDirection.xyz);
    if (any(uv < .005) || any(uv > .995) || receiverDepth <= 0 || receiverDepth >= _BangsDirection.w)
        return color;

    half covered = 0;
    float compareDepth = (receiverDepth - _BangsParams.z) / _BangsDirection.w;
    float radius = _BangsSoftness * _BangsParams.y * 6;
    [unroll] for (int y = -2; y <= 2; ++y)
    [unroll] for (int x = -2; x <= 2; ++x)
    {
        float2 offset = float2(x, y) * radius * 0.5;
        half weight = (3 - abs(x)) * (3 - abs(y));
        float caster = SAMPLE_TEXTURE2D_LOD(_BangsDepth, sampler_BangsDepth, uv + offset, 0).r;
        covered += step(caster, compareDepth) * weight;
    }
    return color * lerp(half3(1, 1, 1), half3(.69, .59, .66), covered * faceFade * (_BangsParams.x / 81));
}
#endif

