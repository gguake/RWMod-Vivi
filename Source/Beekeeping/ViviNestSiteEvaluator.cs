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

        public int growingPeriodDays;

        public bool HasEnoughFlowers => flowerCount >= ViviNestSiteEvaluator.MinFlowerCount;
        public bool HasRoom => bestRoom != null;
        public bool HasEnoughFertileSoil => fertileCellCount >= ViviNestSiteEvaluator.MinFertileCells;
        public bool HasLongEnoughGrowingPeriod => growingPeriodDays >= ViviNestSiteEvaluator.MinGrowingPeriodDays;
        public bool Acceptable => HasEnoughFlowers && HasRoom && HasEnoughFertileSoil && HasLongEnoughGrowingPeriod;

        public float TotalScore => Acceptable
            ? ViviNestSiteEvaluator.FlowerWeight * flowerScore + ViviNestSiteEvaluator.RoomWeight * roomScore + ViviNestSiteEvaluator.ManaWeight * manaScore
            : 0f;

        public int SettleDelayTicks => Mathf.RoundToInt(Mathf.Lerp(ViviNestSiteEvaluator.MaxSettleDelayTicks, ViviNestSiteEvaluator.MinSettleDelayTicks, TotalScore));
        public int SettlerCount => ViviNestSiteEvaluator.MinSettlerCount + Mathf.RoundToInt(TotalScore * (ViviNestSiteEvaluator.MaxSettlerCount - ViviNestSiteEvaluator.MinSettlerCount));

        public string MissingReasons
        {
            get
            {
                var sb = new StringBuilder();
                if (!HasEnoughFlowers)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingFlowers.Translate());
                }
                if (!HasRoom)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingRoom.Translate());
                }
                if (!HasEnoughFertileSoil)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingFertileSoil.Translate());
                }
                if (!HasLongEnoughGrowingPeriod)
                {
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingGrowingPeriod.Translate());
                }
                return sb.ToString();
            }
        }
    }

    public static class ViviNestSiteEvaluator
    {
        public const float Radius = 16f;
        public const float ManaRadius = 8f;

        public const int MinFlowerCount = 10;
        public const int FullFlowerCount = 40;

        public const int MinRoomCells = 12;
        public const int MaxRoomCells = 320;
        public const float BuildingPenalty = 0.15f;

        public const float FullMana = 300f;

        public const float MinFertility = JobGiver_ViviNestPlantSeed.MinFertility;
        public const int MinFertileCells = 80;

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

        private static bool IsHoneyFlowerDef(ThingDef def)
        {
            return def.StatBaseDefined(VVStatDefOf.VV_PlantHoneyGatherYield) && GameComponent_Mana.HasHoneyYield(def);
        }

        public static bool IsHoneyFlower(Thing thing) => thing != null && (thing is Plant || thing is ArcanePlant) && IsHoneyFlowerDef(thing.def);

        private static readonly HashSet<Room> _tmpRooms = new HashSet<Room>();
        private static readonly HashSet<Room> _tmpJudgedRooms = new HashSet<Room>();
        private static readonly List<Room> _tmpStructure = new List<Room>();
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
                if (_tmpJudgedRooms.Contains(room)) { continue; }

                CollectStructureRooms(room, _tmpStructure);
                _tmpJudgedRooms.UnionWith(_tmpStructure);

                var score = StructureScore(_tmpStructure, map, out var structureCells, out var buildingCount);
                if (score > report.roomScore)
                {
                    report.bestRoom = _tmpStructure.MaxByWithFallback(r => r.CellCount);
                    report.roomScore = score;
                    report.roomCellCount = structureCells;
                    report.roomBuildingCount = buildingCount;
                }
            }
            _tmpRooms.Clear();
            _tmpJudgedRooms.Clear();
            _tmpStructure.Clear();

            if (report.bestRoom != null)
            {
                report.bestRoomCell = CentralCell(report.bestRoom, map);
            }

            report.averageMana = AverageMana(map, center);
            report.manaScore = Mathf.Clamp01(report.averageMana / FullMana);

            report.growingPeriodDays = GrowingPeriodDays(map);

            return report;
        }

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

        public static bool IsUsableFertileCell(Map map, IntVec3 cell)
        {
            if (cell.Roofed(map) || cell.GetEdifice(map) != null) { return false; }
            return map.fertilityGrid.FertilityAt(cell) >= MinFertility;
        }

        public static bool IsOutside(Room room) => room.PsychologicallyOutdoors || room.OpenRoofCount >= room.CellCount;

        private static readonly HashSet<Region> _tmpVisitedDoorRegions = new HashSet<Region>();

        public static void CollectStructureRooms(Room start, List<Room> result)
        {
            result.Clear();
            if (start == null || start.IsDoorway || IsOutside(start)) { return; }

            _tmpVisitedDoorRegions.Clear();
            result.Add(start);
            for (int i = 0; i < result.Count; ++i)
            {
                // Room.Regions는 공용 임시 목록을 반환한다.
                var districts = result[i].Districts;
                for (int j = 0; j < districts.Count; ++j)
                {
                    var regions = districts[j].Regions;
                    for (int k = 0; k < regions.Count; ++k)
                    {
                        VisitLinkedDoors(regions[k], result);
                    }
                }
            }
            _tmpVisitedDoorRegions.Clear();
        }

        private static void VisitLinkedDoors(Region region, List<Room> result)
        {
            var links = region.links;
            for (int i = 0; i < links.Count; ++i)
            {
                var door = links[i].GetOtherRegion(region);
                if (door == null || !door.IsDoorway || !_tmpVisitedDoorRegions.Add(door)) { continue; }

                var doorLinks = door.links;
                for (int j = 0; j < doorLinks.Count; ++j)
                {
                    var room = doorLinks[j].GetOtherRegion(door)?.Room;
                    if (room == null || room.IsDoorway || result.Contains(room) || IsOutside(room)) { continue; }
                    result.Add(room);
                }
                VisitLinkedDoors(door, result);
            }
        }

        public static float StructureScore(List<Room> rooms, Map map, out int cellCount, out int buildingCount)
        {
            cellCount = 0;
            buildingCount = 0;
            if (rooms.Count == 0) { return 0f; }

            for (int i = 0; i < rooms.Count; ++i)
            {
                var room = rooms[i];
                if (room.IsDoorway || room.TouchesMapEdge || !room.ProperRoom || room.PsychologicallyOutdoors || room.IsHuge) { return 0f; }
                if (room.Fogged || room.IsPrisonCell || room.OpenRoofCount > 0) { return 0f; }
                cellCount += room.CellCount;
            }
            if (cellCount < MinRoomCells || cellCount > MaxRoomCells) { return 0f; }

            if (!HasBorderDoor(rooms[0], map)) { return 0f; }

            for (int i = 0; i < rooms.Count; ++i)
            {
                if (!TryCountRoomBuildings(rooms[i], map, ref buildingCount)) { return 0f; }
            }

            var emptiness = 1f - BuildingPenalty * buildingCount;
            if (emptiness <= 0f) { return 0f; }

            return RoomSizeFactorCurve.Evaluate(cellCount) * emptiness;
        }

        private static bool HasBorderDoor(Room room, Map map)
        {
            foreach (var border in room.BorderCells)
            {
                if (border.InBounds(map) && border.GetDoor(map) != null) { return true; }
            }
            return false;
        }

        private static bool TryCountRoomBuildings(Room room, Map map, ref int buildingCount)
        {
            foreach (var cell in room.Cells)
            {
                if (cell.InNoBuildEdgeArea(map)) { return false; }

                var foundation = map.terrainGrid.FoundationAt(cell);
                if (foundation != null && foundation.IsSubstructure) { return false; }
            }

            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; ++i)
            {
                if (!(things[i] is Building building) || building.Faction != Faction.OfPlayer) { continue; }
                if (!room.ContainsCell(building.Position)) { continue; }

                if (building is Building_Bed bed && bed.OwnersForReading.Count > 0) { return false; }

                if (building is ArcanePlant || building is ArcanePlantPot || building is Building_ViviLure) { continue; }

                buildingCount++;
            }
            return true;
        }

        public static float AverageMana(Map map, IntVec3 center)
        {
            var manaComp = map.GetManaComponent();
            if (manaComp == null) { return 0f; }

            var sum = manaComp.TotalManaInRadius(center, ManaRadius, out var count);
            return count > 0 ? sum / count : 0f;
        }

        public static IntVec3 CentralCell(Room room, Map map)
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
