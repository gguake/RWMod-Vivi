#version 450

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 0.9, 0.7, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _Core;      // @default = 0.5
    float _Bloom;     // @default = 0.5
    float _Spread;    // @default = 0.03
    float _Expand;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

vec4 tap(vec2 uv)
{
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
        return vec4(0.0);
    vec4 c = texture(_MainTex, uv * u._MainTex_ST.zw + u._MainTex_ST.xy);
    return vec4(c.rgb * c.a, c.a);
}

void main()
{
    vec2 uv = (vUv - 0.5) * u._Expand + 0.5;
    vec4 c0 = tap(uv);

    vec4 blur = vec4(0.0);
    float weight = 0.0;
    for (int ring = 1; ring <= 3; ring++)
    {
        float r = u._Spread * float(ring) / 3.0;
        float w = 1.0 / float(ring);
        for (int i = 0; i < 10; i++)
        {
            float a = (float(i) + 0.5 * float(ring)) * 0.6283185;
            blur += tap(uv + vec2(cos(a), sin(a)) * r) * w;
            weight += w;
        }
    }
    blur /= weight;

    float lum = dot(c0.rgb, vec3(0.299, 0.587, 0.114));
    vec3 core = mix(c0.rgb, vec3(lum) * u._Color.rgb, 0.35) * u._Core;
    float blurLum = dot(blur.rgb, vec3(0.299, 0.587, 0.114));
    vec3 bloom = u._Color.rgb * mix(blur.a, blurLum, 0.5) * (1.0 - 0.6 * c0.a) * u._Bloom;

    outColor = vec4(clamp((core + bloom) * u._Color.a, 0.0, 1.0), 1.0);
}
