#version 450

// 거대 엘텍스 바늘의 조준 경고 띠(가산 혼합). 요정 비비 사출탄의 연꽃 조준 표식(VV_MagicCircle)과 같은 금빛 선·꽃잎 모양으로,
// 발사 직선 전체를 텍스처 없이 수식으로 그린다(U = 길이 방향, V = 폭 방향).
// - 가장자리: 흰금빛 심선과 안쪽으로 번지는 금빛(띠 폭이 곧 피해 범위다)
// - 꽃잎 갈매기: 끝이 뾰족한 꽃잎 두 장이 ">"로 모여 발사 방향(+U)을 가리키며 흐른다
// - 조준 진행도(_Progress)만큼 시작점에서부터 안쪽이 금빛으로 차오르고, 차오르는 앞머리에 밝은 선이 지나간다
// - 발사 직전(진행도 80% 이후) 연꽃 표식처럼 전체가 밝아지며 빠르게 깜빡인다
// _Length, _Width(칸)로 무늬 간격과 선 굵기를 칸 단위로 맞추므로 띠 길이가 달라도 모양이 같다.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.84, 0.45, 1.0)
    vec4 _CoreColor;      // @color @default = vec4(1.0, 0.94, 0.75, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _Progress;      // @default = 0.0
    float _Length;        // @default = 30.0
    float _Width;         // @default = 3.0
} u;

// 세로 방향 렌즈(양 끝이 뾰족한 꽃잎) 거리장(iq sdVesica). VV_MagicCircle의 꽃잎과 같은 모양.
float sdVesica(vec2 p, float r, float d)
{
    p = abs(p);
    float b = sqrt(r * r - d * d);
    return ((p.y - b) * d > p.x * b) ? length(p - vec2(0.0, b)) : length(p - vec2(-d, 0.0)) - r;
}

// 길이 len, 반폭 w인 꽃잎(로컬 y축 방향)
float sdPetal(vec2 p, float len, float w)
{
    float h = len * 0.5;
    return sdVesica(p, (h * h / w + w) * 0.5, (h * h / w - w) * 0.5);
}

float lineCore(float d, float halfWidth, float px)
{
    return 1.0 - smoothstep(halfWidth - px * 0.5, halfWidth + px, d);
}

void main()
{
    float L = u._Length;
    float halfW = u._Width * 0.5;
    float x = vUv.x * L;                         // 시작점부터의 거리(칸)
    float ys = (vUv.y - 0.5) * u._Width;         // 중심선에서의 거리(칸, 부호 있음)
    float y = abs(ys);
    float px = max(max(fwidth(x), fwidth(ys)), 1e-4);   // 화면 1픽셀이 몇 칸인지

    float core = 0.0;   // 흰금빛 선
    float glow = 0.0;   // 금빛 빛

    // 가장자리: 띠 끝에서 0.07칸 안쪽의 심선과 안쪽으로 번지는 빛
    float dEdge = abs(y - (halfW - 0.07));
    core += lineCore(dEdge, 0.035, px);
    glow += exp(-max(halfW - y, 0.0) / 0.3) * 0.3;
    glow += 0.04;

    // 조준 진행도: 시작점에서부터 금빛으로 차오르고, 앞머리에 밝은 선이 지나간다.
    float front = u._Progress * L;
    float charged = 1.0 - smoothstep(front - px, front + px, x);
    float charging = step(0.001, u._Progress) * (1.0 - step(0.999, u._Progress));
    float dFront = abs(x - front);
    glow += charged * 0.09;
    core += lineCore(dFront, 0.04, px) * charging * 0.8;
    glow += exp(-dFront / 0.45) * 0.55 * charging;

    // 꽃잎 갈매기: 2.2칸 간격으로 발사 방향으로 흐른다. 중심선 대칭이므로 위쪽 꽃잎 하나만 계산한다.
    // 꽃잎은 뒤쪽 바깥(-0.5, 0.62)에서 앞쪽 중심(0.5, 0)으로 뻗어, 두 장이 앞쪽 끝에서 만나 ">"가 된다.
    float spacing = 2.2;
    float xs = x - u._AgeSecs * 2.6;
    float lx = xs - (floor(xs / spacing) + 0.5) * spacing;
    vec2 q = vec2(lx, y);
    vec2 tip = vec2(0.5, 0.0);
    vec2 back = vec2(-0.5, 0.62);
    vec2 dir = normalize(tip - back);
    vec2 c = (tip + back) * 0.5;
    vec2 pl = vec2(dot(q - c, vec2(-dir.y, dir.x)), dot(q - c, dir));
    float dPetal = sdPetal(pl, length(tip - back), 0.13);
    float petalFill = 1.0 - smoothstep(-px, px, dPetal);
    float petalLine = lineCore(abs(dPetal), 0.03, px);
    float petalAlpha = 0.35 + 0.65 * charged;
    core += petalLine * 0.55 * petalAlpha;
    glow += petalFill * 0.22 * petalAlpha;

    // 발사 직전: 전체가 밝아지며 빠르게 깜빡인다.
    float flash = smoothstep(0.8, 1.0, u._Progress);
    float boost = 1.0 + flash * (0.5 + 0.4 * sin(u._AgeSecs * 45.0));

    // 양 끝: 시작점은 짧게, 사거리 끝은 0.6칸에 걸쳐 흐려진다(가장자리 선은 끝까지 남긴다).
    float ends = smoothstep(0.0, 0.3, x) * smoothstep(0.0, 0.6, L - x);

    vec3 col = (u._CoreColor.rgb * min(core, 1.5) + u._Color.rgb * glow) * boost * ends * u._Color.a * vColor.rgb;
    outColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
