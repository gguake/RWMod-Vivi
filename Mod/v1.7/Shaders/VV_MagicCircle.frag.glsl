#version 450

// 아포크리파 1페이즈 사출 포격의 조준 마법진(가산 혼합)을 텍스처 없이 수식으로 그린다.
// - 바깥 이중 고리, 천천히 도는 룬 띠(칸마다 정수 해시로 고른 획 몇 개로 만든 글자), 안쪽 고리, 반대로 도는 육망성, 중심 고리.
// - 생성 직후 _InscribeSecs 동안 고리와 룬이 위쪽부터 시계 방향으로 새겨지듯 나타난다.
// - _ShrinkRing > 0이면 조준 시간(_Duration) 동안 바깥 고리에서 중심으로 좁혀 드는 고리가 발사 시점을 알려 주고,
//   끝나기 직전에 마법진 전체가 밝아진다. _CoreGlow는 중심의 빛(시전 마법진은 충전될수록 밝아진다).
// 모트는 Graphic_MoteWithAgeSecs가 _AgeSecs·_RandomPerObjectOffsetRandom을 넣어 주고, 모트 색과 페이드 투명도는 _Color로 온다.
// (_RandomPerObject는 모트 위치가 섞여 있어 움직이는 모트에서 매 틱 바뀌므로 룬 무늬 시드로 쓰지 않는다)

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.84, 0.45, 1.0)
    vec4 _CoreColor;      // @color @default = vec4(1.0, 0.97, 0.86, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _RandomPerObjectOffsetRandom; // @default = 0.0
    float _Duration;      // @default = 4.0
    float _ShrinkRing;    // @default = 1.0
    float _RuneCount;     // @default = 24.0
    float _RotSpeed;      // @default = 0.3
    float _StarAlpha;     // @default = 1.0
    float _CoreGlow;      // @default = 0.15
    float _InscribeSecs;  // @default = 0.6
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

float hash01(ivec2 i)
{
    return float(hashU(i)) * (1.0 / 4294967295.0);
}

