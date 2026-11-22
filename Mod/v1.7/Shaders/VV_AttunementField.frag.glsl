#version 450

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.82, 0.4, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _SizeX;         // @default = 1.0
    float _SizeY;         // @default = 1.0
    float _OriginX;       // @default = 0.0
    float _OriginY;       // @default = 0.0
    float _AX;            // @default = 0.0
    float _AY;            // @default = 0.0
    float _BX;            // @default = 1.0
    float _BY;            // @default = 0.0
    float _CX;            // @default = 0.0
    float _CY;            // @default = 1.0
} u;

uint hashU(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    return (q.x ^ q.y) * 1597334673u;
}

float hashF(uint h, int shift)
{
    return float((h >> uint(shift)) & 1023u) / 1023.0;
}

float sdTriangle(vec2 p, vec2 p0, vec2 p1, vec2 p2)
{
    vec2 e0 = p1 - p0;
    vec2 e1 = p2 - p1;
    vec2 e2 = p0 - p2;
    vec2 v0 = p - p0;
    vec2 v1 = p - p1;
    vec2 v2 = p - p2;
    vec2 pq0 = v0 - e0 * clamp(dot(v0, e0) / max(dot(e0, e0), 1e-6), 0.0, 1.0);
    vec2 pq1 = v1 - e1 * clamp(dot(v1, e1) / max(dot(e1, e1), 1e-6), 0.0, 1.0);
    vec2 pq2 = v2 - e2 * clamp(dot(v2, e2) / max(dot(e2, e2), 1e-6), 0.0, 1.0);
    float s = sign(e0.x * e2.y - e0.y * e2.x);
    vec2 d = min(min(vec2(dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                     vec2(dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                     vec2(dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
    return -sqrt(d.x) * sign(d.y);
}

float sparkleLayer(vec2 pw, float scale, float t, int seed)
{
    vec2 g = pw * scale;
    ivec2 cell = ivec2(floor(g));
    vec2 f = fract(g);
    uint h = hashU(cell + ivec2(seed * 7919, seed * 104729));
    vec2 pos = vec2(hashF(h, 0), hashF(h, 10)) * 0.6 + 0.2;
    float phase = hashF(h, 20) * 6.2831853;
    float blink = pow(max(sin(t * (1.3 + hashF(h, 5)) + phase), 0.0), 6.0);
    float r = length(f - pos) / scale;
    return exp(-r / 0.05) * blink;
}

void main()
{
    vec2 p = vUv * vec2(u._SizeX, u._SizeY);
    vec2 pw = p + vec2(u._OriginX, u._OriginY);
    float t = u._AgeSecs;

    float d = sdTriangle(p, vec2(u._AX, u._AY), vec2(u._BX, u._BY), vec2(u._CX, u._CY));
    float px = max(fwidth(d), 1e-4);

    float inside = 1.0 - smoothstep(-px, px, d);
    float depth = clamp(-d / 1.6, 0.0, 1.0);

    float wave1 = sin(dot(pw, vec2(0.83, 0.56)) * 1.4 - t * 1.3);
    float wave2 = sin(dot(pw, vec2(-0.41, 0.91)) * 1.1 + t * 0.9);
    float wave3 = sin(dot(pw, vec2(0.97, -0.24)) * 0.7 - t * 0.6);
    float waves = (wave1 * wave2 + wave3) * 0.25 + 0.5;

    float sparkles = sparkleLayer(pw, 1.6, t, 1) + sparkleLayer(pw, 2.7, t * 1.3, 2) * 0.7;

    float edgeCore = exp(-abs(d) / 0.05);
    float edgeGlow = exp(-abs(d) / 0.22);
    float rim = smoothstep(0.0, 0.45, -d) * (1.0 - smoothstep(0.45, 1.4, -d));

    float fill = inside * (0.34 + 0.2 * waves * (0.5 + 0.5 * depth) + 0.16 * rim);
    float alpha = fill + inside * sparkles * 0.8 + edgeGlow * 0.45 + edgeCore * 0.6;
    alpha *= u._Color.a;

    vec3 gold = mix(u._Color.rgb, vec3(1.0, 0.95, 0.75), 0.15 + 0.3 * waves * inside);
    vec3 bright = vec3(1.0, 0.98, 0.9);
    float whiteMix = clamp(edgeCore * 0.85 + sparkles * 0.9, 0.0, 1.0);
    vec3 col = mix(gold, bright, whiteMix);

    outColor = vec4(col * (1.0 + 0.25 * inside), clamp(alpha, 0.0, 0.92));
}
