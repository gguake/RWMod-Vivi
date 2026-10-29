using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    // 앵커 감시. 공성이 시작될 때 켜져 퀘스트가 끝날 때까지 250틱마다 확인한다.
    // - 앵커를 잃었는지: 앵커가 다른 맵으로 옮겨진 뒤(오디세이 중력선 등) 그 맵과 함께 버려지면 퀘스트 태그의 자동 신호
    //   (Thing.Destroy의 "Destroyed", 태그를 붙인 생성 시점 맵의 "MapRemoved")가 오지 않으므로 엔딩이 진행 중이면 직접 AnchorLost를 보낸다.
    // - 엔딩 컨디션: 평화 기간부터 대관식 대기까지 〈황금빛 꽃안개〉를, 평화 타이머가 도는 동안은 〈불길한 정적〉도 앵커가 지금 있는 맵에 건다.
    //   앵커 맵이 바뀌면 다른 맵에 걸린 것은 걷어 옮긴다. 앵커가 맵 밖(비행 중인 중력선)에 있는 동안은 그대로 둔다.
    // 평화 타이머가 켜진 뒤에 신호를 받도록 평화 타이머(QuestPart_Delay)보다 뒤에 추가한다.
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

        // 엔딩까지 이어지는 영구 〈황금빛 꽃안개〉(fromEnding). 앵커 맵에 이미 다른 꽃안개가 있으면 엔딩 꽃안개로 바꾼다.
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

        // 평화 타이머가 도는 동안의 〈불길한 정적〉(남은 시간 표시). 아포크리파가 등장하거나 등장이 미뤄지면 아포크리파 부품이 걷는다.
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
