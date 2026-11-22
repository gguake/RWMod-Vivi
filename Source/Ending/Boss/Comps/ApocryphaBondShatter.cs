using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaBondShatter
    {
        private const int MinIntervalTicks = 30;
        private const int ShardDelayTicks = 2;
        private const float ImpactRadius = 1.1f;

        private static readonly IntRange ShardCount = new IntRange(4, 6);
        private static readonly FloatRange ShardSpeed = new FloatRange(2f, 3.6f);
        private static readonly FloatRange ShardSpread = new FloatRange(-32f, 32f);
        private static readonly FloatRange ShardSpin = new FloatRange(-220f, 220f);
        private static readonly FloatRange ShardScale = new FloatRange(0.3f, 0.45f);
        private static readonly IntRange SparkCount = new IntRange(2, 3);
        private static readonly FloatRange SparkSpeed = new FloatRange(2.6f, 4.2f);
        private static readonly FloatRange SparkScale = new FloatRange(0.16f, 0.26f);

        private readonly CompApocrypha _comp;

        private int _lastTick = -99999;
        private int _shardTick = -1;
        private Vector3 _impact;
        private float _hitAngle;

        public ApocryphaBondShatter(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void Trigger(float hitAngle)
        {
            var now = GenTicks.TicksGame;
            var map = _comp.Pawn.Map;
            if (map == null || now - _lastTick < MinIntervalTicks) { return; }

            _lastTick = now;
            _shardTick = now + ShardDelayTicks;
            _hitAngle = hitAngle;

            var center = _comp.BodyCenter;
            _impact = center - Quaternion.AngleAxis(hitAngle, Vector3.up) * Vector3.forward * ImpactRadius;

            var data = FleckMaker.GetDataStatic(center, map, VVFleckDefOf.VV_Fleck_ApocryphaBondHex);
            data.rotation = hitAngle - 90f;
            map.flecks.CreateFleck(data);
        }

        public void Tick(int now)
        {
            if (_shardTick < 0 || now < _shardTick) { return; }

            _shardTick = -1;
            var map = _comp.Pawn.Map;
            if (map == null) { return; }

            var shards = ShardCount.RandomInRange;
            for (int i = 0; i < shards; i++)
            {
                var data = FleckMaker.GetDataStatic(_impact + Gen.RandomHorizontalVector(0.15f), map, VVFleckDefOf.VV_Fleck_ApocryphaBondShard, ShardScale.RandomInRange);
                data.rotation = Rand.Range(0f, 360f);
                data.rotationRate = ShardSpin.RandomInRange;
                data.velocityAngle = _hitAngle + ShardSpread.RandomInRange;
                data.velocitySpeed = ShardSpeed.RandomInRange;
                map.flecks.CreateFleck(data);
            }

            var sparks = SparkCount.RandomInRange;
            for (int i = 0; i < sparks; i++)
            {
                var data = FleckMaker.GetDataStatic(_impact, map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, SparkScale.RandomInRange);
                data.velocityAngle = _hitAngle + ShardSpread.RandomInRange * 0.6f;
                data.velocitySpeed = SparkSpeed.RandomInRange;
                map.flecks.CreateFleck(data);
            }
        }
    }
}
