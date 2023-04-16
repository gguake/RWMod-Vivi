using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ViviHatcher : CompProperties
    {
        public float hatcherDaystoHatch = 10f;

        public SimpleCurve geneCountCurve;
        public List<GeneDef> randomSelectGenes = new List<GeneDef>();

        public CompProperties_ViviHatcher()
        {
            compClass = typeof(CompViviHatcher);
        }
    }

    public class CompViviHatcher : ThingComp
    {
        public CompProperties_ViviHatcher Props => (CompProperties_ViviHatcher)props;

        public CompTemperatureRuinable FreezerComp => parent.GetComp<CompTemperatureRuinable>();

        public bool TemperatureDamaged
        {
            get
            {
                if (FreezerComp != null)
                {
                    return FreezerComp.Ruined;
                }
                return false;
            }
        }

        public bool CanHatch
        {
            get
            {
                var parentHolder = parent.ParentHolder;
                if (parent.Spawned || parentHolder == null || !(parentHolder is ViviEggHatchery hatchery)) { return false; }

                return hatchery.CanHatchNow;
            }
        }

        public float hatchProgress = 0f;
        public Pawn hatcheeParent;
        public List<GeneDef> parentXenogenes;
        public int randomSeed = Rand.Int;

        public override void PostExposeData()
        {
            base.PostExposeData();
            
            Scribe_Values.Look(ref hatchProgress, "hatchProgress", 0f);
            Scribe_References.Look(ref hatcheeParent, "hatcheeParent");
            Scribe_Collections.Look(ref parentXenogenes, "xenogenes", LookMode.Def);
            Scribe_Values.Look(ref randomSeed, "randomSeed", 0);
        }

        public override string CompInspectStringExtra()
        {
            var sb = new StringBuilder();
            if (!TemperatureDamaged)
            {
                sb.Append("EggProgress".Translate())
                    .Append(": ")
                    .Append(hatchProgress.ToStringPercent("F1"));
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        public override void CompTick()
        {
            Tick(1);
        }

        public override void CompTickRare()
        {
            Tick(250);
        }

        public override void CompTickLong()
        {
            Tick(2000);
        }

        public override bool AllowStackWith(Thing other)
        {
            return false;
        }

        public override void PostSplitOff(Thing piece)
        {
            var pieceComp = piece.TryGetComp<CompViviHatcher>();
            if (pieceComp != null)
            {
                pieceComp.hatchProgress = hatchProgress;
                pieceComp.hatcheeParent = hatcheeParent;
                pieceComp.parentXenogenes = parentXenogenes;
                pieceComp.randomSeed = randomSeed;
            }

        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            base.PreAbsorbStack(otherStack, count);
        }

        private void Tick(int ticks)
        {
            if (!CanHatch) { return; }

            hatchProgress += 1f / (Props.hatcherDaystoHatch * 60000f) * ticks;

            if (hatchProgress > 1f)
            {
                Hatch();
            }
        }

        private void Hatch()
        {
            try
            {
                var hatchery = parent.ParentHolder as ViviEggHatchery;
                if (hatchery == null) { return; }

                var faction = hatchery.Faction;

                var pawnKindDef = (faction?.IsPlayer ?? false) ? VVPawnKindDefOf.VV_PlayerVivi : null;
                if (pawnKindDef != null)
                {
                    var xenogeneDefs = parentXenogenes != null ? new List<GeneDef>(parentXenogenes) : new List<GeneDef>();
                    var randomGeneCount = Mathf.FloorToInt(Props.geneCountCurve.Evaluate(Rand.RangeSeeded(0, 10000, randomSeed)));

                    var allRandomGenes = new List<GeneDef>(Props.randomSelectGenes.Where(g => !xenogeneDefs.Contains(g))).ToList();

                    for (int i = 0; i < randomGeneCount; ++i)
                    {
                        var randomGeneDef = allRandomGenes[Rand.RangeSeeded(0, allRandomGenes.Count, randomSeed)];
                        xenogeneDefs.Add(randomGeneDef);
                        allRandomGenes.Remove(randomGeneDef);
                    }

                    var request = new PawnGenerationRequest(
                        pawnKindDef,
                        faction: faction,
                        allowDowned: true,
                        developmentalStages: DevelopmentalStage.Newborn,
                        forcedXenotype: VVXenotypeDefOf.VV_Vivi,
                        forcedXenogenes: xenogeneDefs);

                    Pawn pawn = PawnGenerator.GeneratePawn(request);
                    if (GenSpawn.Spawn(pawn, hatchery.Position, hatchery.Map) != null)
                    {
                        if (pawn != null)
                        {
                            if (hatcheeParent != null)
                            {
                                if (pawn.IsColonist)
                                {
                                    Find.LetterStack.ReceiveLetter(
                                        LocalizeTexts.LetterViviEggHatchedLabel.Translate(),
                                        LocalizeTexts.LetterViviEggHatched.Translate(hatcheeParent.Named("PARENT")),
                                        LetterDefOf.PositiveEvent,
                                        pawn);
                                }
                            }
                            else
                            {
                                if (pawn.IsColonist)
                                {
                                    Find.LetterStack.ReceiveLetter(
                                        LocalizeTexts.LetterViviEggHatchedLabel.Translate(),
                                        LocalizeTexts.LetterViviEggHatchedNoParent.Translate(hatcheeParent.Named("PARENT")),
                                        LetterDefOf.PositiveEvent,
                                        pawn);
                                }
                            }

                            if (hatcheeParent != null)
                            {
                                if (pawn.playerSettings != null && hatcheeParent.playerSettings != null && hatcheeParent.Faction == faction)
                                {
                                    pawn.playerSettings.AreaRestriction = hatcheeParent.playerSettings.AreaRestriction;
                                }

                                if (pawn.RaceProps.IsFlesh)
                                {
                                    pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent, hatcheeParent);

                                    if (hatcheeParent.TryGetMindTransmitter(out var mindTransmitter) && mindTransmitter.CanAddMindLink)
                                    {
                                        mindTransmitter.AssignPawnControl(pawn);
                                    }
                                }
                            }
                        }
                        if (parent.Spawned)
                        {
                            FilthMaker.TryMakeFilth(hatchery.Position, hatchery.Map, ThingDefOf.Filth_AmnioticFluid, count: 3);
                        }
                    }
                    else
                    {
                        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                    }
                }
            }
            finally
            {
                parent.Destroy();
            }
        }
    }
}
