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

        public bool HasEnoughFlowers => flowerCount >= ViviNestSiteEvaluator.MinFlowerCount;
        public bool HasRoom => bestRoom != null;
        public bool Acceptable => HasEnoughFlowers && HasRoom;

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
                return sb.ToString();
            }
        }

        public string ToInspectString()
        {
            var sb = new StringBuilder();
            sb.Append(LocalizeString_Inspector.VV_Inspector_LureScoreFlowers.Translate(flowerCount, ViviNestSiteEvaluator.MinFlowerCount, flowerScore.ToStringPercent()));
            if (HasRoom)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureScoreRoom.Translate(roomCellCount, roomBuildingCount, roomScore.ToStringPercent()));
            }
            else
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureMissingRoom.Translate());
            }
            sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureScoreMana.Translate(averageMana.ToString("0"), manaScore.ToStringPercent()));
            if (Acceptable)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureScoreTotal.Translate(TotalScore.ToStringPercent()));
            }
            return sb.ToString();
        }
    }

    // 유인통 주변이 비비가 살 만한 곳인지 꽃·빈 방·환경 마나로 매긴다.
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
