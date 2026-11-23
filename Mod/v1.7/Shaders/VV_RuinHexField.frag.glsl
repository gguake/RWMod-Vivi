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
    float _Count;         // @default = 3.0
    float _Whiten;        // @default = 0.0
    float _P0X;           // @default = 0.0
    float _P0Y;           // @default = 0.0
    float _P1X;           // @default = 1.0
    float _P1Y;           // @default = 0.0
    float _P2X;           // @default = 0.0
    float _P2Y;           // @default = 1.0
    float _P3X;           // @default = 0.0
    float _P3Y;           // @default = 0.0
    float _P4X;           // @default = 0.0
    float _P4Y;           // @default = 0.0
    float _P5X;           // @default = 0.0
    float _P5Y;           // @default = 0.0
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

float sdPolygon(vec2 p, vec2 v[6], int n)
{
    float d = dot(p - v[0], p - v[0]);
    float s = 1.0;
    for (int i = 0, j = n - 1; i < n; j = i, i++)
    {
        vec2 e = v[j] - v[i];
        vec2 w = p - v[i];
        vec2 b = w - e * clamp(dot(w, e) / max(dot(e, e), 1e-6), 0.0, 1.0);
        d = min(d, dot(b, b));
        bvec3 c = bvec3(p.y >= v[i].y, p.y < v[j].y, e.x * w.y > e.y * w.x);
        if (all(c) || all(not(c)))
            s = -s;
    }
    return s * sqrt(d);
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
    return exp(-r / 0.085) * blink;
}

void main()
{
    vec2 p = vUv * vec2(u._SizeX, u._SizeY);
    vec2 pw = p + vec2(u._OriginX, u._OriginY);
    float t = u._AgeSecs;

    vec2 v[6] = vec2[6](
        vec2(u._P0X, u._P0Y), vec2(u._P1X, u._P1Y), vec2(u._P2X, u._P2Y),
        vec2(u._P3X, u._P3Y), vec2(u._P4X, u._P4Y), vec2(u._P5X, u._P5Y));
    float d = sdPolygon(p, v, int(clamp(u._Count, 3.0, 6.0) + 0.5));
    float px = max(fwidth(d), 1e-4);

    float inside = 1.0 - smoothstep(-px, px, d);
    float depth = clamp(-d / 1.6, 0.0, 1.0);
    float whiten = clamp(u._Whiten, 0.0, 1.0);

    float wave1 = sin(dot(pw, vec2(0.83, 0.56)) * 1.4 - t * 1.3);
    float wave2 = sin(dot(pw, vec2(-0.41, 0.91)) * 1.1 + t * 0.9);
    float wave3 = sin(dot(pw, vec2(0.97, -0.24)) * 0.7 - t * 0.6);
    float waves = (wave1 * wave2 + wave3) * 0.25 + 0.5;

    float sparkles = sparkleLayer(pw, 1.05, t, 1) + sparkleLayer(pw, 1.85, t * 1.3, 2) * 0.75;

    float edgeCore = exp(-abs(d) / 0.05);
    float edgeGlow = exp(-abs(d) / 0.22);
    float rim = smoothstep(0.0, 0.45, -d) * (1.0 - smoothstep(0.45, 1.4, -d));

    float fill = inside * (0.34 + 0.2 * waves * (0.5 + 0.5 * depth) + 0.16 * rim);
    fill = mix(fill, inside * (0.62 + 0.14 * waves + 0.1 * rim), whiten);
    float alpha = fill + inside * sparkles * 0.8 + edgeGlow * 0.45 + edgeCore * 0.6;
    alpha *= u._Color.a;

    vec3 highlight = mix(u._Color.rgb, vec3(1.0, 0.98, 0.92), 0.65);
    vec3 baseCol = mix(u._Color.rgb, highlight, 0.15 + 0.3 * waves * inside);
    vec3 bright = mix(u._Color.rgb, vec3(1.0, 0.99, 0.95), 0.82);
    vec3 white = mix(vec3(1.0, 0.99, 0.95), vec3(1.0), waves);
    float whiteMix = clamp(edgeCore * 0.85 + sparkles * 0.9, 0.0, 1.0);
    vec3 col = mix(mix(baseCol, white, whiten * inside), bright, whiteMix);

    outColor = vec4(col * (1.0 + 0.25 * inside), clamp(alpha, 0.0, 0.92));
}
