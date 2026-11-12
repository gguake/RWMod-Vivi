using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestFightFires : ThinkNode_JobGiver
    {
        private static readonly List<Thing> _tmpFires = new List<Thing>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.WorkTagIsDisabled(WorkTags.Firefighting)) { return null; }

            var map = pawn.Map;
            var fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
            if (fires.Count == 0) { return null; }

            var territory = nest.Territory;
            _tmpFires.Clear();
            for (int i = 0; i < fires.Count; ++i)
            {
                var fire = fires[i];
                if (((AttachableThing)fire).parent is Pawn || !territory.InWorkRange(fire.Position)) { continue; }
                _tmpFires.Add(fire);
            }
            if (_tmpFires.Count == 0) { return null; }

            var target = GenClosest.ClosestThing_Global_Reachable(nest.NestCell, map, _tmpFires, PathEndMode.Touch, TraverseParms.For(pawn),
                validator: t => pawn.CanReserve(t));
            _tmpFires.Clear();
            return target != null ? JobMaker.MakeJob(JobDefOf.BeatFire, target) : null;
        }
    }
}
