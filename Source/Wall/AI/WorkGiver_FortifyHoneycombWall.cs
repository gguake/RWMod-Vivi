using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_FortifyHoneycombWall : WorkGiver_Scanner
    {
        public const int IngredientLifestrandAmount = 1;

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(VVDesignationDefOf.VV_FortifyHoneycombWall))
            {
                yield return designation.target.Thing;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
            => !pawn.Map.designationManager.AnySpawnedDesignationOfDef(VVDesignationDefOf.VV_FortifyHoneycombWall)
            || pawn.Map.listerThings.ThingsOfDef(VVThingDefOf.VV_Lifestrand).Count == 0;

        private Game _cachedIngredientGame;
        private Map _cachedIngredientMap;
        private IntVec3 _cachedIngredientPos;
        private Pawn _cachedIngredientPawn;
        private int _cachedIngredientTick = -1;
        private List<Thing> _cachedIngredients;
        private readonly List<IntVec3> _cachedIngredientPositions = new List<IntVec3>();

        private void ClearIngredientCache()
        {
            _cachedIngredientMap = null;
            _cachedIngredientPawn = null;
            _cachedIngredientTick = -1;
            _cachedIngredients = null;
            _cachedIngredientPositions.Clear();
        }

        private List<Thing> FindBestIngredientsCached(Pawn pawn, int requiredCount, bool forced)
        {
            if (_cachedIngredientGame != Current.Game)
            {
                _cachedIngredientGame = Current.Game;
                ClearIngredientCache();
            }

            if (forced)
            {
                return FindBestIngredients(pawn, requiredCount);
            }

            var now = GenTicks.TicksGame;
            var map = pawn.Map;
            if (_cachedIngredientPawn == pawn &&
                _cachedIngredientTick == now &&
                _cachedIngredientMap == map &&
                _cachedIngredientPos == pawn.Position &&
                _cachedIngredients != null &&
                _cachedIngredients.Count == _cachedIngredientPositions.Count)
            {
                int accumulated = 0;
                bool valid = true;
                for (int i = 0; i < _cachedIngredients.Count; i++)
                {
                    var thing = _cachedIngredients[i];
                    if (thing == null ||
                        thing.Destroyed ||
                        !thing.Spawned ||
                        thing.Map != map ||
                        thing.Position != _cachedIngredientPositions[i] ||
                        thing.def != VVThingDefOf.VV_Lifestrand ||
                        thing.IsForbidden(pawn) ||
                        !pawn.CanReserve(thing))
                    {
                        valid = false;
                        break;
                    }

                    var region = thing.GetRegion();
                    if (region == null || !ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, region, PathEndMode.ClosestTouch, pawn))
                    {
                        valid = false;
                        break;
                    }

                    accumulated += thing.stackCount;
                }

                if (valid && accumulated >= requiredCount)
                {
                    return _cachedIngredients;
                }

                ClearIngredientCache();
            }

            var found = FindBestIngredients(pawn, requiredCount);
            if (found != null)
            {
                _cachedIngredientMap = map;
                _cachedIngredientPos = pawn.Position;
                _cachedIngredientPawn = pawn;
                _cachedIngredientTick = now;
                _cachedIngredients = found;
                _cachedIngredientPositions.Clear();
                for (int i = 0; i < found.Count; i++)
                {
                    _cachedIngredientPositions.Add(found[i].Position);
                }
            }
            return found;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.IsForbidden(pawn) || pawn.Map.designationManager.DesignationOn(t, VVDesignationDefOf.VV_FortifyHoneycombWall) == null)
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }

            if (!pawn.CanReserve(t, ignoreOtherReservations: forced))
            {
                return false;
            }

            var ingredients = FindBestIngredientsCached(pawn, IngredientLifestrandAmount, forced);
            if (ingredients == null)
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // Job 생성 시점에는 항상 직접 BFS를 수행해 원본과 동일한 최단 재료·경로 정합성을 보장한다.
            ClearIngredientCache();
            var ingredients = FindBestIngredients(pawn, IngredientLifestrandAmount);
            if (ingredients == null) { return null; }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_FortifyHoneycombWall, t);
            var queueB = new List<LocalTargetInfo>(ingredients.Count);
            for (int i = 0; i < ingredients.Count; i++)
            {
                queueB.Add(new LocalTargetInfo(ingredients[i]));
            }
            job.targetQueueB = queueB;

            return job;
        }

        public List<Thing> FindBestIngredients(Pawn pawn, int requiredCount, int regionDistance = 999999)
        {
            int accumulated = 0;
            var ingredients = new List<Thing>();

            RegionTraverser.BreadthFirstTraverse(
                pawn.Position.GetRegion(pawn.Map),
                (from, to) => to.Allows(TraverseParms.For(pawn), isDestination: false),
                (region) =>
                {
                    foreach (var thing in region.ListerThings.ThingsOfDef(VVThingDefOf.VV_Lifestrand))
                    {
                        if (ingredients.Contains(thing) || 
                            thing.IsForbidden(pawn) || 
                            !pawn.CanReserve(thing) || 
                            !ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, region, PathEndMode.ClosestTouch, pawn))
                        {
                            continue;
                        }

                        ingredients.Add(thing);
                        accumulated += thing.stackCount;
                        if (accumulated >= requiredCount)
                        {
                            return true;
                        }
                    }

                    return false;
                },
                maxRegions: regionDistance);

            if (accumulated >= requiredCount) { return ingredients; }

            return null;
        }
    }
}
