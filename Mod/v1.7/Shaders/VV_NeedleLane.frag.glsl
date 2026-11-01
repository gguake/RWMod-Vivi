#version 450

// 거대 엘텍스 바늘의 조준 경고 띠(가산 혼합). 발사 직선 전체를 텍스처 없이 수식으로 그린다(U = 길이 방향, V = 폭 방향).
// 띠 안은 흐리게, 가장자리는 밝은 선으로 그리고, 조준 진행도(_Progress)만큼 시작점에서부터 안쪽이 차오른다(앞머리는 더 밝다).
// 발사 방향으로 흐르는 갈매기(>) 무늬가 방향을 알려 주며, 발사 직전에는 띠 전체가 빠르게 깜빡인다.
// _Length, _Width(칸)로 무늬 간격과 선 굵기를 칸 단위로 맞추므로 띠 길이가 달라도 모양이 같다.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 0.42, 0.5, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _Progress;      // @default = 0.0
    float _Length;        // @default = 30.0
    float _Width;         // @default = 3.0
} u;

void main()
{
    float x = vUv.x * u._Length;                 // 시작점부터의 거리(칸)
    float y = abs(vUv.y - 0.5) * u._Width;       // 중심선에서의 거리(칸)
    float halfW = u._Width * 0.5;
    float pxY = max(fwidth(y), 1e-4);            // 화면 1픽셀이 몇 칸인지
    float pxX = max(fwidth(x), 1e-4);

    // 가장자리 선: 0.08칸 두께, 1~2픽셀 부드럽게
    float edgeDist = halfW - y;
    float edge = 1.0 - smoothstep(0.08, 0.08 + pxY * 1.5, edgeDist);

    // 조준 진행도만큼 시작점에서부터 안쪽이 차오르고, 차오르는 앞머리가 밝게 빛난다.
    float front = u._Progress * u._Length;
    float charged = 1.0 - smoothstep(front - pxX, front + pxX, x);
    float charging = step(0.001, u._Progress) * (1.0 - step(0.999, u._Progress));
    float head = exp(-abs(x - front) * 2.5) * charging;

    // 갈매기 무늬: 2칸 간격으로 발사 방향(+U)을 가리키며 흐른다. 가장자리 선 안쪽에만 그린다.
    float c = fract((x + y * 0.8) * 0.5 - u._AgeSecs * 1.6);
    float chevron = smoothstep(0.0, 0.06, c) * (1.0 - smoothstep(0.16, 0.24, c));
    chevron *= 1.0 - smoothstep(halfW * 0.55, halfW * 0.8, y);

    // 발사 직전(진행도 80% 이후) 깜빡임
    float flash = smoothstep(0.8, 1.0, u._Progress) * (0.5 + 0.5 * sin(u._AgeSecs * 50.0));

    // 양 끝: 시작점은 짧게, 사거리 끝은 0.8칸에 걸쳐 흐려진다.
    float ends = smoothstep(0.0, 0.4, x) * smoothstep(0.0, 0.8, u._Length - x);

    float a = 0.10 + 0.18 * charged + 0.55 * head + 0.28 * chevron * (0.4 + 0.6 * charged) + 0.25 * flash;
    a = max(a, edge * (0.75 + 0.25 * flash));

    vec3 col = u._Color.rgb * vColor.rgb * a * ends * u._Color.a;
    outColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
