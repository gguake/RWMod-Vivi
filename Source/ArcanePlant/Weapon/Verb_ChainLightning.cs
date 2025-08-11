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
            public Effecter targetEffecter;

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
            var sourceCell = caster.Position;

            for (int i = 0; i < _effects.Count; ++i)
            {
                var eff = _effects[i];
                var vector = eff.targetPosition - sourceCell.ToVector3Shifted();
                var x = vector.MagnitudeHorizontal();
                var normalized = vector.Yto0().normalized;

                var offsetCaster = i == 0 ? normalized * VerbProps.lightningStartOffset : Vector3.zero;
                var offsetTarget = eff.targetPosition - eff.targetCell.ToVector3Shifted();

                if (eff.mote != null)
                {
                    eff.mote.UpdateTargets(
                        new TargetInfo(sourceCell, caster.Map), 
                        new TargetInfo(eff.targetCell, caster.Map), 
                        offsetCaster, 
                        offsetTarget);

                    eff.mote.Maintain();
                }

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
                    float num2 = 1f * x;
                    for (int j = 0; (float)j < num2; j++)
                    {
                        if (Rand.Chance(VerbProps.lightningLineFleckChanceCurve.Evaluate((float)j / num2)))
                        {
                            var v = j * normalized - normalized * Rand.Value + normalized / 2f;
                            FleckMaker.Static(sourceCell.ToVector3Shifted() + v, caster.Map, VerbProps.lightningLineFleckDef);
                        }
                    }
                }

                sourceCell = eff.targetCell;
            }

        }

        public override void WarmupComplete()
        {
            foreach (var effect in _effects)
            {
                if (effect.mote != null && !effect.mote.Destroyed)
                {
                    effect.mote.Destroy();
                }
            }

            _subTargets.Clear();
            _effects.Clear();

            burstShotsLeft = ShotsPerBurst;
            state = VerbState.Bursting;

            _effects.Add(new LightningEffect()
            {
                targetCell = currentTarget.Cell,
                targetPosition = currentTarget.CenterVector3,
                mote = MoteMaker.MakeInteractionOverlay(
                    VerbProps.lightningMoteDef,
                    caster,
                    new TargetInfo(currentTarget.Cell, caster.Map),
                    Vector3.zero,
                    currentTarget.CenterVector3 - currentTarget.Cell.ToVector3())
            });

            TryCastNextBurstShot();

            var chainSourceCell = currentTarget.Cell;
            var chainSourceOffset = currentTarget.CenterVector3 - chainSourceCell.ToVector3Shifted();
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
                        targetPosition = target.TrueCenter(),
                        mote = MoteMaker.MakeInteractionOverlay(
                            VerbProps.lightningMoteDef,
                            new TargetInfo(chainSourceCell, caster.Map),
                            new TargetInfo(target.Position, caster.Map),
                            chainSourceOffset,
                            target.TrueCenter() - target.Position.ToVector3())
                    });

                    chainSourceCell = target.Position;
                    chainSourceOffset = target.TrueCenter() - chainSourceCell.ToVector3Shifted();
                }
                else
                {
                    break;
                }
            }
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
