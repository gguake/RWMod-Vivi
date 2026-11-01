using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    // 앵커가 다른 맵으로 옮겨진 뒤 그 맵과 함께 버려지면 Destroyed나 생성 시점 맵의 MapRemoved 신호가 오지 않는다.
    // 엔딩이 진행 중이면 직접 AnchorLost를 보내야 한다.
    // 평화 타이머가 활성화된 뒤 신호를 받도록 QuestPart_Delay 다음에 추가한다.
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
            var conditionDef = EndingCoreDefOf.VV_GoldenBloom;
            if (conditionDef == null) { return; }

            foreach (var other in Find.Maps)
            {
                if (other != map)
                {
                    ViviEndingUtility.EndGoldenBloom(other);
                }
            }

            GameCondition_GoldenBloom existing = null;
            foreach (var condition in map.gameConditionManager.ActiveConditions.OfType<GameCondition_GoldenBloom>())
            {
                if (condition.fromEnding) { return; }

                existing = existing ?? condition;
            }

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
            var conditionDef = EndingCoreDefOf.VV_OminousSilence;
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
