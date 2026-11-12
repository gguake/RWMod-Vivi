using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
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

        public Vector3 ArcDrawPos => ExactPosition + Vector3.forward * CurrentArcHeight;

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
