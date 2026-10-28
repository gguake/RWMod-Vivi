#version 450

// 아포크리파 소멸 연출용 디졸브 셰이더.
// _AgeSecs가 흐를수록 노이즈 임계값이 올라가 위쪽부터 녹아내리듯 사라지고, 사라지는 경계에는
// 황금빛 불씨 테두리(_EdgeColor)가 생기며, 남은 부분은 점점 잿빛으로 바랜다.
// 플렉/모트는 Graphic_FleckPulse·Graphic_MoteWithAgeSecs가 _AgeSecs를 넣어 주고,
// C#에서 직접 그릴 때는 MaterialPropertyBlock으로 넣는다.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;              // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _EdgeColor;          // @color @default = vec4(1.0, 0.78, 0.35, 1.0)
    vec4 _MainTex_ST;         // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _AgeSecs;           // @default = 0.0
    float _DissolveDelay;     // @default = 0.6
    float _DissolveDuration;  // @default = 6.0
    float _EdgeWidth;         // @default = 0.07
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _DissolveTex;

void main()
{
    vec2 uv = vUv * u._MainTex_ST.zw + u._MainTex_ST.xy;
    vec4 tex = texture(_MainTex, uv) * u._Color * vColor;
    if (tex.a <= 0.003)
        discard;

    float t = clamp((u._AgeSecs - u._DissolveDelay) / max(u._DissolveDuration, 0.001), 0.0, 1.0);
    float n = texture(_DissolveTex, uv).r;
    float d = n - t * (1.0 + u._EdgeWidth);
    if (d < 0.0)
        discard;

    float edge = (1.0 - smoothstep(0.0, u._EdgeWidth, d)) * step(0.0001, t);
    vec3 ash = tex.rgb * vec3(0.62, 0.58, 0.56);
    vec3 col = mix(tex.rgb, ash, clamp(t * 1.4, 0.0, 0.75));
    col = mix(col, u._EdgeColor.rgb * 1.6, edge);
    outColor = vec4(clamp(col, 0.0, 1.0), tex.a);
}
