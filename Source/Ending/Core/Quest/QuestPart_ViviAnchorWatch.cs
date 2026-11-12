using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class QuestPart_ViviAnchorWatch : QuestPartActivable
    {
        private const int TickInterval = 250;

        public ArcanePlant_Everflower anchor;
        public string inSignalBloomCondensed;
        public string outSignalAnchorLost;

        private Map AnchorMap => anchor != null && !anchor.Destroyed ? anchor.MapHeld : null;

        public override void QuestPartTick()
        {
            base.QuestPartTick();

            if (Find.TickManager.TicksGame % TickInterval != 0) { return; }

            var state = quest.GetFirstPartOfType<QuestPart_ViviEndingState>();
            if (state == null || !state.IsActive) { return; }

            if (anchor == null || anchor.Destroyed)
            {
                Find.SignalManager.SendSignal(new Signal(outSignalAnchorLost));
                return;
            }

            if (state.Stage != ViviEndingStage.Siege)
            {
                EnsureConditions();
            }
        }

        protected override void ProcessQuestSignal(Signal signal)
        {
            base.ProcessQuestSignal(signal);

            if (signal.tag == inSignalBloomCondensed)
            {
                EnsureConditions();
            }
        }

        private void EnsureConditions()
        {
            var map = AnchorMap;
            if (map == null) { return; }

            ViviEndingUtility.RunStep("anchor watch: golden bloom", () => EnsureGoldenBloom(map));
            ViviEndingUtility.RunStep("anchor watch: ominous silence", () => EnsureOminousSilence(map));
        }

        private void EnsureGoldenBloom(Map map)
        {
            var conditionDef = VVGameConditionDefOf.VV_GoldenBloom;
            if (conditionDef == null) { return; }

            foreach (var other in Find.Maps)
            {
                if (other != map)
                {
                    ViviEndingUtility.EndGoldenBloom(other);
                }
            }

            if (GoldenBloomUtility.EndingConditions(map).Any()) { return; }

            var existing = map.gameConditionManager.ActiveConditions.OfType<GameCondition_GoldenBloom>().FirstOrDefault();

            if (existing != null)
            {
                existing.Permanent = true;
                existing.fromEnding = true;
                existing.quest = quest;
                return;
            }

            if (!(GameConditionMaker.MakeCondition(conditionDef) is GameCondition_GoldenBloom goldenBloom) || !goldenBloom.CanApplyOnMap(map)) { return; }

            goldenBloom.Permanent = true;
            goldenBloom.fromEnding = true;
            goldenBloom.quest = quest;
            map.gameConditionManager.RegisterCondition(goldenBloom);
        }

        private void EnsureOminousSilence(Map map)
        {
            var conditionDef = VVGameConditionDefOf.VV_OminousSilence;
            if (conditionDef == null) { return; }

            var calm = quest.PartsListForReading.OfType<QuestPart_Delay>().FirstOrDefault(v => v.debugLabel == QuestNode_Root_ViviBloom.CalmDelayLabel);
            if (calm == null || calm.State != QuestPartState.Enabled || calm.TicksLeft <= 0) { return; }

            foreach (var other in Find.Maps)
            {
                if (other != map)
                {
                    ViviEndingUtility.EndOminousSilence(other);
                }
            }

            if (map.gameConditionManager.ConditionIsActive(conditionDef)) { return; }

            var silence = GameConditionMaker.MakeCondition(conditionDef, calm.TicksLeft);
            if (!silence.CanApplyOnMap(map)) { return; }

            silence.quest = quest;
            map.gameConditionManager.RegisterCondition(silence);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref inSignalBloomCondensed, "inSignalBloomCondensed");
            Scribe_Values.Look(ref outSignalAnchorLost, "outSignalAnchorLost");
        }
    }
}
