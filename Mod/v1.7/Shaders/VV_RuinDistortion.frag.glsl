#version 450

// 거대 육각형 고대 유적 공간 왜곡 결계 셰이더:
// 전체가 거대한 정육각형(Stage 1 외곽 미로, Stage 2 북쪽 장대 복도, Stage 3 중앙 원형 회랑, Stage 4 중앙 제단)으로
// 이루어져 있으며, 봉인된 구역의 지형만 실시간 굴절시키고 신기루 같은 거대 구조물 잔상을 비춘다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;        // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _RiftColor;    // @color @default = vec4(0.94, 0.85, 0.56, 1.0)
    vec4 _MirageColor;  // @color @default = vec4(0.56, 0.84, 1.0, 1.0)
    vec4 _BoundsXZ;     // @default = vec4(110.5, 110.5, 148.0, 132.0) (centerX, centerZ, sizeX, sizeZ)
    vec4 _SealState;    // @default = vec4(1.0, 1.0, 1.0, 1.0) (stage1..4 opacity 0..1)
    float _AgeSecs;     // @default = 0.0
    float _Distortion;  // @default = 13.5
} u;

layout(set = 2, binding = 1) uniform sampler2D _GrabDistortionBackground;

float hash(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    uint n = (q.x ^ q.y) * 1597334673u;
    return float(n) * (1.0 / 4294967295.0);
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
    float amp = 0.5;
    for (int k = 0; k < 4; k++)
    {
        sum += amp * vnoise(p);
        p = p * 2.03 + vec2(13.7, 7.9);
        amp *= 0.5;
    }
    return sum;
}

float hexEdgeDist(vec2 p)
{
    const vec2 s = vec2(1.0, 1.7320508);
    vec4 hC = floor(vec4(p, p - vec2(0.5, 1.0)) / s.xyxy) + 0.5;
    vec4 h = vec4(p - hC.xy * s, p - (hC.zw + 0.5) * s);
    vec2 q = abs((dot(h.xy, h.xy) < dot(h.zw, h.zw)) ? h.xy : h.zw);
    float d = max(dot(q, s * 0.5), q.x);
    return abs(0.5 - d);
}

