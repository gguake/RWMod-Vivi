using RimWorld;
using System.Collections.Generic;
using System.Linq;
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
        private bool _rot;
        private bool _lastRot = true;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public bool IsBreathing => _endTick >= 0;
        public bool IsRotBreath => _rot;
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
            Scribe_Values.Look(ref _rot, "rot", false);
            Scribe_Values.Look(ref _lastRot, "lastRot", true);
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

            if (stunned || now < _readyTick || _comp.Channel.IsChanneling || _comp.Channel.PendingSteal || _comp.EscortSummon.IsCasting) { return; }
            if (!Pawn.IsHashIntervalTick(TargetScanInterval)) { return; }

            var target = FindTarget();
            if (target != null)
            {
                _rot = !_lastRot;
                if (HasBreathEffect(target, _rot) && !HasBreathEffect(target, !_rot))
                {
                    _rot = !_rot;
                }

                _cell = target.Position;
                _endTick = now + Props.breathWarmupTicks;
                Pawn.rotationTracker.FaceCell(_cell);
                _comp.InterruptCurrentJob();
            }
        }

        private static bool HasBreathEffect(Pawn pawn, bool rot)
        {
            var hediffs = pawn.health.hediffSet;
            return rot
                ? hediffs.HasHediff(HediffDefOf.LungRotExposure) || hediffs.HasHediff(HediffDefOf.LungRot)
                : hediffs.HasHediff(HediffDefOf.ToxicBuildup);
        }

        public void Cancel()
        {
            _endTick = -1;
            _cell = IntVec3.Invalid;
        }

        public Vector3 MouthOffset()
        {
            var size = Pawn.ageTracker.CurKindLifeStage?.bodyGraphicData?.drawSize.x ?? 1f;
            var rot = Pawn.Rotation;
            if (rot == Rot4.North) { return new Vector3(0f, 0f, 0.28f * size); }
            if (rot == Rot4.East) { return new Vector3(0.36f * size, 0f, 0.05f * size); }
            if (rot == Rot4.West) { return new Vector3(-0.36f * size, 0f, 0.05f * size); }
            return new Vector3(0f, 0f, 0.1f * size);
        }

        private Pawn FindTarget()
        {
            Pawn best = null;

            var origin = Pawn.Position;
            var facing = Pawn.Rotation.AsAngle;
            var bestDistance = float.MaxValue;

            var pawns = Pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var target = pawns[i];
                var distance = (target.Position - origin).LengthHorizontalSquared;
                if (distance >= bestDistance || distance == 0) { continue; }

                var angle = (target.Position - origin).ToVector3().AngleFlat();
                if (Mathf.Abs(Mathf.DeltaAngle(facing, angle)) > FrontSearchAngle) { continue; }
                if (!_comp.Targeting.IsTargetPawn(target, Props.breathRange, origin)) { continue; }

                bestDistance = distance;
                best = target;
            }

            return best;
        }

        private void Release(int now)
        {
            var map = Pawn.Map;
            var origin = Pawn.Position;
            var target = _cell;

            Cancel();
            _lastRot = _rot;
            _readyTick = now + Props.breathCooldownTicks;
            if (!target.IsValid || target == origin) { return; }

            var baseAngle = (target - origin).ToVector3().AngleFlat();
            var halfAngle = Props.breathAngle * 0.5f;
            var hornetDef = VVThingDefOf.VV_TitanicHornet;

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

                    if (_rot)
                    {
                        ApplyRot(victim);
                    }
                    else
                    {
                        ApplyToxic(victim, baseAngle);
                    }
                }
            }
            _tmpCellThings.Clear();

            if (Props.breathGasRadius > 0f)
            {
                if (_rot)
                {
                    GasUtility.AddGas(target, map, GasType.RotStink, Props.breathGasRadius);
                }
                else if (ModsConfig.BiotechActive)
                {
                    GasUtility.AddGas(target, map, GasType.ToxGas, Props.breathGasRadius);
                }
            }

            ThrowFlecks(baseAngle, halfAngle);
            _comp.SpawnEffecter(VVEffecterDefOf.VV_Effecter_ApocryphaBreath, MouthOffset());
            ApocryphaUtility.ShakeCamera(map, 0.6f);
        }

        private void ApplyToxic(Pawn victim, float angle)
        {
            var toxic = Props.breathToxicSeverity * Mathf.Max(0f, 1f - victim.GetStatValue(StatDefOf.ToxicResistance));
            if (toxic > 0f)
            {
                HealthUtility.AdjustSeverity(victim, HediffDefOf.ToxicBuildup, toxic);
            }

            if (Props.breathAcidBurnDamage > 0f && !victim.Dead)
            {
                victim.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, Props.breathAcidBurnDamage, 0f, angle, Pawn));
            }
        }

        private static void ApplyRot(Pawn victim)
        {
            var race = victim.RaceProps;
            if (!race.Animal && !race.Humanlike) { return; }
            if (race.isImmuneToInfections || victim.health.hediffSet.HasHediff(HediffDefOf.LungRotExposure)) { return; }
            if (!GasUtility.GetLungRotAffectedBodyParts(victim).Any()) { return; }

            victim.health.AddHediff(HediffDefOf.LungRotExposure);
        }

        public FleckDef GasFleck(bool accent)
        {
            if (_rot)
            {
                return accent ? VVFleckDefOf.VV_Fleck_ApocryphaRotGasBile : VVFleckDefOf.VV_Fleck_ApocryphaRotGas;
            }

            return accent ? VVFleckDefOf.VV_Fleck_ApocryphaToxicGasViolet : VVFleckDefOf.VV_Fleck_ApocryphaToxicGas;
        }

        private void ThrowFlecks(float baseAngle, float halfAngle)
        {
            var map = Pawn.Map;
            var mouth = Pawn.DrawPos + MouthOffset();
            if (!mouth.ShouldSpawnMotesAt(map)) { return; }

            for (int i = 0; i < 42; i++)
            {
                var fleckDef = GasFleck(Rand.Chance(0.3f));
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
