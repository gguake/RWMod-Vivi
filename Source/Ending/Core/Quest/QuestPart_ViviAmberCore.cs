using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 〈비비 연방의 밀서〉: 〈고대 제단 복원〉 연구가 끝나면 고대 호박 코어를 보내 준다(첫 전달).
    // 그 뒤 코어를 잃으면(어느 맵·캐러밴에도 코어나 완성된 제단이 없음, StockGenerator_ViviEndingRelic.PlayerHasAmberCoreOrAltar)
    // 그 순간 연방 정착지의 재고를 새로 짜서 곧바로 코어를 다시 살 수 있게 하고,
    // 연방과 거래할 수도 없으면 마지막 전달로부터 하루가 지날 때마다 무소속 드롭포드로 하나를 다시 보내 진행이 막히지 않게 한다.
    public class QuestPart_ViviAmberCore : QuestPartActivable
    {
        private const int CheckIntervalTicks = 250;
        private const int CoreCheckIntervalTicks = 2500;
        private const int DropOpenDelay = 110;
        private const int RedeliveryIntervalTicks = 60000;

        private bool _delivered;
        private int _lastDeliveryTick = -1;

        // 코어를 잃은 상태인지(잃은 순간 한 번만 연방 정착지 재고를 새로 짠다).
        private bool _coreMissing;

        public bool Delivered => _delivered;

        protected override void Enable(SignalArgs receivedArgs)
        {
            base.Enable(receivedArgs);

            // 서신이 도착하면 연방 정착지의 재고를 새로 짜서 호박 코어 재고가 반영되게 한다.
            ViviEndingUtility.RefreshFederationTraderStock();
        }

        // 다시 만든 밀서 퀘스트(앞선 밀서 퀘스트가 뜻밖에 끝났을 때 창구가 만든다): 첫 전달을 이미 했으면 코어를 잃었을 때의 재전달만 한다.
        public void MarkDelivered()
        {
            _delivered = true;
        }

        public override void QuestPartTick()
        {
            base.QuestPartTick();

            var now = Find.TickManager.TicksGame;
            if (now % CheckIntervalTicks != 0) { return; }

            var research = EndingCoreDefOf.VV_AncientAltarRestoration;
            if (research == null || !research.IsFinished) { return; }

            if (!_delivered)
            {
                Deliver(null, redelivery: false);
                return;
            }

            // 코어 확인은 맵의 모든 보관처를 훑으므로 더 드물게 한다.
            if (now % CoreCheckIntervalTicks != 0) { return; }

            if (StockGenerator_ViviEndingRelic.PlayerHasAmberCoreOrAltar())
            {
                _coreMissing = false;
                return;
            }

            // 코어를 잃은 순간: 코어를 갖고 있을 때 짜 둔 연방 정착지 재고(바닐라는 30일 동안 유지)를 버려 다음 방문 때 코어를 살 수 있게 한다.
            if (!_coreMissing)
            {
                _coreMissing = true;
                ViviEndingUtility.RefreshFederationTraderStock();
            }

            if (_lastDeliveryTick >= 0 && now - _lastDeliveryTick < RedeliveryIntervalTicks) { return; }
            if (ViviEndingUtility.FindFederation(requireNonHostile: true) != null) { return; }

            Deliver(null, redelivery: true);
        }

        public bool Deliver(Map preferredMap, bool redelivery)
        {
            if (!DropCore(preferredMap, redelivery, quest)) { return false; }

            _delivered = true;
            _lastDeliveryTick = Find.TickManager.TicksGame;
            _coreMissing = false;
            return true;
        }

        // 패망하지 않은 비적대 비비 연방이 있으면 연방의 스카이위드 상자로, 없으면 무소속 드롭포드로 정착지에 코어 1개를 떨어뜨린다.
        // redelivery면(코어 분실 후 재전달) 출처 모를 무소속 드롭포드와 짧은 편지로 보낸다. 편지는 quest(없을 수 있음)에 묶는다.
        public static bool DropCore(Map preferredMap, bool redelivery, Quest quest)
        {
            var coreDef = EndingCoreDefOf.VV_AncientAmberCore;
            if (coreDef == null) { return false; }

            var map = preferredMap ?? ViviEndingUtility.FindEndingCandidate()?.MapHeld ?? Find.AnyPlayerHomeMap;
            if (map == null) { return false; }

            var federation = redelivery ? null : ViviEndingUtility.FindFederation(requireNonHostile: true);
            var core = ThingMaker.MakeThing(coreDef);
            DropPodUtility.DropThingsNear(
                DropCellFinder.TradeDropSpot(map),
                map,
                new List<Thing>() { core },
                DropOpenDelay,
                canRoofPunch: false,
                forbid: false,
                allowFogged: false,
                faction: federation);

            string text;
            if (redelivery)
            {
                text = LocalizeString_EndingCore.VV_Letter_EndingAmberCoreRedelivered.Translate().Resolve();
            }
            else
            {
                var intro = federation != null
                    ? LocalizeString_EndingCore.VV_Letter_EndingAmberCoreFromFederation.Translate()
                    : LocalizeString_EndingCore.VV_Letter_EndingAmberCoreFromUnknown.Translate();
                text = intro.Resolve() + "\n\n" + LocalizeString_EndingCore.VV_Letter_EndingAmberCoreGuide.Translate().Resolve();
            }

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingAmberCoreLabel.Translate(),
                text,
                LetterDefOf.PositiveEvent,
                new LookTargets(core),
                federation,
                quest);

            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _delivered, "delivered", false);
            Scribe_Values.Look(ref _lastDeliveryTick, "lastDeliveryTick", -1);
            Scribe_Values.Look(ref _coreMissing, "coreMissing", false);
        }
    }
}
