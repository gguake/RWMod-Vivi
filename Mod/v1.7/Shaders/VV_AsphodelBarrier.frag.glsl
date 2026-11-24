#version 450

// 아스포델 28칸 결계 보호막 셰이더: 거의 투명한 황금빛 육각형 결계 격자가 천천히 회전하며,
// 경계선(r = _Radius)은 또렷한 이중 금빛 고리와 회전 장식 마디로 읽히고 투사체 차단 시 피격 지점이 번쩍인다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.82, 0.36, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _Radius;        // @default = 28.0
    float _HalfSize;      // @default = 29.5
    float _ImpactAngle;   // @default = 0.0
    float _ImpactAlpha;   // @default = 0.0
} u;

const float TAU = 6.28318531;
const float HEX_PITCH = 3.6;

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
    vec2 w = (vUv * 2.0 - 1.0) * u._HalfSize;
    float r = length(w);
    float R = max(u._Radius, 1.0);

    // 화면 미분은 분기 전에 계산한다. px: 화면 1픽셀당 월드 칸 수.
    vec2 dwdx = dFdx(w);
    vec2 dwdy = dFdy(w);
    float px = max(max(length(dwdx), length(dwdy)), 1e-4);

    if (r > R + 1.45 || u._Color.a <= 0.001) { discard; }

    float t = u._AgeSecs;
    float rot = t * 0.055;
    float cr = cos(rot);
    float sr = sin(rot);
    vec2 rw = vec2(w.x * cr - w.y * sr, w.x * sr + w.y * cr);

    // 육각형 격자 SDF (평면 간 거리 = HEX_PITCH 월드 칸)
    vec2 p = rw / HEX_PITCH;
    const vec2 s = vec2(1.0, 1.7320508);
    const vec2 h = s * 0.5;
    vec2 a = mod(p, s) - h;
    vec2 b = mod(p - h, s) - h;
    vec2 gv = dot(a, a) < dot(b, b) ? a : b;
    vec2 cellId = p - gv;
    vec2 q = abs(gv);
    float hexD = max(dot(q, vec2(0.5, 0.8660254)), q.x);
    float edgeWorld = max(0.0, (0.5 - hexD) * HEX_PITCH);
    uint hh = hashU(ivec2(floor(cellId * 2.0 + 0.5)));
    float cellSeed = hashF(hh, 0);

    float dRim = r - R;
    float inside = 1.0 - smoothstep(-px, px, dRim);
    float rimProx = smoothstep(R - 10.5, R, r);
    float outerRimProx = smoothstep(R - 3.2, R, r);

    // 육각형 격자선 (내부는 아주 옅고 경계에 가까워질수록 자연스럽게 살아남)
    float hexW = max(0.052, px * 0.85);
    float hexLine = exp(-(edgeWorld * edgeWorld) / (hexW * hexW));
    float hexSoft = exp(-edgeWorld / 0.26);

    float wave = 0.5 + 0.5 * sin(r * 0.28 - t * 1.15 + cellSeed * 2.5);
    float facetPulse = 0.5 + 0.5 * sin(t * (0.85 + cellSeed * 0.65) + cellSeed * TAU);

    float fillAlpha = inside * (0.018 + 0.014 * facetPulse + 0.060 * outerRimProx + 0.038 * outerRimProx * outerRimProx);
    float hexAlpha = inside * (
        hexLine * (0.062 + 0.032 * wave + 0.155 * rimProx + 0.145 * outerRimProx)
        + hexSoft * (0.012 + 0.042 * outerRimProx));

    // 경계선 (r = R): 또렷한 금빛 외곽선 + 옅은 내곽선 + 회전하는 6각/24분 마름모 마디
    float ringW = max(0.095, px * 1.2);
    float outerRing = exp(-(dRim * dRim) / (ringW * ringW));
    float outerHalo = exp(-abs(dRim) / 0.55) * (0.42 + 0.58 * inside);
    float innerW = max(0.052, px * 0.88);
    float innerRing = exp(-((r - (R - 0.72)) * (r - (R - 0.72))) / (innerW * innerW)) * inside;

    // RimWorld AngleFlat 기준 (0 = +Z 북, pi/2 = +X 동)
    float theta = atan(w.x, w.y);
    float rotTheta = theta + rot;
    float crest6 = 0.5 + 0.5 * cos(rotTheta * 6.0);

    // 24개 회전 마름모 마디(외곽선과 내곽선 사이)
    float step24 = TAU / 24.0;
    float localAng = mod(rotTheta + step24 * 0.5, step24) - step24 * 0.5;
    float arcDist = abs(localAng) * R;
    float radDist = abs(r - (R - 0.36));
    float diamond = arcDist / 0.24 + radDist / 0.24;
    float node = (1.0 - smoothstep(0.65, 1.05, diamond)) * inside;

    float ringAlpha = outerRing * (0.62 + 0.18 * crest6)
        + outerHalo * (0.19 + 0.08 * crest6)
        + innerRing * 0.26
        + node * (0.34 + 0.16 * crest6);

    // 투사체 차단 시 피격 지점 금빛 파동 반짝임
    float impAlphaIn = clamp(u._ImpactAlpha, 0.0, 1.0);
    float impFlash = 0.0;
    if (impAlphaIn > 0.001)
    {
        float angDiff = atan(sin(theta - u._ImpactAngle), cos(theta - u._ImpactAngle));
        float impArc = exp(-(angDiff * angDiff) / (0.18 * 0.18)) * impAlphaIn;
        float impRad = exp(-abs(dRim) / 2.2) * inside + exp(-abs(dRim) / 0.9) * (1.0 - inside);
        impFlash = impArc * impRad * (0.52 + 0.78 * hexLine + 0.65 * outerRing);
    }

    float alpha = clamp((fillAlpha + hexAlpha + ringAlpha + impFlash) * u._Color.a, 0.0, 0.92);
    if (alpha <= 0.001) { discard; }

    vec3 gold = u._Color.rgb;
    vec3 pale = mix(gold, vec3(1.0, 0.97, 0.84), 0.78);
    float brightMix = clamp(outerRing * 0.65 + node * 0.45 + hexLine * 0.22 + impFlash * 0.85, 0.0, 1.0);
    vec3 col = mix(gold, pale, brightMix);

    outColor = vec4(col, alpha);
}
