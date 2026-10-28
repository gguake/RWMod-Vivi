using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 【발단】 4단계 영원꽃 감지 → 비비 연방의 밀서(제단 해금) → 〈고대 제단 복원〉 연구 완료 시 고대 호박 코어 전달
    public partial class ViviEndingQuest
    {
        private static readonly IntRange FederationLetterDelayTicks = new IntRange(60000, 120000);
        private const int AmberCoreDropOpenDelay = 110;
        private const int AmberCoreRedeliveryIntervalTicks = 60000;

        // 이미 4단계인 기존 세이브는 레벨 변경 알림이 다시 오지 않으므로 None 단계에서 주기적으로 맵을 훑는다.
        private void ScanForAwakenedEverflower()
        {
            var everflower = FindEndingCandidate();
            if (everflower != null && everflower.AttunementLevel >= LetterTriggerLevel)
            {
                ScheduleFederationLetter();
            }
        }

        private void ScheduleFederationLetter()
        {
            _stage = ViviEndingStage.LetterPending;
            _letterTick = Find.TickManager.TicksGame + FederationLetterDelayTicks.RandomInRange;
        }

        // 연방 대표의 서신을 보내고 제단 연구를 해금한다(연구 잠금은 EndingCorePatch가 IsAltarUnlocked로 판단).
        private void SendFederationLetter(ArcanePlant_Everflower everflower)
        {
            if (everflower == null || everflower.Destroyed)
            {
                everflower = FindEndingCandidate();
            }

            var resonator = everflower?.EverflowerComp?.LinkedPawns?.FirstOrDefault();

            var hyperlinks = new List<ThingDef>();
            if (EndingCoreDefOf.VV_DreamumAltar != null)
            {
                hyperlinks.Add(EndingCoreDefOf.VV_DreamumAltar);
            }
            if (EndingCoreDefOf.VV_AncientAmberCore != null)
            {
                hyperlinks.Add(EndingCoreDefOf.VV_AncientAmberCore);
            }

            _stage = ViviEndingStage.AltarUnlocked;
            _letterTick = -1;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingFederationMissiveLabel.Translate(),
                TranslateWithResonator(LocalizeString_EndingCore.VV_Letter_EndingFederationMissive, resonator),
                LetterDefOf.PositiveEvent,
                everflower != null && everflower.SpawnedOrAnyParentSpawned ? new LookTargets(everflower) : LookTargets.Invalid,
                FindFederation(requireNonHostile: false),
                hyperlinkThingDefs: hyperlinks);

            RefreshFederationTraderStock();
        }

        // 250틱마다: 〈고대 제단 복원〉 연구가 끝났으면 호박 코어를 보내 준다(첫 전달).
        // 그 뒤 코어를 잃었는데(맵·캐러밴 어디에도 코어·제단·제단 청사진/틀이 없음) 연방과 거래할 수도 없으면,
        // 마지막 전달로부터 하루가 지날 때마다 무소속 드롭포드로 하나를 다시 보내 진행이 막히지 않게 한다.
        private void TickCoreDelivery()
        {
            var research = EndingCoreDefOf.VV_AncientAltarRestoration;
            if (research == null || !research.IsFinished) { return; }

            if (!_coreDelivered)
            {
                DeliverAmberCore(null);
                return;
            }

            var now = Find.TickManager.TicksGame;
            if (_lastCoreDeliveryTick >= 0 && now - _lastCoreDeliveryTick < AmberCoreRedeliveryIntervalTicks) { return; }
            if (FindFederation(requireNonHostile: true) != null) { return; }
            if (StockGenerator_ViviEndingRelic.PlayerHasAmberCoreOrAltar()) { return; }

            DeliverAmberCore(null, redelivery: true);
        }

        // 패망하지 않은 비적대 비비 연방이 있으면 연방의 스카이위드 상자로, 없으면 무소속 드롭포드로 정착지에 코어 1개를 떨어뜨린다.
        // redelivery면(코어 분실 후 재전달) 출처 모를 무소속 드롭포드와 짧은 편지로 보낸다.
        private bool DeliverAmberCore(Map preferredMap, bool redelivery = false)
        {
            var coreDef = EndingCoreDefOf.VV_AncientAmberCore;
            if (coreDef == null) { return false; }

            var map = preferredMap ?? FindEndingCandidate()?.MapHeld ?? Find.AnyPlayerHomeMap;
            if (map == null) { return false; }

            var federation = redelivery ? null : FindFederation(requireNonHostile: true);
            var core = ThingMaker.MakeThing(coreDef);
            DropPodUtility.DropThingsNear(
                DropCellFinder.TradeDropSpot(map),
                map,
                new List<Thing>() { core },
                AmberCoreDropOpenDelay,
                canRoofPunch: false,
                forbid: false,
                allowFogged: false,
                faction: federation);

            _coreDelivered = true;
            _lastCoreDeliveryTick = Find.TickManager.TicksGame;

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
                federation);

            return true;
        }

        // 엔딩을 묶을 후보 영원꽃: 플레이어 소유 중 조율 단계가 가장 높은 것. 같으면 제단 안치·정착지 맵·공명자 연결 순으로 우선한다.
        internal static ArcanePlant_Everflower FindEndingCandidate()
        {
            var everflowerDef = VVThingDefOf.VV_Everflower;
            if (everflowerDef == null) { return null; }

            ArcanePlant_Everflower best = null;
            var bestScore = int.MinValue;
            foreach (var map in Find.Maps)
            {
                var things = map.listerThings.ThingsOfDef(everflowerDef);
                for (int i = 0; i < things.Count; ++i)
                {
                    if (!(things[i] is ArcanePlant_Everflower everflower) || everflower.Faction != Faction.OfPlayer) { continue; }

                    var comp = everflower.EverflowerComp;
                    if (comp == null) { continue; }

                    var score = comp.AttunementLevel * 100
                        + (comp.IsEnshrined ? 20 : 0)
                        + (map.IsPlayerHome ? 10 : 0)
                        + (comp.LinkedPawns.Any() ? 1 : 0);

                    if (score > bestScore)
                    {
                        best = everflower;
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        // 패망하지 않은 비비 연방(비적대 우선). requireNonHostile이면 적대 연방밖에 없을 때 null.
        internal static Faction FindFederation(bool requireNonHostile)
        {
            var federationDef = EndingCoreDefOf.VV_ViviFederation;
            if (federationDef == null) { return null; }

            Faction hostile = null;
            var factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; ++i)
            {
                var faction = factions[i];
                if (faction.def != federationDef || faction.defeated) { continue; }

                if (!faction.HostileTo(Faction.OfPlayer))
                {
                    return faction;
                }

                if (hostile == null)
                {
                    hostile = faction;
                }
            }

            return requireNonHostile ? null : hostile;
        }

        // 제단이 해금되면 연방 정착지의 기존 재고를 버려, 다음 방문 때 호박 코어 재고(StockGenerator_ViviEndingRelic)가 반영되게 한다.
        private static void RefreshFederationTraderStock()
        {
            var federationDef = EndingCoreDefOf.VV_ViviFederation;
            if (federationDef == null) { return; }

            foreach (var settlement in Find.WorldObjects.Settlements)
            {
                if (settlement.Faction?.def == federationDef)
                {
                    settlement.trader?.TryDestroyStock();
                }
            }
        }
    }
}
