using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WaterFlower : ArtificialPlant
    {
        public override void Tick()
        {
            base.Tick();

            if (this.IsHashIntervalTick(GenTicks.TickRareInterval))
            {
                CheckNearbyFires();
            }
        }

        public override void TickRare()
        {
            base.TickRare();

            CheckNearbyFires();
        }

        private void CheckNearbyFires()
        {
            var data = ArtificialPlantModExtension;
            if (!Spawned || Energy < data.fireExtinguishMinimumEnergy) { return; }

            var closestFire = GenClosest.ClosestThingReachable(
                Position,
                Map,
                ThingRequest.ForDef(ThingDefOf.Fire),
                PathEndMode.OnCell,
                TraverseParms.For(TraverseMode.NoPassClosedDoors),
                data.fireExtinguishSensorRadius);

            if (closestFire != null)
            {
                Effecter effecter = EffecterDefOf.ExtinguisherExplosion.Spawn();
                effecter.Trigger(new TargetInfo(Position, Map), new TargetInfo(Position, Map));
                effecter.Cleanup();

                GenExplosion.DoExplosion(
                    instigator: this,
                    center: Position,
                    map: Map,
                    radius: data.fireExtinguishExplosiveRadius,
                    damType: DamageDefOf.Extinguish);

                AddEnergy(-data.fireExtinguishEnergy);
            }
        }
    }
}
