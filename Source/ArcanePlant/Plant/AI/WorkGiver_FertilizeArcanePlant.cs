using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_FertilizeArcanePlant : WorkGiver
    {
        private Dictionary<Map, List<ArcanePlant>> _candidatesCache = new Dictionary<Map, List<ArcanePlant>>();
        private int _lastCachedTick = 0;

        public List<ArcanePlant> GetCachedCandidates(Map map)
        {
            RefreshCandidatesCache();

            return _candidatesCache.TryGetValue(map, out var list) ? list : null;
        }

        public void RefreshCandidatesCache(bool force = false)
        {
            if (!force && GenTicks.TicksGame < _lastCachedTick + GenTicks.TickRareInterval) { return; }

            _candidatesCache.RemoveAll(v => v.Key.Disposed);

            foreach (var map in Find.Maps)
            {
                if (!_candidatesCache.TryGetValue(map, out var list))
                {
                    list = new List<ArcanePlant>();
                    _candidatesCache.Add(map, list);
                }

                list.Clear();
                list.AddRange(ManaFluxGrid.GetFertilizeRequiredArcanePlants(map));
            }

            _lastCachedTick = GenTicks.TicksGame;
        }

        public override Job NonScanJob(Pawn pawn)
        {
            if (!pawn.Spawned || pawn.Map == null || !pawn.IsColonistPlayerControlled) { return null; }

            RefreshCandidatesCache();

            if (!_candidatesCache.TryGetValue(pawn.Map, out var plants) || plants.NullOrEmpty())
            {
                return null;
            }

            ArcanePlant target = null;
            foreach (var plant in plants)
            {
                if (!plant.Spawned || plant.IsForbidden(pawn) || pawn.Faction != plant.Faction) { continue; }
                if (!plant.ShouldAutoFertilizeNowIgnoringManaPct || !plant.FertilizeAutoActivated || plant.Mana > plant.FertilizeAutoThreshold) { continue; }
                if (!pawn.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Deadly)) { continue; }

                target = plant;
                break;
            }

            if (target != null)
            {
                var fertilizer = FindFertilizer(pawn);
                if (fertilizer != null)
                {
                    return JobMaker.MakeJob(VVJobDefOf.VV_FertilizeArcanePlant, target, fertilizer);
                }
            }

            return null;
        }

        public static Thing FindFertilizer(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(VVThingDefOf.VV_Fertilizer),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                validator: thing => !thing.IsForbidden(pawn) && pawn.CanReserve(thing));
        }
    }
}
