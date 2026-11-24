#version 450

// 쿼드는 최종 크기로 고정하고 _AgeSecs로 파면만 넓혀 텍스처 확대·압축을 피한다.
// 굴절 폭은 쿼드 크기에 비례하도록 화면 미분으로 픽셀 단위로 바꾼다.
// 플렉의 경우 Graphic_FleckPulse가 _AgeSecs·_RandomPerObject를 넣어 주고, 플렉 색과 페이드 투명도는 _Color로 전달한다.
// _MainTex는 공용 타일링 FBM 노이즈(VFX/VV_TileableFbmNoise, 기본 주기 16)다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _CoreColor;      // @color @default = vec4(1.0, 0.82, 0.98, 1.0)
    vec4 _GlowColor;      // @color @default = vec4(1.0, 0.25, 0.85, 1.0)
    vec4 _FringeColor;    // @color @default = vec4(0.55, 0.2, 1.0, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _RandomPerObject; // @default = 0.0
    float _Duration;      // @default = 1.4
    float _Width;         // @default = 0.012
    float _Distortion;    // @default = 0.012
    float _Brightness;    // @default = 1.4
    float _ArcIntensity;  // @default = 1.0
    float _Darken;        // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _GrabDistortionBackground;

const float NOISE_INV_PERIOD = 0.0625;

// 정수 격자 해시. _RandomPerObject를 0~64 범위 시드로 변환하는 데 사용한다.
float hash(ivec2 i)
{
    uvec2 q = uvec2(i) * uvec2(1597334673u, 3812015801u);
    uint n = (q.x ^ q.y) * 1597334673u;
    return float(n) * (1.0 / 4294967295.0);
}

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p);

    // 화면 미분은 discard 전에 구한다. gradR: 화면에서 바깥(반지름 증가) 방향, 크기는 픽셀당 r 변화량.
    vec2 gradR = vec2(dFdx(r), dFdy(r));
    float pxPerUnit = 1.0 / max(length(gradR), 1e-6);
    vec2 outward = gradR * pxPerUnit;

    float t = clamp(u._AgeSecs / max(u._Duration, 0.001), 0.0, 1.0);
    float ease = 1.0 - pow(1.0 - t, 3.0);
    // 큰 위치 해시인 _RandomPerObject는 float 정밀도 손실을 피하도록 정수 해시로 0~64 범위에 줄인다.
    float seed = hash(ivec2(floatBitsToInt(u._RandomPerObject), 0x5bd1e995)) * 64.0;

    vec2 dir = p / max(r, 1e-4);
    float frontNoise = textureLod(_MainTex, (dir * 2.5 + seed) * NOISE_INV_PERIOD, 0.0).b;
    float front = mix(0.02, 0.96, ease) * (1.0 + 0.03 * (frontNoise - 0.5));
    float w = u._Width * (0.7 + 1.3 * ease);
    float d = r - front;                               // > 0: 파면 앞, < 0: 지나간 뒤
    float wakeLen = w * 5.0 + 0.10 * ease;

    float fade = u._Color.a * (1.0 - smoothstep(0.55, 1.0, t)) * (1.0 - smoothstep(0.93, 1.0, r));
    if (fade <= 0.001 || d > w * 7.0 || d < -wakeLen * 3.0)
        discard;

    float core = exp(-(d * d) / (w * w));
    float halo = exp(-(d * d) / (9.0 * w * w));
    float wake = d < 0.0 ? exp(-(d * d) / (wakeLen * wakeLen)) : 0.0;
    float ahead = smoothstep(0.0, 3.0 * w, d) * halo;

    float flicker = 0.75 + 0.5 * textureLod(_MainTex, (dir * 7.0 + vec2(u._AgeSecs * 3.0, seed)) * NOISE_INV_PERIOD, 0.0).b;
    float n = textureLod(_MainTex, (dir * (8.0 + 0.5 * d / w) + vec2(seed, u._AgeSecs * 2.5)) * NOISE_INV_PERIOD, 0.0).r;
    float arcs = pow(1.0 - abs(2.0 * n - 1.0), 14.0) * exp(-(d * d) / (9.0 * w * w)) * u._ArcIntensity;

    // 어두운 파동(_Darken > 0): 파면 둘레와 지나간 자리를 연기처럼 얼룩진 그림자로 덮는다. 0이면 기존 파동과 같다.
    float smoke = 0.55 + 0.45 * textureLod(_MainTex, (dir * 5.0 + vec2(seed + 3.7, u._AgeSecs * 1.2) + vec2(d * 6.0)) * NOISE_INV_PERIOD, 0.0).g;
    float shade = clamp((wake * 1.3 + halo * 1.2 + ahead * 0.6) * smoke * fade * u._Darken, 0.0, 0.85);

    float bump = exp(-(d * d) / (4.0 * w * w)) + 0.35 * wake;
    float dispPx = u._Distortion * pxPerUnit * bump * fade;
    vec2 texSize = vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 grabUv = gl_FragCoord.xy / texSize;
    vec2 shift = outward * dispPx / texSize;
    vec3 bg;
    bg.r = texture(_GrabDistortionBackground, grabUv - shift * 1.18).r;
    bg.g = texture(_GrabDistortionBackground, grabUv - shift).g;
    bg.b = texture(_GrabDistortionBackground, grabUv - shift * 0.82).b;
    bg *= 1.0 - shade;

    vec3 glow = u._CoreColor.rgb * (core * 1.1 + arcs * 1.3)
              + u._GlowColor.rgb * (halo * 0.45 + wake * 0.28)
              + u._FringeColor.rgb * ahead * 0.6;
    glow *= flicker * u._Brightness * fade * u._Color.rgb;

    // 캡처 이후에 그린 연결선이 사라지지 않도록 파면 띠와 빛, 그림자가 있는 부분만 덮는다.
    float cover = clamp(exp(-(d * d) / (4.0 * w * w)) * fade * 3.0 + max(glow.r, max(glow.g, glow.b)) + shade * 2.0, 0.0, 1.0);
    outColor = vec4(bg + glow, cover);
}
