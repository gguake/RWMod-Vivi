using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class NeedleProperties : ProjectileProperties
    {
        public int maxAttackCount;
        public float targettingRadius;
        public IntRange overrunTicks;

        public float maxAngleVelocity;
        public float forceDirectingRadiusSqr;

        public int maxTargettingTicks = 1000;
    }

    public class Needle : ThingWithComps
    {
        public Thing caster;
        public int attackedCount;

        public LocalTargetInfo curTarget;
        public List<Thing> targetHistory = new List<Thing>();
        public int targetLossTicks;
        public int targetHoldTicks;

        public Vector3 curPos;
        public Vector3 curDirection;
        public IntVec3 lastAttackedTargetCell;

        public ThingDef equipmentDef;
        public float damageMultiplier = 1f;
        public QualityCategory equipmentQuality;
        public float psychicMultiplier = 1f;

        private PriorityQueue<Thing, int> _tmpTargetCandidates = new PriorityQueue<Thing, int>();
        private PriorityQueue<Thing, int> _tmpTargetDuplicatedCandidates = new PriorityQueue<Thing, int>();

        public void Launch(Thing caster, Thing equipment, LocalTargetInfo target)
        {
            this.caster = caster;
            this.curTarget = target;

            curPos = this.TrueCenter();
            curDirection = (curTarget.CenterVector3 - curPos).normalized.Yto0();

            equipmentDef = equipment.def;
            damageMultiplier = equipment.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier);
            equipment.TryGetQuality(out equipmentQuality);
            psychicMultiplier = Mathf.Min(10f, Mathf.Pow(2.4f, caster.GetStatValue(StatDefOf.PsychicSensitivity) / 2.67f - 1f));
        }

        public override void Tick()
        {
            try
            {
                if (!Spawned) { return; }

                var casterPawn = caster as Pawn;
                if (casterPawn == null || casterPawn.DeadOrDowned || caster.DestroyedOrNull())
                {
                    Destroy();
                    return;
                }

                var props = def.projectile as NeedleProperties;
                if (props == null)
                {
                    return;
                }

                if (!curTarget.IsValid)
                {
                    if (targetLossTicks <= 0)
                    {
                        RefreshTarget(props);
                    }
                    else
                    {
                        targetLossTicks--;
                    }
                }
                else
                {
                    targetHoldTicks++;

                    if (targetHoldTicks > props.maxTargettingTicks)
                    {
                        RefreshTarget(props);
                        targetHoldTicks = 0;
                    }
                }

                if (!TryMoveForward(props))
                {
                    Destroy();
                    return;
                }

                var impact = false;
                if (curTarget.IsValid)
                {
                    var targetThingDef = curTarget.Thing.def;
                    if (targetThingDef.Size.x == 1 && targetThingDef.Size.z == 1)
                    {
                        if (Position == curTarget.Cell || (curTarget.CenterVector3 - curPos).sqrMagnitude < 1)
                        {
                            impact = true;
                        }
                    }
                    else
                    {
                        var d = curTarget.Thing.TrueCenter() - curPos;
                        if (Mathf.Abs(d.x) < targetThingDef.Size.x && Math.Abs(d.z) < targetThingDef.Size.z)
                        {
                            impact = true;
                        }
                    }
                }

                if (impact)
                {
                    ImpactToTarget(props);
                }

                if (Spawned && !Destroyed)
                {
                    var curCellThings = new List<Thing>(Position.GetThingList(Map));
                    foreach (var thing in curCellThings)
                    {
                        if (thing is IAttackTarget target && GenHostility.IsActiveThreatTo(target, casterPawn.Faction) && !target.ThreatDisabled(casterPawn))
                        {
                            DamageTo(props, thing);
                        }
                    }
                }
            }
            finally
            {
                base.Tick();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref caster, "caster");
            Scribe_TargetInfo.Look(ref curTarget, "curTarget");
            Scribe_Collections.Look(ref targetHistory, "targetHistory", LookMode.Reference);

            Scribe_Values.Look(ref curPos, "curPos");
            Scribe_Values.Look(ref curDirection, "curDirection");
            Scribe_Values.Look(ref lastAttackedTargetCell, "lastAttackedTargetCell");
        }

        public override Vector3 DrawPos => curPos;

        public int MaxAttackCount => Mathf.Max(1, Mathf.RoundToInt(((NeedleProperties)def.projectile).maxAttackCount * psychicMultiplier * psychicMultiplier));

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var vector = drawLoc + Vector3.up * def.Altitude;
            var rotation = Quaternion.LookRotation(curDirection.Yto0());

            if (def.projectile.useGraphicClass)
            {
                Graphic.Draw(vector, Rotation, this, rotation.eulerAngles.y);
            }
            else
            {
                Graphics.DrawMesh(
                    MeshPool.GridPlane(def.graphicData.drawSize), 
                    vector, 
                    rotation, 
                    def.graphic.MatSingleFor(this), 
                    0);
            }

            Comps_PostDraw();
        }

        private bool TryMoveForward(NeedleProperties props)
        {
            float deltaAngle = 0f;

            if (curTarget.IsValid)
            {
                var directionToTarget = (curTarget.CenterVector3 - curPos).normalized.Yto0();
                var sqrDist = (curTarget.CenterVector3 - curPos).Yto0().sqrMagnitude;

                deltaAngle = Quaternion.FromToRotation(directionToTarget, curDirection).eulerAngles.y;
                if (sqrDist >= props.forceDirectingRadiusSqr)
                {
                    if (deltaAngle >= 180)
                    {
                        deltaAngle = 180 - deltaAngle;
                    }

                    deltaAngle = Mathf.Clamp(deltaAngle, -props.maxAngleVelocity, props.maxAngleVelocity);
                    curDirection = curDirection.RotatedBy(-deltaAngle).normalized.Yto0();
                }
                else
                {
                    curDirection = directionToTarget;
                }
            }

            var speed = props.SpeedTilesPerTick * Mathf.Lerp(1f, 0.707f, Mathf.Abs(deltaAngle / 180f));

            curPos += curDirection * speed;
            var curPosCell = curPos.ToIntVec3();
            if (curPosCell.InBounds(Map))
            {
                if (Position != curPosCell)
                {
                    Position = curPosCell;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private void ImpactToTarget(NeedleProperties props)
        {
            if (curTarget == caster)
            {
                Destroy();
                return;
            }

            DamageTo(props, curTarget.Thing);
            lastAttackedTargetCell = curTarget.Cell;

            targetHoldTicks = 0;
            curTarget = LocalTargetInfo.Invalid;
            targetLossTicks = props.overrunTicks.RandomInRange;
            attackedCount++;
        }

        private void DamageTo(NeedleProperties props, Thing target)
        {
            if (target == null) { return; }
            if (targetHistory.Contains(target)) { return; }

            var instigatorGuilty = !(caster is Pawn casterPawn) || !casterPawn.Drafted;
            var dinfo = new DamageInfo(
                def.projectile.damageDef,
                Mathf.Max(1f, def.projectile.GetDamageAmount(damageMultiplier * psychicMultiplier)),
                Mathf.Clamp01(def.projectile.GetArmorPenetration(damageMultiplier * psychicMultiplier)),
                Quaternion.LookRotation(curDirection.Yto0()).eulerAngles.y,
                caster,
                null,
                equipmentDef,
                DamageInfo.SourceCategory.ThingOrUnknown,
                curTarget.IsValid ? curTarget.Thing : target,
                instigatorGuilty);

            dinfo.SetWeaponQuality(equipmentQuality);
            target.TakeDamage(dinfo);

            targetHistory.Add(target);
        }

        private void RefreshTarget(NeedleProperties props)
        {
            var casterPawn = caster as Pawn;
            if (casterPawn == null || attackedCount >= MaxAttackCount)
            {
                curTarget = caster;
                return;
            }

            try
            {
                var rootRegion = lastAttackedTargetCell.GetRegion(Map);
                var radiusSq = props.targettingRadius * props.targettingRadius;
                var prioritySign = Rand.Chance(0.75f) ? -1 : 1;

                if (rootRegion == null)
                {
                    curTarget = caster;
                    return;
                }

                RegionTraverser.BreadthFirstTraverse(
                    rootRegion,
                    delegate (Region from, Region r)
                    {
                        var traverseParams = TraverseParms.For(TraverseMode.PassAllDestroyableThings);
                        if (!r.Allows(traverseParams, isDestination: false))
                        {
                            return false;
                        }

                        var extentsClose = r.extentsClose;
                        var xRange = Math.Abs(lastAttackedTargetCell.x - Math.Max(extentsClose.minX, Math.Min(lastAttackedTargetCell.x, extentsClose.maxX)));
                        if (xRange > props.targettingRadius)
                        {
                            return false;
                        }

                        var zRange = Math.Abs(lastAttackedTargetCell.z - Math.Max(extentsClose.minZ, Math.Min(lastAttackedTargetCell.z, extentsClose.maxZ)));
                        return !(zRange > props.targettingRadius) && (xRange * xRange + zRange * zRange) <= radiusSq;
                    },
                    delegate (Region r)
                    {
                        var targets = new List<Thing>();
                        var candidates = r.ListerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
                        for (int i = 0; i < candidates.Count; ++i)
                        {
                            var thing = candidates[i];
                            if (!(thing is IAttackTarget targetThing) || thing.Position.Fogged(Map) || thing.Position == Position || !GenSight.LineOfSightToThing(casterPawn.PositionHeld, thing, Map, skipFirstCell: true)) { continue; }
                            if (!GenHostility.IsActiveThreatTo(targetThing, casterPawn.Faction) || targetThing.ThreatDisabled(casterPawn)) { continue; }

                            if (targetHistory.Contains(thing))
                            {
                                _tmpTargetDuplicatedCandidates.Enqueue(thing, prioritySign * Position.DistanceToSquared(thing.Position));
                            }
                            else
                            {
                                _tmpTargetCandidates.Enqueue(thing, prioritySign * Position.DistanceToSquared(thing.Position));
                            }
                        }

                        return _tmpTargetCandidates.Count > 0;
                    },
                    9);

                if (_tmpTargetCandidates.Count > 0)
                    curTarget = _tmpTargetCandidates.Dequeue();
                else if (_tmpTargetDuplicatedCandidates.Count > 0)
                    curTarget = _tmpTargetDuplicatedCandidates.Dequeue();
                else
                    curTarget = caster;

                targetHistory.Clear();
            }
            finally
            {
                _tmpTargetCandidates.Clear();
                _tmpTargetDuplicatedCandidates.Clear();
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            var pawn = caster as Pawn;
            var cooldownStance = pawn?.stances?.curStance as Stance_Cooldown;
            if (cooldownStance != null && cooldownStance.verb is Verb_SpawnNeedle)
            {
                pawn.stances.CancelBusyStanceHard();
            }

            base.Destroy(mode);
        }
    }
}
