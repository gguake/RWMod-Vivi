using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class VerbProperties_ChainLightning : VerbProperties
    {
        public DamageDef damageDef;
        public int? damageAmount;

        public ThingDef lightningMoteDef;
        public float lightningStartOffset;
        public EffecterDef lightningTargetEffectDef;

        public VerbProperties_ChainLightning()
        {
            verbClass = typeof(Verb_ChainLightning);
        }
    }

    public class Verb_ChainLightning : Verb
    {
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

        protected override int ShotsPerBurst => base.BurstShotCount;

        public VerbProperties_ChainLightning VerbProps => (VerbProperties_ChainLightning)verbProps;

        private IntVec3 _targetCell;
        private Vector3 _targetPosition;

        [Unsaved]
        private MoteDualAttached _mote;
        [Unsaved]
        private Effecter _targetEffecter;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _targetCell, "targetCell");
            Scribe_Values.Look(ref _targetPosition, "targetPosition");
        }

        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }

            var compMana = ManaComp;
            if (compMana == null) { return false; }

            var manaPerShoot = EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) ?? 0;
            if (compMana.Stored < manaPerShoot) { return false; }

            var los = TryFindShootLineFromTo(caster.Position, currentTarget, out var resultingLine);
            if (verbProps.stopBurstWithoutLos && !los)
            {
                return false;
            }

            if (EquipmentSource != null)
            {
                EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                EquipmentSource.GetComp<CompApparelReloadable>()?.UsedOnce();
            }

            lastShotTick = Find.TickManager.TicksGame;

            if (currentTarget.HasThing)
            {
                var log = new BattleLogEntry_RangedImpact(caster, currentTarget.Thing, currentTarget.Thing, EquipmentSource.def, null, null);
                var angleFlat = (currentTarget.Cell - caster.Position).AngleFlat;
                var amount = (float)(VerbProps.damageAmount ?? VerbProps.damageDef.defaultDamage);

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
                        currentTarget.Thing);

                    currentTarget.Thing.TakeDamage(dinfo).AssociateWithLog(log);
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
                    currentTarget.Thing);

                currentTarget.Thing.TakeDamage(empIinfo).AssociateWithLog(log);
            }

            compMana.Stored -= manaPerShoot;
            return true;
        }

        public override void BurstingTick()
        {
            var vector = currentTarget.CenterVector3 - caster.Position.ToVector3Shifted();
            var x = vector.MagnitudeHorizontal();
            var normalized = vector.Yto0().normalized;

            var offsetCaster = normalized * VerbProps.lightningStartOffset;
            var offsetTarget = _targetPosition - _targetCell.ToVector3Shifted();

            if (_mote != null)
            {
                _mote.UpdateTargets(caster, new TargetInfo(currentTarget.Cell, caster.Map), offsetCaster, offsetTarget);
                _mote.Maintain();
            }

            if (_targetEffecter == null && VerbProps.lightningTargetEffectDef != null)
            {
                _targetEffecter = VerbProps.lightningTargetEffectDef.Spawn(_targetCell, caster.Map, offsetTarget);
            }
            if (_targetEffecter != null)
            {
                _targetEffecter.offset = offsetTarget;
                _targetEffecter.EffectTick(new TargetInfo(_targetCell, caster.Map), TargetInfo.Invalid);
                _targetEffecter.ticksLeft--;
            }

            if (verbProps.beamLineFleckDef != null)
            {
                float num2 = 1f * x;
                for (int i = 0; (float)i < num2; i++)
                {
                    if (Rand.Chance(verbProps.beamLineFleckChanceCurve.Evaluate((float)i / num2)))
                    {
                        Vector3 vector4 = i * normalized - normalized * Rand.Value + normalized / 2f;
                        FleckMaker.Static(caster.Position.ToVector3Shifted() + vector4, caster.Map, verbProps.beamLineFleckDef);
                    }
                }
            }
        }

        public override void WarmupComplete()
        {
            burstShotsLeft = ShotsPerBurst;
            state = VerbState.Bursting;

            _mote = MoteMaker.MakeInteractionOverlay(
                VerbProps.lightningMoteDef, 
                caster, 
                new TargetInfo(currentTarget.Cell, caster.Map));

            TryCastNextBurstShot();

            _targetCell = currentTarget.Cell;
            _targetPosition = currentTarget.CenterVector3;
        }
    }
}
