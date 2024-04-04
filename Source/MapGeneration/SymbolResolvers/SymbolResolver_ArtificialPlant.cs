using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_ArcanePlant : SymbolResolver
    {
        public override bool CanResolve(ResolveParams resolveParams)
        {
            if (!base.CanResolve(resolveParams))
            {
                return false;
            }

            if (resolveParams.singleThingDef == null)
            {
                return false;
            }

            return true;
        }

        public override void Resolve(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var thing = ThingMaker.MakeThing(resolveParams.singleThingDef);
            thing.stackCount = 1;
            
            if (thing.def.CanHaveFaction && thing.Faction != resolveParams.faction)
            {
                thing.SetFaction(resolveParams.faction);
            }

            if (thing is ArcanePlant plant)
            {
                plant.AddMana(plant.ManaExtension.manaCapacity);
            }

            GenSpawn.Spawn(
                thing, 
                resolveParams.rect.CenterCell, 
                BaseGen.globalSettings.map, 
                resolveParams.singleThingDef.defaultPlacingRot);
        }
    }
}
