#version 450

// 아스포델 연결선: 두 가닥 나선을 따라 꽃잎이 흩날리며 흘러간다(가산 혼합).
// U는 길이 방향(a→b), V는 폭 방향이며 _Length·_Width는 칸 단위다. 꽃잎 그림은 _MainTex 한 장을 돌려 가며 찍는다.
// 나선은 위에서 내려다본 투영이라 앞쪽(cos θ > 0) 꽃잎은 크고 밝게, 뒤쪽은 작고 어둡게 그려 입체감을 낸다.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.9, 0.6, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _Length;        // @default = 10.0
    float _Width;         // @default = 1.2
    float _Seed;          // @default = 0.0
    float _FlowSpeed;     // @default = 1.1
    float _Wavelength;    // @default = 1.9
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float PI = 3.14159265;
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
    float L = u._Length;
    float W = u._Width;
    float x = vUv.x * L;
    float y = (vUv.y - 0.5) * W;
    float t = u._AgeSecs;

    // 화면 미분은 분기 전에 구한다. 꽃잎 UV는 이 좌표의 회전·축척이라 같은 변환으로 텍스처 기울기를 얻는다.
    vec2 dpdx = vec2(dFdx(x), dFdx(y));
    vec2 dpdy = vec2(dFdy(x), dFdy(y));
    float px = max(max(length(vec2(dpdx.x, dpdy.x)), length(vec2(dpdx.y, dpdy.y))), 1e-4);

    // 연한 연결선 색은 가산 혼합에서 흰색으로 바래므로 채도를 올려 쓴다(색상은 그대로).
    vec3 tint = pow(u._Color.rgb, vec3(1.7));
    float k = TAU / u._Wavelength;
    float radius = W * 0.22;

    // 나선 가닥 자국: 꽃잎이 지나는 길을 희미하게 이어 나선으로 읽히게 한다(앞쪽 반바퀴만 조금 더 밝다).
    vec3 col = vec3(0.0);
    float pinchX = smoothstep(0.0, 1.0, x) * smoothstep(0.0, 1.0, L - x);
    for (int s = 0; s < 2; s++)
    {
        float th = k * x + float(s) * PI;
        float yc = radius * pinchX * sin(th);
        float slope = radius * pinchX * k * cos(th);
        float dist = abs(y - yc) / sqrt(1.0 + slope * slope);
        float lineW = max(W * 0.012, px * 0.6);
        float front = cos(th) * 0.5 + 0.5;
        col += tint * exp(-(dist * dist) / (lineW * lineW)) * mix(0.04, 0.15, front);
    }

    // 꽃잎: 가닥마다 간격 d로 늘어선 꽃잎이 흐름 속도로 a→b를 따라 움직이며 자기 자리의 나선 위상을 탄다.
    float d = 0.28;
    float spriteSize = W * 0.38;
    int seed = int(u._Seed);
    float xs = x - t * u._FlowSpeed;
    int i0 = int(floor(xs / d));
    vec3 petals = vec3(0.0);
    for (int s = 0; s < 2; s++)
    {
        for (int n = -1; n <= 1; n++)
        {
            int i = i0 + n;
            uint h = hashU(ivec2(i, s * 7919 + seed * 131));
            float r1 = hashF(h, 0);
            float r2 = hashF(h, 10);
            float r3 = hashF(h, 20);

            float xc = (float(i) + 0.5 + (r1 - 0.5) * 0.3) * d + t * u._FlowSpeed;
            if (xc < 0.0 || xc > L) { continue; }

            // 바람결: 꽃잎보다 조금 빠르게 a→b로 지나가는 완만한 물결이 이웃 꽃잎을 함께 흔든다(꽃잎마다 따로 흔들지 않는다).
            float gust = sin(xc * 1.7 - t * 2.4);
            float theta = k * xc + float(s) * PI + (r2 - 0.5) * 0.25;
            float pinch = smoothstep(0.0, 1.0, xc) * smoothstep(0.0, 1.0, L - xc);
            float rad = radius * pinch * (0.94 + 0.12 * r3) * (1.0 + 0.1 * gust);
            vec2 c = vec2(xc, rad * sin(theta));
            float facing = cos(theta);
            float front = facing * 0.5 + 0.5;

            // 꽃잎 끝은 나선 접선(진행 방향)을 향하고 바람결에 살짝 들렸다 놓인다.
            // 나선 옆면을 지날 때는 비스듬히 보이므로 폭이 좁아진다.
            float slope = rad * k * cos(theta);
            float rot = atan(slope, 1.0) + gust * 0.22 + (r3 - 0.5) * 0.15;
            float flip = 0.35 + 0.65 * abs(facing);
            float size = spriteSize * mix(0.68, 1.08, front) * (0.9 + 0.2 * r2) * mix(0.4, 1.0, pinch);

            vec2 axisL = vec2(cos(rot), sin(rot));
            vec2 axisW = vec2(-axisL.y, axisL.x);
            vec2 rel = vec2(x, y) - c;
            float sw = (r1 > 0.5 ? 1.0 : -1.0) / (size * flip);
            vec2 uv = vec2(dot(rel, axisW) * sw, dot(rel, axisL) / size) + 0.5;
            if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) { continue; }

            vec2 gx = vec2(dot(dpdx, axisW) * sw, dot(dpdx, axisL) / size);
            vec2 gy = vec2(dot(dpdy, axisW) * sw, dot(dpdy, axisL) / size);
            vec3 tex = textureGrad(_MainTex, uv, gx, gy).rgb;
            float lum = dot(tex, vec3(0.299, 0.587, 0.114));

            vec3 petalCol = mix(tint, vec3(1.0), clamp(lum * lum * lum * lum * 0.35 + float(s) * 0.08, 0.0, 1.0));
            float bright = mix(0.15, 1.15, front) * smoothstep(0.0, 0.5, xc) * smoothstep(0.0, 0.5, L - xc);
            petals += petalCol * lum * bright;
        }
    }
    col += petals;

    float ends = smoothstep(0.0, 0.2, x) * smoothstep(0.0, 0.2, L - x);
    float edge = 1.0 - smoothstep(W * 0.4, W * 0.5, abs(y));
    col *= ends * edge * u._Color.a * vColor.rgb;
    outColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
