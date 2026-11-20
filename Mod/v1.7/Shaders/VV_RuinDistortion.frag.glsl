#version 450

// 거대 육각형 고대 유적 공간 왜곡 결계 셰이더 (비비 풍: 은은하고 흐릿한 고대 영원꽃 신기루 결계)
// - 기존 B안의 고대 영원꽃(연꽃) 문양·햇살 물결빛(caustics)·꿀빛 아지랑이 경계·흩날리는 꽃잎/꽃가루 감성을 유지하되,
//   전체 농도와 경계 광휘를 절반 이하로 부드럽게 낮춰 지형 위로 아련하게 스며들도록 조율했다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;        // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _WarmColor;    // @color @default = vec4(1.0, 0.89, 0.56, 1.0)
    vec4 _BlossomColor; // @color @default = vec4(1.0, 0.78, 0.85, 1.0)
    vec4 _CreamColor;   // @color @default = vec4(1.0, 0.97, 0.88, 1.0)
    vec4 _BoundsXZ;     // @default = vec4(110.5, 110.5, 156.0, 140.0)
    vec4 _SealState;    // @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _AgeSecs;     // @default = 0.0
    float _Distortion;  // @default = 13.5
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _GrabDistortionBackground;

const float PI = 3.14159265;
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

float hash(ivec2 i)
{
    return float(hashU(i) >> 8) * (1.0 / 16777215.0);
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
    float amp = 0.52;
    for (int k = 0; k < 4; k++)
    {
        sum += amp * vnoise(p);
        p = p * 2.03 + vec2(13.7, 7.9);
        amp *= 0.48;
    }
    return sum;
}

