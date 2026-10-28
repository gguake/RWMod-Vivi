using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace VVRace
{
    // 6단계 〈생명의 회귀〉: 아포크리파에게 귀속되어 있다가 풀려난 고대 제국의 요정 비비 하나를 성체 비비의 육체로 되돌려 정착민으로 맞이한다.
    // 고대 비비는 일반 비비보다 무작위 유전자를 더 많이 지니고, 초월 유전자를 반드시 하나 지닌다.
    public class RitualOutcomeEffectWorker_ReturnOfLife : RitualOutcomeEffectWorker_FromQuality
    {
        private static readonly IntRange AncientGeneCount = new IntRange(4, 6);
        private const int AncientArchiteGeneCount = 1;

        // 제국이 무너진 날 요정화되어 수백 년을 보낸 비비이므로 달력상 나이만 그만큼 더한다(육체 나이는 그대로).
        private static readonly IntRange AncientYearsRange = new IntRange(300, 600);
        private const float SpawnRadius = 4.9f;

        public RitualOutcomeEffectWorker_ReturnOfLife() { }
        public RitualOutcomeEffectWorker_ReturnOfLife(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return; }

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
            var text = new StringBuilder(LocalizeString_EndingRituals.VV_Letter_ReturnOfLife.Translate(pawn.Named("PAWN")).Resolve());

            var xenogenes = pawn.genes?.Xenogenes;
            if (!xenogenes.NullOrEmpty())
            {
                text.Append("\n\n").Append(LocalizeString_EndingRituals.VV_Letter_ReturnOfLifeGenes.Translate().Resolve());
                text.Append("\n").Append(string.Join(", ", xenogenes.Select(v => v.def.LabelCap.Resolve())));
            }

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingRituals.VV_Letter_ReturnOfLifeLabel.Translate(pawn.Named("PAWN")),
                text.ToString(),
                LetterDefOf.PositiveEvent,
                pawn);
        }
    }
}
