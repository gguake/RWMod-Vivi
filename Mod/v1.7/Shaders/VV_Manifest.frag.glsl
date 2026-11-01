#version 450

// 아포크리파 1페이즈 등장 연출: 영원꽃 위 허공에 모여든 검은 에너지가 여왕의 형체를 빚어낸다.
// - _Progress(0~1): 몸 중심에서 바깥으로, 노이즈와 잔 알갱이를 섞어 들쭉날쭉 조각조각 채워진다.
//   채워지는 경계에는 자홍빛 테두리(_EdgeColor)가 생기고, 아직 채워지지 않은 곳에도 흐릿한 검은 윤곽(_GhostAlpha)이 비친다.
// - 채워진 몸은 처음엔 검은 에너지(_DarkColor, 원화 밝기만큼 결이 남음) 위로 자홍 실금이 흐르다가,
//   _Reveal(0~1)만큼 본래 색이 드러난다(1이면 원화 그대로라 실제 폰과 정확히 이어진다).
// C#(ApocryphaManifestation)이 MaterialPropertyBlock으로 _AgeSecs·_Progress·_Reveal·_Color(투명도)를 매 프레임 넣는다.

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
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _DissolveTex;

// 정수 격자 해시. 실수 fract 해시는 좌표가 커지면 정밀도가 떨어져 긴 직선 무늬가 생긴다.
float hash(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    uint n = (q.x ^ q.y) * 1597334673u;
    return float(n) * (1.0 / 4294967295.0);
}

float vnoise(vec2 p)
{
    ivec2 i = ivec2(floor(p));
    vec2 f = fract(p);
    vec2 w = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + ivec2(1, 0));
    float c = hash(i + ivec2(0, 1));
    float d = hash(i + ivec2(1, 1));
    return mix(mix(a, b, w.x), mix(c, d, w.x), w.y);
}

float fbm(vec2 p)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int k = 0; k < 4; k++)
    {
        sum += amp * vnoise(p);
        p = p * 2.03 + vec2(17.1, 9.7);
        amp *= 0.5;
    }
    return sum;
}

void main()
{
    vec2 uv = vUv * u._MainTex_ST.zw + u._MainTex_ST.xy;
    vec4 tex = texture(_MainTex, uv) * vColor;
    if (tex.a <= 0.003)
        discard;

    // 채워지는 순서: 몸통 중심에서의 거리 + 디졸브 노이즈 + 잔 알갱이(모여든 입자가 하나씩 박히는 느낌)
    float radial = clamp(length((vUv - vec2(0.5, 0.52)) * vec2(1.25, 1.0)) * 1.75, 0.0, 1.0);
    float n = texture(_DissolveTex, uv).r;
    float grain = vnoise(vUv * 70.0);
    float order = radial * 0.55 + n * 0.3 + grain * 0.15;

    float p = clamp(u._Progress, 0.0, 1.0);
    float w = u._EdgeWidth;
    float d = p * (1.0 + w) - order;   // > 0: 이미 채워진 곳

    float luma = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    vec3 dark = u._DarkColor.rgb * (0.45 + 1.1 * luma);

    if (d < 0.0)
    {
        // 아직 채워지지 않은 곳: 흐릿한 검은 윤곽이 온몸에 비치고, 채워지는 경계에 가까울수록 진하다.
        float ghost = u._GhostAlpha * smoothstep(0.0, 0.2, p) * (0.4 + 0.6 * (1.0 - smoothstep(0.0, 0.35, -d))) * (1.0 - p * p);
        if (ghost <= 0.004)
            discard;
        outColor = vec4(dark, tex.a * ghost * u._Color.a);
        return;
    }

    float edge = (1.0 - smoothstep(0.0, w, d)) * (1.0 - smoothstep(0.9, 1.0, p));

    // 검은 몸 위를 흐르는 자홍 실금: 위로 흐르는 fbm의 등고선
    float flow = fbm(vUv * vec2(5.0, 6.5) + vec2(0.0, -u._AgeSecs * 0.35));
    float veins = pow(clamp(1.0 - abs(flow - 0.5) * 9.0, 0.0, 1.0), 2.0);
    vec3 body = dark + u._EdgeColor.rgb * veins * 0.55;

    vec3 col = mix(body, tex.rgb, clamp(u._Reveal, 0.0, 1.0));
    col = mix(col, u._EdgeColor.rgb * 1.5, edge);
    outColor = vec4(clamp(col, 0.0, 1.0), tex.a * u._Color.a);
}
