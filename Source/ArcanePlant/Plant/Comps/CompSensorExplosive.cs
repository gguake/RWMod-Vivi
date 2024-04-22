using System;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_SensorExplosive : CompProperties
    {
        public int requiredManaAmount;
        public int useManaAmount;

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

        public CompProperties_SensorExplosive()
        {
            compClass = typeof(CompSensorExplosive);
        }
    }

    public class CompSensorExplosive : ThingComp
    {
        CompProperties_SensorExplosive Props => (CompProperties_SensorExplosive)props;

        private int _remainedCooldown = -1;
        public bool IsCooldown => _remainedCooldown > 0;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref _remainedCooldown, "remainedCooldown", defaultValue: -1);
        }

        public override void CompTick()
        {
            if (parent.IsHashIntervalTick(GenTicks.TickRareInterval))
            {
                Tick(GenTicks.TickRareInterval);
            }
        }

        public override void CompTickRare()
        {
            Tick(GenTicks.TickRareInterval);
        }

        public override void CompTickLong()
        {
            Tick(GenTicks.TickLongInterval);
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            GenDraw.DrawRadiusRing(parent.Position, Props.sensorRadius);
            GenDraw.DrawRadiusRing(parent.Position, Props.explosiveRadius);
        }

        public void Tick(int ticks = 1)
        {
            if (!parent.Spawned || parent.Destroyed) { return; }

            var plant = parent as ArcanePlant;
            if (plant == null || plant.ManaChargeRatio > 0f)
            {
                if (_remainedCooldown > 0)
                {
                    _remainedCooldown = Mathf.Max(0, _remainedCooldown - ticks);
                }
                else if (Props.SensorWorker.Detected(parent, Props.sensorRadius))
                {
                    TryExplosive();
                }
            }
        }

        public bool TryExplosive()
        {
            var plant = parent as ArcanePlant;
            if (plant != null && plant.Mana < Props.requiredManaAmount)
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

            plant?.AddMana(-Props.useManaAmount);

            _remainedCooldown = Props.explosiveCooldownTicks.RandomInRange;
            return true;
        }
    }
}
