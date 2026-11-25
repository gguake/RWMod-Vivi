#version 450

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

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float TAU = 6.28318531;

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

    float ang = atan(p.y, p.x);
    float dAng = abs(mod(ang, TAU) - 3.14159265);
    float rimW = max(0.012, px * 1.2);
    float rim = exp(-pow((r - R) / rimW, 2.0)) * exp(-pow(dAng / 0.55, 2.0)) * (1.0 - smoothstep(0.0, 0.7, t));
    float rimHalo = exp(-pow((r - R) / (rimW * 4.0), 2.0)) * exp(-pow(dAng / 0.7, 2.0)) * 0.35 * (1.0 - t);

    float grow = smoothstep(0.0, 0.22, t);
    float crackFade = 1.0 - smoothstep(0.35, 0.85, t);
    float crack = 0.0;
    float crackGlow = 0.0;
    if (grow > 0.001 && crackFade > 0.001 && inside > 0.001)
    {
        vec2 dHit = (p - hit) * (0.34 / max(u._CrackLength, 0.01));
        if (((seed >> 2) & 1) != 0) { dHit.y = -dHit.y; }
        vec2 localUv = vec2((dHit.x + 0.06) / 0.52, (dHit.y + 0.36) / 0.72);
        if (localUv.x > 0.01 && localUv.x < 0.99 && localUv.y > 0.01 && localUv.y < 0.99)
        {
            vec2 tile = vec2(float(seed & 1), float((seed >> 1) & 1));
            vec2 atlasUv = (tile + localUv) * 0.5;
            vec3 ck = textureLod(_MainTex, atlasUv, 0.0).rgb;
            float gProg = grow * 1.05;
            float reveal = smoothstep(gProg, gProg - 0.06, ck.b);
            crack = ck.r * reveal * crackFade * inside;
            crackGlow = ck.g * reveal * crackFade * inside;
        }
    }

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
