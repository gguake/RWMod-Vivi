using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ViviNestSiteReport
    {
        public int flowerCount;
        public float flowerScore;

        public Room bestRoom;
        public IntVec3 bestRoomCell = IntVec3.Invalid;
        public int roomCellCount;
        public int roomBuildingCount;
        public float roomScore;

        public float averageMana;
        public float manaScore;

        public int fertileCellCount;

        public bool HasEnoughFlowers => flowerCount >= ViviNestSiteEvaluator.MinFlowerCount;
        public bool HasRoom => bestRoom != null;
        public bool HasEnoughFertileSoil => fertileCellCount >= ViviNestSiteEvaluator.MinFertileCells;
        public bool Acceptable => HasEnoughFlowers && HasRoom && HasEnoughFertileSoil;

        public float TotalScore => Acceptable
            ? ViviNestSiteEvaluator.FlowerWeight * flowerScore + ViviNestSiteEvaluator.RoomWeight * roomScore + ViviNestSiteEvaluator.ManaWeight * manaScore
            : 0f;

        // 점수가 높을수록 정착대가 빨리, 많이 온다.
        public int SettleDelayTicks => Mathf.RoundToInt(Mathf.Lerp(ViviNestSiteEvaluator.MaxSettleDelayTicks, ViviNestSiteEvaluator.MinSettleDelayTicks, TotalScore));
        public int SettlerCount => ViviNestSiteEvaluator.MinSettlerCount + Mathf.RoundToInt(TotalScore * (ViviNestSiteEvaluator.MaxSettlerCount - ViviNestSiteEvaluator.MinSettlerCount));

        public string MissingReasons
        {
            get
            {
                var sb = new StringBuilder();
                if (!HasEnoughFlowers)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingFlowers.Translate(flowerCount, ViviNestSiteEvaluator.MinFlowerCount));
                }
                if (!HasRoom)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingRoom.Translate());
                }
                if (!HasEnoughFertileSoil)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingFertileSoil.Translate(fertileCellCount, ViviNestSiteEvaluator.MinFertileCells));
                }
                return sb.ToString();
            }
        }
    }

    // 유인통 주변이 비비가 살 만한 곳인지 꽃·빈 방·비옥한 땅·환경 마나로 매긴다. 비옥한 땅은 최소 요건만 보고 점수에는 넣지 않는다.
    public static class ViviNestSiteEvaluator
    {
        public const float Radius = 16f;
        public const float ManaRadius = 8f;

        public const int MinFlowerCount = 10;
        public const int FullFlowerCount = 40;

        public const int MinRoomCells = 12;
        public const int MaxRoomCells = 150;
        public const float BuildingPenalty = 0.15f;

        public const float FullMana = 300f;

        // 비옥한 땅: 둥지가 땅에 기대는 일은 모두 비옥도 0.7 이상 칸에서 한다(씨앗·풍년화 자리 JobGiver_ViviNestPlantSeed.MinFertility, 땅 비료 파기,
        // 바닐라 목화·장미 심기 하한 fertilityMin 0.7). 그런 바깥 칸에 목화밭 6칸(ViviNestGarden.CottonCellCount), 풍년화 1칸,
        // 바깥 마법식물(장기 계측 온대 60일: 다 자란 마법식물이 35일에 20그루), 꽃 목표 40송이(ViviNestGarden.MinFlowerTarget)의 장미 밭을 두면 70칸 남짓이고,
        // 땅 비료는 바깥 보관 칸 둘레 10칸(317칸)에서 무작위 20칸을 골라 비옥한 빈 칸을 파므로 그 원의 10%(32칸)가 비옥하면 한 번에 찾을 확률이 88%다.
        // 그래서 유인통 반경 16칸(약 800칸)의 10%인 80칸을 최소로 둔다. 지붕·건물·구역이 있는 칸은 둥지가 쓰지 못하므로 세지 않는다.
        public const float MinFertility = JobGiver_ViviNestPlantSeed.MinFertility;
        public const int MinFertileCells = 80;

        public const float FlowerWeight = 0.45f;
        public const float RoomWeight = 0.35f;
        public const float ManaWeight = 0.2f;

        public const int MinSettleDelayTicks = GenDate.TicksPerDay / 2;
        public const int MaxSettleDelayTicks = GenDate.TicksPerDay * 3;
        public const int MinSettlerCount = 3;
        public const int MaxSettlerCount = 6;

        private static readonly SimpleCurve RoomSizeFactorCurve = new SimpleCurve
        {
            new CurvePoint(MinRoomCells, 0.4f),
            new CurvePoint(30f, 1f),
            new CurvePoint(70f, 1f),
            new CurvePoint(MaxRoomCells, 0.5f),
        };

        private static HashSet<ThingDef> _honeyFlowerDefs;
        private static HashSet<ThingDef> HoneyFlowerDefs
        {
            get
            {
                if (_honeyFlowerDefs == null)
                {
                    _honeyFlowerDefs = new HashSet<ThingDef>();
                    var stat = VVStatDefOf.VV_PlantHoneyGatherYield;
                    foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def.StatBaseDefined(stat) && def.GetStatValueAbstract(stat) > 0f)
                        {
                            _honeyFlowerDefs.Add(def);
                        }
                    }
                }

                return _honeyFlowerDefs;
            }
        }

        public static bool IsHoneyFlower(Thing thing) => thing != null && HoneyFlowerDefs.Contains(thing.def) && (thing is Plant || thing is ArcanePlant);

        private static readonly HashSet<Room> _tmpRooms = new HashSet<Room>();
        public static ViviNestSiteReport Evaluate(Map map, IntVec3 center)
        {
            var report = new ViviNestSiteReport();

            _tmpRooms.Clear();
            var cellCount = GenRadial.NumCellsInRadius(Radius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = center + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) { continue; }

                var things = map.thingGrid.ThingsListAtFast(cell);
                for (int j = 0; j < things.Count; ++j)
                {
                    if (IsHoneyFlower(things[j]))
                    {
                        report.flowerCount++;
                        break;
                    }
                }

                var room = cell.GetRoom(map);
                if (room != null)
                {
                    _tmpRooms.Add(room);
                }

                if (IsUsableFertileCell(map, cell))
                {
                    report.fertileCellCount++;
                }
            }

            report.flowerScore = Mathf.InverseLerp(MinFlowerCount, FullFlowerCount, report.flowerCount);

            foreach (var room in _tmpRooms)
            {
                var score = RoomScore(room, map, out var buildingCount);
                if (score > report.roomScore)
                {
                    report.bestRoom = room;
                    report.roomScore = score;
                    report.roomCellCount = room.CellCount;
                    report.roomBuildingCount = buildingCount;
                }
            }
            _tmpRooms.Clear();

            if (report.bestRoom != null)
            {
                report.bestRoomCell = CentralCell(report.bestRoom, map);
            }

            report.averageMana = AverageMana(map, center);
            report.manaScore = Mathf.Clamp01(report.averageMana / FullMana);

            return report;
        }

        // 둥지가 꽃·목화·마법식물을 심고 땅 비료를 팔 수 있는 바깥 칸. 둥지 바깥 자리 고르기처럼 지붕·건물·구역이 있는 칸은 뺀다(ViviNestPlanner.IsFreeOutdoorCell).
        public static bool IsUsableFertileCell(Map map, IntVec3 cell)
        {
            if (cell.Roofed(map) || cell.GetEdifice(map) != null || map.zoneManager.ZoneAt(cell) != null) { return false; }
            return map.fertilityGrid.FertilityAt(cell) >= MinFertility;
        }

        // 0이면 둥지로 쓸 수 없는 방이다.
        public static float RoomScore(Room room, Map map, out int buildingCount)
        {
            buildingCount = 0;
            if (room == null || room.IsDoorway || room.TouchesMapEdge || !room.ProperRoom || room.PsychologicallyOutdoors || room.IsHuge) { return 0f; }
            if (room.Fogged || room.IsPrisonCell) { return 0f; }

            var cellCount = room.CellCount;
            if (cellCount < MinRoomCells || cellCount > MaxRoomCells) { return 0f; }
            if (room.OpenRoofCount > 0) { return 0f; }

            var hasDoor = false;
            foreach (var border in room.BorderCells)
            {
                if (border.InBounds(map) && border.GetDoor(map) != null)
                {
                    hasDoor = true;
                    break;
                }
            }
            if (!hasDoor) { return 0f; }

            foreach (var cell in room.Cells)
            {
                // 가장자리 건설 금지 구역이면 증축·수리가 막히고, 그래비쉽 기판이면 이륙할 때 둥지째 날아간다.
                if (cell.InNoBuildEdgeArea(map)) { return 0f; }

                var foundation = map.terrainGrid.FoundationAt(cell);
                if (foundation != null && foundation.IsSubstructure) { return 0f; }
            }

            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; ++i)
            {
                if (!(things[i] is Building building) || building.Faction != Faction.OfPlayer) { continue; }
                if (!room.ContainsCell(building.Position)) { continue; }

                // 누군가의 침실이면 들어가지 않는다.
                if (building is Building_Bed bed && bed.OwnersForReading.Count > 0) { return 0f; }

                if (building is ArcanePlant || building is ArcanePlantPot || building is Building_ViviLure) { continue; }

                buildingCount++;
            }

            var emptiness = 1f - BuildingPenalty * buildingCount;
            if (emptiness <= 0f) { return 0f; }

            return RoomSizeFactorCurve.Evaluate(cellCount) * emptiness;
        }

        public static float AverageMana(Map map, IntVec3 center)
        {
            var manaComp = map.GetManaComponent();
            if (manaComp == null) { return 0f; }

            var sum = 0f;
            var count = 0;
            var cellCount = GenRadial.NumCellsInRadius(ManaRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = center + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) { continue; }

                sum += manaComp[cell];
                count++;
            }

            return count > 0 ? sum / count : 0f;
        }

        // 둥지 이동·산책의 기준이 되므로 방 한가운데에 가장 가까운 설 수 있는 칸을 고른다.
        private static IntVec3 CentralCell(Room room, Map map)
        {
            var sumX = 0;
            var sumZ = 0;
            var count = 0;
            foreach (var cell in room.Cells)
            {
                sumX += cell.x;
                sumZ += cell.z;
                count++;
            }
            if (count == 0) { return IntVec3.Invalid; }

            var center = new IntVec3(sumX / count, 0, sumZ / count);
            var best = IntVec3.Invalid;
            var bestDist = int.MaxValue;
            foreach (var cell in room.Cells)
            {
                if (!cell.Standable(map)) { continue; }

                var dist = (cell - center).LengthHorizontalSquared;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = cell;
                }
            }

            return best;
        }
    }
}
