using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ProjectileFleckSpray : CompProperties
    {
        public FleckDef fleckDef;
        public int intervalTicks = 1;
        public IntRange burstCount = new IntRange(1, 1);

        public float positionRadius = 0.05f;

        public FloatRange scaleRange = new FloatRange(0.4f, 0.7f);
        public FloatRange speedRange = new FloatRange(0.15f, 0.5f);
        public FloatRange rotationRate = new FloatRange(-240f, 240f);

        public CompProperties_ProjectileFleckSpray()
        {
            compClass = typeof(CompProjectileFleckSpray);
        }
    }

    public class CompProjectileFleckSpray : ThingComp
    {
        public CompProperties_ProjectileFleckSpray Props => (CompProperties_ProjectileFleckSpray)props;

        private int _ticksToNext;

        public override void CompTick()
        {
            if (Props.fleckDef == null || !parent.Spawned) { return; }

            var map = parent.Map;
            if (map == null) { return; }

            if (_ticksToNext > 0)
            {
                _ticksToNext--;
                return;
            }
            _ticksToNext = Mathf.Max(1, Props.intervalTicks);

            var basePos = parent.DrawPos;
            if (!basePos.ToIntVec3().ShouldSpawnMotesAt(map)) { return; }

            var count = Props.burstCount.RandomInRange;
            for (var i = 0; i < count; i++)
            {
                var offset = new Vector3(
                    Rand.Range(-Props.positionRadius, Props.positionRadius),
                    0f,
                    Rand.Range(-Props.positionRadius, Props.positionRadius));

                var data = FleckMaker.GetDataStatic(basePos + offset, map, Props.fleckDef, Props.scaleRange.RandomInRange);
                data.rotationRate = Props.rotationRate.RandomInRange;
                data.velocityAngle = Rand.Range(0f, 360f);
                data.velocitySpeed = Props.speedRange.RandomInRange;
                map.flecks.CreateFleck(data);
            }
        }
    }
}
