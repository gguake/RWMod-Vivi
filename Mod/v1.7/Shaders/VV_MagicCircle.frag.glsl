#version 450

// 바닐라 예고 연출처럼 굵은 선 몇 개로만 그려 멀리서도 읽히게 한다(가는 장식은 기본 줌에서 1px 아래로 뭉개진다).
// 모트는 Graphic_MoteWithAgeSecs가 _AgeSecs·_RandomPerObjectOffsetRandom을 넣어 주고, 모트 색과 페이드 투명도는 _Color로 온다.
// (_RandomPerObject는 모트 위치가 섞여 있어 움직이는 모트에서 매 틱 바뀌므로 꽃 방향 시드로 쓰지 않는다)
// _MainTex는 8잎 연꽃 2채널 SDF 텍스처(VFX/VV_MagicCircleLotusSdf: R=외곽, G=내부, 범위 [-1.15, 1.15], D_MAX=0.30)다.

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

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float PI = 3.14159265;
const float TAU = 6.28318531;
const float SDF_INV_SPAN = 1.0 / 2.3; // 범위 [-1.15, +1.15] -> 스팬 2.3
const float SDF_DMAX = 0.30;

// 정수 격자 해시. 실수 fract 해시는 좌표가 커지면 정밀도가 떨어져 무늬가 뭉개진다.
uint hashU(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    return (q.x ^ q.y) * 1597334673u;
}

float decodeSdf(float enc)
{
    return (enc - 128.0 / 255.0) * (255.0 / 127.0) * SDF_DMAX;
}

void lotus(vec2 p, float s, float angle, out float outer, out float inner)
{
    vec2 q0 = p / s;
    float c = cos(angle);
    float sn = sin(angle);
    vec2 q = vec2(q0.x * c + q0.y * sn, -q0.x * sn + q0.y * c);
    float rq = length(q);
    vec2 enc = textureLod(_MainTex, q * SDF_INV_SPAN + 0.5, 0.0).rg;
    outer = max(decodeSdf(enc.r), rq - 0.86) * s;
    inner = max(decodeSdf(enc.g), rq - 0.56) * s;
}

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
    float lw = max(u._LineScale, 0.1);

    float core = 0.0;
    float glow = 0.0;

    // 바깥 고리: 사출탄 폭발 범위
    float dRing = abs(r - 0.95);
    core += lineCore(dRing, 0.02 * lw, px) * u._RingAlpha * fadeIn;
    glow += lineGlow(dRing, 0.035 * lw) * 0.5 * u._RingAlpha * fadeIn;
    glow += 0.045 * smoothstep(0.96, 0.9, r) * u._RingAlpha * fadeIn;

    // 흐린 윤곽: 다 핀 꽃(발사 시점의 모양). 안쪽 겹에 가려진 바깥 꽃잎 선은 그리지 않는다.
    if (u._GuideAlpha > 0.001)
    {
        float gOuter, gInner;
        lotus(p, 1.0, angle, gOuter, gInner);
        float guide = lineCore(abs(gOuter), 0.011 * lw, px) * step(0.0, gInner) + lineCore(abs(gInner), 0.011 * lw, px);
        core += guide * 0.32 * u._GuideAlpha * fadeIn;
    }

    float bloom = smoothstep(0.0, 1.0, t);
    float s = mix(0.12, 1.0, bloom);
    float twist = (1.0 - bloom) * (PI / count) * 0.6;
    float bOuter, bInner;
    lotus(p, s, angle + twist, bOuter, bInner);
    float bloomAlpha = (0.55 + 0.45 * t) * fadeIn;
    float petalLine = lineCore(abs(bOuter), 0.016 * lw, px) * step(0.0, bInner) + lineCore(abs(bInner), 0.016 * lw, px);
    core += petalLine * 0.9 * bloomAlpha;
    glow += (lineGlow(abs(bOuter), 0.03 * lw) * 0.35 * step(0.0, bInner) + lineGlow(abs(bInner), 0.03 * lw) * 0.25) * bloomAlpha;
    float fillOuter = 1.0 - smoothstep(-px, px, bOuter);
    float fillInner = 1.0 - smoothstep(-px, px, bInner);
    glow += (fillOuter * 0.1 + fillInner * 0.08) * bloomAlpha;

    float dCenter = abs(r - 0.06 * lw);
    core += lineCore(dCenter, 0.012 * lw, px) * fadeIn;
    glow += exp(-r / 0.12) * u._CoreGlow * (0.35 + 0.65 * t) * fadeIn;

    float charge = 1.0 + 0.6 * smoothstep(0.85, 1.0, t);

    float pulse = 0.9 + 0.1 * sin(age * 2.4 + seed * TAU);

    vec3 color = (u._CoreColor.rgb * min(core, 1.5) + u._Color.rgb * glow) * charge * pulse * u._Brightness * u._Color.a;
    outColor = vec4(clamp(color, 0.0, 1.0), 1.0);
}
