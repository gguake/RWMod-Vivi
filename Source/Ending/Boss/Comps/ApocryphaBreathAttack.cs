using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaBreathAttack : IExposable
    {
        private const int TargetScanInterval = 20;
        private const int StunRecoverTicks = 300;
        private const float FrontSearchAngle = 45f;

        private static readonly List<Thing> _tmpCellThings = new List<Thing>();

        private readonly CompApocrypha _comp;

        private int _readyTick;
        private IntVec3 _cell = IntVec3.Invalid;
        private int _endTick = -1;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public bool IsBreathing => _endTick >= 0;
        public IntVec3 BreathCell => _cell;

        public ApocryphaBreathAttack(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _readyTick, "readyTick", 0);
            Scribe_Values.Look(ref _cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref _endTick, "endTick", -1);
        }

        public void SetReadyTick(int tick)
        {
            _readyTick = tick;
        }

        public void Tick(int now, bool stunned)
        {
            if (IsBreathing)
            {
                if (stunned)
                {
                    Cancel();
                    _readyTick = now + StunRecoverTicks;
                    return;
                }

                Pawn.rotationTracker.FaceCell(_cell);
                if (now >= _endTick)
                {
                    Release(now);
                }
                return;
            }

            if (stunned || now < _readyTick || _comp.Channel.IsChanneling || _comp.Channel.PendingSteal) { return; }
            if (!Pawn.IsHashIntervalTick(TargetScanInterval)) { return; }

            if (TryFindTarget(out var cell))
            {
                _cell = cell;
                _endTick = now + Props.breathWarmupTicks;
                Pawn.rotationTracker.FaceCell(cell);
                _comp.InterruptCurrentJob();
            }
        }

        public void Cancel()
        {
            _endTick = -1;
            _cell = IntVec3.Invalid;
        }

        public Vector3 MouthOffset()
        {
            var size = _comp.CurrentVisualDrawSize;
            var rot = Pawn.Rotation;
            if (rot == Rot4.North) { return new Vector3(0f, 0f, 0.28f * size); }
            if (rot == Rot4.East) { return new Vector3(0.36f * size, 0f, 0.05f * size); }
            if (rot == Rot4.West) { return new Vector3(-0.36f * size, 0f, 0.05f * size); }
            return new Vector3(0f, 0f, 0.1f * size);
        }

        private bool TryFindTarget(out IntVec3 cell)
        {
            cell = IntVec3.Invalid;

            var map = Pawn.Map;
            var origin = Pawn.Position;
            var facing = Pawn.Rotation.AsAngle;
            var rangeSquared = Props.breathRange * Props.breathRange;
            var bestDistance = float.MaxValue;

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var target = pawns[i];
                if (target == Pawn || target.Dead || target.Downed) { continue; }

                var distance = (target.Position - origin).LengthHorizontalSquared;
                if (distance > rangeSquared || distance >= bestDistance || distance == 0) { continue; }

                var angle = (target.Position - origin).ToVector3().AngleFlat();
                if (Mathf.Abs(Mathf.DeltaAngle(facing, angle)) > FrontSearchAngle) { continue; }
                if (!Pawn.HostileTo(target)) { continue; }
                if (!GenSight.LineOfSight(origin, target.Position, map, skipFirstCell: true)) { continue; }

                bestDistance = distance;
                cell = target.Position;
            }

            return cell.IsValid;
        }

        private void Release(int now)
        {
            var map = Pawn.Map;
            var origin = Pawn.Position;
            var target = _cell;

            Cancel();
            _readyTick = now + Props.breathCooldownTicks;
            if (!target.IsValid || target == origin) { return; }

            var baseAngle = (target - origin).ToVector3().AngleFlat();
            var halfAngle = Props.breathAngle * 0.5f;
            var hornetDef = VVThingDefOf.VV_TitanicHornet;

            // GenExplosion.affectedAngle은 ±180° 경계에서 잘리는 문제가 있어 부채꼴 칸을 직접 계산한다.
            foreach (var cell in GenRadial.RadialCellsAround(origin, Props.breathRange, useCenter: false))
            {
                if (!cell.InBounds(map)) { continue; }
                if (Mathf.Abs(Mathf.DeltaAngle(baseAngle, (cell - origin).ToVector3().AngleFlat())) > halfAngle) { continue; }
                if (!GenSight.LineOfSight(origin, cell, map, skipFirstCell: true)) { continue; }

                _tmpCellThings.Clear();
                _tmpCellThings.AddRange(cell.GetThingList(map));
                foreach (var thing in _tmpCellThings)
                {
                    if (!(thing is Pawn victim) || victim == Pawn || victim.Dead) { continue; }
                    if (victim.def == hornetDef) { continue; }
                    if (victim.Faction != null && victim.Faction == Pawn.Faction) { continue; }

                    var toxic = Props.breathToxicSeverity * Mathf.Max(0f, 1f - victim.GetStatValue(StatDefOf.ToxicResistance));
                    if (toxic > 0f)
                    {
                        HealthUtility.AdjustSeverity(victim, HediffDefOf.ToxicBuildup, toxic);
                    }

                    if (Props.breathAcidBurnDamage > 0f && !victim.Dead && DamageDefOf.AcidBurn != null)
                    {
                        victim.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, Props.breathAcidBurnDamage, 0f, baseAngle, Pawn));
                    }
                }
            }
            _tmpCellThings.Clear();

            // 브레스가 닿은 대상 자리에 짙은 독성 가스를 남긴다. 말벌은 오염된 영원꽃 동화로 독성 환경에 면역이다.
            if (ModsConfig.BiotechActive && Props.breathGasRadius > 0f)
            {
                GasUtility.AddGas(target, map, GasType.ToxGas, Props.breathGasRadius);
            }

            ThrowFlecks(baseAngle, halfAngle);
            _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaBreath, MouthOffset());
            ApocryphaUtility.ShakeCamera(map, 0.6f);
        }

        private void ThrowFlecks(float baseAngle, float halfAngle)
        {
            var map = Pawn.Map;
            var mouth = Pawn.DrawPos + MouthOffset();
            if (!mouth.ShouldSpawnMotesAt(map)) { return; }

            for (int i = 0; i < 42; i++)
            {
                var fleckDef = Rand.Chance(0.3f) ? VVFleckDefOf.VV_Fleck_ApocryphaToxicGasViolet : VVFleckDefOf.VV_Fleck_ApocryphaToxicGas;
                var data = FleckMaker.GetDataStatic(mouth + Gen.RandomHorizontalVector(0.3f), map, fleckDef, Rand.Range(1.1f, 2.1f));
                data.velocityAngle = baseAngle + Rand.Range(-halfAngle, halfAngle);
                data.velocitySpeed = Rand.Range(2.5f, 6.5f);
                data.rotation = Rand.Range(0f, 360f);
                data.rotationRate = Rand.Range(-70f, 70f);
                map.flecks.CreateFleck(data);
            }
        }
    }
}
