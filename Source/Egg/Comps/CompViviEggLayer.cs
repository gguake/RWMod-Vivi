using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_EggLayer : CompProperties
    {
        public float eggProgressDays = 15f;

        public CompProperties_EggLayer()
        {
            compClass = typeof(CompViviEggLayer);
        }
    }

    public class CompViviEggLayer : ThingComp
    {
        public CompVivi CompVivi
        {
            get
            {
                if (_compVivi == null)
                {
                    _compVivi = parent.TryGetComp<CompVivi>();
                }

                return _compVivi;
            }
        }
        private CompVivi _compVivi;

        public float EggProgressPerDays
        {
            get
            {
                var pawn = (Pawn)parent;
                var speed = PawnUtility.BodyResourceGrowthSpeed(pawn) * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Metabolism);
                return Mathf.Clamp01(speed / ((CompProperties_EggLayer)props).eggProgressDays);
            }
        }
        public float eggProgress;

        public bool CanLayEgg => eggProgress >= 1f;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref eggProgress, "eggProgress");
        }

        public override void CompTick()
        {
            if (CompVivi.isRoyal)
            {
                if (eggProgress < 1f)
                {
                    eggProgress = Mathf.Clamp01(eggProgress + EggProgressPerDays / 60000f);
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Spawned && CompVivi.isRoyal)
            {
                yield return new EggProgressGizmo((Pawn)parent);

                if (DebugSettings.godMode)
                {
                    Command_Action command_addEggProgress = new Command_Action();
                    command_addEggProgress.defaultLabel = "DEV: Add Egg Progress 10%";
                    command_addEggProgress.action = () =>
                    {
                        eggProgress = Mathf.Clamp01(eggProgress + 0.1f);
                    };

                    yield return command_addEggProgress;
                }
            }
        }

        public Thing ProduceEgg(bool force = false)
        {
            if (!CanLayEgg && !force) { return null; }

            try
            {
                var pawn = (Pawn)parent;
                var egg = ThingMaker.MakeThing(VVThingDefOf.VV_ViviEgg);
                var hatcher = egg.TryGetComp<CompViviHatcher>();
                hatcher.hatcheeParent = pawn;
                hatcher.parentXenogenes = pawn.genes.Xenogenes.Where(v => v.def.biostatArc == 0).Select(v => v.def).ToList();
                hatcher.parentXenogenes.RemoveAll(
                    v => v.endogeneCategory == EndogeneCategory.BodyType ||
                    v.endogeneCategory == EndogeneCategory.Melanin ||
                    v.endogeneCategory == EndogeneCategory.HairColor ||
                    v.endogeneCategory == EndogeneCategory.Head ||
                    v.endogeneCategory == EndogeneCategory.Jaw);

                return egg;
            }
            finally
            {
                eggProgress = 0f;
            }
        }

    }
}
