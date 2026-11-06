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

        // 그 땅의 성장 기간(일): 꿀 꽃이 제 속도로 자라는 철의 길이(ViviNestSiteEvaluator.GrowingPeriodDays)
        public int growingPeriodDays;

        public bool HasEnoughFlowers => flowerCount >= ViviNestSiteEvaluator.MinFlowerCount;
        public bool HasRoom => bestRoom != null;
        public bool HasEnoughFertileSoil => fertileCellCount >= ViviNestSiteEvaluator.MinFertileCells;
        public bool HasLongEnoughGrowingPeriod => growingPeriodDays >= ViviNestSiteEvaluator.MinGrowingPeriodDays;
        public bool Acceptable => HasEnoughFlowers && HasRoom && HasEnoughFertileSoil && HasLongEnoughGrowingPeriod;

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
                if (!HasLongEnoughGrowingPeriod)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingGrowingPeriod.Translate(growingPeriodDays, ViviNestSiteEvaluator.MinGrowingPeriodDays));
                }
                return sb.ToString();
            }
        }
    }

    // 유인통 주변이 비비가 살 만한 곳인지 꽃·빈 방·비옥한 땅·기후(성장 기간)·환경 마나로 매긴다. 비옥한 땅과 기후는 최소 요건만 보고 점수에는 넣지 않는다.
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

        // 기후: 그 땅의 성장 기간(바닐라 '성장 기간'과 같은 계산 — 세계 지도 지형 탭·재배 구역 정보창, Zone_Growing.GrowingQuadrumsDescription:
        // 1/12년 평균 기온이 꿀 꽃(민들레)의 최적 성장 기온 6~42°C 안인 1/12년 수 × 5일)이 이만큼은 되어야 정착한다(사용자 결정 "버티지 못할 땅에는 정착하지 않게").
        // 둥지는 꽃이 자라는 철에 꿀을 모아 두고, 겨울(바깥 평균 영하, 꽃이 얼어 죽는다)은 모은 것으로만 버티므로 꽃 철이 짧으면 겨울 비축을 다 모으지 못한다.
        // 지난 장기 계측(60일 = 한 해, 기온 표본으로 맞춘 그 타일의 성장 기간): 0일(툰드라 위도 65°, 가장 추운 1/12년 평균 −43°C)은 정착 뒤 15일 동안 모은
        // 식량(영양 129)이 겨울 동안 바닥나 47일째 굶주림으로 떠났고, 10일(툰드라 위도 64°, −36°C)은 겨울에도 알을 낳던 코드로 51일째 바닥났다.
        // 20일인 다섯 판(툰드라 위도 56~58°·북방림 위도 43~59°, −19~−31°C)은 모두 60일을 버텼다(구성원 4~9, 겨울 동안 식량 최저 6.1일 치).
        // 1/12년 평균은 한 해 가운데를 두고 대칭이라 성장 기간은 0·10·20일처럼 10일씩 바뀐다.
        public const int MinGrowingPeriodDays = 20;

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

            report.growingPeriodDays = GrowingPeriodDays(map);

            return report;
        }

        // 꿀 꽃이 제 속도로 자라는 철의 길이(일). 바닐라 성장 기간(Zone_Growing.GrowingQuadrumsDescription)과 같은 1/12년 평균 기온 판정이다.
        // 기온이 늘 같은 생물군계(바닐라 MapTemperature.SeasonalTemp가 그 값을 쓴다)는 그 기온이 범위 안이면 한 해 내내다.
        public static int GrowingPeriodDays(Map map)
        {
            var plant = ThingDefOf.Plant_Dandelion.plant;
            var constant = map.Biome.constantOutdoorTemperature;
            if (constant.HasValue)
            {
                return constant.Value >= plant.minOptimalGrowthTemperature && constant.Value <= plant.maxOptimalGrowthTemperature ? GenDate.DaysPerYear : 0;
            }

            return GenTemperature.TwelfthsInAverageTemperatureRange(map.Tile, plant.minOptimalGrowthTemperature, plant.maxOptimalGrowthTemperature).Count * GenDate.DaysPerTwelfth;
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
