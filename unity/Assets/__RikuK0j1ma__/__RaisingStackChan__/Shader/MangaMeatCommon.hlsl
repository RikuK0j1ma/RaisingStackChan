// Shared by the MangaMeat shaders: value noise and the bite helpers.
// A shader that includes this declares _MetersPerUnit, _BiteCount and _Bite0.._Bite7 in its UnityPerMaterial buffer first.
// Bites: xyz is the center in the meat's object space, w is the radius in meters. _MetersPerUnit converts object space to meters.
#ifndef RSC_MANGA_MEAT_COMMON
#define RSC_MANGA_MEAT_COMMON

float Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// Value noise in [0, 1].
float ValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = Hash31(i);
    float n100 = Hash31(i + float3(1, 0, 0));
    float n010 = Hash31(i + float3(0, 1, 0));
    float n110 = Hash31(i + float3(1, 1, 0));
    float n001 = Hash31(i + float3(0, 0, 1));
    float n101 = Hash31(i + float3(1, 0, 1));
    float n011 = Hash31(i + float3(0, 1, 1));
    float n111 = Hash31(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

// How far (m) a point in meters (meat object space * _MetersPerUnit) is inside the deepest bite. Negative: outside all.
// radiusScale shrinks the bites a little, so a point on a bite's own surface does not count as inside it.
#define RSC_BITE_CUT(pm, radiusScale, result)                                                       \
    {                                                                                               \
        float4 rscBites[8] = { _Bite0, _Bite1, _Bite2, _Bite3, _Bite4, _Bite5, _Bite6, _Bite7 };    \
        int rscCount = (int)_BiteCount;                                                             \
        result = -1.0;                                                                              \
        [unroll]                                                                                    \
        for (int rscK = 0; rscK < 8; rscK++)                                                        \
        {                                                                                           \
            if (rscK < rscCount)                                                                    \
                result = max(result, rscBites[rscK].w * (radiusScale) - distance(pm, rscBites[rscK].xyz * _MetersPerUnit)); \
        }                                                                                           \
    }

#endif
