#version 450

// _AgeSecs는 PawnRenderSubWorker_ApocryphaGrow가 갱신한다. 전환 양 끝에서는 원본 그림과 이어지도록 왜곡을 없앤다.

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
    float env = sin(3.14159265 * t);
    float beat = pow(0.5 + 0.5 * cos(6.2831853 * time * u._BeatRate), 8.0);

    vec2 flow = vec2(fbm(uv * 3.2 + vec2(0.0, time * 0.9)), fbm(uv * 3.2 + vec2(time * 0.7, 4.3))) - 0.5;
    vec2 warp = flow * u._Warp * env * (1.0 + 0.5 * beat);

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

    float n = fbm(uv * 5.0 + 11.0);
    float radial = length((uv - vec2(0.5, 0.47)) * vec2(1.0, 1.15));
    float m = radial * 1.1 + n * 0.5;
    float front = mix(-0.1, 1.45, smoothstep(0.08, 0.92, t));
    float reveal = 1.0 - smoothstep(front - 0.05, front + 0.05, m);
    float seam = 1.0 - abs(reveal * 2.0 - 1.0);

    float vn = fbm(uv * 9.0 + flow * 3.0 + 23.0);
    float vein = pow(1.0 - abs(vn * 2.0 - 1.0), 12.0);
    vein *= smoothstep(front + 0.45, front + 0.05, m) * (1.0 - reveal) * smoothstep(0.02, 0.2, t);

    vec3 oldCol = mix(oldC.rgb, oldC.rgb * vec3(0.7, 0.32, 0.5), smoothstep(0.05, 0.7, t) * 0.65);
    oldCol = mix(oldCol, u._VeinColor.rgb * (1.1 + 0.8 * beat), clamp(vein * 1.3, 0.0, 1.0));
    vec3 newCol = newC.rgb * (vec3(1.0) + 0.25 * beat * env * vec3(1.0, 0.4, 0.8));

    float alpha = mix(oldC.a, newC.a, reveal);
    vec3 col = mix(oldCol, newCol, reveal);

    float body = max(oldC.a, newC.a);
    float membrane = clamp(seam * body * (0.75 + 0.5 * beat), 0.0, 1.0);
    col = mix(col, u._MembraneColor.rgb * 1.3, membrane);
    alpha = max(alpha, membrane * 0.9);

    vec4 result = vec4(col, alpha) * u._Color * vColor;
    if (result.a <= 0.003)
        discard;

    outColor = vec4(clamp(result.rgb, 0.0, 1.0), result.a);
}
