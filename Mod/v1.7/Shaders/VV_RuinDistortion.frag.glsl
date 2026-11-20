#version 450

// 고대 유적 공간 왜곡 결계 셰이더:
// 봉인된 구역(Stage 1~4)의 지형을 실시간 굴절시키고, 경계면의 일렁이는 균열과
// 공간 너머 거대 성소의 희미한 육각 잔상만 비춘다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;        // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _RiftColor;    // @color @default = vec4(0.92, 0.84, 0.56, 1.0)
    vec4 _MirageColor;  // @color @default = vec4(0.56, 0.82, 0.98, 1.0)
    vec4 _BoundsXZ;     // @default = vec4(60.5, 60.5, 96.0, 104.0) (centerX, centerZ, sizeX, sizeZ)
    vec4 _SealState;    // @default = vec4(1.0, 1.0, 1.0, 1.0) (stage1..4 opacity 0..1)
    float _AgeSecs;     // @default = 0.0
    float _Distortion;  // @default = 12.0
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

// 육각형 격자 거리(가장 가까운 육각 변까지의 거리)
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

// Stage 1: 미로 요새 (dz: -35 .. -15, dx: -18 .. +18, 계단형 모서리)
float sdStage1(vec2 rel)
{
    vec2 c = rel - vec2(0.0, -25.0);
    return min(sdBox(c, vec2(14.5, 10.5)), sdBox(c, vec2(18.5, 6.5)));
}

// Stage 2: 시련의 회랑 (dz: -15 .. +7.5, dx: -9.5 .. +9.5 + 측면 돌출)
float sdStage2(vec2 rel)
{
    float mainHall = sdBox(rel - vec2(0.0, -3.75), vec2(9.5, 11.35));
    float sideBay = sdBox(rel - vec2(0.0, -4.0), vec2(11.5, 4.5));
    return min(mainHall, sideBay);
}

// Stage 3: 원형 회랑 (중심 (0, +26), 반지름 7.5 ~ 19.8)
float sdStage3(vec2 rel)
{
    float r = length(rel - vec2(0.0, 26.0));
    return max(r - 19.8, 7.45 - r);
}

// Stage 4: 중심 제단 성소 (중심 (0, +26), 반지름 7.6)
float sdStage4(vec2 rel)
{
    return length(rel - vec2(0.0, 26.0)) - 7.65;
}

float stageWeight(float sd, float state, float wobble)
{
    if (state <= 0.001) { return 0.0; }
    // 해금 애니메이션(state: 1 -> 0) 중에는 바깥/아래에서부터 경계가 스르륵 물러난다.
    float shrink = (1.0 - state) * 14.0;
    float d = sd + shrink + wobble;
    return smoothstep(1.1, -0.8, d) * smoothstep(0.0, 0.18, state);
}

