using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 2페이즈 전환 동안 아포크리파 몸통을 그리는 모핑 재질(VV_Morph 셰이더).
    // 방향별(남·동·북, 서쪽은 동쪽 메시를 좌우 반전하므로 동쪽 재질 사용)로 1페이즈 텍스처(_MainTex) 위에 2페이즈 텍스처(_MorphTex)를 얹는다.
    // 렌더 서브워커는 병렬로 실행되므로 재질은 메인 스레드(전환 시작·불러오기)에서 미리 만들어 두고, 서브워커는 읽기만 한다.
    // 재질 필드가 있는 정적 클래스는 바닐라가 시작 시 경고하므로 StaticConstructorOnStartup을 붙인다(재질은 여전히 지연 생성).
    [StaticConstructorOnStartup]
    public static class ApocryphaMorphMaterials
    {
        private const string ShaderPath = "Map/VV_Morph";
        private static readonly Color VeinColor = new Color(0.85f, 0.05f, 0.22f, 1f);
        private static readonly Color MembraneColor = new Color(1f, 0.32f, 0.86f, 1f);

        // 0: 남, 1: 동(서), 2: 북
        private static Material[] _materials;

        // 심장 박동 속도(초당 박동). 셰이더(_BeatRate)와 C#(몸통 맥동·박동 연출)이 같은 값을 쓴다.
        public const float BeatRate = 1.25f;

        // [메인 스레드] 전환이 시작되거나 전환 중 불러왔을 때 호출한다(불러오기 스레드에서는 CompApocrypha가 메인 스레드로 미룬다).
        public static void EnsureInitialized(float durationSecs)
        {
            if (_materials == null)
            {
                var stages = ApocryphaPawnKindDefOf.VV_Apocrypha?.lifeStages;
                var boundPath = stages.NullOrEmpty() ? null : stages[0].bodyGraphicData?.texPath;
                var unboundPath = stages.NullOrEmpty() ? null : stages[stages.Count - 1].bodyGraphicData?.texPath;
                if (boundPath.NullOrEmpty() || unboundPath.NullOrEmpty()) { return; }

                var shader = ShaderDatabase.LoadShader(ShaderPath);
                if (shader == null) { return; }

                var materials = new Material[3];
                var suffixes = new[] { "_south", "_east", "_north" };
                for (int i = 0; i < suffixes.Length; i++)
                {
                    var from = ContentFinder<Texture2D>.Get(boundPath + suffixes[i], reportFailure: false);
                    var to = ContentFinder<Texture2D>.Get(unboundPath + suffixes[i], reportFailure: false);
                    if (from == null || to == null) { return; }

                    // (텍스처, VV_Morph, 흰색) 요청은 이 연출만 쓰므로 공유 재질의 매개변수를 직접 바꿔도 다른 곳에 영향이 없다.
                    var material = MaterialPool.MatFrom(from, shader, Color.white);
                    material.SetTexture("_MorphTex", to);
                    material.SetColor("_VeinColor", VeinColor);
                    material.SetColor("_MembraneColor", MembraneColor);
                    material.SetFloat("_BeatRate", BeatRate);
                    materials[i] = material;
                }

                _materials = materials;
            }

            foreach (var material in _materials)
            {
                material.SetFloat("_Duration", durationSecs);
            }
        }

        public static Material Get(Rot4 facing)
        {
            var materials = _materials;
            if (materials == null) { return null; }

            if (facing == Rot4.South) { return materials[0]; }
            if (facing == Rot4.North) { return materials[2]; }
            return materials[1];
        }

        // 셰이더와 같은 박동 곡선(박동 순간에 1). ageSecs는 전환 시작부터 경과한 시간(초).
        public static float Beat(float ageSecs)
        {
            return Mathf.Pow(0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * ageSecs * BeatRate), 8f);
        }
    }
}
