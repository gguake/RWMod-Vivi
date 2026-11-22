#version 450

// 결속 피격(가산 혼합): 몸을 감싼 방벽의 맞은 쪽 벌집 칸이 번쩍이고 금이 간다.
// 쿼드 중심은 몸 중심, 쿼드 x축은 투사체 진행 방향(플렉 회전으로 맞춘다). 좌표는 쿼드 반지름을 1로 둔 값이다.
// 플렉은 Graphic_FleckPulse가 _AgeSecs·_RandomPerObject를 넣어 주고, 플렉 색과 페이드 투명도는 _Color로 온다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;            // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _CoreColor;        // @color @default = vec4(1.0, 0.97, 0.86, 1.0)
    vec4 _GlowColor;        // @color @default = vec4(1.0, 0.72, 0.26, 1.0)
    float _AgeSecs;         // @default = 0.0
    float _RandomPerObject; // @default = 0.0
    float _Duration;        // @default = 0.42
    float _ShieldRadius;    // @default = 0.82
    float _CellWidth;       // @default = 0.19
    float _PatchRadius;     // @default = 0.4
    float _CrackLength;     // @default = 0.34
    float _Brightness;      // @default = 1.0
} u;

const float TAU = 6.28318531;
const int CRACKS = 4;
const int CRACK_SEGMENTS = 3;

uint hashU(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    return (q.x ^ q.y) * 1597334673u;
}

float hashF(uint h, int shift)
{
    return float((h >> uint(shift)) & 1023u) / 1023.0;
}

float hexDist(vec2 p)
{
    p = abs(p);
    return max(dot(p, vec2(0.5, 0.8660254)), p.x);
}

float segDist(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-6), 0.0, 1.0);
    return length(pa - ba * h);
}

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float px = max(max(length(dFdx(p)), length(dFdy(p))), 1e-5);

    float t = clamp(u._AgeSecs / max(u._Duration, 0.001), 0.0, 1.0);
    float fade = u._Color.a * (1.0 - smoothstep(0.55, 1.0, t));
    if (fade <= 0.001) { discard; }

    float R = u._ShieldRadius;
    vec2 hit = vec2(-R * 0.9, 0.0);
    float r = length(p);
    int seed = int(hashU(ivec2(floatBitsToInt(u._RandomPerObject), 0x5bd1e995)) & 4095u);

    // 방벽 면: 가장자리로 갈수록 살짝 눌려 둥근 막처럼 보인다.
    float rn = clamp(r / R, 0.0, 1.0);
    vec2 g = p * (1.0 + 0.28 * rn * rn) / max(u._CellWidth, 0.01);
    vec2 cellSize = vec2(1.0, 1.7320508);
    vec2 a = mod(g, cellSize) - cellSize * 0.5;
    vec2 b = mod(g - cellSize * 0.5, cellSize) - cellSize * 0.5;
    vec2 local = dot(a, a) < dot(b, b) ? a : b;
    vec2 id = g - local;
    vec2 center = id * u._CellWidth / (1.0 + 0.28 * rn * rn);
    uint ch = hashU(ivec2(floor(id * 2.0 + 0.5)) + ivec2(seed, seed * 7));
    float cellRand = hashF(ch, 0);

    float edgeGap = 0.5 - hexDist(local);
    float edgePx = px * (1.0 + 0.28 * rn * rn) / max(u._CellWidth, 0.01);
    float lineW = max(0.05, edgePx * 0.8);
    float line = 1.0 - smoothstep(lineW - edgePx, lineW + edgePx, edgeGap);

    float dc = length(center - hit);
    float front = u._PatchRadius * 1.1 * (1.0 - pow(1.0 - min(t * 1.6, 1.0), 2.4));
    float pass = exp(-pow((dc - front) / (u._CellWidth * 0.9), 2.0));
    float behind = smoothstep(front + u._CellWidth * 0.5, front - u._CellWidth * 0.5, dc);
    float patch = exp(-pow(dc / u._PatchRadius, 2.0));
    float inside = 1.0 - smoothstep(R - px * 1.5, R + px * 1.5, r);
    float lit = patch * inside * (pass * (0.8 + 0.5 * cellRand) + behind * 0.3 * (1.0 - t) * (0.6 + 0.8 * cellRand));

    float cells = lit * (line + 0.16 * (1.0 - line));

    // 방벽 테: 맞은 자리 근처만 짧게 밝아진다.
    float ang = atan(p.y, p.x);
    float dAng = abs(mod(ang, TAU) - 3.14159265);
    float rimW = max(0.012, px * 1.2);
    float rim = exp(-pow((r - R) / rimW, 2.0)) * exp(-pow(dAng / 0.55, 2.0)) * (1.0 - smoothstep(0.0, 0.7, t));
    float rimHalo = exp(-pow((r - R) / (rimW * 4.0), 2.0)) * exp(-pow(dAng / 0.7, 2.0)) * 0.35 * (1.0 - t);

    // 금: 맞은 자리에서 안쪽으로 가늘게 뻗었다가 사라진다.
    float grow = smoothstep(0.0, 0.22, t);
    float crackFade = 1.0 - smoothstep(0.35, 0.85, t);
    float crack = 0.0;
    float crackGlow = 0.0;
    float cw = max(0.006, px * 0.75);
    for (int k = 0; k < CRACKS; k++)
    {
        uint h = hashU(ivec2(k, seed * 31 + 17));
        float dir = (float(k) - float(CRACKS - 1) * 0.5) / float(CRACKS - 1) * 1.5 + (hashF(h, 0) - 0.5) * 0.4;
        float len = u._CrackLength * (0.55 + 0.6 * hashF(h, 10)) * grow;
        vec2 a0 = hit;
        float travelled = 0.0;
        float heading = dir;
        for (int s = 0; s < CRACK_SEGMENTS; s++)
        {
            uint hs = hashU(ivec2(k * 13 + s, seed * 57 + 3));
            float segLen = len / float(CRACK_SEGMENTS) * (0.7 + 0.6 * hashF(hs, 0));
            heading += (hashF(hs, 10) - 0.5) * 1.2;
            vec2 a1 = a0 + vec2(cos(heading), sin(heading)) * segLen;
            float d = segDist(p, a0, a1);
            float taper = 1.0 - (travelled + segLen * 0.5) / max(len * 1.2, 1e-4);
            crack = max(crack, (1.0 - smoothstep(cw * 0.5, cw * 1.5, d)) * taper);
            crackGlow = max(crackGlow, exp(-d / (cw * 4.0)) * taper);
            travelled += segLen;
            a0 = a1;
        }
    }
    crack *= crackFade * inside;
    crackGlow *= crackFade * inside;

    // 맞은 자리 섬광
    vec2 dh = p - hit;
    float dh2 = dot(dh, dh);
    float flash = exp(-dh2 / (0.012 + px * px * 4.0)) * exp(-t * 9.0) * 1.6
                + exp(-dh2 / 0.03) * exp(-t * 7.0) * 0.45;

    vec3 col = u._CoreColor.rgb * (cells * 2.0 + crack * 1.4 + rim * 1.1 + flash)
             + u._GlowColor.rgb * (lit * 0.35 + crackGlow * 0.45 + rimHalo + flash * 0.4);
    float border = 1.0 - smoothstep(0.88, 1.0, max(abs(p.x), abs(p.y)));
    col *= fade * border * u._Brightness * u._Color.rgb;
    outColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
