#version 450

// U는 발사 방향, V는 폭 방향이다. _Front는 시작점에서 바늘 촉까지의 거리(칸)다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;                 // @color @default = vec4(1.0, 0.95, 0.85, 1.0)
    float _AgeSecs;              // @default = 0.0
    float _Duration;             // @default = 0.55
    float _Length;               // @default = 30.0
    float _Width;                // @default = 7.0
    float _Front;                // @default = 30.0
    float _distortionIntensity;  // @default = 0.15
    float _noiseIntensity;       // @default = 2.0
    float _brightnessMultiplier; // @default = 1.0
    float _LineAlpha;            // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;        // 바닐라 사이킥 선 그림(PsycastPsychicLine)
layout(set = 2, binding = 2) uniform sampler2D _DistortionTex;  // 바닐라 사이킥 선 왜곡 마스크(PsycastPsychicLineMask)
layout(set = 2, binding = 3) uniform sampler2D _NoiseTex;       // 바닐라 사이캐스트 노이즈(PsycastNoise)
layout(set = 2, binding = 4) uniform sampler2D _GrabDistortionBackground;

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

void main()
{
    float L = u._Length;
    float x = vUv.x * L;
    float yc = (vUv.y - 0.5) * u._Width;

    // 화면 미분은 discard 전에 구한다.
    vec2 gy = vec2(dFdx(yc), dFdy(yc));
    vec2 across = gy / max(length(gy), 1e-6);
    vec2 gx = vec2(dFdx(x), dFdy(x));
    vec2 along = gx / max(length(gx), 1e-6);

    float t = u._AgeSecs;
    float life = clamp(t / max(u._Duration, 0.001), 0.0, 1.0);
    float fade = (1.0 - life) * (1.0 - life);
    float flash = pow(1.0 - life, 4.0);
    float ease = 1.0 - pow(1.0 - life, 3.0);

    float passed = smoothstep(-0.3, 0.8, u._Front - x);
    float ends = smoothstep(0.0, 1.0, x) * smoothstep(0.0, 1.0, L - x);

    float spread = mix(0.5, 1.6, ease);
    float c = yc / spread;
    // 지나간 구간의 중점을 기준으로 양 끝을 향해 장면을 늘인다.
    float halfPassed = max(min(u._Front, L) * 0.5, 0.5);
    float a = clamp((x - halfPassed) / halfPassed, -1.0, 1.0);
    float inside = step(abs(c), 1.0);
    vec2 maskUv = flipV(vec2(vUv.x, 0.5 + 0.5 * c));
    float mask = texture(_DistortionTex, maskUv).x * inside;

    float noiseCoord = x * 0.12 + t * 1.6;
    float noise = texture(_NoiseTex, flipV(vec2(noiseCoord))).x;

    float distortion = mask * u._distortionIntensity * (1.0 + noise * u._noiseIntensity) * passed * fade * ends;

    vec2 tipRel = vec2((x - u._Front) * 0.5, yc);
    float lens = exp(-dot(tipRel, tipRel) / 2.0) * flash * ends;
    float lensDistortion = lens * u._distortionIntensity * 1.5;

    vec2 lineUv = flipV(vec2(vUv.x, clamp(0.5 + yc / 1.2, 0.0, 1.0)));
    vec4 lineTex = texture(_MainTex, lineUv) * step(abs(yc), 0.6);
    float lineAmount = passed * pow(1.0 - life, 3.0) * ends * u._LineAlpha;

    float total = distortion + lensDistortion;
    if (total < 0.0005 && lineAmount * lineTex.a < 0.004)
        discard;

    vec2 texSize = vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 tipDir = normalize(along * tipRel.x + across * tipRel.y + vec2(1e-5));
    vec2 offsetPx = ((along * a + across * c) * distortion + tipDir * min(length(tipRel), 1.0) * lensDistortion) * texSize.y * u._Color.a;
    vec2 grabUv = gl_FragCoord.xy / texSize;
    vec4 bg = texture(_GrabDistortionBackground, grabUv - offsetPx / texSize);
    bg = mix(bg, bg * u._brightnessMultiplier, clamp(u._Color.a * total * 8.0, 0.0, 1.0));

    vec3 line = lineTex.rgb * lineTex.a * u._Color.rgb * lineAmount * u._Color.a;

    // 장면 캡처 뒤에 그려진 것을 지우지 않도록 일그러진 만큼만 덮는다.
    float cover = clamp(total * 40.0 + max(line.r, max(line.g, line.b)), 0.0, 1.0);
    outColor = vec4(bg.rgb + line, cover);
}