float sdBox(vec2 p, vec2 b)
{
    vec2 d = abs(p) - b;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

// 위·아래가 평평한 정육각형 유클리드 SDF (내접원 반지름 r = 평평한 변까지 거리)
float sdFlatHexagon(vec2 p, float r)
{
    const vec3 k = vec3(-0.866025404, 0.5, 0.577350269);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= vec2(clamp(p.x, -k.z * r, k.z * r), r);
    return length(p) * sign(p.y);
}

// 중첩 SDF 구조: 상위 봉인이 닫혀 있으면 하위 경계선이 내부에 드러나지 않아 구조 스포일러 없이 하나의 거대한 공간 왜곡으로 보인다.
// Stage 4: 중앙 제단 성소 (origin 중심 반지름 8.65)
float sdStage4(vec2 rel)
{
    return length(rel) - 8.65;
}

// Stage 3: 중앙 원형 성소 전체 (origin 중심 반지름 22.7)
float sdStage3(vec2 rel)
{
    return length(rel) - 22.7;
}

// Stage 2: 북쪽 장대 복도 + 중앙 원형 성소 합집합
float sdStage2(vec2 rel)
{
    float corridor = sdBox(rel - vec2(0.0, 35.7), vec2(10.4, 15.8));
    return min(corridor, length(rel) - 22.7);
}

// Stage 1: 거대 정육각형 전체 (평평한 정육각형 내접원 반지름 58.5)
float sdStage1(vec2 rel)
{
    return sdFlatHexagon(rel, 58.5);
}

float stageWeight(float sd, float state, float wobble)
{
    if (state <= 0.001) { return 0.0; }
    float shrink = (1.0 - state) * 16.0;
    float d = sd + shrink + wobble;
    return smoothstep(1.15, -0.85, d) * smoothstep(0.0, 0.16, state);
}

void main()
{
    vec2 rel = (vUv - 0.5) * u._BoundsXZ.zw;

    // 화면 미분은 discard 전에 계산한다.
    vec2 dRelDx = dFdx(rel);
    vec2 dRelDy = dFdy(rel);
    float pxPerCell = 1.0 / max(length(dRelDx), 1e-4);

    float t = u._AgeSecs;
    float edgeNoise = (fbm(rel * 0.14 + vec2(t * 0.22, -t * 0.17)) - 0.5) * 1.45;

    float d1 = sdStage1(rel);
    float d2 = sdStage2(rel);
    float d3 = sdStage3(rel);
    float d4 = sdStage4(rel);

    float w1 = stageWeight(d1, u._SealState.x, edgeNoise);
    float w2 = stageWeight(d2, u._SealState.y, edgeNoise);
    float w3 = stageWeight(d3, u._SealState.z, edgeNoise);
    float w4 = stageWeight(d4, u._SealState.w, edgeNoise);

    float mask = max(max(w1, w2), max(w3, w4));
    if (mask <= 0.004)
        discard;

    // 활성화된 봉인 영역들의 합집합 외곽 거리
    float activeSd = 1e4;
    if (u._SealState.x > 0.01) activeSd = min(activeSd, d1 + (1.0 - u._SealState.x) * 16.0);
    if (u._SealState.y > 0.01) activeSd = min(activeSd, d2 + (1.0 - u._SealState.y) * 16.0);
    if (u._SealState.z > 0.01) activeSd = min(activeSd, d3 + (1.0 - u._SealState.z) * 16.0);
    if (u._SealState.w > 0.01) activeSd = min(activeSd, d4 + (1.0 - u._SealState.w) * 16.0);
    float rimDist = abs(activeSd + edgeNoise * 0.55);

    // 소용돌이치는 공간 왜곡 벡터장
    float n1 = fbm(rel * 0.11 + vec2(t * 0.28, t * 0.18));
    float n2 = fbm(rel * 0.11 + vec2(-t * 0.23 + 31.4, t * 0.25 + 11.7));
    float rCenter = length(rel);
    float ripple = sin(rCenter * 0.42 - t * 1.65) * 0.5 + 0.5;
    vec2 flow = vec2(n1 - 0.5, n2 - 0.5) * 2.25;
    flow += normalize(rel + vec2(1e-4)) * (ripple - 0.5) * 0.68;

    float rimBoost = exp(-(rimDist * rimDist) / 2.2);
    float hexLine = smoothstep(0.075, 0.01, hexEdgeDist(rel * 0.12 + flow * 0.06));

    // 공간 너머 거대 육각 성소의 희미한 신기루(직접 벽을 그리지 않고 파동이 지날 때만 아련히 드러남)
    float hexMirage = (exp(-pow(abs(sdFlatHexagon(rel, 46.0)), 2.0) / 0.85)
                     + exp(-pow(abs(sdFlatHexagon(rel, 31.0)), 2.0) / 0.85)) * w1 * 0.45;
    float hallMirage = exp(-pow(abs(abs(rel.x) - 6.0), 2.0) / 0.45) * step(22.0, rel.y) * step(rel.y, 50.0) * w2 * (1.0 - 0.85 * w1) * 0.65;
    float ringMirage = (exp(-pow(abs(rCenter - 14.0), 2.0) / 0.55) * w3 * (1.0 - 0.85 * w1)
                      + exp(-pow(abs(rCenter - 3.2), 2.0) / 0.85) * w4 * (1.0 - 0.80 * w3)) * 0.65;

    float mirageWave = smoothstep(0.22, 0.94, sin(rCenter * 0.26 - t * 1.35 + n1 * 4.5) * 0.5 + 0.5);
    float archHint = clamp((hexMirage + hallMirage + ringMirage) * mirageWave, 0.0, 1.0);

    vec2 lensPull = -normalize(rel + vec2(1e-4)) * sin(rCenter * 0.55 - t * 2.0) * 0.45;
    float distStrength = (0.72 + 1.10 * rimBoost + 0.65 * archHint + 0.30 * hexLine) * mask;
    vec2 dispCells = (flow + lensPull) * (u._Distortion * 0.11) * distStrength;
    vec2 dispPx = dispCells * pxPerCell;

    vec2 texSize = vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 grabUv = gl_FragCoord.xy / texSize;
    vec2 shift = dispPx / texSize;

    // 은은한 프리즘 색수차를 섞은 배경 지형 샘플링
    vec3 bg;
    bg.r = texture(_GrabDistortionBackground, grabUv - shift * 1.15).r;
    bg.g = texture(_GrabDistortionBackground, grabUv - shift).g;
    bg.b = texture(_GrabDistortionBackground, grabUv - shift * 0.85).b;

    float haze = (0.06 + 0.09 * n2) * mask;
    bg = mix(bg, bg * vec3(0.88, 0.93, 1.02), haze);

    float rimLine = exp(-(rimDist * rimDist) / 0.14) * mask;
    float rimHalo = exp(-(rimDist * rimDist) / 1.35) * mask;
    float shimmer = 0.68 + 0.32 * sin(t * 2.8 + (rel.x - rel.y) * 0.42 + n1 * 3.14);

    // 재봉인 등으로 _Distortion이 강해질수록 금빛 균열 광휘도 함께 고조된다.
    float surge = clamp((u._Distortion - 13.5) / 14.0, 0.0, 1.0);
    vec3 glow = u._RiftColor.rgb * (rimLine * (0.36 + 0.45 * surge) + archHint * (0.17 + 0.22 * surge) + mask * surge * 0.16) * shimmer
              + u._MirageColor.rgb * (rimHalo * (0.12 + 0.18 * surge) + hexLine * (0.05 + 0.10 * archHint + 0.12 * surge) * mask * mirageWave);

    float cover = clamp(mask * 0.90 + rimLine * 0.38, 0.0, 1.0) * u._Color.a;
    outColor = vec4(clamp(bg + glow, 0.0, 1.0), cover);
}
