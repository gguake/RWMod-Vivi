#version 450

// 아포크리파 1페이즈 사출 포격의 조준 표식(가산 혼합)을 텍스처 없이 수식으로 그린다. 요정꽃처럼 끝이 뾰족한 연꽃 꽃잎 모양이다.
// - 바깥 고리(폭발 범위)와 흐린 꽃 윤곽(다 핀 모양)이 먼저 나타나고, 밝은 꽃이 중심에서 피어나 _Duration 동안 윤곽까지 커진다.
//   밝은 꽃이 윤곽과 겹치는 순간이 발사 시점이고, 피는 동안 꽃잎이 살짝 비틀려 있다가 다 피면 윤곽과 정확히 맞는다.
// - 끝나기 직전에 전체가 밝아지고, _CoreGlow는 중심의 빛(시전 표식은 충전될수록 밝아진다).
// 바닐라 예고 연출처럼 굵은 선 몇 개로만 그려 멀리서도 읽히게 한다(가는 장식은 기본 줌에서 1px 아래로 뭉개진다).
// 모트는 Graphic_MoteWithAgeSecs가 _AgeSecs·_RandomPerObjectOffsetRandom을 넣어 주고, 모트 색과 페이드 투명도는 _Color로 온다.
// (_RandomPerObject는 모트 위치가 섞여 있어 움직이는 모트에서 매 틱 바뀌므로 꽃 방향 시드로 쓰지 않는다)

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.84, 0.45, 1.0)
    vec4 _CoreColor;      // @color @default = vec4(1.0, 0.97, 0.86, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _RandomPerObjectOffsetRandom; // @default = 0.0
    float _Duration;      // @default = 4.0
    float _PetalCount;    // @default = 8.0
    float _RingAlpha;     // @default = 1.0
    float _GuideAlpha;    // @default = 1.0
    float _CoreGlow;      // @default = 0.15
    float _FadeInSecs;    // @default = 0.3
    float _LineScale;     // @default = 1.0
    float _Brightness;    // @default = 1.0
} u;

const float PI = 3.14159265;
const float TAU = 6.28318531;

// 정수 격자 해시. 실수 fract 해시는 좌표가 커지면 정밀도가 떨어져 무늬가 뭉개진다.
uint hashU(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    return (q.x ^ q.y) * 1597334673u;
}

// 세로 방향 렌즈(양 끝이 뾰족한 꽃잎) 거리장. 반지름 r인 두 원이 중심 간격 2d로 겹친 모양(iq sdVesica).
float sdVesica(vec2 p, float r, float d)
{
    p = abs(p);
    float b = sqrt(r * r - d * d);
    return ((p.y - b) * d > p.x * b) ? length(p - vec2(0.0, b)) : length(p - vec2(-d, 0.0)) - r;
}

// 중심에서 뻗는 꽃잎 count장의 합집합 거리장. 꽃잎 하나는 반지름 r0~r1, 반폭 w.
// 가장 가까운 꽃잎과 양옆 꽃잎만 보면 겹치는 부분까지 정확하다.
float sdPetals(vec2 p, float count, float angle, float r0, float r1, float w)
{
    float h = (r1 - r0) * 0.5;
    float vr = (h * h / w + w) * 0.5;
    float vd = (h * h / w - w) * 0.5;
    float sector = TAU / count;
    float rr = length(p);
    float a = atan(p.y, p.x) - angle;
    a = mod(a + sector * 0.5, sector) - sector * 0.5;

    float d = 1e3;
    for (int k = -1; k <= 1; k++)
    {
        float ak = a + float(k) * sector;
        vec2 q = vec2(rr * sin(ak), rr * cos(ak) - (r0 + h));
        d = min(d, sdVesica(q, vr, vd));
    }
    return d;
}

// 연꽃 한 송이: 바깥 꽃잎 count장과 그 사이로 엇갈린 안쪽 꽃잎 count장. 크기 s로 줄여 그릴 수 있다.
// outer/inner는 각 겹의 거리장(안쪽 겹이 위에 겹친다).
void lotus(vec2 p, float s, float count, float angle, out float outer, out float inner)
{
    vec2 q = p / s;
    outer = sdPetals(q, count, angle, 0.10, 0.86, 0.15) * s;
    inner = sdPetals(q, count, angle + PI / count, 0.08, 0.56, 0.11) * s;
}

