using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 〈황금빛 꽃안개〉의 안개층: 바닐라 안개 텍스처를 복사한 재질에 옅은 금빛을 입힌다.
    // 바닐라 FogOverlay 셰이더는 안개 텍스처 두 장을 곱해 그려서 낮에는 거의 보이지 않으므로 밝기(_ColorScale)를 올려 안개 자락이 흐르는 것이 보이게 하고,
    // 화면 전체가 누렇게 뜨지 않도록 색은 흰빛에 가까운 금빛으로 둔다(맑은 날과 비교한 황색 지수 증가 +5 이내).
    // 안개는 조명을 받아 어두워지므로 해 질 녘·밤에는 하늘빛 밝기(날씨의 overlay 색)의 제곱만큼 옅게 해 칙칙한 연기처럼 보이지 않게 한다.
    [StaticConstructorOnStartup]
    public class WeatherOverlay_GoldenMist : WeatherOverlayDualPanner
    {
        private const float MistColorScale = 2.5f;
        private const float MistOpacity = 0.8f;
        private static readonly Color MistColor = new Color(1f, 0.93f, 0.78f);
        private static readonly Material GoldenMistMat = MakeMistMaterial();

        private static Material MakeMistMaterial()
        {
            var material = new Material(MatLoader.LoadMat("Weather/FogOverlayWorld"));
            material.SetFloat("_ColorScale", MistColorScale);
            return material;
        }

        public WeatherOverlay_GoldenMist()
        {
            worldOverlayMat = GoldenMistMat;
            worldOverlayPanSpeed1 = 0.0004f;
            worldOverlayPanSpeed2 = 0.0003f;
            worldPanDir1 = new Vector2(1f, 0.5f).normalized;
            worldPanDir2 = new Vector2(1f, -0.3f).normalized;
        }

        // color: 하늘빛에 따른 날씨 overlay 색, a는 날씨 전환 비율.
        public override void SetOverlayColor(Color color)
        {
            var brightness = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            base.SetOverlayColor(new Color(MistColor.r, MistColor.g, MistColor.b, color.a * MistOpacity * brightness * brightness));
        }
    }

    // 〈황금빛 꽃안개〉의 꽃잎: 보고 있는 화면 곳곳에서 영원꽃 꽃잎이 바람을 타고 비스듬히 흩날린다.
    // 날씨가 바뀌는 동안에는 전환 비율(lerpFactor)만큼 서서히 늘고 준다. 그리는 것은 플렉이 맡으므로 이 오버레이는 그리지 않는다.
    public class WeatherOverlay_GoldenPetals : SkyOverlay
    {
        // 화면 한 칸당 틱마다 생기는 꽃잎 수(기본 확대에서 화면에 60~80장 정도).
        private const float PetalsPerCellPerTick = 0.00014f;
        private const int MaxPetalsPerTick = 3;
        private static readonly FloatRange PetalScale = new FloatRange(0.9f, 1.5f);
        private const int ViewMargin = 4;

        private static readonly Color[] PetalColors =
        {
            new Color(1f, 0.97f, 0.88f, 0.75f),
            new Color(1f, 0.92f, 0.78f, 0.7f),
            new Color(1f, 0.88f, 0.86f, 0.65f),
            new Color(1f, 1f, 0.95f, 0.7f),
        };

        private float _pendingPetals;

        public override void TickOverlay(Map map, float lerpFactor)
        {
            if (map != Find.CurrentMap || lerpFactor <= 0f) { return; }

            var fleckDef = EndingCoreDefOf.VV_Fleck_GoldenBloomPetal;
            if (fleckDef == null) { return; }

            var view = Find.CameraDriver.CurrentViewRect.ExpandedBy(ViewMargin).ClipInsideMap(map);
            if (view.Area <= 0) { return; }

            _pendingPetals = Mathf.Min(_pendingPetals + view.Area * PetalsPerCellPerTick * lerpFactor, MaxPetalsPerTick);
            while (_pendingPetals >= 1f)
            {
                _pendingPetals -= 1f;

                var position = new Vector3(Rand.Range(view.minX, view.maxX + 1f), 0f, Rand.Range(view.minZ, view.maxZ + 1f));
                map.flecks.CreateFleck(new FleckCreationData()
                {
                    def = fleckDef,
                    spawnPosition = position,
                    scale = PetalScale.RandomInRange,
                    rotation = Rand.Range(0f, 360f),
                    rotationRate = Rand.Range(-160f, 160f),
                    velocityAngle = Rand.Range(100f, 140f),
                    velocitySpeed = Rand.Range(0.6f, 1.6f),
                    instanceColor = PetalColors[Rand.Range(0, PetalColors.Length)],
                    ageTicksOverride = -1,
                });
            }
        }

        public override void DrawOverlay(Map map)
        {
        }

        public override void SetOverlayColor(Color color)
        {
        }

        public override void Reset()
        {
            _pendingPetals = 0f;
        }
    }
}