void main()
{
    vec2 rel = (vUv - 0.5) * u._BoundsXZ.zw;

    // 화면 미분은 discard 전에 계산한다.
    vec2 dRelDx = dFdx(rel);
    vec2 dRelDy = dFdy(rel);
    float pxPerCell = 1.0 / max(length(dRelDx), 1e-4);

    float t = u._AgeSecs;
    float edgeNoise = (fbm(rel * 0.18 + vec2(t * 0.22, -t * 0.17)) - 0.5) * 1.35;

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

    // 활성화된 가장 가까운 봉인 외곽까지의 부호 있는 거리 (경계선 강조용)
    float activeSd = 1e4;
    if (u._SealState.x > 0.01) activeSd = min(activeSd, d1 + (1.0 - u._SealState.x) * 14.0);
    if (u._SealState.y > 0.01) activeSd = min(activeSd, d2 + (1.0 - u._SealState.y) * 14.0);
    if (u._SealState.z > 0.01) activeSd = min(activeSd, d3 + (1.0 - u._SealState.z) * 14.0);
    if (u._SealState.w > 0.01) activeSd = min(activeSd, d4 + (1.0 - u._SealState.w) * 14.0);
    float rimDist = abs(activeSd + edgeNoise * 0.55);

    // 소용돌이치는 공간 왜곡 벡터장
    float n1 = fbm(rel * 0.14 + vec2(t * 0.31, t * 0.19));
    float n2 = fbm(rel * 0.14 + vec2(-t * 0.24 + 31.4, t * 0.28 + 11.7));
    float ripple = sin(length(rel - vec2(0.0, 12.0)) * 0.55 - t * 1.65) * 0.5 + 0.5;
    vec2 flow = vec2(n1 - 0.5, n2 - 0.5) * 2.2;
    flow += normalize(rel - vec2(0.0, 26.0) + vec2(1e-4)) * (ripple - 0.5) * 0.65;

    // 경계면에서는 굴절이 더욱 강하게 일렁인다.
    float rimBoost = exp(-(rimDist * rimDist) / 1.8);
    float hexLine = smoothstep(0.075, 0.01, hexEdgeDist(rel * 0.16 + flow * 0.08));

    // 공간 너머 숨겨진 거대 성소의 희미한 윤곽(직접 건물을 그리진 않고 신기루 같은 기하학적 간섭만 형성)
    vec2 ringRel = rel - vec2(0.0, 26.0);
    float ringR = length(ringRel);
    float innerRings = (exp(-pow(abs(ringR - 15.8), 2.0) / 0.45) + exp(-pow(abs(ringR - 11.8), 2.0) / 0.38)) * w3
                     + (exp(-pow(abs(ringR - 7.3), 2.0) / 0.35) * (1.0 - 0.72 * w3)
                      + exp(-pow(abs(ringR - 3.0), 2.0) / 0.7) * 0.65) * w4;
    float hallLanes = exp(-pow(abs(abs(rel.x) - 6.0), 2.0) / 0.35) * step(-13.0, rel.y) * step(rel.y, 6.0) * w2;
    float mazeLanes = (exp(-pow(sin(rel.x * 0.7854), 2.0) / 0.04) * 0.45
                     + exp(-pow(sin((rel.y + 33.0) * 0.7854), 2.0) / 0.04) * 0.45) * w1 * (1.0 - rimBoost);

    float mirageWave = smoothstep(0.25, 0.95, sin(length(rel - vec2(0.0, 18.0)) * 0.38 - t * 1.45 + n1 * 4.5) * 0.5 + 0.5);
    float archHint = clamp((innerRings * 0.85 + hallLanes * 0.7 + mazeLanes * 0.55) * mirageWave, 0.0, 1.0);

    // 실제 바닥 지형을 물결·중력렌즈처럼 강하게 굴절시켜 지형만 보이는데도 공간이 뒤틀린 느낌을 만든다.
    vec2 lensPull = -normalize(ringRel + vec2(1e-4)) * sin(ringR * 0.7 - t * 2.1) * 0.45;
    float distStrength = (0.72 + 1.05 * rimBoost + 0.65 * archHint + 0.30 * hexLine) * mask;
    vec2 dispCells = (flow + lensPull) * (u._Distortion * 0.105) * distStrength;
    vec2 dispPx = dispCells * pxPerCell;

    vec2 texSize = vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 grabUv = gl_FragCoord.xy / texSize;
    vec2 shift = dispPx / texSize;

    // 은은한 프리즘 색수차를 섞은 배경 지형 샘플링 (지형은 선명히 비치되 아지랑이·수정 렌즈처럼 일그러짐)
    vec3 bg;
    bg.r = texture(_GrabDistortionBackground, grabUv - shift * 1.14).r;
    bg.g = texture(_GrabDistortionBackground, grabUv - shift).g;
    bg.b = texture(_GrabDistortionBackground, grabUv - shift * 0.86).b;

    // 과도한 백탁 없이 은은한 공기 색조와 잔상 파동만 얹는다.
    float haze = (0.06 + 0.09 * n2) * mask;
    bg = mix(bg, bg * vec3(0.88, 0.93, 1.02), haze);

    float rimLine = exp(-(rimDist * rimDist) / 0.11) * mask;
    float rimHalo = exp(-(rimDist * rimDist) / 1.15) * mask;
    float shimmer = 0.65 + 0.35 * sin(t * 2.9 + (rel.x - rel.y) * 0.55 + n1 * 3.14);

    vec3 glow = u._RiftColor.rgb * (rimLine * 0.34 + archHint * 0.16) * shimmer
              + u._MirageColor.rgb * (rimHalo * 0.11 + hexLine * (0.045 + 0.09 * archHint) * mask * mirageWave);

    float cover = clamp(mask * 0.90 + rimLine * 0.35, 0.0, 1.0) * u._Color.a;
    outColor = vec4(clamp(bg + glow, 0.0, 1.0), cover);
}
