#version 450

// 아포크리파와 꽃/비비를 잇는 영혼 연결 빔(가산 혼합).
// 가로로 타일링되는 빔 텍스처(U = 빔 길이 방향)를 두 겹으로 서로 다른 속도로 흘려 실이 꼬이는 느낌을 주고,
// 전체 밝기를 천천히 맥동시킨다. _MainTex 랩 모드는 ShaderDef에서 Repeat로 지정한다.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _ScrollSpeed;   // @default = 0.55
    float _Tiling;        // @default = 1.0
    float _PulseSpeed;    // @default = 3.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    float x = vUv.x * u._Tiling;
    vec3 c1 = texture(_MainTex, vec2(x - u._AgeSecs * u._ScrollSpeed, vUv.y)).rgb;
    vec3 c2 = texture(_MainTex, vec2(x * 0.73 + u._AgeSecs * u._ScrollSpeed * 0.41 + 0.37, vUv.y)).rgb;

    // 양 끝이 서서히 사라지게 해 시작점과 끝점이 갑자기 끊겨 보이지 않도록 한다.
    float ends = smoothstep(0.0, 0.06, vUv.x) * smoothstep(0.0, 0.06, 1.0 - vUv.x);
    float pulse = 0.78 + 0.22 * sin(u._AgeSecs * u._PulseSpeed);
    vec3 c = (c1 * 0.75 + c2 * 0.45) * u._Color.rgb * vColor.rgb * pulse * ends * u._Color.a;
    outColor = vec4(clamp(c, 0.0, 1.0), 1.0);
}
