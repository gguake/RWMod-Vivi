#version 450

// 거대 엘텍스 바늘이 발사될 때 촉이 지나간 직선 위의 공간이 순간 일그러지는 굴절 연출(장면 캡처 _GrabDistortionBackground).
// 쿼드는 발사 직선 전체(U = 길이 방향, V = 폭 방향)를 덮고, 촉이 지나간 구간(_Front 앞쪽)만 일그러진다.
// - 지나간 구간: 직선에서 양옆으로 원통형 충격파처럼 파면이 퍼지며 그 자리의 장면을 바깥으로 밀어내고(채널마다 조금씩 어긋남),
//   직선 가까이는 물결치며 일그러진다. 파면에는 가는 흰금빛 테두리가 일렁이고, _Duration 동안 퍼지며 잦아든다.
// - 날아가는 촉 둘레는 렌즈처럼 크게 부풀고, 지나간 직선에 가는 흰금빛 심선이 번쩍였다가 빠르게 꺼진다.
// - 밀림 폭은 칸 단위(_Strength)이고 화면 미분으로 픽셀로 바꾸므로 확대/축소와 무관하게 같은 모양이다.
// C#(ApocryphaNeedleRenderer.DrawWarp)이 MaterialPropertyBlock으로 _AgeSecs(발사 뒤 경과)·_Length·_Width·_Front·_Color를 넣는다.

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.9, 0.7, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _Duration;      // @default = 0.55
    float _Length;        // @default = 30.0
    float _Width;         // @default = 5.0
    float _Front;         // @default = 30.0
    float _Strength;      // @default = 0.65
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

void main()
{
    float L = u._Length;
    float x = vUv.x * L;                          // 시작점부터의 거리(칸)
    float yc = (vUv.y - 0.5) * u._Width;          // 중심선에서의 거리(칸, 부호 있음)
    float y = abs(yc);

    // 화면 미분은 discard 전에 구한다. across: 화면에서 +y(폭) 방향 단위 벡터, along: 발사 방향 단위 벡터.
    vec2 gy = vec2(dFdx(yc), dFdy(yc));
    float pxPerCell = 1.0 / max(length(gy), 1e-6);
    vec2 across = gy * pxPerCell;
    vec2 gx = vec2(dFdx(x), dFdy(x));
    vec2 along = gx / max(length(gx), 1e-6);

    float t = u._AgeSecs;
    float life = clamp(t / max(u._Duration, 0.001), 0.0, 1.0);
    float fade = (1.0 - life) * (1.0 - life);
    float flash = pow(1.0 - life, 4.0);

    float passed = smoothstep(-0.3, 0.8, u._Front - x);                 // 촉이 지나간 구간
    float ends = smoothstep(0.0, 1.0, x) * smoothstep(0.0, 1.0, L - x);

    // 길이 방향 물결: 지나간 자리에서 뒤쪽으로 흐르며 잦아든다(규칙적인 물결 + 잔 일렁임).
    float jitter = vnoise(vec2(x * 1.4, t * 10.0)) - 0.5;
    float ripple = 0.7 + 0.5 * sin(x * 1.7 + t * 24.0) + 0.7 * jitter;

    // 퍼지는 파면: 직선에서 양옆으로 원통형 충격파처럼 빠르게 퍼지다 느려진다(처음엔 빠르게, 끝으로 갈수록 느리게).
    // 민무늬 땅에서도 보이도록 굴절을 파면에 모으고, 파면에 가는 흰금빛 테두리를 남긴다. 테두리는 길이 방향으로 일렁인다.
    float ease = 1.0 - pow(1.0 - life, 3.0);
    float wave = mix(0.2, 2.7, ease) * (1.0 + 0.12 * jitter);
    float waveW = 0.2 + 0.3 * ease;
    float dWave = y - wave;
    float front = exp(-(dWave * dWave) / (waveW * waveW));

    // 폭 방향 안쪽: 지나간 직선 근처의 장면이 물결치며 일그러진다.
    float prof = exp(-(y * y) / 0.8);

    // 촉 렌즈: 날아가는 촉 둘레가 크게 부풀었다가 빠르게 사그라든다.
    float dTip = length(vec2((x - u._Front) * 0.5, yc));
    float lens = exp(-(dTip * dTip) / 2.0) * flash;

    float amount = u._Strength * (passed * (front + 0.5 * prof * ripple) * fade + lens * 1.4) * ends;   // 밀림(칸)

    // 지나간 직선의 가는 흰금빛 심선, 퍼지는 파면의 테두리, 촉 섬광
    float coreLine = exp(-(y * y) / 0.04) * passed * pow(1.0 - life, 3.0) * ends;
    float rimLine = exp(-(dWave * dWave) / (0.06 * 0.06 + 0.02 * ease)) * passed * fade * ends * (0.8 + 0.4 * ripple);
    vec3 glow = u._Color.rgb * (coreLine * 0.8 + rimLine * 0.32 + lens * 0.35) * u._Color.a;
    float glowLum = max(glow.r, max(glow.g, glow.b));

    if (abs(amount) < 0.002 && glowLum < 0.004)
        discard;

    // 굴절: 중심선 쪽 장면을 바깥으로 밀어내 그리고(촉 둘레는 발사 방향으로도 끌림), 채널마다 조금씩 다르게 어긋나게 한다.
    vec2 dispPx = (across * sign(yc) * amount + along * lens * u._Strength * 0.9) * pxPerCell * u._Color.a;
    vec2 texSize = vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 grabUv = gl_FragCoord.xy / texSize;
    vec2 shift = dispPx / texSize;
    vec3 bg;
    bg.r = texture(_GrabDistortionBackground, grabUv - shift * 1.2).r;
    bg.g = texture(_GrabDistortionBackground, grabUv - shift).g;
    bg.b = texture(_GrabDistortionBackground, grabUv - shift * 0.8).b;

    // 장면 캡처 뒤에 그려진 것을 지우지 않도록 일그러진 만큼만 덮는다.
    float cover = clamp(abs(amount) * 6.0 + glowLum, 0.0, 1.0);
    outColor = vec4(bg + glow, cover);
}
