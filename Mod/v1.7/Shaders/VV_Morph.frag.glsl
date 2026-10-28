#version 450

// 아포크리파 2페이즈 전환: 1페이즈 몸(_MainTex)이 꿈틀거리며 2페이즈 몸(_MorphTex)으로 뒤바뀌는 기괴한 모핑.
// - 살갗이 기어 다니듯 흐르는 왜곡과, 심장 박동에 맞춰 부풀었다 꺼지는 혹
// - 몸 중심에서 바깥으로 들쭉날쭉 번지는 변이 전선. 전선 앞의 옛 몸에는 핏빛 혈관이 기어오르며 썩어 들어가고,
//   전선에는 젖은 자홍빛 막이 박동하며, 전선 뒤에서는 새 몸이 기존 몸을 찢고 나온다
// - 절정에서 가로로 찢기는 글리치와 새 몸의 색수차
// 진행도는 _AgeSecs(전환 시작부터 경과한 시간(초)) / _Duration. 시작(0)과 끝(1)에서는 왜곡이 모두 0이 되어
// 원래 1페이즈·2페이즈 그림과 정확히 이어진다. C#(PawnRenderSubWorker_ApocryphaGrow)이 _AgeSecs를 매 프레임 넣는다.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST;     // @default = vec4(0.0, 0.0, 1.0, 1.0)
    vec4 _VeinColor;      // @color @default = vec4(0.85, 0.05, 0.22, 1.0)
    vec4 _MembraneColor;  // @color @default = vec4(1.0, 0.32, 0.86, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _Duration;      // @default = 7.0
    float _BeatRate;      // @default = 1.25
    float _Warp;          // @default = 0.06
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _MorphTex;

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
    float time = u._AgeSecs;
    float t = clamp(time / max(u._Duration, 0.001), 0.0, 1.0);
    float env = sin(3.14159265 * t);                                        // 0 → 1 → 0
    float beat = pow(0.5 + 0.5 * cos(6.2831853 * time * u._BeatRate), 8.0); // 박동 순간에 1

    // 1) 살갗이 기어 다니는 흐름
    vec2 flow = vec2(fbm(uv * 3.2 + vec2(0.0, time * 0.9)), fbm(uv * 3.2 + vec2(time * 0.7, 4.3))) - 0.5;
    vec2 warp = flow * u._Warp * env * (1.0 + 0.5 * beat);

    // 2) 박동에 맞춰 부푸는 혹(부푼 곳은 가운데를 끌어와 그려 확대되어 보인다)
    vec2 bulge = vec2(0.0);
    for (int i = 0; i < 4; i++)
    {
        float fi = float(i);
        vec2 c = vec2(0.30, 0.28) + 0.40 * vec2(hash(ivec2(i, 2)), hash(ivec2(i, 9)));
        float rad = 0.07 + 0.06 * hash(ivec2(i, 4));
        float swell = env * (0.4 + 0.6 * beat) * (0.6 + 0.4 * sin(time * 2.3 + fi * 1.9));
        vec2 dv = uv - c;
        bulge += dv * exp(-dot(dv, dv) / (rad * rad)) * swell * 0.6;
    }

    // 3) 절정에서 가로로 찢기는 줄
    int row = int(floor(uv.y * 56.0));
    float g = hash(ivec2(row, int(floor(time * 15.0))));
    float tear = step(0.9, g) * (g - 0.9) * 10.0 * env * 0.04 * (hash(ivec2(row, 3)) * 2.0 - 1.0);

    vec2 uvOld = uv + warp - bulge + vec2(tear, 0.0);
    vec2 uvNew = uv - warp.yx * 0.8 - bulge * 0.7 - vec2(tear * 0.6, 0.0);

    vec4 oldC = texture(_MainTex, uvOld);
    float ca = 0.007 * env;
    vec4 newC = texture(_MorphTex, uvNew);
    newC.r = texture(_MorphTex, uvNew + vec2(ca, 0.0)).r;
    newC.b = texture(_MorphTex, uvNew - vec2(ca, 0.0)).b;

    // 4) 변이 전선: 몸 중심에서 바깥으로 번진다(노이즈로 들쭉날쭉)
    float n = fbm(uv * 5.0 + 11.0);
    float radial = length((uv - vec2(0.5, 0.47)) * vec2(1.0, 1.15));
    float m = radial * 1.1 + n * 0.5;
    float front = mix(-0.1, 1.45, smoothstep(0.08, 0.92, t));
    float reveal = 1.0 - smoothstep(front - 0.05, front + 0.05, m);
    float seam = 1.0 - abs(reveal * 2.0 - 1.0);

    // 5) 전선 앞 옛 몸을 기어오르는 핏빛 혈관과 썩어 들어가는 빛깔
    float vn = fbm(uv * 9.0 + flow * 3.0 + 23.0);
    float vein = pow(1.0 - abs(vn * 2.0 - 1.0), 12.0);
    vein *= smoothstep(front + 0.45, front + 0.05, m) * (1.0 - reveal) * smoothstep(0.02, 0.2, t);

    vec3 oldCol = mix(oldC.rgb, oldC.rgb * vec3(0.7, 0.32, 0.5), smoothstep(0.05, 0.7, t) * 0.65);
    oldCol = mix(oldCol, u._VeinColor.rgb * (1.1 + 0.8 * beat), clamp(vein * 1.3, 0.0, 1.0));
    vec3 newCol = newC.rgb * (vec3(1.0) + 0.25 * beat * env * vec3(1.0, 0.4, 0.8));

    float alpha = mix(oldC.a, newC.a, reveal);
    vec3 col = mix(oldCol, newCol, reveal);

    // 6) 전선의 젖은 막(몸이 있는 곳에서만)
    float body = max(oldC.a, newC.a);
    float membrane = clamp(seam * body * (0.75 + 0.5 * beat), 0.0, 1.0);
    col = mix(col, u._MembraneColor.rgb * 1.3, membrane);
    alpha = max(alpha, membrane * 0.9);

    vec4 result = vec4(col, alpha) * u._Color * vColor;
    if (result.a <= 0.003)
        discard;

    outColor = vec4(clamp(result.rgb, 0.0, 1.0), result.a);
}
