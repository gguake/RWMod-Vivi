using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Blueprint_PlantSeed : Blueprint
    {
        public ThingWithComps Seed => _seed;

        private ThingWithComps _seed;
        private ThingDef _plantDef;

        protected override float WorkTotal => 500f;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad && !_seed.BeingTransportedOnGravship && (_seed.Destroyed || !_seed.SpawnedOrAnyParentSpawned))
            {
                Destroy();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            _seed?.GetComp<CompArcaneSeed>().Notify_DespawnBlueprint(this);
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_References.Look(ref _seed, "seed");
            Scribe_Defs.Look(ref _plantDef, "plantDef");
        }

        public override BuildableDef EntityToBuild() => VVThingDefOf.VV_ArcanePlantSeedling;

        public override ThingDef EntityToBuildStuff() => null;

        public override ThingStyleDef EntityToBuildStyle() => null;

        public override List<ThingDefCountClass> TotalMaterialCost()
        {
            return new List<ThingDefCountClass>();
        }

        protected override Thing MakeSolidThing(out bool shouldSelect)
        {
            var thing = ThingMaker.MakeThing(VVThingDefOf.VV_ArcanePlantSeedling) as ArcanePlant_Seedling;
            if (thing == null) { shouldSelect = false; return null; }

            shouldSelect = Find.Selector.NumSelected == 1 && Find.Selector.IsSelected(this);

            return thing;
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (_seed != null)
            {
                GenDraw.DrawLineBetween(_seed.TrueCenter(), this.TrueCenter());
            }
        }

        public void SetSeedToPlant(ThingWithComps seed)
        {
            _seed = seed;
            _plantDef = seed.GetComp<CompArcaneSeed>()?.Props.targetPlantDef;
        }
    }
}
