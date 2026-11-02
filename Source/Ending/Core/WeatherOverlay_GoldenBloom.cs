using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // FogOverlay는 안개 텍스처 두 장을 곱해 낮에 잘 보이지 않으므로 _ColorScale로 밝기를 높인다.
    // 해 질 녘과 밤에는 조명으로 안개가 칙칙해지므로 하늘빛 밝기의 제곱에 비례해 옅게 만든다.
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

        public override void SetOverlayColor(Color color)
        {
            var brightness = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            base.SetOverlayColor(new Color(MistColor.r, MistColor.g, MistColor.b, color.a * MistOpacity * brightness * brightness));
        }
    }

    // 렌더링은 플렉이 담당하므로 이 오버레이는 직접 그리지 않는다.
    public class WeatherOverlay_GoldenPetals : SkyOverlay
    {
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

            var fleckDef = VVFleckDefOf.VV_Fleck_GoldenBloomPetal;
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
