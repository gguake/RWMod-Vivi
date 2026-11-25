#version 450

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.9, 0.6, 1.0)
    vec4 _CoreColor;      // @color @default = vec4(1.0, 0.97, 0.86, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _RandomPerObjectOffsetRandom; // @default = 0.0
    float _Brightness;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float NOISE_INV_PERIOD = 0.0625;

void main()
{
    vec2 p = (vUv - 0.5) * 2.0;
    float t = u._AgeSecs;
    float seed = fract(abs(u._RandomPerObjectOffsetRandom) * 13.7) * 64.0;

    vec2 warp = vec2(texture(_MainTex, vec2(p.x * 2.2 + seed, p.y * 2.0 - t * 0.9) * NOISE_INV_PERIOD).r,
                     texture(_MainTex, vec2(p.x * 2.2 + seed + 7.3, p.y * 2.0 - t * 1.1 + 3.1) * NOISE_INV_PERIOD).g) - 0.5;

    vec2 q = vec2(p.x / 0.48, (p.y - 0.04) / 0.7);
    float e = length(q);
    float ew = length(q + warp * 0.6);

    float rim = exp(-pow((ew - 0.92) / 0.24, 2.0));

    float tongue = texture(_MainTex, vec2(p.x * 4.5 + warp.x * 2.0 + seed, p.y * 1.4 - t * 1.5) * NOISE_INV_PERIOD).r;
    tongue = smoothstep(0.42, 0.8, tongue);
    float tongueMask = exp(-max(ew - 0.75, 0.0) * 2.6) * smoothstep(-0.8, 0.3, p.y) * smoothstep(0.35, 0.8, ew);

    float c1 = sin(p.x * 10.0 + sin(p.y * 7.0 + t * 2.3) * 1.6 + t * 1.4);
    float c2 = sin(p.y * 9.0 - t * 1.9 + sin(p.x * 6.0 - t * 1.2) * 1.4);
    float caustic = pow(1.0 - abs(c1 * 0.5 + c2 * 0.5), 6.0) * exp(-e * e * 1.4);

    float glow = exp(-ew * ew * 1.8) * 0.18;
    float breathe = 0.85 + 0.15 * sin(t * 2.4 + seed);
    float outer = 1.0 - smoothstep(0.78, 1.0, length(p));

    vec3 col = u._Color.rgb * (glow + rim * (0.35 + 0.65 * tongue) * 0.9 + tongue * tongueMask * 0.75)
             + u._CoreColor.rgb * caustic * 0.3;
    col *= breathe * outer * u._Brightness * u._Color.a;
    outColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
