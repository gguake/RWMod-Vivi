using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 정찰대가 만족하고 떠난 뒤 유인통이 정해 둔 시각에 직접 발동하는 정착대(Building_ViviLure.FireSettlers).
    // 한 번만 시도하므로 불발도 '발동'으로 처리해 이유를 알리고 유인을 되돌린다. 바닐라 발동 판정에서 떨어지면 유인통이 되돌린다.
    public class IncidentWorker_ViviNestSettlers : IncidentWorker
    {
        // 꽃 재채집을 기다리는 첫 며칠을 버틸 만큼(약 4일 치) 가져온다.
        private const int HoneyPerSettler = 35;
        private const int InitialVivicream = 60;
        private const int InitialViviwax = 80;
        private const int InitialWood = 40;
        private const int InitialFertilizer = 50;

        private const float ColdSeasonTemp = 10f;
        // 추운 땅에서 가져오는 잔불꽃 수(사용자 결정). 처음 방(36칸)의 잔불꽃 상한(방 18칸당 하나, ViviNestPlanner.MaxEmberPots)이 둘이라 처음에는 둘 다
        // 처음 방에 서서 겨울을 데우고(장기 계측 final_cold60b: 잔불꽃 둘인 처음 방이 바깥 −31°C에 27.7°C 이상, 예전 하나일 때 −24~−27°C에 4.3~12°C),
        // 증축 방이 서면 설계자가 하나를 그 방 화분으로 옮긴다(잔불꽃 없는 방 먼저, ViviNestPlanner.SpareRoomEmber). 예전에는 하나만 가져와 증축 방이
        // 남는 잔불꽃을 얻지 못하면 겨울에 바깥과 같이 얼었다(장기 계측 final_cold60b: 잔불꽃 없는 증축 방이 바깥 −31°C에 −30°C).
        private const int ColdLandEmberCount = 2;
        // 둥지 둘레 바깥에 세울 포장 방출기 수(사용자 요구 "두어 개"). 방 안 방출기(초기 설비)와 따로 씨앗 자리를 넓힌다(ViviNestPlanner.FindOutdoorEmitterCell).
        private const int OutdoorEmitterCount = 2;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && parms.faction.IsWanderingViviFaction();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            var faction = parms.faction;

            // 유인통이 없어졌으면(포장·철거·파괴) 플레이어가 이미 아는 일이라 알리지 않고 조용히 거둔다.
            var lure = BeekeepingUtility.FindReservingLure(map, faction);
            if (lure == null)
            {
                BeekeepingUtility.ReleaseWanderingFaction(faction);
                return true;
            }

            if (!BeekeepingUtility.CheckArrivalConditions(map, out var reason))
            {
                Fail(lure, reason);
                return true;
            }

            if (!TrySpawnSettlers(lure, faction, out reason))
            {
                Fail(lure, reason);
            }

            return true;
        }

        public static bool TrySpawnSettlers(Building_ViviLure lure, Faction faction, out string reason)
        {
            var map = lure.Map;
            var report = lure.EvaluateSiteNow();
            if (!report.Acceptable)
            {
                reason = report.MissingReasons;
                return false;
            }

            if (!BeekeepingUtility.TryFindEntryCellReaching(map, report.bestRoomCell, out var entryCell))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionUnreachable.Translate();
                return false;
            }

            var pawns = GenerateSettlers(faction, map, report.SettlerCount);
            foreach (var pawn in pawns)
            {
                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entryCell, map, 5), map);
            }
            GiveInitialStock(map, pawns);

            var nest = new LordJob_ViviNest(faction, report.bestRoomCell);
            LordMaker.MakeNewLord(faction, nest, map, pawns);
            MarkInitialStockOwned(nest, pawns);
            // 정착이 시작되면 유인통은 할 일을 마친다. 세력은 이제 둥지 구성원이 붙잡고 있다.
            lure.ResetAttraction(releaseFaction: false);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ViviNestSettlersLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ViviNestSettlers.Translate(faction.Name, pawns.Count),
                LetterDefOf.PositiveEvent,
                new LookTargets(pawns));

            reason = null;
            return true;
        }

        // 정찰을 건너뛰고 바로 정착대를 부른다(개발 모드 시험용).
        public static void DebugSettleNow(Building_ViviLure lure)
        {
            var faction = BeekeepingUtility.CreateWanderingFaction();
            lure.Notify_ScoutVisitStarted(faction);
            if (!TrySpawnSettlers(lure, faction, out var reason))
            {
                lure.ResetAttraction(releaseFaction: true);
                Messages.Message(reason, lure, MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        private static void Fail(Building_ViviLure lure, string reason)
        {
            lure.ResetAttraction(releaseFaction: true);
            Messages.Message(LocalizeString_Message.VV_Message_ViviNestSettlersCanceled.Translate(reason), lure, MessageTypeDefOf.NeutralEvent);
        }

        // 로열 1명 + 나머지는 비무장·새총잡이 반반
        private static List<Pawn> GenerateSettlers(Faction faction, Map map, int count)
        {
            var kinds = new List<PawnKindDef> { VVPawnKindDefOf.VV_WanderingRoyalVivi };
            var others = Mathf.Max(1, count - 1);
            for (int i = 0; i < others; ++i)
            {
                kinds.Add(i % 2 == 0 ? VVPawnKindDefOf.VV_WanderingViviWorker : VVPawnKindDefOf.VV_WanderingViviSlinger);
            }

            var pawns = new List<Pawn>();
            foreach (var kind in kinds)
            {
                // 전투원(새총잡이)은 폭력이 가능한 폰으로 만든다. 폭력 불가 성장기로 나오면 새총을 들지 못해 둥지를 지키지 못한다(자동 테스트에서 새총잡이 8/60).
                var request = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, tile: map.Tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: kind.isFighter);
                var pawn = PawnGenerator.GeneratePawn(request);
                BeekeepingUtility.RemoveRandomGenes(pawn);
                BeekeepingUtility.RemoveChemicalDependencies(pawn);
                pawns.Add(pawn);
            }

            return pawns;
        }

        // 둥지를 꾸릴 짐을 나눠 든다. 정착하면 둥지 방에 풀어 놓는다.
        private static void GiveInitialStock(Map map, List<Pawn> pawns)
        {
            var stock = new List<Thing>();
            BeekeepingUtility.MakeStacks(VVThingDefOf.VV_FilteredHoney, HoneyPerSettler * pawns.Count, stock);
            BeekeepingUtility.MakeStacks(VVThingDefOf.VV_Vivicream, InitialVivicream, stock);
            BeekeepingUtility.MakeStacks(VVThingDefOf.VV_Viviwax, InitialViviwax, stock);
            BeekeepingUtility.MakeStacks(ThingDefOf.WoodLog, InitialWood, stock);
            BeekeepingUtility.MakeStacks(VVThingDefOf.VV_Fertilizer, InitialFertilizer, stock);

            // 씨앗은 새싹으로 며칠 자라야 하므로 다 자란 마법식물을 포장해 가져온다. 정착하면 둥지가 설치 청사진을 놓아 세운다(ViviNestPlanner).
            foreach (var plantDef in InitialPlantDefs(map))
            {
                stock.Add(ThingMaker.MakeThing(plantDef).MakeMinified());
            }
            // 방출기도 지을 재료(밀랍·원목·비료) 없이 바로 세우도록 포장해 가져온다(연료는 가득 차 있다).
            for (int i = 0; i < OutdoorEmitterCount; ++i)
            {
                stock.Add(ThingMaker.MakeThing(VVThingDefOf.VV_ManaEmitter).MakeMinified());
            }

            var index = 0;
            foreach (var thing in stock)
            {
                var pawn = pawns[index++ % pawns.Count];
                if (!pawn.inventory.innerContainer.TryAdd(thing))
                {
                    thing.Destroy();
                }
            }
        }

        // 들고 온 짐을 처음부터 둥지 몫으로 표시한다(금지 표시 + 장부). 짐을 풀기 전에 쓰러지거나 죽으면 바닐라가 지닌 물건을 그 자리에 떨어뜨리는데
        // (Pawn.DropAndForbidEverything: 집 구역 밖이면 금지하고 안이면 금지하지 않는다) 둥지 몫 표시가 없어 둥지 비비가 다시 쓰지 못했다(리뷰 D2-2, 자동 시험 t11).
        // 지닌 동안은 장부가 구성원 소지품으로 잡아 두고(ViviNestStock.KeepClaim), 떨어지면 둥지 몫으로 남아 운반이 들여온다.
        // 활동 범위 밖에 떨어진 것은 들여오기 전까지 장부·쓸 물건에서 빠진다(ViviNestStock.IsNestOwned).
        // 넣을 때 같은 소지품 더미에 합쳐질 수 있어 넣은 물건이 아니라 넣은 뒤의 소지품을 표시한다.
        private static void MarkInitialStockOwned(LordJob_ViviNest nest, List<Pawn> pawns)
        {
            foreach (var pawn in pawns)
            {
                var inventory = pawn.inventory.innerContainer;
                for (int i = 0; i < inventory.Count; ++i)
                {
                    nest.MarkOwned(inventory[i]);
                }
            }
        }

        // 정착과 생존에 도움되는 마법식물: 겨울이 추우면 잔불꽃 둘(29°C까지 데운다, 둥지 방 화분에 둔다), 그리고 풍년화(흙 비옥도로 마나를 만든다, 비옥한 바깥 칸에 둔다).
        // 도착한 계절이 아니라 그 타일의 한 해 기온으로 고른다(봄에 온 둥지가 겨울에 아기를 얼려 죽였다, 장기 계측).
        // 서리꽃은 -20°C까지 식히는 냉동용이라 방에 두면 더운 땅에서도 방을 얼린다(장기 계측: 바깥 10~30°C에 방 -8°C, 아이 동사).
        // 비비는 36°C까지 편하므로 더운 땅에서는 식힐 꽃 없이 지낸다.
        private static IEnumerable<ThingDef> InitialPlantDefs(Map map)
        {
            if (GenTemperature.MinTemperatureAtTile(map.Tile) < ColdSeasonTemp)
            {
                for (int i = 0; i < ColdLandEmberCount; ++i)
                {
                    yield return VVThingDefOf.VV_EmberBloom;
                }
            }

            yield return VVThingDefOf.VV_Richflower;
        }
    }
}
