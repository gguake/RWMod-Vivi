#version 450

// 거대 엘텍스 바늘이 발사될 때 촉이 지나간 직선 위의 공간이 순간 일그러지는 굴절 연출(장면 캡처 _GrabDistortionBackground).
// 일그러짐 자체는 바닐라 사이캐스트 사이킥 선(PsycastPsychicLine: MoteGlowDistortBackground)·큰 왜곡 파동(MoteLargeDistortionWave)과
// 같은 방식이다: 바닐라 선 마스크(_DistortionTex) × 세기만큼, 바닐라 노이즈(_NoiseTex)로 일렁이며, 중심에서의 거리에 비례해 화면 비율로
// 장면을 바깥으로 밀어내고, 밀린 만큼 밝아지며(_brightnessMultiplier), 바닐라 사이킥 선 그림(_MainTex)이 은은하게 겹친다.
// 바닐라와 다른 점은 모양뿐이다: 쿼드는 발사 직선 전체(U = 길이 방향, V = 폭 방향)를 덮고,
// - 촉이 지나간 구간(_Front 앞쪽)만 일그러지며, 일그러지는 폭이 직선에서 양옆으로 퍼지다(처음엔 빠르게, 끝으로 갈수록 느리게) _Duration 동안 잦아든다.
// - 날아가는 촉 둘레는 렌즈처럼 부푼다.
// 바닐라 선 텍스처도 FleckMaker.ConnectingLine에서 같은 축(U = 길이 방향)으로 늘여 그리므로, 폭 방향만 퍼지는 폭에 맞춰 늘여 읽는다.
// C#(ApocryphaNeedleRenderer.DrawWarp)이 MaterialPropertyBlock으로 _AgeSecs(발사 뒤 경과)·_Length·_Width·_Front·_Color(투명도 = 전체 세기)를 넣는다.

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
    float x = vUv.x * L;                          // 시작점부터의 거리(칸)
    float yc = (vUv.y - 0.5) * u._Width;          // 중심선에서의 거리(칸, 부호 있음)

    // 화면 미분은 discard 전에 구한다. across: 화면에서 +y(폭) 방향 단위 벡터, along: 발사 방향 단위 벡터.
    vec2 gy = vec2(dFdx(yc), dFdy(yc));
    vec2 across = gy / max(length(gy), 1e-6);
    vec2 gx = vec2(dFdx(x), dFdy(x));
    vec2 along = gx / max(length(gx), 1e-6);

    float t = u._AgeSecs;
    float life = clamp(t / max(u._Duration, 0.001), 0.0, 1.0);
    float fade = (1.0 - life) * (1.0 - life);
    float flash = pow(1.0 - life, 4.0);
    float ease = 1.0 - pow(1.0 - life, 3.0);

    float passed = smoothstep(-0.3, 0.8, u._Front - x);                 // 촉이 지나간 구간
    float ends = smoothstep(0.0, 1.0, x) * smoothstep(0.0, 1.0, L - x);

    // 일그러지는 폭(반폭, 칸)이 바닐라 사이킥 선(폭 1칸)에서 시작해 양옆으로 조금 퍼진다. 마스크는 이 폭에 맞춰 늘여 읽는다(바닐라 선 마스크의 단면 모양 그대로).
    float spread = mix(0.5, 1.6, ease);
    float c = yc / spread;                        // 바닐라 centeredOffset의 폭 성분(-1~1)
    // 바닐라 centeredOffset의 길이 성분: 바닐라 사이킥 선은 선의 가운데에서 양 끝으로 장면을 늘여 민다. 여기서는 촉이 지나간 구간(시작점~촉)의 가운데 기준.
    float halfPassed = max(min(u._Front, L) * 0.5, 0.5);
    float a = clamp((x - halfPassed) / halfPassed, -1.0, 1.0);
    float inside = step(abs(c), 1.0);
    vec2 maskUv = flipV(vec2(vUv.x, 0.5 + 0.5 * c));
    float mask = texture(_DistortionTex, maskUv).x * inside;

    // 바닐라처럼 노이즈로 일렁인다(바닐라 큰 왜곡 파동은 둘레 각도, 여기서는 길이 방향 좌표를 시간에 따라 흘린다).
    float noiseCoord = x * 0.12 + t * 1.6;
    float noise = texture(_NoiseTex, flipV(vec2(noiseCoord))).x;

    float distortion = mask * u._distortionIntensity * (1.0 + noise * u._noiseIntensity) * passed * fade * ends;

    // 촉 렌즈: 날아가는 촉 둘레가 바닐라 왜곡 파동처럼 중심에서 바깥으로 부풀었다가 빠르게 사그라든다.
    vec2 tipRel = vec2((x - u._Front) * 0.5, yc);
    float lens = exp(-dot(tipRel, tipRel) / 2.0) * flash * ends;
    float lensDistortion = lens * u._distortionIntensity * 1.5;

    // 바닐라 사이킥 선 그림: 지나간 직선 위로 은은하게 겹쳤다가 사라진다(폭 1.2칸).
    vec2 lineUv = flipV(vec2(vUv.x, clamp(0.5 + yc / 1.2, 0.0, 1.0)));
    vec4 lineTex = texture(_MainTex, lineUv) * step(abs(yc), 0.6);
    float lineAmount = passed * pow(1.0 - life, 3.0) * ends * u._LineAlpha;

    float total = distortion + lensDistortion;
    if (total < 0.0005 && lineAmount * lineTex.a < 0.004)
        discard;

    // 바닐라처럼 중심에서의 거리에 비례해 화면 비율로 밀어낸다(길이·폭 방향, 촉 둘레는 촉에서 바깥으로).
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
