#version 450

// 요정 간섭 비비를 감싸 일렁이는 빛. 몸 윤곽을 따라 흔들리는 빛 테두리, 위로 피어오르는 빛 자락, 몸 위를 흐르는 물결 반짝임을 겹친다.
// 모트는 Graphic_MoteWithAgeSecs가 _AgeSecs·_RandomPerObjectOffsetRandom을 넣어 주고, 모트 색과 페이드 투명도는 _Color로 온다.

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

// 정수 격자 해시. 실수 fract 해시는 좌표가 커지면 정밀도가 떨어져 무늬가 뭉개진다.
float hash(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    uint n = (q.x ^ q.y) * 1597334673u;
    return float(n >> 8) * (1.0 / 16777215.0);
}

float vnoise(vec2 p)
{
    ivec2 i = ivec2(floor(p));
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + ivec2(1, 0));
    float c = hash(i + ivec2(0, 1));
    float d = hash(i + ivec2(1, 1));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p)
{
    return vnoise(p) * 0.55 + vnoise(p * 2.03 + 17.1) * 0.3 + vnoise(p * 4.07 + 41.7) * 0.15;
}

void main()
{
    vec2 p = (vUv - 0.5) * 2.0;
    float t = u._AgeSecs;
    float seed = fract(abs(u._RandomPerObjectOffsetRandom) * 13.7) * 64.0;

    // 아지랑이처럼 위로 흘러가는 노이즈로 좌표를 흔든다.
    vec2 warp = vec2(fbm(vec2(p.x * 2.2 + seed, p.y * 2.0 - t * 0.9)),
                     fbm(vec2(p.x * 2.2 + seed + 7.3, p.y * 2.0 - t * 1.1 + 3.1))) - 0.5;

    // 몸을 감싸는 세로로 긴 타원(약간 위로 올림)
    vec2 q = vec2(p.x / 0.48, (p.y - 0.04) / 0.7);
    float e = length(q);
    float ew = length(q + warp * 0.6);

    // 흔들리는 빛 테두리
    float rim = exp(-pow((ew - 0.92) / 0.24, 2.0));

    // 위로 피어오르는 빛 자락: 세로로 늘인 노이즈를 위로 흘려 몸 둘레·위쪽에서만 보이게 한다.
    float tongue = fbm(vec2(p.x * 4.5 + warp.x * 2.0 + seed, p.y * 1.4 - t * 1.5));
    tongue = smoothstep(0.42, 0.8, tongue);
    float tongueMask = exp(-max(ew - 0.75, 0.0) * 2.6) * smoothstep(-0.8, 0.3, p.y) * smoothstep(0.35, 0.8, ew);

    // 몸 위를 흐르는 물결 반짝임(물속 빛 무늬처럼 가는 밝은 선)
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
