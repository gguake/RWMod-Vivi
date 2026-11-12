using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public static class AmberCoreUtility
    {
        private static readonly List<Thing> _tmpThings = new List<Thing>();

        public static bool AnyAmberCoreExists()
        {
            var coreDef = VVThingDefOf.VV_AncientAmberCore;
            var altarDef = VVThingDefOf.VV_EverflowerAltar;

            try
            {
                foreach (var map in Find.Maps)
                {
                    if (altarDef != null && map.listerThings.ThingsOfDef(altarDef).Count > 0)
                    {
                        return true;
                    }

                    if (coreDef != null)
                    {
                        ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForDef(coreDef), _tmpThings);
                        if (_tmpThings.Count > 0)
                        {
                            return true;
                        }
                    }
                }

                foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
                {
                    if (worldObject is Caravan || worldObject is TravellingTransporters)
                    {
                        if (HoldsCoreOrAltar((IThingHolder)worldObject, coreDef, altarDef))
                        {
                            return true;
                        }
                    }
                    else if (worldObject is Gravship gravship)
                    {
                        foreach (var thing in gravship.Things)
                        {
                            if (IsCoreOrAltar(thing, coreDef, altarDef) || (thing is IThingHolder holder && HoldsCoreOrAltar(holder, coreDef, altarDef)))
                            {
                                return true;
                            }
                        }

                        foreach (var pawn in gravship.Pawns)
                        {
                            if (HoldsCoreOrAltar(pawn, coreDef, altarDef))
                            {
                                return true;
                            }
                        }
                    }
                }

                var worldPawns = Find.WorldPawns.AllPawnsAliveOrDead;
                for (int i = 0; i < worldPawns.Count; ++i)
                {
                    if (HoldsCoreOrAltar(worldPawns[i], coreDef, altarDef))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                _tmpThings.Clear();
            }
        }

        private static bool HoldsCoreOrAltar(IThingHolder holder, ThingDef coreDef, ThingDef altarDef)
        {
            ThingOwnerUtility.GetAllThingsRecursively(holder, _tmpThings);
            for (int i = 0; i < _tmpThings.Count; ++i)
            {
                if (IsCoreOrAltar(_tmpThings[i], coreDef, altarDef))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCoreOrAltar(Thing thing, ThingDef coreDef, ThingDef altarDef)
        {
            if (thing == null) { return false; }

            return (coreDef != null && thing.def == coreDef) || (altarDef != null && thing.def == altarDef);
        }
    }
}