// 선의 선명한 심(픽셀 크기 px로 앤티에일리어싱)과 둘레의 빛
float lineCore(float d, float halfWidth, float px)
{
    return 1.0 - smoothstep(halfWidth - px * 0.5, halfWidth + px, d);
}

float lineGlow(float d, float width)
{
    return exp(-d / width);
}

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p);
    float px = max(fwidth(r), 1e-4);

    if (r > 1.0) { discard; }

    float seed = float(hashU(ivec2(floatBitsToInt(u._RandomPerObjectOffsetRandom), 0x2c1b3c6d)) & 1023u) / 1023.0;
    float age = u._AgeSecs;
    float fadeIn = smoothstep(0.0, 1.0, age / max(u._FadeInSecs, 0.001));
    float t = u._Duration > 0.0 ? clamp(age / u._Duration, 0.0, 1.0) : 1.0;
    float count = max(floor(u._PetalCount + 0.5), 3.0);
    float angle = seed * TAU;
    float lw = max(u._LineScale, 0.1);   // 선 굵기 배율(작게 그리는 시전 표식은 굵게)

    float core = 0.0;   // 흰금빛 심
    float glow = 0.0;   // 금빛 둘레 빛

    // 바깥 고리: 사출탄 폭발 범위
    float dRing = abs(r - 0.95);
    core += lineCore(dRing, 0.02 * lw, px) * u._RingAlpha * fadeIn;
    glow += lineGlow(dRing, 0.035 * lw) * 0.5 * u._RingAlpha * fadeIn;
    glow += 0.045 * smoothstep(0.96, 0.9, r) * u._RingAlpha * fadeIn;

    // 흐린 윤곽: 다 핀 꽃(발사 시점의 모양). 안쪽 겹에 가려진 바깥 꽃잎 선은 그리지 않는다.
    float gOuter, gInner;
    lotus(p, 1.0, count, angle, gOuter, gInner);
    float guide = lineCore(abs(gOuter), 0.011 * lw, px) * step(0.0, gInner) + lineCore(abs(gInner), 0.011 * lw, px);
    core += guide * 0.32 * u._GuideAlpha * fadeIn;

    // 피어나는 꽃: 중심에서 윤곽 크기까지 자라며, 덜 핀 동안은 꽃잎이 비틀려 있다.
    float bloom = smoothstep(0.0, 1.0, t);
    float s = mix(0.12, 1.0, bloom);
    float twist = (1.0 - bloom) * (PI / count) * 0.6;
    float bOuter, bInner;
    lotus(p, s, count, angle + twist, bOuter, bInner);
    float bloomAlpha = (0.55 + 0.45 * t) * fadeIn;
    float petalLine = lineCore(abs(bOuter), 0.016 * lw, px) * step(0.0, bInner) + lineCore(abs(bInner), 0.016 * lw, px);
    core += petalLine * 0.9 * bloomAlpha;
    glow += (lineGlow(abs(bOuter), 0.03 * lw) * 0.35 * step(0.0, bInner) + lineGlow(abs(bInner), 0.03 * lw) * 0.25) * bloomAlpha;
    // 꽃잎 안을 은은하게 채운다(안쪽 겹이 조금 더 밝다).
    float fillOuter = 1.0 - smoothstep(-px, px, bOuter);
    float fillInner = 1.0 - smoothstep(-px, px, bInner);
    glow += (fillOuter * 0.1 + fillInner * 0.08) * bloomAlpha;

    // 꽃술과 중심의 빛
    float dCenter = abs(r - 0.06 * lw);
    core += lineCore(dCenter, 0.012 * lw, px) * fadeIn;
    glow += exp(-r / 0.12) * u._CoreGlow * (0.35 + 0.65 * t) * fadeIn;

    // 발사 직전 전체가 밝아진다.
    float charge = 1.0 + 0.6 * smoothstep(0.85, 1.0, t);

    // 아주 느린 맥동
    float pulse = 0.9 + 0.1 * sin(age * 2.4 + seed * TAU);

    vec3 color = (u._CoreColor.rgb * min(core, 1.5) + u._Color.rgb * glow) * charge * pulse * u._Brightness * u._Color.a;
    outColor = vec4(clamp(color, 0.0, 1.0), 1.0);
}
