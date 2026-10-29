using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 〈만개하는 여명〉의 진행 단계. 퀘스트의 첫 부품이라 모든 신호를 가장 먼저 받고, 단계는 신호로만 바뀐다.
    // 앵커 퀘스트 태그의 자동 신호(파괴·맵 제거)를 받아 엔딩이 진행 중이면 AnchorLost를 보내고,
    // 퀘스트가 성공 외의 이유로 끝나면(정리) 엔딩이 건 컨디션을 걷는다.
    public class QuestPart_ViviEndingState : QuestPart
    {
        public ArcanePlant_Everflower anchor;

        public string inSignalBloomCondensed;
        public string inSignalApocryphaArrived;
        public string inSignalApocryphaDefeated;
        public string inSignalCoronationCompleted;
        public string inSignalStolen;
        public string inSignalAnchorLost;
        public string inSignalAnchorDestroyed;
        public string inSignalAnchorMapRemoved;
        public string outSignalAnchorLost;

        private ViviEndingStage _stage = ViviEndingStage.Siege;
        private bool _stolen;

        public ViviEndingStage Stage => _stage;

        // 아포크리파가 영원꽃을 찬탈해 실패했는지(보스·영원꽃은 보스 모듈이 직접 치운다).
        public bool Stolen => _stolen;

        // 앵커를 잃으면 실패하는 단계(엔딩 연출이 시작된 뒤에는 앵커를 잃어도 실패하지 않는다).
        private bool IsActive => _stage == ViviEndingStage.Siege || _stage == ViviEndingStage.Calm || _stage == ViviEndingStage.Boss || _stage == ViviEndingStage.Coronation;

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                foreach (var target in base.QuestLookTargets)
                {
                    yield return target;
                }

                if (anchor != null && anchor.SpawnedOrAnyParentSpawned)
                {
                    yield return anchor;
                }
            }
        }

        // 퀘스트 탭 설명 아래에 붙는 현재 상황(개화 응축·단계 안내).
        public override string DescriptionPart
        {
            get
            {
                var ending = ViviEndingQuest.Current;
                return ending != null && ending.BloomQuest == quest ? ending.GetStatusText(forQuestTab: true) : null;
            }
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);

            var tag = signal.tag;
            if (tag.NullOrEmpty()) { return; }

            if (tag == inSignalBloomCondensed)
            {
                _stage = ViviEndingStage.Calm;
            }
            else if (tag == inSignalApocryphaArrived)
            {
                _stage = ViviEndingStage.Boss;
            }
            else if (tag == inSignalApocryphaDefeated)
            {
                _stage = ViviEndingStage.Coronation;
            }
            else if (tag == inSignalCoronationCompleted)
            {
                _stage = ViviEndingStage.Completed;
            }
            else if (tag == inSignalStolen)
            {
                _stolen = true;
                _stage = ViviEndingStage.Failed;
            }
            else if (tag == inSignalAnchorLost)
            {
                _stage = ViviEndingStage.Failed;
            }
            else if (tag == inSignalAnchorDestroyed)
            {
                if (IsActive)
                {
                    Find.SignalManager.SendSignal(new Signal(outSignalAnchorLost));
                }
            }
            else if (tag == inSignalAnchorMapRemoved)
            {
                // 맵이 사라질 때 앵커가 함께 버려졌을 때만 실패한다(캐러밴·다른 맵으로 옮겨졌으면 계속).
                // 바닐라는 맵의 물건을 모두 버린(Discarded) 뒤에 MapRemoved를 보내므로 Destroyed로 판단할 수 있다.
                if (IsActive && (anchor == null || anchor.Destroyed))
                {
                    Find.SignalManager.SendSignal(new Signal(outSignalAnchorLost));
                }
            }
        }

        // 퀘스트가 끝날 때(성공·실패·개발자 도구). 실패로 끝나면 엔딩이 건 〈불길한 정적〉·〈황금빛 꽃안개〉를 곧바로 걷고 기믹을 정리한다.
        // 성공 시 꽃안개는 엔딩 연출(QuestPart_ViviEndingFinale)이 이미 서서히 걷히게 해 두었다.
        public override void Cleanup()
        {
            base.Cleanup();

            ViviEndingUtility.RunStep("end ominous silence", ViviEndingUtility.EndOminousSilence);
            if (quest.State == QuestState.EndedSuccess) { return; }

            ViviEndingUtility.RunStep("end golden bloom", ViviEndingUtility.EndGoldenBloom);
            ViviEndingUtility.RunStep("cleanup gimmicks", ViviEndingUtility.CleanupGimmicks);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref inSignalBloomCondensed, "inSignalBloomCondensed");
            Scribe_Values.Look(ref inSignalApocryphaArrived, "inSignalApocryphaArrived");
            Scribe_Values.Look(ref inSignalApocryphaDefeated, "inSignalApocryphaDefeated");
            Scribe_Values.Look(ref inSignalCoronationCompleted, "inSignalCoronationCompleted");
            Scribe_Values.Look(ref inSignalStolen, "inSignalStolen");
            Scribe_Values.Look(ref inSignalAnchorLost, "inSignalAnchorLost");
            Scribe_Values.Look(ref inSignalAnchorDestroyed, "inSignalAnchorDestroyed");
            Scribe_Values.Look(ref inSignalAnchorMapRemoved, "inSignalAnchorMapRemoved");
            Scribe_Values.Look(ref outSignalAnchorLost, "outSignalAnchorLost");
            Scribe_Values.Look(ref _stage, "stage", ViviEndingStage.Siege);
            Scribe_Values.Look(ref _stolen, "stolen", false);
        }
    }
}
