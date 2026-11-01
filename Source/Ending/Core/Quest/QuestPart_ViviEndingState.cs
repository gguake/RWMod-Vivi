using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class QuestPart_ViviEndingState : QuestPart
    {
        public ArcanePlant_Everflower anchor;

        public string inSignalBloomCondensed;
        public string inSignalApocryphaArrived;
        public string inSignalApocryphaDefeated;
        public string inSignalCoronationCompleted;
        public string inSignalStolen;
        public string inSignalAnchorLost;
        public string inSignalResonatorFallen;
        public string inSignalAnchorDestroyed;
        public string inSignalAnchorMapRemoved;
        public string outSignalAnchorLost;

        private ViviEndingStage _stage = ViviEndingStage.Siege;
        private bool _stolen;

        public ViviEndingStage Stage => _stage;

        public bool Stolen => _stolen;

        public bool IsActive => _stage.IsActiveStage();

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
            else if (tag == inSignalResonatorFallen)
            {
                if (IsActive)
                {
                    _stage = ViviEndingStage.Failed;
                }
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
                // 바닐라는 맵의 물건을 Discarded 처리한 뒤 MapRemoved를 보내므로 이 시점에는 Destroyed로 앵커 소실을 판정할 수 있다.
                if (IsActive && (anchor == null || anchor.Destroyed))
                {
                    Find.SignalManager.SendSignal(new Signal(outSignalAnchorLost));
                }
            }
        }

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
            Scribe_Values.Look(ref inSignalResonatorFallen, "inSignalResonatorFallen");
            Scribe_Values.Look(ref inSignalAnchorDestroyed, "inSignalAnchorDestroyed");
            Scribe_Values.Look(ref inSignalAnchorMapRemoved, "inSignalAnchorMapRemoved");
            Scribe_Values.Look(ref outSignalAnchorLost, "outSignalAnchorLost");
            Scribe_Values.Look(ref _stage, "stage", ViviEndingStage.Siege);
            Scribe_Values.Look(ref _stolen, "stolen", false);
        }
    }
}
