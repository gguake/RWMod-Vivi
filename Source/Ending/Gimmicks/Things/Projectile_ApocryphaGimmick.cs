using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 바닐라 DrawPos에는 포물선 높이가 없어 꼬리 위치를 직접 계산한다.
    // 두꺼운 지붕에 막혀 Impact 없이 사라지지 않도록 지붕·명중 판정을 우회한다.
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

        // 비공개 Projectile.ArcHeightFactor와 동일하게 짧은 비행의 포물선 높이를 거리의 20%로 제한한다.
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

        // 포물선 높이를 더한 실제로 보이는 위치
        public Vector3 ArcDrawPos => ExactPosition + Vector3.forward * CurrentArcHeight;

        // 텍스처의 머리가 실제 위치에 오도록 중심에서 앞선 거리만큼 그리기 위치를 뒤로 보정한다.
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

            var position = ArcDrawPos;
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
