using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class Hediff_ViviFairyFollow : HediffWithComps
    {
        public const int PenaltyFreeAttunementLevel = 6;

        private static readonly Dictionary<HediffDef, List<HediffStage>> PenaltyFreeStagesCache = new Dictionary<HediffDef, List<HediffStage>>();

        [Unsaved]
        private CompVivi _compVivi;

        public override HediffStage CurStage
        {
            get
            {
                var stage = base.CurStage;
                if (stage == null || !IsPenaltyFree) { return stage; }

                var stagesWithoutPenalty = GetPenaltyFreeStages(def);
                var index = CurStageIndex;
                if (stagesWithoutPenalty != null && index >= 0 && index < stagesWithoutPenalty.Count)
                {
                    return stagesWithoutPenalty[index];
                }

                return stage;
            }
        }

        private bool IsPenaltyFree
        {
            get
            {
                if (_compVivi == null)
                {
                    _compVivi = pawn?.GetCompVivi();
                }

                var linkedEverflower = _compVivi?.LinkedEverflower;
                return linkedEverflower != null && linkedEverflower.AttunementLevel >= PenaltyFreeAttunementLevel;
            }
        }

        private static List<HediffStage> GetPenaltyFreeStages(HediffDef def)
        {
            if (!PenaltyFreeStagesCache.TryGetValue(def, out var stages))
            {
                stages = MakePenaltyFreeStages(def);
                PenaltyFreeStagesCache.Add(def, stages);
            }

            return stages;
        }

        private static List<HediffStage> MakePenaltyFreeStages(HediffDef def)
        {
            var penaltyStats = def.GetModExtension<FairyBondPenaltyExtension>()?.penaltyStatFactors;
            if (def.stages == null || penaltyStats.NullOrEmpty()) { return null; }

            var stages = new List<HediffStage>(def.stages.Count);
            foreach (var stage in def.stages)
            {
                var copy = Gen.MemberwiseClone(stage);
                copy.statFactors = stage.statFactors?.Where(modifier => !penaltyStats.Contains(modifier.stat)).ToList();
                stages.Add(copy);
            }

            return stages;
        }
    }
}
