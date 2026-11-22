using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaBondShatter
    {
        private const int MinIntervalTicks = 30;
        private const int BreakDelayTicks = 16;
        private const int SweepTicks = 8;
        private const int ShardsPerEdge = 4;
        private const float HexRadius = 1.45f;
        private const float ShardScale = 0.62f;
        private const int EmberCount = 5;

        private static readonly FloatRange ShardSpeed = new FloatRange(1.2f, 2.6f);
        private static readonly FloatRange ShardSpread = new FloatRange(-16f, 16f);
        private static readonly FloatRange ShardSpin = new FloatRange(-50f, 50f);
        private static readonly FloatRange EmberSpeed = new FloatRange(1.2f, 2.4f);
        private static readonly FloatRange EmberScale = new FloatRange(0.3f, 0.55f);

        private readonly CompApocrypha _comp;

        private int _lastTick = -99999;
        private int _breakTick = -1;
        private Vector3 _center;
        private float _windAngle;
        private Vector3 _wind;

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
            _breakTick = now + BreakDelayTicks;
            _center = _comp.BodyCenter;
            _windAngle = hitAngle;
            _wind = Quaternion.AngleAxis(hitAngle, Vector3.up) * Vector3.forward;

            map.flecks.CreateFleck(FleckMaker.GetDataStatic(_center, map, VVFleckDefOf.VV_Fleck_ApocryphaBondHex));
        }

        public void Tick(int now)
        {
            if (_breakTick < 0 || now < _breakTick) { return; }

            var step = now - _breakTick;
            if (step > SweepTicks)
            {
                _breakTick = -1;
                return;
            }

            var map = _comp.Pawn.Map;
            if (map == null) { return; }

            for (int edge = 0; edge < 6; edge++)
            {
                var a = Vertex(edge);
                var b = Vertex(edge + 1);
                var edgeAngle = (b - a).AngleFlat();
                for (int k = 0; k < ShardsPerEdge; k++)
                {
                    var local = Vector3.Lerp(a, b, (k + 0.5f) / ShardsPerEdge);
                    if (SweepStep(local) != step) { continue; }

                    var data = FleckMaker.GetDataStatic(_center + local, map, VVFleckDefOf.VV_Fleck_ApocryphaBondShard, ShardScale * Rand.Range(0.8f, 1.15f));
                    data.rotation = edgeAngle - 90f + (Rand.Bool ? 0f : 180f);
                    data.rotationRate = ShardSpin.RandomInRange;
                    data.velocityAngle = _windAngle + ShardSpread.RandomInRange;
                    data.velocitySpeed = ShardSpeed.RandomInRange;
                    map.flecks.CreateFleck(data);
                }
            }

            if (step == SweepTicks / 2)
            {
                for (int i = 0; i < EmberCount; i++)
                {
                    var data = FleckMaker.GetDataStatic(_center + Gen.RandomHorizontalVector(HexRadius), map, VVFleckDefOf.VV_Fleck_ApocryphaEmber, EmberScale.RandomInRange);
                    data.velocityAngle = _windAngle + ShardSpread.RandomInRange;
                    data.velocitySpeed = EmberSpeed.RandomInRange;
                    map.flecks.CreateFleck(data);
                }
            }
        }

        private int SweepStep(Vector3 local)
        {
            var along = Vector3.Dot(local, _wind);
            return Mathf.Clamp(Mathf.RoundToInt((along + HexRadius) / (2f * HexRadius) * SweepTicks), 0, SweepTicks);
        }

        private static Vector3 Vertex(int index)
        {
            return Quaternion.AngleAxis(index * 60f, Vector3.up) * Vector3.forward * HexRadius;
        }
    }
}