float sdBox(vec2 p, vec2 b)
{
    vec2 d = abs(p) - b;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

float sdFlatHexagon(vec2 p, float r)
{
    const vec3 k = vec3(-0.866025404, 0.5, 0.577350269);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= vec2(clamp(p.x, -k.z * r, k.z * r), r);
    return length(p) * sign(p.y);
}

// 연꽃/영원꽃 꽃잎 거리장 (VV_MagicCircle 문법 통일)
float sdVesica(vec2 p, float r, float d)
{
    p = abs(p);
    float b = sqrt(max(r * r - d * d, 1e-6));
    return ((p.y - b) * d > p.x * b) ? length(p - vec2(0.0, b)) : length(p - vec2(-d, 0.0)) - r;
}

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

// 중첩 SDF: 상위 봉인이 닫혀 있으면 하위 경계선이 내부에 드러나지 않는다.
float sdStage4(vec2 rel) { return length(rel) - 8.65; }
float sdStage3(vec2 rel) { return length(rel) - 22.7; }
float sdStage2(vec2 rel) { return min(sdBox(rel - vec2(0.0, 35.7), vec2(10.4, 15.8)), length(rel) - 22.7); }
float sdStage1(vec2 rel) { return sdFlatHexagon(rel, 58.5); }

float stageSoftMask(float sd, float state, float wobble, float feather)
{
    if (state <= 0.001) { return 0.0; }
    float shrink = (1.0 - state) * 16.0;
    float d = sd + shrink + wobble;
    return smoothstep(feather, -feather * 0.85, d) * smoothstep(0.0, 0.16, state);
}

// 봄바람에 흩날리는 영원꽃 꽃잎 레이어 (_MainTex = VFX/VV_EverflowerPetal)
vec3 sampleDriftingPetals(vec2 rel, float t, float cellSize, float density, float petalScale, vec2 wind, int layerSeed, vec2 dRelDx, vec2 dRelDy)
{
    vec2 p = rel - wind * t;
    ivec2 cell = ivec2(floor(p / cellSize));
    vec3 acc = vec3(0.0);

    for (int oy = -1; oy <= 1; oy++)
    {
        for (int ox = -1; ox <= 1; ox++)
        {
            ivec2 id = cell + ivec2(ox, oy);
            uint h = hashU(id + ivec2(layerSeed * 193, layerSeed * 977));
            float chance = hashF(h, 0);
            if (chance > density) { continue; }

            float jx = hashF(h, 10);
            float jy = hashF(h, 20);
            uint h2 = hashU(id * 3 + ivec2(71 + layerSeed, 29));
            float phase = hashF(h2, 0) * TAU;
            float sizeVar = mix(0.68, 1.32, hashF(h2, 10));
            float pinkMix = hashF(h2, 20);

            vec2 center = (vec2(id) + vec2(0.18 + 0.64 * jx, 0.18 + 0.64 * jy)) * cellSize;
            center += vec2(sin(t * 0.95 + phase) * 0.72, cos(t * 0.78 + phase * 1.3) * 0.65);

            vec2 delta = p - center;
            float sz = petalScale * sizeVar;
            if (abs(delta.x) > sz * 0.75 || abs(delta.y) > sz * 0.75) { continue; }

            float ang = phase + sin(t * 1.15 + phase) * 0.85 + t * mix(-0.35, 0.35, jx);
            float ca = cos(ang);
            float sa = sin(ang);
            vec2 rot = vec2(delta.x * ca - delta.y * sa, delta.x * sa + delta.y * ca);
            float flutter = 0.62 + 0.38 * abs(cos(t * 1.6 + phase));
            vec2 local = vec2(rot.x / max(flutter, 0.35), rot.y) / sz;
            vec2 uv = local + 0.5;
            if (uv.x < 0.02 || uv.x > 0.98 || uv.y < 0.02 || uv.y > 0.98) { continue; }

            vec2 duvx = vec2(dRelDx.x * ca - dRelDx.y * sa, dRelDx.x * sa + dRelDx.y * ca) / sz;
            vec2 duvy = vec2(dRelDy.x * ca - dRelDy.y * sa, dRelDy.x * sa + dRelDy.y * ca) / sz;
            vec4 tex = textureGrad(_MainTex, vec2(uv.x, 1.0 - uv.y), duvx, duvy);

            float pulse = 0.55 + 0.45 * sin(t * 1.4 + phase * 2.0);
            vec3 tint = mix(u._CreamColor.rgb, mix(u._WarmColor.rgb, u._BlossomColor.rgb, smoothstep(0.35, 0.9, pinkMix)), 0.72);
            acc += tex.rgb * tex.a * tint * pulse;
        }
    }
    return acc;
}

// 햇살 속 반딧불이처럼 은은히 반짝이는 황금 꽃가루 알갱이
vec3 samplePollenSpecks(vec2 rel, float t, float cellSize, float density, float radiusCells, int layerSeed)
{
    vec2 drift = vec2(-0.22, 0.36) * t;
    vec2 p = rel - drift;
    ivec2 cell = ivec2(floor(p / cellSize));
    vec3 acc = vec3(0.0);

    for (int oy = -1; oy <= 1; oy++)
    {
        for (int ox = -1; ox <= 1; ox++)
        {
            ivec2 id = cell + ivec2(ox, oy);
            uint h = hashU(id + ivec2(layerSeed * 521, layerSeed * 173));
            if (hashF(h, 0) > density) { continue; }

            float jx = hashF(h, 10);
            float jy = hashF(h, 20);
            float phase = hashF(hashU(id + ivec2(19, 83)), 0) * TAU;
            float pink = hashF(hashU(id + ivec2(41, 13)), 10);

            vec2 center = (vec2(id) + vec2(0.15 + 0.70 * jx, 0.15 + 0.70 * jy)) * cellSize;
            center += vec2(sin(t * 0.65 + phase) * 0.55, cos(t * 0.55 + phase * 1.4) * 0.55);

            float d = length(p - center);
            float core = exp(-(d * d) / (radiusCells * radiusCells));
            float halo = exp(-(d * d) / (radiusCells * radiusCells * 7.5)) * 0.35;
            float twinkle = pow(0.5 + 0.5 * sin(t * (1.35 + 0.9 * jx) + phase), 1.8);
            vec3 col = mix(u._CreamColor.rgb, mix(u._WarmColor.rgb, u._BlossomColor.rgb, step(0.72, pink)), 0.65);
            acc += col * (core + halo) * twinkle;
        }
    }
    return acc;
}

void main()
{
    vec2 rel = (vUv - 0.5) * u._BoundsXZ.zw;

    // 화면 미분은 discard/분기 전에 계산한다.
    vec2 dRelDx = dFdx(rel);
    vec2 dRelDy = dFdy(rel);
    float pxPerCell = 1.0 / max(length(dRelDx), 1e-4);

    float t = u._AgeSecs;

    // 유기적인 아지랑이 흐름 노이즈 (B안 원본 기반)
    vec2 mistFlow1 = vec2(t * 0.16, -t * 0.12);
    vec2 mistFlow2 = vec2(-t * 0.13, t * 0.15);
    float mSlow = fbm(rel * 0.055 + mistFlow1);
    float mFine = fbm(rel * 0.145 + mistFlow2 + vec2(mSlow * 1.2, -mSlow * 0.9));

    float edgeNoise = (mSlow - 0.5) * 3.8 + (mFine - 0.5) * 1.71;
    const float feather = 3.6;

    float d1 = sdStage1(rel);
    float d2 = sdStage2(rel);
    float d3 = sdStage3(rel);
    float d4 = sdStage4(rel);

    float w1 = stageSoftMask(d1, u._SealState.x, edgeNoise, feather);
    float w2 = stageSoftMask(d2, u._SealState.y, edgeNoise, feather);
    float w3 = stageSoftMask(d3, u._SealState.z, edgeNoise, feather);
    float w4 = stageSoftMask(d4, u._SealState.w, edgeNoise, feather);
    float mask = max(max(w1, w2), max(w3, w4));

    float activeSd = 1e4;
    if (u._SealState.x > 0.01) activeSd = min(activeSd, d1 + (1.0 - u._SealState.x) * 16.0);
    if (u._SealState.y > 0.01) activeSd = min(activeSd, d2 + (1.0 - u._SealState.y) * 16.0);
    if (u._SealState.z > 0.01) activeSd = min(activeSd, d3 + (1.0 - u._SealState.z) * 16.0);
    if (u._SealState.w > 0.01) activeSd = min(activeSd, d4 + (1.0 - u._SealState.w) * 16.0);
    float warpedSd = activeSd + edgeNoise * 0.85;

    float maxState = max(max(u._SealState.x, u._SealState.y), max(u._SealState.z, u._SealState.w));
    float outerPresence = smoothstep(7.2, -0.5, warpedSd) * clamp(maxState * 6.0, 0.0, 1.0);
    if (mask <= 0.002 && outerPresence <= 0.002)
        discard;

    float interior = smoothstep(1.5, -6.5, warpedSd) * mask;
    float edgeBand = exp(-(warpedSd * warpedSd) / 14.0) * outerPresence;
    float wideEdgeBand = exp(-(warpedSd * warpedSd) / 36.0) * outerPresence;
    float surge = clamp((u._Distortion - 13.5) / 14.0, 0.0, 1.0);

    // 1) 봄날 아지랑이 굴절 + 부드러운 몽환 블러 (원본 B안보다 맑게 절제)
    float h1 = fbm(rel * 0.13 + vec2(t * 0.18, -t * 0.28));
    float h2 = fbm(rel * 0.13 + vec2(-t * 0.15 + 23.1, -t * 0.24 + 9.4));
    vec2 shimmerVec = vec2(h1 - 0.5, h2 - 0.5) * 2.0 + vec2(0.0, sin(rel.x * 0.35 + rel.y * 0.22 - t * 1.4) * 0.22);

    float distWeight = (mask * 0.72 + edgeBand * 0.50) * 0.92;
    vec2 dispCells = shimmerVec * (u._Distortion * 0.092) * distWeight;
    vec2 dispPx = dispCells * pxPerCell;

    vec2 texSize = vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 grabUv = gl_FragCoord.xy / texSize;
    vec2 baseUv = grabUv - dispPx / texSize;

    vec2 blurUv = (0.17 * pxPerCell * (0.30 + 0.70 * mask)) / texSize;
    vec3 bgC = texture(_GrabDistortionBackground, baseUv).rgb;
    vec3 bg1 = texture(_GrabDistortionBackground, baseUv + vec2(blurUv.x, blurUv.y)).rgb;
    vec3 bg2 = texture(_GrabDistortionBackground, baseUv + vec2(-blurUv.x, blurUv.y)).rgb;
    vec3 bg3 = texture(_GrabDistortionBackground, baseUv + vec2(blurUv.x, -blurUv.y)).rgb;
    vec3 bg4 = texture(_GrabDistortionBackground, baseUv + vec2(-blurUv.x, -blurUv.y)).rgb;
    vec3 bgBlur = (bgC * 2.0 + bg1 + bg2 + bg3 + bg4) * (1.0 / 6.0);
    vec3 bg = mix(bgC, bgBlur, mask * 0.55);

    // 2) 꽃잎·황금 꽃가루 레이어
    vec3 petalsFar = sampleDriftingPetals(rel, t, 6.4, 0.34, 1.85, vec2(0.55, 0.32), 1, dRelDx, dRelDy);
    vec3 petalsEdge = sampleDriftingPetals(rel, t, 4.2, 0.44, 1.55, vec2(-0.35, 0.52), 2, dRelDx, dRelDy);
    vec3 pollen1 = samplePollenSpecks(rel, t, 4.4, 0.44, 0.21, 3);
    vec3 pollen2 = samplePollenSpecks(rel, t, 7.6, 0.48, 0.32, 4);

    // 3) 유기적 햇살 물결빛(caustics) — 원본 B안의 감성을 절반 이하로 부드럽게
    vec2 cWarp = rel + vec2(mSlow - 0.5, mFine - 0.5) * 9.5;
    float ca1 = sin(cWarp.x * 0.21 + sin(cWarp.y * 0.16 + t * 0.85) * 1.9 + t * 0.52);
    float ca2 = sin(cWarp.y * 0.19 - t * 0.68 + sin(cWarp.x * 0.14 - t * 0.45) * 1.7);
    float caustics = pow(max(0.0, 1.0 - abs(ca1 * 0.5 + ca2 * 0.5)), 5.5);

    // 4) 원본 B안 영원꽃(연꽃) 문양 — 선을 더 넓고 아련하게 풀어 신기루처럼 흐릿하게 비치도록
    float rCenter = length(rel);
    float slowRot = t * 0.035;
    float lotusOuter = sdPetals(rel / 54.0, 12.0, slowRot, 0.24, 0.96, 0.16) * 54.0;
    float lotusInner = sdPetals(rel / 34.0, 6.0, -slowRot * 1.3 + PI / 6.0, 0.18, 0.92, 0.19) * 34.0;

    float petalLineOuter = exp(-(lotusOuter * lotusOuter) / 3.4);
    float petalLineInner = exp(-(lotusInner * lotusInner) / 2.5);
    float petalFill = smoothstep(1.4, -3.8, min(lotusOuter, lotusInner));
    float breathWave = smoothstep(0.18, 0.92, sin(rCenter * 0.13 - t * 0.75 + mSlow * 3.2) * 0.5 + 0.5);
    float motif = clamp((petalLineOuter * 0.82 + petalLineInner * 0.92 + petalFill * 0.14) * (0.36 + 0.64 * breathWave), 0.0, 1.0) * mask;

    // 경계 꿀빛 아지랑이 빛자락 (원본 B안의 ~42% 강도)
    float tongue = smoothstep(0.32, 0.78, fbm(vec2(rel.x * 0.15 + mSlow * 1.4, rel.y * 0.09 - t * 0.45)));
    float rimGlow = exp(-(warpedSd * warpedSd) / 6.5) * (0.45 + 0.55 * tongue) * outerPresence;

    float warmWash = (0.036 + 0.045 * mSlow + 0.16 * surge) * mask;
    vec3 outRgb = mix(bg, mix(u._WarmColor.rgb, u._CreamColor.rgb, 0.48), warmWash);

    vec3 motifCol = mix(u._WarmColor.rgb, u._CreamColor.rgb, 0.35 + 0.35 * breathWave);
    vec3 glow = motifCol * motif * (0.125 + 0.24 * surge)
              + mix(u._WarmColor.rgb, u._BlossomColor.rgb, 0.28) * (rimGlow * (0.14 + 0.30 * surge) + wideEdgeBand * 0.030)
              + u._CreamColor.rgb * caustics * (0.064 + 0.042 * breathWave) * interior;

    vec3 petalGlow = petalsEdge * (0.64 * wideEdgeBand + 0.18 * interior)
                   + petalsFar * (0.28 * interior);
    vec3 pollenGlow = (pollen1 * 0.45 + pollen2 * 0.36) * (0.62 * mask + 0.44 * wideEdgeBand);

    outRgb = outRgb + glow + petalGlow + pollenGlow;

    float cover = clamp(mask * 0.88 + rimGlow * 0.55 + wideEdgeBand * 0.32 + max(petalGlow.r, max(petalGlow.g, petalGlow.b)) * 1.4, 0.0, 1.0);
    outColor = vec4(clamp(outRgb, 0.0, 1.0), clamp(cover * u._Color.a, 0.0, 1.0));
}
