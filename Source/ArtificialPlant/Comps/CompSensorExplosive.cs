using System;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_SensorExplosive : CompProperties
    {
        public int requiredEnergyAmount;
        public int useEnergyAmount;

        public float sensorRadius;
        public Type sensorWorkerClass;

        public FloatRange explosiveRadius;
        public IntRange explosiveCooldownTicks;
        public EffecterDef explosiveEffecterDef;
        public DamageDef explosiveDamageDef;

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
            if (parent.IsHashIntervalTick(60))
            {
                Tick(60);
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
            GenDraw.DrawRadiusRing(parent.Position, Props.explosiveRadius.Average);
        }

        public void Tick(int ticks = 1)
        {
            var plant = parent as ArtificialPlant;
            if (plant == null || plant.EnergyChargeRatio > 0f)
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
            var plant = parent as ArtificialPlant;
            if (plant != null && plant.Energy < Props.requiredEnergyAmount)
            {
                return false;
            }

            if (Props.explosiveDamageDef != null)
            {
                var effecter = Props.explosiveEffecterDef.Spawn();
                effecter.Trigger(new TargetInfo(parent.Position, parent.Map), new TargetInfo(parent.Position, parent.Map));
                effecter.Cleanup();
            }

            GenExplosion.DoExplosion(
                instigator: parent,
                center: parent.Position,
                map: parent.Map,
                radius: Props.explosiveRadius.RandomInRange,
                damType: Props.explosiveDamageDef);

            plant?.AddEnergy(-Props.useEnergyAmount);

            _remainedCooldown = Props.explosiveCooldownTicks.RandomInRange;
            return true;
        }
    }
}
