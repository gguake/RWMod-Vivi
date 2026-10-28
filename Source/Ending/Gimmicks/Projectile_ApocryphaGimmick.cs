using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 아포크리파가 쏘는 기믹 투사체(요정 비비 사출탄·씨앗) 공용 처리.
    // - 포물선을 따라 반짝임 꼬리를 남긴다(바닐라 DrawPos에는 포물선 높이가 없어 직접 계산).
    // - 지붕/명중 판정 없이 목표 칸에 바로 착탄한다(두꺼운 지붕에 막히면 Impact 없이 사라지는 바닐라 동작을 피함).
    public abstract class Projectile_ApocryphaGimmick : Projectile
    {
        [Unsaved]
        private ApocryphaProjectileExtension _extension;
        protected ApocryphaProjectileExtension Extension => _extension ?? (_extension = def.GetModExtension<ApocryphaProjectileExtension>());

        protected Vector3 FlightDirection
        {
            get
            {
                var direction = (destination - origin).Yto0();
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            }
        }

        // 바닐라 Projectile.ArcHeightFactor(private)와 같은 계산: 짧게 날아가면 포물선 높이를 거리의 20%로 제한한다.
        protected float CurrentArcHeight
        {
            get
            {
                var arcHeightFactor = def.projectile.arcHeightFactor;
                var distanceSquared = (destination - origin).MagnitudeHorizontalSquared();
                if (arcHeightFactor * arcHeightFactor > distanceSquared * 0.2f * 0.2f)
                {
                    arcHeightFactor = Mathf.Sqrt(distanceSquared) * 0.2f;
                }

                return arcHeightFactor * GenMath.InverseParabola(DistanceCoveredFractionArc);
            }
        }

        // 텍스처의 머리가 중심보다 앞쪽에 그려져 있으면 그만큼 뒤로 당겨 그려, 머리가 실제 위치와 맞게 한다(그리기에만 영향).
        public override Vector3 DrawPos
        {
            get
            {
                var headOffset = Extension?.headOffset ?? 0f;
                return headOffset != 0f ? base.DrawPos - FlightDirection * headOffset : base.DrawPos;
            }
        }

        protected override void Tick()
        {
            base.Tick();

            if (Spawned && !landed)
            {
                TrySpawnTrail();
            }
        }

        protected override void ImpactSomething()
        {
            Impact(null);
        }

        private void TrySpawnTrail()
        {
            var extension = Extension;
            if (extension?.trailFleck == null) { return; }
            if (!this.IsHashIntervalTick(Mathf.Max(1, extension.trailIntervalTicks))) { return; }

            var position = ExactPosition + Vector3.forward * CurrentArcHeight;
            if (!position.ShouldSpawnMotesAt(Map, drawOffscreen: false)) { return; }

            var data = FleckMaker.GetDataStatic(position + Gen.RandomHorizontalVector(0.12f), Map, extension.trailFleck, extension.trailScale.RandomInRange);
            data.instanceColor = extension.trailColor;
            data.rotation = Rand.Range(0f, 360f);
            data.rotationRate = Rand.Range(-90f, 90f);
            data.velocityAngle = Rand.Range(0f, 360f);
            data.velocitySpeed = Rand.Range(0.05f, 0.3f);
            Map.flecks.CreateFleck(data);
        }
    }
}
