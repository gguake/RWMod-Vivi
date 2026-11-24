#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.87, 0.6, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _Progress;      // @default = 1.05
    float _Intensity;     // @default = 1.0
    float _Flash;         // @default = 0.0
    float _LaneLength;    // @default = 25.0
    float _HalfWidth;     // @default = 0.3
    float _FaintAlpha;    // @default = 0.12
} u;

uint hashU(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    return (q.x ^ q.y) * 1597334673u;
}

float hashF(uint h, int shift)
{
    return float((h >> uint(shift)) & 1023u) / 1023.0;
}

void main()
{
    float key = vUv.x;
    // vUv.y: 중심선에서의 수직 거리(0~1 = 0~_HalfWidth), vColor.g: 열린 끝 캡에서의 연장 거리(0~1 = 0~_HalfWidth)
    float d = length(vec2(abs(vUv.y), vColor.g)) * u._HalfWidth;
    float across = clamp(d / max(u._HalfWidth, 1e-4), 0.0, 1.0);
    float px = max(fwidth(d), 1e-4);
    float along = key * u._LaneLength;
    float t = u._AgeSecs;

    float behind = (u._Progress - key) * u._LaneLength;
    float reveal = smoothstep(-0.05, 0.05, behind);
    float glowReveal = smoothstep(-0.4, 0.05, behind);
    float fresh = exp(-max(behind, 0.0) / 1.5) * reveal;

    float coreWidth = 0.04 + 0.025 * u._Flash;
    float core = 1.0 - smoothstep(coreWidth - px * 0.5, coreWidth + px, d);
    float spread = 1.0 + 0.8 * u._Flash;
    float glow = exp(-d / (0.09 * spread)) * 0.5 + exp(-d / (0.2 * spread)) * 0.16;
    glow *= 1.0 - smoothstep(0.7, 1.0, across);

    float shimmer = 0.86 + 0.14 * sin(along * 1.7 - t * 2.2);
    uint h = hashU(ivec2(int(floor(along * 2.5)), int(floor(t * 3.0)) + int(floor(key * 7.0)) * 131));
    float sparkle = step(0.94, hashF(h, 0)) * reveal * (1.0 - smoothstep(0.0, 0.1, d)) * (0.5 + 0.5 * hashF(h, 10));

    float weight = vColor.r;
    float boost = (1.0 + 0.6 * fresh) * shimmer * weight * u._Intensity;
    float coreLevel = (u._FaintAlpha + (1.0 - u._FaintAlpha) * reveal) * boost;
    float glowLevel = (u._FaintAlpha + (1.0 - u._FaintAlpha) * glowReveal) * boost;

    vec3 coreColor = mix(u._Color.rgb, vec3(1.0), 0.7);
    vec3 col = coreColor * min(core + sparkle * 0.7, 1.5) * coreLevel + u._Color.rgb * glow * glowLevel;
    col = mix(col, vec3(1.0, 0.98, 0.92) * (core * coreLevel + glow * glowLevel * 1.4) * 1.6, u._Flash);
    col *= u._Color.a * vColor.a;
    outColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
