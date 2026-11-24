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

    [StaticConstructorOnStartup]
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
                var speed = PawnUtility.BodyResourceGrowthSpeed(pawn) * pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
                if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_ArchiteGermlineImprint))
                {
                    speed *= Hediff_ArchiteGermlineImprint.EggProgressFactor;
                }
                return Mathf.Clamp01(speed / ((CompProperties_EggLayer)props).eggProgressDays);
            }
        }
        public float eggProgress;
        public bool canLayEgg = true;

        public bool CanLayEgg => eggProgress >= 1f && canLayEgg;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref eggProgress, "eggProgress");
            Scribe_Values.Look(ref canLayEgg, "canLayEgg", defaultValue: true, forceSave: true);
        }

        public override void CompTickInterval(int delta)
        {
            if (CompVivi?.isRoyal == true && parent.IsHashIntervalTick(2000, delta))
            {
                if (eggProgress < 1f)
                {
                    eggProgress = Mathf.Clamp01(eggProgress + (EggProgressPerDays / 60000f * 2000));
                }
            }
        }

        private static readonly Texture2D ToggleLayEggTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_LayEgg");

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || !(parent is Pawn pawn) || !pawn.IsColonistPlayerControlled || CompVivi?.isRoyal != true)
            {
                return Enumerable.Empty<Gizmo>();
            }

            return CompGetGizmosExtraIterator(pawn);
        }

        private IEnumerable<Gizmo> CompGetGizmosExtraIterator(Pawn pawn)
        {
            yield return new EggProgressGizmo(pawn);

            yield return new Command_Toggle
            {
                icon = ToggleLayEggTex,
                defaultLabel = LocalizeString_Command.VV_Command_ToggleLayEgg.Translate(),
                defaultDesc = LocalizeString_Command.VV_Command_ToggleLayEggDesc.Translate(),
                isActive = () => canLayEgg,
                toggleAction = () => { canLayEgg = !canLayEgg; }
            };

            if (DebugSettings.godMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Add Egg Progress 10%",
                    action = () =>
                    {
                        eggProgress = Mathf.Clamp01(eggProgress + 0.1f);
                    }
                };
            }
        }

        public override void Notify_DuplicatedFrom(Pawn source)
        {
            var compEggLayer = source.GetComp<CompViviEggLayer>();
            if (compEggLayer != null)
            {
                eggProgress = compEggLayer.eggProgress;
                canLayEgg = compEggLayer.canLayEgg;
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
                hatcher.parentGeneInheritanceGenerations = hatcher.parentXenogenes.ToDictionary(
                    geneDef => geneDef,
                    geneDef => CompVivi.GetGeneInheritanceGeneration(geneDef));

                var architeImprint = pawn.health.hediffSet
                    .GetFirstHediffOfDef(VVHediffDefOf.VV_ArchiteGermlineImprint) as Hediff_ArchiteGermlineImprint;
                architeImprint?.Notify_EggProduced(hatcher);

                return egg;
            }
            finally
            {
                eggProgress = 0f;
            }
        }

    }
}
