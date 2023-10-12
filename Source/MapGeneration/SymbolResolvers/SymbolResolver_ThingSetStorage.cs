using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_ThingSetStorage : SymbolResolver
    {
        public override void Resolve(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var thingSetMakerDef = resolveParams.thingSetMakerDef ?? ThingSetMakerDefOf.MapGen_DefaultStockpile;

            var cell = resolveParams.rect.CenterCell;
            var hasStorage = cell.GetFirstThing<Building_Storage>(map) != null;
            var thingSetMakerParams = new ThingSetMakerParams
            {
                countRange = hasStorage ? new IntRange(1, 3) : new IntRange(1, 1),
                techLevel = resolveParams.faction.def.techLevel,
                makingFaction = resolveParams.faction,
                totalMarketValueRange = new FloatRange(130f, 400f),
            };

            if (thingSetMakerDef.root.CanGenerate(thingSetMakerParams))
            {
                var items = thingSetMakerDef.root.Generate(thingSetMakerParams);
                foreach (var item in items)
                {
                    GenSpawn.Spawn(item, cell, map);
                }
            }
        }
    }
}
