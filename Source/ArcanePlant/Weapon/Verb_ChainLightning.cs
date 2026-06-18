using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class VerbProperties_ChainLightning : VerbProperties
    {
        public DamageDef damageDef;
        public int? damageAmount;

        public ThingDef lightningMoteDef;
        public float lightningStartOffset;
        public EffecterDef lightningTargetEffectDef;

        public FleckDef lightningLineFleckDef;
        public SimpleCurve lightningLineFleckChanceCurve;
        public float lightningPathSegmentLength = 1.15f;
        public float lightningPathJitter = 0.45f;
        public int lightningPathMaxSegments = 20;

        public int chainCount;
        public float chainRadius;

        public VerbProperties_ChainLightning()
        {
            verbClass = typeof(Verb_ChainLightning);
        }
    }

    public class Verb_ChainLightning : Verb
    {
        public class LightningEffect : IExposable
        {
            public IntVec3 targetCell;
            public Vector3 targetPosition;

            [Unsaved]
            public MoteDualAttached mote;

            [Unsaved]
            public List<MoteDualAttached> motes;

            [Unsaved]
            public List<Vector3> pathPoints;

            [Unsaved]
            public Effecter targetEffecter;

            public void DestroyMotes()
            {
                if (mote != null && !mote.Destroyed)
                {
                    mote.Destroy();
                }
                mote = null;

                if (motes != null)
                {
                    for (int i = 0; i < motes.Count; ++i)
                    {
                        if (motes[i] != null && !motes[i].Destroyed)
                        {
                            motes[i].Destroy();
                        }
                    }
                    motes.Clear();
                }
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref targetCell, "targetCell");
                Scribe_Values.Look(ref targetPosition, "targetPosition");
            }
        }

        public VerbProperties_ChainLightning VerbProps => (VerbProperties_ChainLightning)verbProps;

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null)
                {
                    _manaComp = caster.TryGetComp<CompMana>();
                }

                if (_manaComp == null)
                {
                    _manaComp = EquipmentSource?.TryGetComp<CompMana>();
                }

                return _manaComp;
            }
        }
        [Unsaved]
        private CompMana _manaComp;

        private List<Thing> _subTargets = new List<Thing>();
        private List<LightningEffect> _effects = new List<LightningEffect>();

        protected override int ShotsPerBurst => BurstShotCount;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref _subTargets, "subTargets", LookMode.Reference);
            Scribe_Collections.Look(ref _effects, "effects", LookMode.Deep);
        }

        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }

            var compMana = ManaComp;
            if (compMana == null) { return false; }

            var manaPerShoot = EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) / ShotsPerBurst ?? 0;
            if (compMana.Stored < manaPerShoot) { return false; }

            var los = TryFindShootLineFromTo(caster.Position, currentTarget, out var resultingLine);
            if (verbProps.stopBurstWithoutLos && !los)
            {
                return false;
            }

            lastShotTick = Find.TickManager.TicksGame;

            var shot = false;
            if (currentTarget.HasThing)
            {
                shot = TryGiveDamage(currentTarget.Thing);
            }

            for (int i = 0; i < _subTargets.Count; ++i)
            {
                if (!TryGiveDamage(_subTargets[i]))
                {
                    break;
                }
            }

            if (shot)
            {
                compMana.Stored -= manaPerShoot;
            }
            return true;

            bool TryGiveDamage(Thing thing)
            {
                if (thing == null)
                {
                    return false;
                }

                var log = new BattleLogEntry_RangedImpact(caster, thing, thing, EquipmentSource.def, null, null);
                var angleFlat = (currentTarget.Cell - caster.Position).AngleFlat;

                var amount = (float)(VerbProps.damageAmount ?? VerbProps.damageDef.defaultDamage);
                if (amount <= 0) { return false; }

                amount *= EquipmentSource?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;

                if (VerbProps.damageDef != null)
                {
                    var dinfo = new DamageInfo(
                        VerbProps.damageDef,
                        amount,
                        VerbProps.damageDef.defaultArmorPenetration,
                        angleFlat,
                        caster,
                        null,
                        EquipmentSource.def,
                        DamageInfo.SourceCategory.ThingOrUnknown,
                        thing);

                    thing.TakeDamage(dinfo).AssociateWithLog(log);
                }

                var empIinfo = new DamageInfo(
                    DamageDefOf.EMP,
                    amount * verbProps.burstShotCount,
                    VerbProps.damageDef.defaultArmorPenetration,
                    angleFlat,
                    caster,
                    null,
                    EquipmentSource.def,
                    DamageInfo.SourceCategory.ThingOrUnknown,
                    thing);

                thing.TakeDamage(empIinfo).AssociateWithLog(log);

                return true;
            }
        }

        public override void BurstingTick()
        {
            EnsureLightningVisuals();

            for (int i = 0; i < _effects.Count; ++i)
            {
                var eff = _effects[i];
                var offsetTarget = eff.targetPosition - eff.targetCell.ToVector3Shifted();

                MaintainLightningMotes(eff);

                if (eff.targetEffecter == null && VerbProps.lightningTargetEffectDef != null)
                {
                    eff.targetEffecter = VerbProps.lightningTargetEffectDef.Spawn(eff.targetCell, caster.Map, offsetTarget);
                }
                if (eff.targetEffecter != null)
                {
                    eff.targetEffecter.offset = offsetTarget;
                    eff.targetEffecter.EffectTick(new TargetInfo(eff.targetCell, caster.Map), TargetInfo.Invalid);
                    eff.targetEffecter.ticksLeft--;
                }

                if (VerbProps.lightningLineFleckDef != null)
                {
                    ThrowLightningLineFlecks(eff, i);
                }
            }

        }

        public override void WarmupComplete()
        {
            foreach (var effect in _effects)
            {
                effect.DestroyMotes();
            }

            _subTargets.Clear();
            _effects.Clear();

            burstShotsLeft = ShotsPerBurst;
            state = VerbState.Bursting;

            _effects.Add(new LightningEffect()
            {
                targetCell = currentTarget.Cell,
                targetPosition = currentTarget.CenterVector3
            });

            TryCastNextBurstShot();

            var chainSourceCell = currentTarget.Cell;
            for (int i = 0; i < VerbProps.chainCount; ++i)
            {
                var target = GenClosest.ClosestThing_Global(
                    chainSourceCell,
                    caster.Map.attackTargetsCache.GetPotentialTargetsFor((IAttackTargetSearcher)caster),
                    VerbProps.chainRadius,
                    validator: (Thing t) =>
                    {
                        var attackTarget = t as IAttackTarget;
                        if (attackTarget == null) { return false; }

                        if (t is Building) { return false; }
                        if (t == currentTarget.Thing || _subTargets.Contains(t)) { return false; }

                        if (!t.Spawned || !t.HostileTo(caster.Faction)) { return false; }
                        if (!GenSight.LineOfSightToThing(chainSourceCell, t, caster.Map, true)) { return false; }

                        return true;
                    },
                    priorityGetter: _ => Rand.Value);

                if (target != null)
                {
                    _subTargets.Add(target);

                    _effects.Add(new LightningEffect()
                    {
                        targetCell = target.Position,
                        targetPosition = target.TrueCenter()
                    });

                    chainSourceCell = target.Position;
                }
                else
                {
                    break;
                }
            }

            RebuildLightningVisuals();
        }

        private void EnsureLightningVisuals()
        {
            if (_effects.Count <= 0)
            {
                return;
            }

            for (int i = 0; i < _effects.Count; ++i)
            {
                var effect = _effects[i];
                if (effect.pathPoints == null || effect.pathPoints.Count < 2)
                {
                    RebuildLightningVisuals();
                    return;
                }

                if (VerbProps.lightningMoteDef != null)
                {
                    if (effect.motes == null || effect.motes.Count != effect.pathPoints.Count - 1)
                    {
                        RebuildLightningVisuals();
                        return;
                    }

                    for (int j = 0; j < effect.motes.Count; ++j)
                    {
                        if (effect.motes[j] == null || effect.motes[j].Destroyed)
                        {
                            RebuildLightningVisuals();
                            return;
                        }
                    }
                }
            }
        }

        private void RebuildLightningVisuals()
        {
            if (_effects.Count <= 0 || caster?.Map == null)
            {
                return;
            }

            foreach (var effect in _effects)
            {
                effect.DestroyMotes();
                effect.pathPoints = null;
            }

            var anchors = new List<Vector3>(_effects.Count + 1);
            var casterPosition = caster.Position.ToVector3Shifted();
            var firstTarget = _effects[0].targetPosition;
            var firstDirection = (firstTarget - casterPosition).Yto0();
            if (firstDirection.sqrMagnitude > 0.0001f)
            {
                firstDirection.Normalize();
                casterPosition += firstDirection * VerbProps.lightningStartOffset;
            }
            anchors.Add(casterPosition);

            for (int i = 0; i < _effects.Count; ++i)
            {
                anchors.Add(_effects[i].targetPosition);
            }

            for (int i = 0; i < _effects.Count; ++i)
            {
                var start = anchors[i];
                var end = anchors[i + 1];
                var distance = (end - start).MagnitudeHorizontal();
                var startTangent = ClampHorizontalMagnitude(GetAnchorTangent(anchors, i), distance * 1.35f);
                var endTangent = ClampHorizontalMagnitude(GetAnchorTangent(anchors, i + 1), distance * 1.35f);

                var seed = Gen.HashCombineInt(Find.TickManager.TicksGame, Gen.HashCombineInt(caster.thingIDNumber, i));
                _effects[i].pathPoints = BuildLightningPath(start, end, startTangent, endTangent, seed);

                if (VerbProps.lightningMoteDef != null)
                {
                    _effects[i].motes = new List<MoteDualAttached>();
                    for (int j = 0; j < _effects[i].pathPoints.Count - 1; ++j)
                    {
                        Rand.PushState(Gen.HashCombineInt(seed, j));
                        _effects[i].motes.Add(MakeLightningMote(_effects[i].pathPoints[j], _effects[i].pathPoints[j + 1]));
                        Rand.PopState();
                    }
                }
            }
        }

        private List<Vector3> BuildLightningPath(Vector3 start, Vector3 end, Vector3 startTangent, Vector3 endTangent, int seed)
        {
            var distance = (end - start).MagnitudeHorizontal();
            var segmentLength = Mathf.Max(0.3f, VerbProps.lightningPathSegmentLength);
            var maxSegments = Mathf.Max(2, VerbProps.lightningPathMaxSegments);
            var segmentCount = Mathf.Clamp(Mathf.CeilToInt(distance / segmentLength), 2, maxSegments);
            var points = new List<Vector3>(segmentCount + 1);
            points.Add(start);

            Rand.PushState(seed);
            var side = Rand.Chance(0.5f) ? 1f : -1f;
            var amplitude = Mathf.Min(Mathf.Max(0f, VerbProps.lightningPathJitter), distance * 0.18f);

            for (int i = 1; i < segmentCount; ++i)
            {
                var t = (float)i / segmentCount;
                var point = Hermite(start, end, startTangent, endTangent, t);
                var tangent = HermiteDerivative(start, end, startTangent, endTangent, t).Yto0();
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = (end - start).Yto0();
                }
                tangent.Normalize();

                if (Rand.Chance(0.65f))
                {
                    side = -side;
                }

                var normal = new Vector3(-tangent.z, 0f, tangent.x);
                var envelope = 16f * t * t * (1f - t) * (1f - t);
                point += normal * side * Rand.Range(amplitude * 0.35f, amplitude) * envelope;
                points.Add(point);
            }

            Rand.PopState();
            points.Add(end);
            return points;
        }

        private MoteDualAttached MakeLightningMote(Vector3 start, Vector3 end)
        {
            var startCell = start.ToIntVec3();
            var endCell = end.ToIntVec3();
            return MoteMaker.MakeInteractionOverlay(
                VerbProps.lightningMoteDef,
                new TargetInfo(startCell, caster.Map),
                new TargetInfo(endCell, caster.Map),
                start - startCell.ToVector3Shifted(),
                end - endCell.ToVector3Shifted());
        }

        private void MaintainLightningMotes(LightningEffect effect)
        {
            if (effect.pathPoints == null || effect.motes == null)
            {
                return;
            }

            for (int i = 0; i < effect.motes.Count; ++i)
            {
                var start = effect.pathPoints[i];
                var end = effect.pathPoints[i + 1];
                var startCell = start.ToIntVec3();
                var endCell = end.ToIntVec3();

                effect.motes[i].UpdateTargets(
                    new TargetInfo(startCell, caster.Map),
                    new TargetInfo(endCell, caster.Map),
                    start - startCell.ToVector3Shifted(),
                    end - endCell.ToVector3Shifted());
                effect.motes[i].Maintain();
            }
        }

        private void ThrowLightningLineFlecks(LightningEffect effect, int effectIndex)
        {
            if (effect.pathPoints == null || effect.pathPoints.Count < 2)
            {
                return;
            }

            var length = GetPathLength(effect.pathPoints);
            if (length <= 0.001f)
            {
                return;
            }

            var steps = Mathf.CeilToInt(length);
            var seed = Gen.HashCombineInt(Find.TickManager.TicksGame, Gen.HashCombineInt(caster.thingIDNumber, effectIndex));
            Rand.PushState(seed);
            for (int i = 0; i < steps; ++i)
            {
                var t = (i + Rand.Value) / steps;
                var chance = VerbProps.lightningLineFleckChanceCurve?.Evaluate(t) ?? 1f;
                if (Rand.Chance(chance))
                {
                    FleckMaker.Static(PointOnPath(effect.pathPoints, t * length), caster.Map, VerbProps.lightningLineFleckDef);
                }
            }
            Rand.PopState();
        }

        private static Vector3 GetAnchorTangent(List<Vector3> anchors, int index)
        {
            if (anchors.Count <= 1)
            {
                return Vector3.zero;
            }
            if (index <= 0)
            {
                return (anchors[1] - anchors[0]).Yto0();
            }
            if (index >= anchors.Count - 1)
            {
                return (anchors[anchors.Count - 1] - anchors[anchors.Count - 2]).Yto0();
            }
            return ((anchors[index + 1] - anchors[index - 1]) * 0.5f).Yto0();
        }

        private static Vector3 ClampHorizontalMagnitude(Vector3 vector, float maxMagnitude)
        {
            vector = vector.Yto0();
            if (maxMagnitude <= 0f || vector.MagnitudeHorizontal() <= maxMagnitude)
            {
                return vector;
            }
            return vector.normalized * maxMagnitude;
        }

        private static Vector3 Hermite(Vector3 start, Vector3 end, Vector3 startTangent, Vector3 endTangent, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * start
                + (t3 - 2f * t2 + t) * startTangent
                + (-2f * t3 + 3f * t2) * end
                + (t3 - t2) * endTangent;
        }

        private static Vector3 HermiteDerivative(Vector3 start, Vector3 end, Vector3 startTangent, Vector3 endTangent, float t)
        {
            var t2 = t * t;
            return (6f * t2 - 6f * t) * start
                + (3f * t2 - 4f * t + 1f) * startTangent
                + (-6f * t2 + 6f * t) * end
                + (3f * t2 - 2f * t) * endTangent;
        }

        private static float GetPathLength(List<Vector3> pathPoints)
        {
            var length = 0f;
            for (int i = 0; i < pathPoints.Count - 1; ++i)
            {
                length += (pathPoints[i + 1] - pathPoints[i]).MagnitudeHorizontal();
            }
            return length;
        }

        private static Vector3 PointOnPath(List<Vector3> pathPoints, float distance)
        {
            for (int i = 0; i < pathPoints.Count - 1; ++i)
            {
                var start = pathPoints[i];
                var end = pathPoints[i + 1];
                var segmentLength = (end - start).MagnitudeHorizontal();
                if (distance <= segmentLength)
                {
                    var t = segmentLength <= 0.001f ? 0f : distance / segmentLength;
                    return Vector3.Lerp(start, end, t);
                }
                distance -= segmentLength;
            }

            return pathPoints[pathPoints.Count - 1];
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);

            if (VerbProps.chainRadius > 0f)
            {
                GenDraw.DrawRadiusRing(target.Cell, VerbProps.chainRadius);
            }
        }
    }
}
