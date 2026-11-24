#version 450

// C#(ApocryphaManifestation)이 MaterialPropertyBlock으로 _AgeSecs·_Progress·_Reveal·_Color(투명도)를 매 프레임 넣는다.
// _NoiseTex는 공용 타일링 FBM 노이즈(VFX/VV_TileableFbmNoise, 기본 주기 16)다.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST;     // @default = vec4(0.0, 0.0, 1.0, 1.0)
    vec4 _EdgeColor;      // @color @default = vec4(1.0, 0.3, 0.85, 1.0)
    vec4 _DarkColor;      // @color @default = vec4(0.07, 0.02, 0.1, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _Progress;      // @default = 1.0
    float _Reveal;        // @default = 0.0
    float _EdgeWidth;     // @default = 0.08
    float _GhostAlpha;    // @default = 0.22
    float _Soft;          // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _DissolveTex;
layout(set = 2, binding = 3) uniform sampler2D _NoiseTex;

const float NOISE_INV_PERIOD = 0.0625;

void main()
{
    vec2 uv = vUv * u._MainTex_ST.zw + u._MainTex_ST.xy;
    vec4 tex = texture(_MainTex, uv) * vColor;
    if (tex.a <= 0.003)
        discard;

    float radial = clamp(length((vUv - vec2(0.5, 0.52)) * vec2(1.25, 1.0)) * 1.75, 0.0, 1.0);
    float n = texture(_DissolveTex, uv).r;
    float grain = textureLod(_NoiseTex, vUv * (70.0 * NOISE_INV_PERIOD), 0.0).b;
    float soft = clamp(u._Soft, 0.0, 1.0);
    float order = radial * 0.55 + n * 0.3 + grain * 0.15 * (1.0 - soft);

    float p = clamp(u._Progress, 0.0, 1.0);
    float w = u._EdgeWidth;
    float d = p * (1.0 + w) - order;   // > 0: 이미 채워진 곳

    float luma = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    vec3 dark = u._DarkColor.rgb * (0.45 + 1.1 * luma);

    if (d < 0.0)
    {
        float ghost = u._GhostAlpha * smoothstep(0.0, 0.2, p) * (0.4 + 0.6 * (1.0 - smoothstep(0.0, 0.35, -d))) * (1.0 - p * p);
        float front = soft * smoothstep(-0.24, 0.0, d);
        float cover = max(ghost, front);
        if (cover <= 0.004)
            discard;
        outColor = vec4(mix(dark, clamp(u._EdgeColor.rgb * 1.5, 0.0, 1.0), front), tex.a * cover * u._Color.a);
        return;
    }

    float edge = (1.0 - smoothstep(0.0, w, d)) * (1.0 - smoothstep(0.9, 1.0, p));

    float flow = textureLod(_NoiseTex, (vUv * vec2(5.0, 6.5) + vec2(0.0, -u._AgeSecs * 0.35)) * NOISE_INV_PERIOD, 0.0).r;
    float veins = pow(clamp(1.0 - abs(flow - 0.5) * 9.0, 0.0, 1.0), 2.0);
    vec3 body = dark + u._EdgeColor.rgb * veins * 0.55;

    vec3 col = mix(body, tex.rgb, clamp(u._Reveal, 0.0, 1.0));
    col = mix(col, u._EdgeColor.rgb * 1.5, edge);
    outColor = vec4(clamp(col, 0.0, 1.0), tex.a * u._Color.a);
}
