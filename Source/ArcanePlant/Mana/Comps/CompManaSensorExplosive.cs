using System;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ManaSensorExplosive : CompProperties
    {
        public float requiredManaPct;

        public float sensorRadius;
        public Type sensorWorkerClass;

        public float explosiveRadius = 1.9f;
        public IntRange explosiveCooldownTicks;
        public EffecterDef explosiveEffecterDef;
        public DamageDef explosiveDamageDef;

        public int damageAmountBase = -1;
        public float armorPenetrationBase = -1f;

        public SoundDef explosionSound;

        public ThingDef preExplosionSpawnThingDef;
        public float preExplosionSpawnChance;
        public int preExplosionSpawnThingCount = 1;

        public ThingDef postExplosionSpawnThingDef;
        public float postExplosionSpawnChance;
        public int postExplosionSpawnThingCount = 1;
        public GasType? postExplosionGasType;

        public float chanceToStartFire;
        public bool damageFalloff;

        public bool applyDamageToExplosionCellsNeighbors;

        public bool doVisualEffects = true;
        public float propagationSpeed = 1f;

        [Unsaved]
        private SensorWorker _sensorWorker;
        public SensorWorker SensorWorker
        {
            get
            {
                if (_sensorWorker == null)
                {
                    _sensorWorker = (SensorWorker)Activator.CreateInstance(sensorWorkerClass);
                }

                return _sensorWorker;
            }
        }

        public CompProperties_ManaSensorExplosive()
        {
            compClass = typeof(CompManaSensorExplosive);
        }
    }

    public class CompManaSensorExplosive : ThingComp
    {
        CompProperties_ManaSensorExplosive Props => (CompProperties_ManaSensorExplosive)props;

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null) { _manaComp = parent.GetComp<CompMana>(); }
                return _manaComp;
            }
        }
        private CompMana _manaComp;

        private int _remainedCooldown = -1;
        public bool IsCooldown => _remainedCooldown > 0;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _remainedCooldown, "remainedCooldown", defaultValue: -1);
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            GenDraw.DrawRadiusRing(parent.Position, Props.sensorRadius, Color.yellow, c => GenSight.LineOfSight(parent.Position, c, parent.Map));
            GenDraw.DrawRadiusRing(parent.Position, Props.explosiveRadius, Color.red);
        }

        public override void CompTickInterval(int delta)
        {
            if (!parent.Spawned || parent.Destroyed || !ManaComp.Active) { return; }

            if (_remainedCooldown > 0)
            {
                _remainedCooldown = Mathf.Max(0, _remainedCooldown - delta);
            }
            else if (Props.SensorWorker.Detected(parent, Props.sensorRadius))
            {
                TryExplosive();
            }
        }

        public bool TryExplosive()
        {
            if (ManaComp.StoredPct < Props.requiredManaPct)
            {
                return false;
            }

            if (Props.explosiveEffecterDef != null)
            {
                var effecter = Props.explosiveEffecterDef.Spawn();
                effecter.Trigger(new TargetInfo(parent.Position, parent.Map), new TargetInfo(parent.Position, parent.Map));
                effecter.Cleanup();
            }

            GenExplosion.DoExplosion(
                instigator: parent,
                center: parent.Position,
                map: parent.Map,
                radius: Props.explosiveRadius,
                damType: Props.explosiveDamageDef,
                damAmount: Props.damageAmountBase,
                armorPenetration: Props.armorPenetrationBase,
                explosionSound: Props.explosionSound,
                preExplosionSpawnThingDef: Props.preExplosionSpawnThingDef,
                preExplosionSpawnChance: Props.preExplosionSpawnChance,
                preExplosionSpawnThingCount: Props.preExplosionSpawnThingCount,
                postExplosionSpawnThingDef: Props.postExplosionSpawnThingDef,
                postExplosionSpawnChance: Props.postExplosionSpawnChance,
                postExplosionSpawnThingCount: Props.postExplosionSpawnThingCount,
                postExplosionGasType: Props.postExplosionGasType,
                chanceToStartFire: Props.chanceToStartFire,
                damageFalloff: Props.damageFalloff,
                applyDamageToExplosionCellsNeighbors: Props.applyDamageToExplosionCellsNeighbors,
                doVisualEffects: Props.doVisualEffects,
                propagationSpeed: Props.propagationSpeed);

            ManaComp.Stored -= ManaComp.Props.manaCapacity * Props.requiredManaPct;

            _remainedCooldown = Props.explosiveCooldownTicks.RandomInRange;
            return true;
        }
    }
}
