#version 450

// _BlurTex: R(spread=0.05), G(spread=0.15), B(spread=0.30)에 사전 베이크한 블러 스칼라(expand=1.9 UV 기준).

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 0.9, 0.7, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _Core;      // @default = 0.5
    float _Bloom;     // @default = 0.5
    float _Spread;    // @default = 0.03
    float _Expand;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _BlurTex;

const float BLUR_TEX_EXPAND = 1.9;

vec4 tap(vec2 uv)
{
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
        return vec4(0.0);
    vec4 c = texture(_MainTex, uv * u._MainTex_ST.zw + u._MainTex_ST.xy);
    return vec4(c.rgb * c.a, c.a);
}

void main()
{
    vec2 uv = (vUv - 0.5) * u._Expand + 0.5;
    vec4 c0 = tap(uv);

    vec2 blurUv = (uv - 0.5) / BLUR_TEX_EXPAND + 0.5;
    vec3 b = (blurUv.x >= 0.0 && blurUv.y >= 0.0 && blurUv.x <= 1.0 && blurUv.y <= 1.0)
        ? texture(_BlurTex, blurUv * u._MainTex_ST.zw + u._MainTex_ST.xy).rgb
        : vec3(0.0);
    float blurScalar = u._Spread <= 0.15
        ? mix(b.r, b.g, clamp((u._Spread - 0.05) / 0.10, 0.0, 1.0))
        : mix(b.g, b.b, clamp((u._Spread - 0.15) / 0.15, 0.0, 1.0));

    float lum = dot(c0.rgb, vec3(0.299, 0.587, 0.114));
    vec3 core = mix(c0.rgb, vec3(lum) * u._Color.rgb, 0.35) * u._Core;
    vec3 bloom = u._Color.rgb * blurScalar * (1.0 - 0.6 * c0.a) * u._Bloom;

    outColor = vec4(clamp((core + bloom) * u._Color.a, 0.0, 1.0), 1.0);
}
