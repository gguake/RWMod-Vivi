using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_ReturnOfLife : RitualOutcomeEffectWorker_EndingEverflower
    {
        private static readonly IntRange AncientGeneCount = new IntRange(4, 6);
        private const int AncientArchiteGeneCount = 1;

        private static readonly IntRange AncientYearsRange = new IntRange(300, 600);
        private const float SpawnRadius = 4.9f;

        public RitualOutcomeEffectWorker_ReturnOfLife() { }
        public RitualOutcomeEffectWorker_ReturnOfLife(RitualOutcomeEffectDef def) : base(def) { }

        protected override void Apply(ArcanePlant_Everflower everflower, float quality, LordJob_Ritual jobRitual)
        {
            var map = everflower.Map;
            Pawn ancient = null;
            if (map != null)
            {
                try
                {
                    ancient = SummonAncientVivi(everflower, map);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[ViViRace] failed to summon an ancient vivi with the return of life: {ex}");
                }
            }

            if (ancient != null)
            {
                def.effecter?.Spawn(ancient, map).Cleanup();
                SendArrivalLetter(ancient);
            }
            else if (everflower.Spawned)
            {
                def.effecter?.Spawn(everflower, map).Cleanup();
            }

            everflower.Notify_RitualComplete(quality);
        }

        private static Pawn SummonAncientVivi(ArcanePlant_Everflower everflower, Map map)
        {
            var genes = ViviUtility.SelectRandomGeneForVivi(AncientGeneCount.RandomInRange);
            genes.AddRange(ViviUtility.SelectRandomArchiteGenesForVivi(AncientArchiteGeneCount).Where(v => !genes.Contains(v)));

            var request = new PawnGenerationRequest(
                VVPawnKindDefOf.VV_PlayerVivi,
                faction: Faction.OfPlayer,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                developmentalStages: DevelopmentalStage.Adult,
                forcedXenotype: VVXenotypeDefOf.VV_Vivi,
                forcedXenogenes: genes);

            var pawn = PawnGenerator.GeneratePawn(request);
            pawn.ageTracker.AgeChronologicalTicks += AncientYearsRange.RandomInRange * (long)GenDate.TicksPerYear;

            if (!CellFinder.TryFindRandomCellNear(everflower.Position, map, (int)SpawnRadius, c => c.Standable(map) && !c.Fogged(map) && c != everflower.Position, out var cell))
            {
                cell = CellFinder.RandomClosewalkCellNear(everflower.Position, map, (int)SpawnRadius);
            }

            GenSpawn.Spawn(pawn, cell, map);
            return pawn;
        }

        private static void SendArrivalLetter(Pawn pawn)
        {
            var text = new StringBuilder(LocalizeString_Letter.VV_Letter_ReturnOfLife.Translate(pawn.Named("PAWN")).Resolve());

            var xenogenes = pawn.genes?.Xenogenes;
            if (!xenogenes.NullOrEmpty())
            {
                text.Append("\n\n").Append(LocalizeString_Letter.VV_Letter_ReturnOfLifeGenes.Translate().Resolve());
                text.Append("\n").Append(string.Join(", ", xenogenes.Select(v => v.def.LabelCap.Resolve())));
            }

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ReturnOfLifeLabel.Translate(pawn.Named("PAWN")),
                text.ToString(),
                LetterDefOf.PositiveEvent,
                pawn);
        }
    }
}
