﻿using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class SymbolResolver_ArtificialPlant : SymbolResolver
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
            var thing = ThingMaker.MakeThing(resolveParams.singleThingDef);
            thing.stackCount = 1;
            if (thing.def.CanHaveFaction && thing.Faction != resolveParams.faction)
            {
                thing.SetFaction(resolveParams.faction);
            }

            thing = GenSpawn.Spawn(
                thing, 
                resolveParams.rect.CenterCell, 
                BaseGen.globalSettings.map, 
                resolveParams.singleThingDef.defaultPlacingRot);

            if (thing != null && thing is ArtificialPlant plant)
            {
                plant.AddEnergy(plant.ArtificialPlantModExtension.energyCapacity);
            }
        }
    }
}
