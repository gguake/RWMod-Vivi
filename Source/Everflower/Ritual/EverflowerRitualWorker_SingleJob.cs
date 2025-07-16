using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class EverflowerRitualWorker_SingleJob : EverflowerRitualWorker
    {
        public EverflowerRitualWorker_SingleJob(EverflowerRitualDef def) : base(def)
        {
        }

        public override IEnumerable<Pawn> GetCandidates(ArcanePlant_Everflower flower)
        {
            foreach (var pawn in flower.Map.mapPawns.AllPawnsSpawned.Where(p => p.IsColonist))
            {
                var compVivi = pawn.GetCompVivi();
                if (compVivi == null || !compVivi.isRoyal) { continue; }

                var linkedFlower = compVivi.LinkedEverflower;
                if (linkedFlower == null || linkedFlower == flower)
                {
                    yield return pawn;
                }
            }
        }

        public override void StartRitual(ArcanePlant_Everflower flower, Pawn caster, Action onStartCallback)
        {
            var job = TryGiveJob(flower, caster);
            if (job == null) { return; }

            caster.jobs.StartJob(job, JobCondition.InterruptForced);
            onStartCallback();
        }

        public virtual Job TryGiveJob(ArcanePlant_Everflower everflower, Pawn caster)
        {
            var job = JobMaker.MakeJob(_def.job, everflower);
            return job;
        }
    }
}
