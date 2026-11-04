using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 둥지의 채집 장소와 바닥 작업 자리를 두고, 비축 목표에 맞춰 지시서를 멈추고 푼다.
    // 채집·제작 자체는 기존 채집 지시서와 바닐라 작업 지시서를 그대로 쓴다.
    public class ViviNestProduction : IExposable
    {
        public const int RefreshInterval = 600;

        public const int HoneyCapacity = 600;
        public const int PollenTarget = 60;
        public const int PropolisTarget = 20;
        public const int CreamTarget = 60;
        public const int WaxTarget = 80;
        public const int WoodTarget = 60;
        public const int FertilizerTarget = 60;
        public const int CakeTarget = 40;
        public const int ClothForCostume = 50;
        public const int CreamReserveForWax = 30;
        // 크림은 비비의 열량을 쓰므로 둥지 식량이 이만큼(일) 남았을 때만 만든다.
        public const float MinFoodDaysForCream = 2f;

        private readonly LordJob_ViviNest _owner;

        private Building_GatherWorkTable _gatherSpot;
        private Building_WorkTable _workSpot;

        public ViviNestProduction(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;
        private ViviNestStock Stock => _owner.Stock;

        public Building_GatherWorkTable GatherSpot => _gatherSpot != null && _gatherSpot.Spawned ? _gatherSpot : null;
        public Building_WorkTable WorkSpot => _workSpot != null && _workSpot.Spawned ? _workSpot : null;

        public void Tick()
        {
            EnsureGatherSpot();
            EnsureWorkSpot();
            UpdateBills();
        }

        // 간이 채집 장소(작업량 0)는 바닐라 설계자처럼 바로 세운다. 문 밖 가까운 빈 땅 중 채집 반경 안 꿀 꽃이 가장 많은 칸.
        private void EnsureGatherSpot()
        {
            if (GatherSpot != null) { return; }

            var best = IntVec3.Invalid;
            var bestFlowers = -1;
            foreach (var cell in _owner.Territory.OutdoorStorageCells)
            {
                if (!CanPlaceSpot(VVThingDefOf.VV_GatheringSpot, cell, requireRoom: false)) { continue; }

                var flowers = CountHoneyFlowers(cell, Building_GatherWorkTable.GatherMaxRadius);
                if (flowers > bestFlowers)
                {
                    best = cell;
                    bestFlowers = flowers;
                }
            }

            if (best.IsValid)
            {
                var cell = best;
                var spot = (Building_GatherWorkTable)ThingMaker.MakeThing(VVThingDefOf.VV_GatheringSpot);
                spot.SetFaction(_owner.Faction);
                GenSpawn.Spawn(spot, cell, Map, Rot4.South);
                spot.GatherRadius = Building_GatherWorkTable.GatherMaxRadius;

                AddBill(spot, VVRecipeDefOf.VV_GatherHoney);
                AddBill(spot, VVRecipeDefOf.VV_GatherPollen);
                AddBill(spot, VVRecipeDefOf.VV_GatherPropolis);

                _gatherSpot = spot;
            }
        }

        private int CountHoneyFlowers(IntVec3 center, float radius)
        {
            var map = Map;
            var count = 0;
            var cellCount = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = center + GenRadial.RadialPattern[i];
                if (cell.InBounds(map) && ViviNestSiteEvaluator.IsHoneyFlower(cell.GetPlant(map)))
                {
                    count++;
                }
            }
            return count;
        }

        // 바닥 작업 자리는 방 안, 상호작용 칸까지 방 안에 들어가는 빈 칸에 둔다.
        private void EnsureWorkSpot()
        {
            if (WorkSpot != null) { return; }

            foreach (var room in _owner.Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (!CanPlaceSpot(VVThingDefOf.VV_ViviNestWorkSpot, cell, requireRoom: true)) { continue; }

                    var spot = (Building_WorkTable)ThingMaker.MakeThing(VVThingDefOf.VV_ViviNestWorkSpot);
                    spot.SetFaction(_owner.Faction);
                    GenSpawn.Spawn(spot, cell, Map, Rot4.South);

                    // 위에서부터 고르므로 필요한 것부터 둔다. 옷·새총은 드물고 빨리 끝나는 장비라 공사 재료(크림·밀랍)보다 먼저.
                    AddBill(spot, VVRecipeDefOf.Make_VV_BasicWorkerCostume);
                    AddBill(spot, VVRecipeDefOf.Make_VV_Slingshot);
                    AddBill(spot, VVRecipeDefOf.VV_MakeVivicream);
                    AddBill(spot, VVRecipeDefOf.VV_MakeViviwax);
                    AddBill(spot, VVRecipeDefOf.VV_FilteringHoney);
                    AddBill(spot, VVRecipeDefOf.VV_Make_HoneyPollenCake);

                    _workSpot = spot;
                    return;
                }
            }
        }

        private bool CanPlaceSpot(ThingDef def, IntVec3 cell, bool requireRoom)
        {
            var map = Map;
            var interaction = ThingUtility.InteractionCellWhenAt(def, cell, Rot4.South, map);
            foreach (var c in new[] { cell, interaction })
            {
                if (!c.InBounds(map) || !c.Standable(map)) { return false; }
                if (requireRoom ? !_owner.Territory.IsRoomCell(c) : _owner.Territory.IsRoomCell(c)) { return false; }
                if (c.GetEdifice(map) != null) { return false; }
                foreach (var thing in c.GetThingList(map))
                {
                    if (thing is Building || thing is Pawn || thing.def.IsBlueprint || thing.def.IsFrame) { return false; }
                    if (thing.def.category == ThingCategory.Item) { return false; }
                }
                foreach (var door in _owner.Territory.BorderDoors)
                {
                    if (c.AdjacentTo8WayOrInside(door.Position)) { return false; }
                }
            }

            return true;
        }

        private static void AddBill(Building_WorkTable table, RecipeDef recipe)
        {
            if (recipe == null) { return; }

            var bill = recipe.MakeNewBill();
            if (bill is Bill_Production production)
            {
                production.repeatMode = BillRepeatModeDefOf.Forever;
                production.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
            }
            table.BillStack.AddBill(bill);
        }

        private static Bill FindBill(Building_WorkTable table, RecipeDef recipe)
        {
            if (table == null) { return null; }

            foreach (var bill in table.BillStack)
            {
                if (bill.recipe == recipe) { return bill; }
            }
            return null;
        }

        private static void SetActive(Building_WorkTable table, RecipeDef recipe, bool active)
        {
            var bill = FindBill(table, recipe);
            if (bill != null)
            {
                // 재료가 미완성품에 들어가 재고가 줄어도, 만들던 비비가 끝낼 수 있게 그동안은 멈추지 않는다.
                bill.suspended = !active && !(bill is Bill_ProductionWithUft uftBill && uftBill.BoundWorker != null);
            }
        }

        private void UpdateBills()
        {
            var stock = Stock;
            var rawHoney = stock.CountOf(VVThingDefOf.VV_RawHoney);
            var filteredHoney = stock.CountOf(VVThingDefOf.VV_FilteredHoney);
            var pollen = stock.CountOf(VVThingDefOf.VV_Pollen);
            var propolis = stock.CountOf(VVThingDefOf.VV_Propolis);
            var cream = stock.CountOf(VVThingDefOf.VV_Vivicream);
            var wax = stock.CountOf(VVThingDefOf.VV_Viviwax);
            var wood = stock.CountOf(ThingDefOf.WoodLog);
            var cloth = stock.CountOf(ThingDefOf.Cloth);
            var cakes = stock.CountOf(VVThingDefOf.VV_HoneyPollenCake);

            var gather = GatherSpot;
            SetActive(gather, VVRecipeDefOf.VV_GatherHoney, rawHoney + filteredHoney < HoneyCapacity);
            SetActive(gather, VVRecipeDefOf.VV_GatherPollen, pollen < PollenTarget);
            SetActive(gather, VVRecipeDefOf.VV_GatherPropolis, propolis < PropolisTarget);

            var work = WorkSpot;
            SetActive(work, VVRecipeDefOf.VV_MakeVivicream, cream < Target(VVThingDefOf.VV_Vivicream, CreamTarget) && FoodReserveDays() >= MinFoodDaysForCream);
            SetActive(work, VVRecipeDefOf.VV_MakeViviwax, wax < Target(VVThingDefOf.VV_Viviwax, WaxTarget) && cream >= CreamReserveForWax);
            SetActive(work, VVRecipeDefOf.Make_VV_BasicWorkerCostume, cloth >= ClothForCostume && AnyMemberNeedsCostume());
            SetActive(work, VVRecipeDefOf.Make_VV_Slingshot, wood >= 20 && wax >= 10 && AnySlingerNeedsWeapon());
            SetActive(work, VVRecipeDefOf.VV_FilteringHoney, rawHoney >= 20);
            SetActive(work, VVRecipeDefOf.VV_Make_HoneyPollenCake, cakes < CakeTarget && filteredHoney >= 3 && pollen >= 6);
        }

        // 비축 목표: 기본 목표에 둥지 청사진·골조에 아직 들어가야 할 양을 더한다.
        public int Target(ThingDef def, int baseTarget) => baseTarget + _owner.Planner.ResourceNeeded(def);

        // 둥지 식량이 며칠 치인지(구성원이 배부를 때 배고파지는 속도 기준)
        public float FoodReserveDays()
        {
            var pawns = _owner.lord?.ownedPawns;
            var perDay = 0f;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; ++i)
                {
                    var food = pawns[i].needs?.food;
                    if (food != null)
                    {
                        perDay += food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) * GenDate.TicksPerDay;
                    }
                }
            }
            return perDay > 0f ? Stock.FoodNutrition / perDay : float.MaxValue;
        }

        public bool AnyMemberNeedsCostume()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return false; }

            for (int i = 0; i < pawns.Count; ++i)
            {
                if (NeedsCostume(pawns[i]) && Stock.CountOf(VVThingDefOf.VV_BasicWorkerCostume) == 0) { return true; }
            }
            return false;
        }

        // 어른 일반 비비는 기본옷을 입는다(로열·아이는 처음 입고 온 옷을 그대로 쓴다).
        public static bool NeedsCostume(Pawn pawn)
        {
            if (!pawn.DevelopmentalStage.Adult() || pawn.IsRoyalVivi() || pawn.apparel == null) { return false; }

            foreach (var apparel in pawn.apparel.WornApparel)
            {
                if (apparel.def == VVThingDefOf.VV_BasicWorkerCostume && apparel.HitPoints >= apparel.MaxHitPoints * 0.4f)
                {
                    return false;
                }
            }
            return true;
        }

        public bool AnySlingerNeedsWeapon()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return false; }

            for (int i = 0; i < pawns.Count; ++i)
            {
                if (NeedsSlingshot(pawns[i]) && Stock.CountOf(VVThingDefOf.VV_Slingshot) == 0) { return true; }
            }
            return false;
        }

        public static bool NeedsSlingshot(Pawn pawn)
        {
            return pawn.kindDef == VVPawnKindDefOf.VV_WanderingViviSlinger && pawn.DevelopmentalStage.Adult()
                && pawn.equipment != null && pawn.equipment.Primary == null && !pawn.WorkTagIsDisabled(WorkTags.Violent);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref _gatherSpot, "gatherSpot");
            Scribe_References.Look(ref _workSpot, "workSpot");
        }
    }
}
