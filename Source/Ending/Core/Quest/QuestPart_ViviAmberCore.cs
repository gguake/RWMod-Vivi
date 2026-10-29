using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 〈비비 연방의 밀서〉: 〈고대 제단 복원〉 연구가 끝나면 고대 호박 코어를 보내 준다(첫 전달).
    // 그 뒤 코어를 잃었는데(맵·캐러밴 어디에도 코어·제단·제단 청사진/틀이 없음) 연방과 거래할 수도 없으면,
    // 마지막 전달로부터 하루가 지날 때마다 무소속 드롭포드로 하나를 다시 보내 진행이 막히지 않게 한다.
    public class QuestPart_ViviAmberCore : QuestPartActivable
    {
        private const int CheckIntervalTicks = 250;
        private const int DropOpenDelay = 110;
        private const int RedeliveryIntervalTicks = 60000;

        private bool _delivered;
        private int _lastDeliveryTick = -1;

        protected override void Enable(SignalArgs receivedArgs)
        {
            base.Enable(receivedArgs);

            // 서신이 도착하면 연방 정착지의 재고를 새로 짜서 호박 코어 재고가 반영되게 한다.
            ViviEndingUtility.RefreshFederationTraderStock();
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

            if (_lastDeliveryTick >= 0 && now - _lastDeliveryTick < RedeliveryIntervalTicks) { return; }
            if (ViviEndingUtility.FindFederation(requireNonHostile: true) != null) { return; }
            if (StockGenerator_ViviEndingRelic.PlayerHasAmberCoreOrAltar()) { return; }

            Deliver(null, redelivery: true);
        }

        // 패망하지 않은 비적대 비비 연방이 있으면 연방의 스카이위드 상자로, 없으면 무소속 드롭포드로 정착지에 코어 1개를 떨어뜨린다.
        // redelivery면(코어 분실 후 재전달) 출처 모를 무소속 드롭포드와 짧은 편지로 보낸다.
        public bool Deliver(Map preferredMap, bool redelivery)
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

            _delivered = true;
            _lastDeliveryTick = Find.TickManager.TicksGame;

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
        }
    }
}
