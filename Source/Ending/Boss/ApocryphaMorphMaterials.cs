using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 렌더 서브워커는 병렬 실행되므로 재질은 전환 시작·불러오기 시 메인 스레드에서 준비하고 서브워커는 읽기만 한다.
    // 재질을 가진 정적 클래스의 시작 경고를 피하도록 StaticConstructorOnStartup을 붙이되 재질 생성은 지연한다.
    [StaticConstructorOnStartup]
    public static class ApocryphaMorphMaterials
    {
        private const string ShaderPath = "Map/VV_Morph";
        private static readonly Color VeinColor = new Color(0.85f, 0.05f, 0.22f, 1f);
        private static readonly Color MembraneColor = new Color(1f, 0.32f, 0.86f, 1f);

        // 0: 남, 1: 동(서), 2: 북
        private static Material[] _materials;

        // 초당 박동 수. 셰이더의 _BeatRate와 C#의 몸통 맥동·박동 연출이 공유한다.
        public const float BeatRate = 1.25f;

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

                    // 이 연출만 사용하는 (텍스처, VV_Morph, 흰색) 조합이므로 공유 재질의 매개변수를 바꿔도 다른 연출에 영향이 없다.
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

        // 셰이더와 같은 박동 곡선으로 박동 순간에 1이다. ageSecs는 전환 시작 후 경과 시간(초)이다.
        public static float Beat(float ageSecs)
        {
            return Mathf.Pow(0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * ageSecs * BeatRate), 8f);
        }
    }
}