float sdSegment(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
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

// 위쪽(12시)에서 시계 방향으로 잰 각도 비율(0~1)
float clockwiseFraction(vec2 p)
{
    return fract(0.25 - atan(p.y, p.x) / TAU);
}

// 룬 띠 칸 하나의 글자. local은 칸 중심 기준 물리 좌표, halfSize는 글자 반폭.
// 등뼈 획 하나(세로 또는 대각선)에 나머지 획을 해시 비트로 골라 더한다.
float runeGlyph(vec2 local, float halfSize, uint h, float halfWidth, float px, out float glowDist)
{
    vec2 s = vec2(halfSize);
    float d = 1e3;

    uint spine = h & 3u;
    if (spine == 0u || spine == 3u) { d = min(d, sdSegment(local, vec2(0.0, -s.y), vec2(0.0, s.y))); }
    else if (spine == 1u) { d = min(d, sdSegment(local, vec2(-s.x, -s.y), vec2(s.x, s.y))); }
    else { d = min(d, sdSegment(local, vec2(-s.x, s.y), vec2(s.x, -s.y))); }

    if (((h >> 2u) & 3u) == 0u) { d = min(d, sdSegment(local, vec2(-s.x, s.y), vec2(s.x, s.y))); }
    if (((h >> 4u) & 3u) == 0u) { d = min(d, sdSegment(local, vec2(-s.x, 0.0), vec2(s.x, 0.0))); }
    if (((h >> 6u) & 3u) == 0u) { d = min(d, sdSegment(local, vec2(-s.x, -s.y), vec2(s.x, -s.y))); }
    if (((h >> 8u) & 3u) == 0u) { d = min(d, sdSegment(local, vec2(-s.x, -s.y), vec2(-s.x, s.y))); }
    if (((h >> 10u) & 3u) == 0u) { d = min(d, sdSegment(local, vec2(s.x, -s.y), vec2(s.x, s.y))); }
    if (((h >> 12u) & 3u) == 0u) { d = min(d, sdSegment(local, vec2(0.0, s.y), vec2(s.x, 0.0))); }
    if (((h >> 14u) & 3u) == 0u) { d = min(d, sdSegment(local, vec2(-s.x, 0.0), vec2(0.0, -s.y))); }
    if (((h >> 16u) & 7u) == 0u) { d = min(d, length(local - vec2(s.x * 0.55, s.y * 0.55)) - halfWidth * 0.6); }

    glowDist = max(d, 0.0);
    return lineCore(d, halfWidth, px);
}

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p);
    float px = max(fwidth(r), 1e-4);

    if (r > 1.0) { discard; }

    float seed = float(hashU(ivec2(floatBitsToInt(u._RandomPerObjectOffsetRandom), 0x2c1b3c6d)) & 1023u);
    float age = u._AgeSecs;
    float inscribe = clamp(age / max(u._InscribeSecs, 0.001), 0.0, 1.0);
    float sweep = clockwiseFraction(p);
    float drawn = smoothstep(0.0, 0.04, inscribe * 1.04 - sweep);

    float t = u._Duration > 0.0 ? clamp(age / u._Duration, 0.0, 1.0) : 0.0;
    float rot = age * u._RotSpeed + seed * 0.37;
    float lw = max(u._LineScale, 0.1);   // 선 굵기 배율(작게 그리는 시전 마법진은 굵게)

    float core = 0.0;   // 흰금빛 심
    float glow = 0.0;   // 금빛 둘레 빛

    // 바깥 이중 고리
    float dOuter = abs(r - 0.95);
    float dSecond = abs(r - 0.885);
    core += lineCore(dOuter, 0.011 * lw, px) * drawn;
    core += lineCore(dSecond, 0.005 * lw, px) * 0.8 * drawn;
    glow += (lineGlow(dOuter, 0.025 * lw) * 0.55 + lineGlow(dSecond, 0.02 * lw) * 0.25) * drawn;

    // 바깥 고리 위 네 방향의 작은 보석 고리(마법진과 함께 돈다)
    for (int k = 0; k < 4; k++)
    {
        float a = rot * 0.5 + float(k) * (PI * 0.5);
        vec2 node = 0.9175 * vec2(cos(a), sin(a));
        float dn = abs(length(p - node) - 0.03);
        core += lineCore(dn, 0.006 * lw, px) * drawn;
        glow += lineGlow(dn, 0.015 * lw) * 0.3 * drawn;
    }

    // 룬 띠: 반지름 0.755~0.845 사이를 _RuneCount칸으로 나눠 칸마다 글자 하나
    float bandCenter = 0.80;
    float bandHalf = 0.045;
    if (abs(r - bandCenter) < bandHalf + 0.03)
    {
        float runeCount = max(u._RuneCount, 3.0);
        float ang = atan(p.y, p.x) + rot;
        float f = fract(ang / TAU) * runeCount;
        float cell = floor(f);
        float cellWidth = TAU * r / runeCount;
        vec2 local = vec2((fract(f) - 0.5) * cellWidth, r - bandCenter);
        uint h = hashU(ivec2(int(cell) + int(seed) * 131, 0x68e31da4));

        float glyphGlow;
        float glyph = runeGlyph(local, min(bandHalf * 0.72, cellWidth * 0.3), h, 0.0055 * lw, px, glyphGlow);

        // 글자는 칸 순서대로(시계 방향) 새겨진다.
        float cellDrawn = smoothstep(0.0, 0.06, inscribe * 1.1 - sweep);
        core += glyph * 0.85 * cellDrawn;
        glow += lineGlow(glyphGlow, 0.012 * lw) * 0.22 * cellDrawn;

        // 칸 사이의 작은 점
        float dDot = length(vec2((fract(f) < 0.5 ? fract(f) : fract(f) - 1.0) * cellWidth, r - bandCenter));
        core += lineCore(dDot, 0.006 * lw, px) * 0.6 * cellDrawn;
    }

    // 안쪽 고리
    float dInner = abs(r - 0.715);
    core += lineCore(dInner, 0.0065 * lw, px) * drawn;
    glow += lineGlow(dInner, 0.02 * lw) * 0.3 * drawn;

    // 육망성: 반지름 0.70 원에 내접한 정삼각형 두 개 = 중심에서 0.35 떨어진 현 여섯 개(원 안에서 잘린 직선이 곧 삼각형 변)
    if (u._StarAlpha > 0.0 && r < 0.70)
    {
        float starRot = -rot * 0.6;
        float dStar = 1e3;
        for (int k = 0; k < 6; k++)
        {
            float a = starRot + float(k) * (PI / 3.0);
            dStar = min(dStar, abs(dot(p, vec2(cos(a), sin(a))) - 0.35));
        }
        float starDrawn = smoothstep(0.0, 0.25, inscribe - 0.35) * u._StarAlpha;
        core += lineCore(dStar, 0.005 * lw, px) * 0.65 * starDrawn;
        glow += lineGlow(dStar, 0.018 * lw) * 0.25 * starDrawn;
    }

    // 중심 고리와 중심의 빛
    float dCenter = abs(r - 0.16);
    core += lineCore(dCenter, 0.006 * lw, px) * drawn;
    glow += lineGlow(dCenter, 0.02 * lw) * 0.3 * drawn;
    glow += exp(-r / 0.12) * u._CoreGlow * (0.35 + 0.65 * t) * inscribe;

    // 마법진 안쪽을 은은하게 채워 범위를 알린다.
    glow += 0.05 * smoothstep(0.96, 0.9, r) * drawn;

    // 좁혀 드는 고리: 바깥 고리에서 중심으로 줄어들며 점점 밝아진다(발사 시점).
    if (u._ShrinkRing > 0.0 && u._Duration > 0.0)
    {
        float shrinkR = mix(0.95, 0.12, t);
        float dShrink = abs(r - shrinkR);
        float shrinkAlpha = (0.45 + 0.55 * t) * smoothstep(0.0, 0.08, t) * smoothstep(0.6, 1.0, inscribe) * u._ShrinkRing;
        core += lineCore(dShrink, 0.009 * lw, px) * shrinkAlpha;
        glow += lineGlow(dShrink, 0.03 * lw) * 0.5 * shrinkAlpha;
    }

    // 발사 직전 전체가 밝아진다.
    float charge = 1.0 + 0.6 * smoothstep(0.8, 1.0, t);

    // 아주 느린 맥동
    float pulse = 0.88 + 0.12 * sin(age * 3.0 + seed);

    vec3 color = (u._CoreColor.rgb * min(core, 1.5) + u._Color.rgb * glow) * charge * pulse * u._Brightness * u._Color.a;
    outColor = vec4(clamp(color, 0.0, 1.0), 1.0);
}
