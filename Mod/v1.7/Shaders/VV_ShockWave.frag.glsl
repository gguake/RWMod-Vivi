#version 450

// 맵 전체로 퍼지는 충격파(아포크리파 2페이즈 전환, 멸망꽃 만개)를 텍스처 없이 수식으로 그린다.
// 쿼드를 growthRate로 키우면 텍스처가 확대·압축되어 깨져 보이므로, 쿼드는 최종 크기로 고정하고
// 파면 반지름을 _AgeSecs로 넓힌다. 모든 무늬를 픽셀마다 계산하므로 아무리 커져도 선명하다.
// - 파면: 장면 캡처(_GrabDistortionBackground)를 바깥으로 밀어내 굴절시키고(채널별로 살짝 어긋나게),
//   그 위에 빛나는 파면·뒤따르는 잔광·파면을 가로지르는 전기 줄기를 더한다.
// - 굴절 폭은 쿼드 크기에 비례하도록 화면 미분으로 픽셀 단위로 바꾼다(확대/축소와 무관하게 같은 모양).
// 플렉의 경우 Graphic_FleckPulse가 _AgeSecs·_RandomPerObject를 넣어 주고, 플렉 색과 페이드 투명도는 _Color로 전달한다.

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
} u;

layout(set = 2, binding = 1) uniform sampler2D _GrabDistortionBackground;

// 정수 격자 해시. 실수 fract 해시는 좌표가 커지면 정밀도가 떨어져 긴 직선 무늬가 생긴다.
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
        p = p * 2.03 + vec2(17.1, 9.7);
        amp *= 0.5;
    }
    return sum;
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
    float seed = u._RandomPerObject * 0.173;

    // 파면: 처음엔 빠르게, 끝으로 갈수록 느려지며 퍼진다. 둘레를 따라 낮은 주파수로 일렁인다.
    vec2 dir = p / max(r, 1e-4);
    float front = mix(0.02, 0.96, ease) * (1.0 + 0.03 * (vnoise(dir * 2.5 + seed) - 0.5));
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

    // 파면을 따라 이동하는 밝기의 일렁임과 전기 줄기. 줄기는 노이즈 등고선인데, 노이즈를 둘레보다 반지름 방향으로
    // 훨씬 빨리 변하게 해서 고리를 따라 뻗고 반지름 방향으로 꿈틀거리게 한다.
    float flicker = 0.75 + 0.5 * vnoise(dir * 7.0 + vec2(u._AgeSecs * 3.0, seed));
    float n = fbm(dir * (8.0 + 0.5 * d / w) + vec2(seed, u._AgeSecs * 2.5));
    float arcs = pow(1.0 - abs(2.0 * n - 1.0), 14.0) * exp(-(d * d) / (9.0 * w * w)) * u._ArcIntensity;

    // 굴절: 파면 주변의 장면을 바깥으로 밀어내고(안쪽을 끌어와 그림), 채널마다 조금씩 다르게 어긋나게 한다.
    float bump = exp(-(d * d) / (4.0 * w * w)) + 0.35 * wake;
    float dispPx = u._Distortion * pxPerUnit * bump * fade;
    vec2 texSize = vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 grabUv = gl_FragCoord.xy / texSize;
    vec2 shift = outward * dispPx / texSize;
    vec3 bg;
    bg.r = texture(_GrabDistortionBackground, grabUv - shift * 1.18).r;
    bg.g = texture(_GrabDistortionBackground, grabUv - shift).g;
    bg.b = texture(_GrabDistortionBackground, grabUv - shift * 0.82).b;

    vec3 glow = u._CoreColor.rgb * (core * 1.1 + arcs * 1.3)
              + u._GlowColor.rgb * (halo * 0.45 + wake * 0.28)
              + u._FringeColor.rgb * ahead * 0.6;
    glow *= flicker * u._Brightness * fade * u._Color.rgb;

    // 장면 캡처 뒤에 그려진 것(영혼 연결선 등)을 지우지 않도록, 굴절·빛이 있는 만큼만 덮고 나머지는 비친다.
    // 뒤따르는 잔광 쪽 굴절은 약하므로 덮지 않고(넓은 원판 안의 연결선이 사라져 보임) 파면 띠만 덮는다.
    float cover = clamp(exp(-(d * d) / (4.0 * w * w)) * fade * 3.0 + max(glow.r, max(glow.g, glow.b)), 0.0, 1.0);
    outColor = vec4(bg + glow, cover);
}
