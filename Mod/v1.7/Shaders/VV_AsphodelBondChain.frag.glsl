#version 450

// 아스포델 결속 축소 사슬 고리(반투명): 남은 시간에 비례해 줄어드는 하얗게 빛나는 사슬을 요정 아래 레이어에 그린다.
// 좌표는 타원 중심 기준 월드 셀 단위다.
// _MainTex는 사슬 1쌍(타원 고리+연결 막대) 스트립 텍스처(VFX/VV_BondChainStrip: R=core, G=glow, B=barHighlight)다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(0.95, 0.98, 1.0, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _RadiusX;       // @default = 2.8
    float _RadiusZ;       // @default = 2.0
    float _HalfX;         // @default = 3.4
    float _HalfZ;         // @default = 2.6
    float _Progress;      // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float PI = 3.14159265;
const float TAU = 6.28318531;
const float LINK_COUNT = 32.0;

void main()
{
    vec2 p = (vUv * 2.0 - 1.0) * vec2(u._HalfX, u._HalfZ);
    vec2 dpdx = dFdx(p);
    vec2 dpdy = dFdy(p);
    float px = max(max(length(dpdx), length(dpdy)), 1e-4);

    float a = max(u._RadiusX, 0.05);
    float b = max(u._RadiusZ, 0.05);
    float progress = clamp(u._Progress, 0.0, 1.0);
    float fade = u._Color.a * (1.0 - smoothstep(0.96, 1.0, progress));
    if (fade <= 0.001) { discard; }

    // 타원 위의 가장 가까운 점(접선 투영 2회 보정)
    float phi = atan(p.y / b, p.x / a);
    for (int it = 0; it < 2; it++)
    {
        vec2 q = vec2(a * cos(phi), b * sin(phi));
        vec2 t = vec2(-a * sin(phi), b * cos(phi));
        float dPhi = dot(p - q, t) / max(dot(t, t), 1e-6);
        phi += clamp(dPhi, -0.4, 0.4);
    }

    vec2 q = vec2(a * cos(phi), b * sin(phi));
    vec2 tVec = vec2(-a * sin(phi), b * cos(phi));
    vec2 tDir = normalize(tVec);
    vec2 nDir = vec2(tDir.y, -tDir.x);
    float v = dot(p - q, nDir);

    float bandLimit = max(0.42 * clamp(a / 2.8, 0.35, 1.0), 0.16);
    if (abs(v) > bandLimit) { discard; }

    // 라마누잔 둘레 근사 + 2차 호길이 보정으로 타원 전 구간에서 사슬 고리 간격을 균일하게 유지
    float h = (a - b) / (a + b);
    h = h * h;
    float perim = PI * (a + b) * (1.0 + 3.0 * h / (10.0 + sqrt(max(4.0 - 3.0 * h, 0.01))));

    float k = (a * a - b * b) / max(a * a + b * b, 1e-5);
    float sAngle = phi - (0.25 * k) * sin(2.0 * phi) - (0.015625 * k * k) * sin(4.0 * phi);
    float sNorm = fract(sAngle / TAU - u._AgeSecs * 0.04);

    float pitch = perim / LINK_COUNT;
    float scale = clamp(pitch / 0.44, 0.28, 1.0);
    float vSpan = max(0.32 * scale, px * 4.0);
    float uvV = v / (2.0 * vSpan) + 0.5;

    vec3 ch = vec3(0.0);
    if (uvV > 0.01 && uvV < 0.99)
    {
        float pairScale = LINK_COUNT * 0.5;
        vec2 gx = vec2(dot(dpdx, tDir) * (pairScale / max(perim, 1e-4)), dot(dpdx, nDir) / (2.0 * vSpan));
        vec2 gy = vec2(dot(dpdy, tDir) * (pairScale / max(perim, 1e-4)), dot(dpdy, nDir) / (2.0 * vSpan));
        ch = textureGrad(_MainTex, vec2(sNorm * pairScale, uvV), gx, gy).rgb;
    }

    float chainCore = ch.r;
    float chainGlow = ch.g;
    float barHighlight = ch.b;

    // 궤도 전체를 부드럽게 이어 주는 하얀 잔광 띠
    float haloW = max(0.11 * scale, 0.045);
    float bandGlow = exp(-(v * v) / (haloW * haloW)) * (1.0 - smoothstep(bandLimit * 0.65, bandLimit, abs(v)));

    // 사슬을 따라 흐르는 은은한 백광 물결과 미세 맥동
    float wave = 0.85 + 0.15 * sin(sAngle * 3.0 - u._AgeSecs * 3.2);
    float pulse = 0.90 + 0.10 * sin(u._AgeSecs * 4.5);
    float light = wave * pulse * mix(0.92, 1.12, progress);

    // 요정(Additive)이 겹쳐도 선명히 드러나도록 과포화를 억제한 반투명 백광
    float alpha = (chainCore * 0.68 + chainGlow * 0.22 + bandGlow * 0.16) * light * fade;
    alpha = clamp(alpha, 0.0, 0.80);

    vec3 haloCol = vec3(0.82, 0.91, 1.0) * u._Color.rgb;
    vec3 coreCol = mix(vec3(0.94, 0.98, 1.0), vec3(1.0, 1.0, 1.0), clamp(chainCore + barHighlight * 0.5, 0.0, 1.0)) * u._Color.rgb;
    vec3 col = mix(haloCol, coreCol, clamp(chainCore * 0.85 + barHighlight * 0.25, 0.0, 1.0));

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
