#version 450

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _CoreColor;      // @color @default = vec4(1.0, 0.93, 0.66, 1.0)
    vec4 _GlowColor;      // @color @default = vec4(1.0, 0.58, 0.12, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _RandomPerObject; // @default = 0.0
    float _Duration;      // @default = 1.85
    float _Width;         // @default = 0.005
    float _Brightness;    // @default = 1.0
    float _LeafCount;     // @default = 14.0
    float _LeafSize;      // @default = 0.08
    float _EmberCount;    // @default = 56.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float TAU = 6.28318531;

// 정수 격자 해시. 실수 fract 해시는 좌표가 커지면 정밀도가 떨어져 무늬가 뭉개진다.
uint hashU(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    return (q.x ^ q.y) * 1597334673u;
}

float hashF(uint h, int shift)
{
    return float((h >> uint(shift)) & 1023u) / 1023.0;
}

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p);

    // 화면 미분은 분기 전에 구한다. px: 화면 1픽셀이 반지름 몇 단위인지.
    vec2 dpdx = dFdx(p);
    vec2 dpdy = dFdy(p);
    float px = max(max(length(dpdx), length(dpdy)), 1e-5);

    float t = clamp(u._AgeSecs / max(u._Duration, 0.001), 0.0, 1.0);
    float front = mix(0.033, 0.89, t);
    float fade = u._Color.a * smoothstep(0.0, 0.03, t) * (1.0 - smoothstep(0.4, 1.0, t)) * (1.0 - smoothstep(0.94, 1.0, r));
    float band = max(u._LeafSize, 0.05);
    if (r > 1.0 || fade <= 0.001 || abs(r - front) > band) { discard; }

    int seed = int(hashU(ivec2(floatBitsToInt(u._RandomPerObject), 0x2c1b3c6d)) & 4095u);
    float theta = atan(p.y, p.x);
    float d = r - front;

    float w = max(u._Width, px * 0.9);
    float core = exp(-(d * d) / (w * w));
    float win = 1.0 - smoothstep(band * 0.6, band, abs(d));
    float halo = (exp(-(d * d) / (16.0 * w * w)) * 0.6 + exp(-abs(d) / (10.0 * w)) * 0.25) * win;

    float wisps = 0.0;
    for (int s = 0; s < 2; s++)
    {
        float ph = float(seed) * 0.11 + float(s) * 2.3;
        float rw = front + 0.012 * sin(theta * (5.0 + float(s) * 2.0) + ph + u._AgeSecs * 1.5) * (s == 0 ? 1.0 : -1.0);
        float dw = r - rw;
        float ww = max(w * 0.45, px * 0.7);
        float seg = smoothstep(0.35, 0.85, sin(theta * (3.0 + float(s)) + ph * 1.7));
        wisps += (exp(-(dw * dw) / (ww * ww)) + exp(-(dw * dw) / (9.0 * ww * ww)) * 0.4) * seg;
    }
    wisps *= win;

    vec3 leaves = vec3(0.0);
    float n = max(floor(u._LeafCount + 0.5), 3.0);
    float sector = TAU / n;
    int i0 = int(floor(theta / sector));
    for (int k = -1; k <= 1; k++)
    {
        int i = i0 + k;
        int slot = ((i % int(n)) + int(n)) % int(n);
        uint h = hashU(ivec2(slot, seed * 131));
        float r1 = hashF(h, 0);
        float r2 = hashF(h, 10);
        float r3 = hashF(h, 20);

        float ac = (float(i) + 0.5 + (r1 - 0.5) * 0.6) * sector;
        vec2 radial = vec2(cos(ac), sin(ac));
        vec2 tangent = vec2(-radial.y, radial.x) * (r2 > 0.5 ? 1.0 : -1.0);
        vec2 c = radial * (front + (r3 - 0.5) * u._LeafSize * 0.6);
        float tilt = 0.5 + 0.4 * r1;
        vec2 axisL = tangent * cos(tilt) + radial * sin(tilt);
        vec2 axisW = vec2(-axisL.y, axisL.x);
        
        float size = u._LeafSize * (0.7 + 0.5 * r3) * mix(0.3, 1.0, smoothstep(0.03, 0.5, front));
        if (size <= 0.0) { continue; }

        vec2 rel = p - c;
        vec2 uv = vec2(dot(rel, axisW) / size, dot(rel, axisL) / size) + 0.5;
        if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) { continue; }

        vec2 gx = vec2(dot(dpdx, axisW), dot(dpdx, axisL)) / size;
        vec2 gy = vec2(dot(dpdy, axisW), dot(dpdy, axisL)) / size;
        vec3 tex = textureGrad(_MainTex, uv, gx, gy).rgb;
        float lum = dot(tex, vec3(0.299, 0.587, 0.114));
        leaves += mix(u._GlowColor.rgb, u._CoreColor.rgb, clamp(lum * lum * 1.4, 0.0, 1.0)) * lum * (1.0 + 0.5 * r2);
    }

    float embers = 0.0;
    float m = max(floor(u._EmberCount + 0.5), 3.0);
    float esector = TAU / m;
    int j0 = int(floor(theta / esector));
    for (int k = -1; k <= 1; k++)
    {
        int j = j0 + k;
        int slot = ((j % int(m)) + int(m)) % int(m);
        uint h = hashU(ivec2(slot, seed * 131 + 7919));
        float ac = (float(j) + hashF(h, 0)) * esector;
        vec2 c = vec2(cos(ac), sin(ac)) * (front + (hashF(h, 10) - 0.5) * band * 1.4);
        float size = max(0.004 * (0.6 + 0.8 * hashF(h, 20)), px * 1.2);
        vec2 q = (p - c) / size;
        float tw = 0.55 + 0.45 * sin(u._AgeSecs * 8.0 + hashF(h, 5) * TAU);
        embers += exp(-dot(q, q)) * tw;
    }
    embers *= win;

    vec3 col = u._CoreColor.rgb * (core * 1.2 + embers * 0.9)
             + u._GlowColor.rgb * (halo + wisps * 0.45)
             + leaves;
    col *= fade * u._Brightness * u._Color.rgb;
    outColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
