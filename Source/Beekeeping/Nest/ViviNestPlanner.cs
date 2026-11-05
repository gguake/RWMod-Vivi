using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지의 공사 계획: 경계 구멍은 늘 메우고 포장 식물은 늘 설치하며, 그 밖의 공사는 하나가 끝나야 다음 것을 놓는다.
    // 청사진만 놓고 실제 건설·설치는 바닐라 건설 워크기버가 한다(공성과 같은 방식). 빈 지붕은 둥지 지붕 잡이 얹는다.
    public class ViviNestPlanner : IExposable
    {
        public const int PlanInterval = 2500;

        public const int CellsPerShelf = 12;
        public const int CellsPerHatchery = 20;
        public const int MaxHatcheries = 4;
        public const int FloorTilesPerProject = 4;
        public const int WaxForFloor = 40;
        public const int IdleForHorseshoes = 2;
        public const int RoomCellsPerMember = 6;

        private const float OutdoorBuildRadius = 8f;
        // 시체를 버릴 거리(둥지 중심에서). 무리가 오가는 반경(24칸) 안에서 둥지와 떨어진 곳
        private const float CorpseDumpMinDistance = 14f;
        private const float CorpseDumpMaxDistance = 22f;
        private const float ExpansionSearchRadius = 16f;
        private const float ExpansionMinDistance = 7f;
        private const int ExpansionSearchStep = 3;
        private const int ResourceNeedInterval = 250;

        private readonly LordJob_ViviNest _owner;

        private List<IntVec3> _doorCells = new List<IntVec3>();
        private bool _initialFacilitiesPlanned;
        private List<IntVec3> _expansionInterior = new List<IntVec3>();
        private IntVec3 _expansionDoor = IntVec3.Invalid;

        private readonly Dictionary<ThingDef, int> _resourceNeed = new Dictionary<ThingDef, int>();
        private int _resourceNeedTick = -99999;

        private const int ConstructionCacheInterval = 60;
        private readonly List<Thing> _constructionCache = new List<Thing>();
        private int _constructionCacheTick = -99999;

        public ViviNestPlanner(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;
        private Faction Faction => _owner.Faction;
        private ViviNestTerritory Territory => _owner.Territory;

        // 정착할 때 경계 중 문 칸을 기억해 둔다(문이 부서지면 문으로 다시 짓는다).
        public void Notify_Established()
        {
            _doorCells.Clear();
            foreach (var door in Territory.BorderDoors)
            {
                _doorCells.Add(door.Position);
            }
        }

        public void Tick()
        {
            RepairBoundary();
            PlanInstalls();
            _owner.Garden.EnsureCottonCells();

            if (AnyPendingProject()) { return; }
            if (TryFinishExpansion()) { return; }

            if (!_initialFacilitiesPlanned)
            {
                _initialFacilitiesPlanned = true;
                if (PlanInitialFacilities()) { return; }
            }

            if (TryPlanExpansion()) { return; }
            if (TryPlanShelf()) { return; }
            if (TryPlanHatchery()) { return; }
            if (TryPlanFloor()) { return; }
            if (TryPlanHorseshoesPin()) { return; }
        }

        #region 경계 수리
        // 방 경계 스냅숏에서 벽·문이 사라진 칸을 크림 벽(문 자리는 밀랍 문)으로 메운다.
        private void RepairBoundary()
        {
            var map = Map;
            foreach (var cell in Territory.BoundaryCells)
            {
                if (!cell.InBounds(map)) { continue; }

                var edifice = cell.GetEdifice(map);
                if (edifice != null && (edifice.def.passability == Traversability.Impassable || edifice is Building_Door)) { continue; }
                if (HasNestConstructionAt(cell)) { continue; }

                if (_doorCells.Contains(cell))
                {
                    TryPlace(ThingDefOf.Door, cell, Rot4.North, VVThingDefOf.VV_Viviwax);
                }
                else
                {
                    TryPlace(VVThingDefOf.VV_ViviCreamWall, cell, Rot4.North, null);
                }
            }
        }
        #endregion

        #region 포장 식물 설치
        // 둥지 몫 포장 마법식물(정착대가 가져온 잔불꽃·풍년화)은 바닐라 설치 청사진(플레이어의 다시 심기와 같은 것)을 놓아 다 자란 채로 세운다.
        // 포장을 가져가 그 자리에 세우는 일은 바닐라 건설 워크기버가 한다(설치 청사진의 자재 운반이 곧 설치다). 화분 위 설치도 기존 마법식물 시스템이 받는다.
        // 경계 수리처럼 다른 공사를 기다리지 않고, 설치 청사진은 다른 공사를 막지 않는다(AnyPendingProject).
        // 생존용 꽃(잔불꽃)은 방 안 둥지 화분에, 나머지는 비옥한 바깥 땅에 둔다. 둘 곳이 없으면 포장한 채 둥지에 남겨 둔다(씨앗과 같다).
        private void PlanInstalls()
        {
            var map = Map;
            var minis = map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing);
            for (int i = 0; i < minis.Count; ++i)
            {
                if (!(minis[i] is MinifiedThing mini) || !(mini.InnerThing is ArcanePlant plant)) { continue; }
                if (!_owner.Stock.IsNestOwned(mini) || InstallBlueprintUtility.ExistingBlueprintFor(mini) != null) { continue; }

                var cell = JobGiver_ViviNestPlantSeed.IsPotPlant(plant.def) ? FindPotInstallCell(plant.def) : FindOutdoorInstallCell(plant.def);
                if (!cell.IsValid) { continue; }

                _resourceNeedTick = -99999;
                _constructionCacheTick = -99999;
                GenConstruct.PlaceBlueprintForInstall(mini, cell, map, Rot4.North, Faction);
            }
        }

        // 방 안 둥지 화분 중 식물·새싹·설치 청사진이 없는 칸(기존 마법식물 놓기 판정, 씨앗 심기의 화분 고르기와 같다)
        private IntVec3 FindPotInstallCell(ThingDef plantDef)
        {
            var map = Map;
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!(building is ArcanePlantPot pot) || pot.Faction != Faction || !Territory.IsRoomCell(pot.Position)) { continue; }

                var cell = pot.Position;
                if (cell.GetFirstThing<ArcanePlant>(map) != null || cell.GetFirstThing<ArcanePlant_Seedling>(map) != null) { continue; }
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(map, cell, plantDef).Accepted) { continue; }

                return cell;
            }
            return IntVec3.Invalid;
        }

        // 바깥 마법식물(풍년화 등)을 둘 칸: 바깥 씨앗 자리(JobGiver_ViviNestPlantSeed)처럼 둥지 반경 12칸 안 바깥 빈 땅 중 비옥도가 가장 높은 칸
        // (같으면 환경 마나가 많은 칸, 비옥도 0.7 미만 제외). 다 자란 식물은 새싹처럼 제 칸 마나를 하루 80씩 받아야 자라는 것이 아니라 마나 하한은 두지 않는다.
        // 풍년화는 흙 비옥도로 마나를 만들고(비옥도 1 → 하루 150, 1.4 → 1050, 소모 300, 남는 마나는 둘레에 내놓고 제 칸 흙도 차츰 기름지게 한다)
        // 꿀이 많이 나는 꽃이라 둥지가 거둘 수 있게 채집 자리 반경 안 칸만 쓴다. 설치하면 그 칸 식물이 지워지므로 꿀 꽃이 자라는 칸은 피한다.
        private IntVec3 FindOutdoorInstallCell(ThingDef plantDef)
        {
            var map = Map;
            var mana = map.GetManaComponent();
            var spot = _owner.Production.GatherSpot;
            var best = IntVec3.Invalid;
            var bestFertility = 0f;
            var bestMana = -1f;
            var cellCount = GenRadial.NumCellsInRadius(JobGiver_ViviNestPlantSeed.OutdoorSearchRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = _owner.NestCell + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) { continue; }

                var fertility = map.fertilityGrid.FertilityAt(cell);
                var cellMana = mana != null ? mana[cell] : 0f;
                if (fertility < JobGiver_ViviNestPlantSeed.MinFertility || fertility < bestFertility || (fertility == bestFertility && cellMana <= bestMana)) { continue; }
                if (spot != null && !spot.Position.InHorDistOf(cell, spot.GatherRadius)) { continue; }
                if (!IsFreeOutdoorCell(cell, allowPlants: true)) { continue; }

                var plant = cell.GetPlant(map);
                if (plant != null && ViviNestGarden.IsHoneyFlowerDef(plant.def)) { continue; }
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(map, cell, plantDef).Accepted) { continue; }

                best = cell;
                bestFertility = fertility;
                bestMana = cellMana;
            }
            return best;
        }
        #endregion

        #region 초기 설비
        // 정착 직후: 마나 방출기(바깥)와 정착대가 가져온 잔불꽃을 둘 밀랍 화분(방 안)
        // 화분은 방 안 벽 쪽 칸에, 마나 방출기는 그 바로 옆 벽 쪽 방 칸에 나란히 둔다.
        // 잔불꽃의 열은 마나가 있을 때만 나오고 방이 20°C 미만이면 스스로 마나를 못 만들어, 방출기와 붙어 있어야 겨울을 난다.
        // 장기 계측: 방출기를 벽 바깥에 세운 둥지는 실제로는 화분과 떨어져 화분 칸 마나가 0~2였고 잔불꽃 저장이 겨울 동안 700→50으로 줄었다.
        // 실험(한파 속 8×8 방): 방출기가 화분 옆이면 저장 700을 유지하며 방을 28.5°C로 지켰다. 나란히 둘 자리가 없으면 벽 바깥에 세운다.
        private bool PlanInitialFacilities()
        {
            if (TryPlacePotWithEmitter()) { return true; }

            var placed = false;
            var emitterCell = IntVec3.Invalid;

            foreach (var cell in OutdoorBuildCells().OrderByDescending(TouchesBoundaryWall))
            {
                if (TryPlace(VVThingDefOf.VV_ManaEmitter, cell, Rot4.North, null) != null)
                {
                    emitterCell = cell;
                    placed = true;
                    break;
                }
            }

            var potCells = WallSideRoomCells().ToList();
            if (emitterCell.IsValid)
            {
                potCells.SortBy(c => c.DistanceToSquared(emitterCell));
            }
            foreach (var cell in potCells)
            {
                if (TryPlace(VVThingDefOf.VV_ArcanePlantPot, cell, Rot4.North, VVThingDefOf.VV_Viviwax) != null)
                {
                    placed = true;
                    break;
                }
            }

            return placed;
        }

        private bool TryPlacePotWithEmitter()
        {
            var wallSide = new HashSet<IntVec3>(WallSideRoomCells());
            foreach (var potCell in wallSide)
            {
                foreach (var dir in GenAdj.CardinalDirections)
                {
                    var emitterCell = potCell + dir;
                    if (!wallSide.Contains(emitterCell)) { continue; }

                    var pot = TryPlace(VVThingDefOf.VV_ArcanePlantPot, potCell, Rot4.North, VVThingDefOf.VV_Viviwax);
                    if (pot == null) { break; }
                    if (TryPlace(VVThingDefOf.VV_ManaEmitter, emitterCell, Rot4.North, null) != null) { return true; }

                    pot.Destroy(DestroyMode.Cancel);
                }
            }
            return false;
        }

        private bool TouchesBoundaryWall(IntVec3 cell)
        {
            foreach (var adj in GenAdj.CardinalDirections)
            {
                var c = cell + adj;
                var edifice = c.InBounds(Map) ? c.GetEdifice(Map) : null;
                if (edifice != null && edifice.def.passability == Traversability.Impassable && Territory.BoundaryCells.Contains(c)) { return true; }
            }
            return false;
        }
        #endregion

        #region 선반·부화기·바닥·편자 막대
        // 선반은 방 크기에 맞춰(12칸당 하나) 짓는다. 바닥 보관 칸이 남아 있어도 짓는다(사용자 요구).
        private bool TryPlanShelf()
        {
            var roomCells = Territory.Rooms.Sum(r => r.CellCount);
            if (CountNestBuildings(ThingDefOf.ShelfSmall) >= roomCells / CellsPerShelf) { return false; }

            foreach (var cell in WallSideRoomCells())
            {
                if (TryPlace(ThingDefOf.ShelfSmall, cell, Rot4.North, VVThingDefOf.VV_Viviwax) != null) { return true; }
            }
            return false;
        }

        // 로열의 알 게이지가 다 찼는데 알을 낳을 부화기가 없으면 방 안에 부화기를 짓는다(방 크기에 맞춰 개수 제한).
        private bool TryPlanHatchery()
        {
            if (!AnyRoyalReadyToLay()) { return false; }

            var hatcheries = Map.listerBuildings.allBuildingsNonColonist.OfType<ViviEggHatchery>().Where(h => h.Faction == Faction).ToList();
            if (hatcheries.Any(h => h.CanLayHere)) { return false; }

            var roomCells = Territory.Rooms.Sum(r => r.CellCount);
            if (hatcheries.Count >= System.Math.Min(MaxHatcheries, roomCells / CellsPerHatchery)) { return false; }

            foreach (var cell in WallSideRoomCells())
            {
                if (TryPlace(VVThingDefOf.VV_ViviHatchery, cell, Rot4.South, null) != null) { return true; }
            }
            return false;
        }

        private bool AnyRoyalReadyToLay()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return false; }

            foreach (var pawn in pawns)
            {
                var layer = pawn.GetCompViviEggLayer();
                if (layer != null && pawn.IsRoyalVivi() && layer.CanLayEgg) { return true; }
            }
            return false;
        }

        // 바닥이 깔리지 않은 방 칸에 밀랍 타일을 조금씩 깐다(밀랍에 여유가 있을 때만).
        private bool TryPlanFloor()
        {
            if (_owner.Stock.CountOf(VVThingDefOf.VV_Viviwax) < WaxForFloor) { return false; }

            var map = Map;
            var placed = 0;
            foreach (var room in Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (placed >= FloorTilesPerProject) { return true; }

                    var terrain = map.terrainGrid.TerrainAt(cell);
                    if (terrain.IsFloor || terrain == VVTerrainDefOf.VV_HoneycombTile) { continue; }
                    if (HasNestConstructionAt(cell) || cell.GetEdifice(map) != null) { continue; }

                    if (TryPlace(VVTerrainDefOf.VV_HoneycombTile, cell, Rot4.North, null) != null)
                    {
                        placed++;
                    }
                }
            }
            return placed > 0;
        }

        private bool TryPlanHorseshoesPin()
        {
            if (CountNestBuildings(VVThingDefOf.HorseshoesPin) > 0) { return false; }
            if (IdleMemberCount() < IdleForHorseshoes) { return false; }

            foreach (var cell in OutdoorBuildCells())
            {
                if (TryPlace(VVThingDefOf.HorseshoesPin, cell, Rot4.North, VVThingDefOf.VV_Viviwax) != null) { return true; }
            }
            return false;
        }

        private int IdleMemberCount()
        {
            var count = 0;
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return 0; }

            foreach (var pawn in pawns)
            {
                var job = pawn.CurJobDef;
                if (pawn.DevelopmentalStage.Adult() && (job == JobDefOf.GotoWander || job == JobDefOf.Wait_Wander))
                {
                    count++;
                }
            }
            return count;
        }
        #endregion

        #region 증축
        // 구성원에 비해 방이 좁으면 둥지 가까운 빈터에 육각 방(안 되면 사각 방)을 크림 벽·밀랍 문으로 짓는다.
        // 채굴이 필요한 자리(바위·광맥)와 산 지붕 아래는 후보에서 뺀다. 지붕은 방이 닫히면 둥지 지붕 잡이 얹는다.
        private bool TryPlanExpansion()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null || pawns.Count == 0) { return false; }
            if (Territory.Rooms.Sum(r => r.CellCount) >= pawns.Count * RoomCellsPerMember) { return false; }

            if (!TryFindExpansionSite(out var walls, out var interior, out var door)) { return false; }

            foreach (var cell in walls)
            {
                if (cell == door)
                {
                    TryPlace(ThingDefOf.Door, cell, Rot4.North, VVThingDefOf.VV_Viviwax);
                }
                else
                {
                    TryPlace(VVThingDefOf.VV_ViviCreamWall, cell, Rot4.North, null);
                }
            }

            _expansionInterior = interior;
            _expansionDoor = door;
            return true;
        }

        // 증축 벽이 다 서서 방이 닫히면 둥지 방으로 넣는다. 닫히지 않았으면(벽을 못 세운 칸이 있으면) 그만둔다.
        private bool TryFinishExpansion()
        {
            if (_expansionInterior.Count == 0) { return false; }

            var map = Map;
            var room = _expansionInterior[0].GetRoom(map);
            var enclosed = room != null && !room.TouchesMapEdge && !room.IsDoorway && room.CellCount <= _expansionInterior.Count;
            if (enclosed)
            {
                Territory.AddRoom(_expansionInterior[0]);
                if (_expansionDoor.IsValid && !_doorCells.Contains(_expansionDoor))
                {
                    _doorCells.Add(_expansionDoor);
                }
            }

            _expansionInterior.Clear();
            _expansionDoor = IntVec3.Invalid;
            return true;
        }

        private bool TryFindExpansionSite(out List<IntVec3> walls, out List<IntVec3> interior, out IntVec3 door)
        {
            walls = null;
            interior = null;
            door = IntVec3.Invalid;

            var origin = _doorCells.Count > 0 ? _doorCells[0] : _owner.NestCell;
            var bestScore = float.MinValue;
            var cellCount = GenRadial.NumCellsInRadius(ExpansionSearchRadius);

            foreach (var hexagonal in new[] { true, false })
            {
                for (int i = 0; i < cellCount; i += ExpansionSearchStep)
                {
                    var center = _owner.NestCell + GenRadial.RadialPattern[i];
                    if (center.DistanceTo(_owner.NestCell) < ExpansionMinDistance) { continue; }
                    if (!TryMakeShape(center, hexagonal, origin, out var shapeWalls, out var shapeInterior, out var shapeDoor, out var score)) { continue; }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        walls = shapeWalls;
                        interior = shapeInterior;
                        door = shapeDoor;
                    }
                }

                // 육각 방 자리가 있으면 사각 방은 보지 않는다.
                if (walls != null) { return true; }
            }

            return false;
        }

        private bool TryMakeShape(IntVec3 center, bool hexagonal, IntVec3 origin, out List<IntVec3> walls, out List<IntVec3> interior, out IntVec3 door, out float score)
        {
            var map = Map;
            walls = new List<IntVec3>();
            interior = new List<IntVec3>();
            door = IntVec3.Invalid;
            score = 0f;

            var doorCandidates = new List<IntVec3>();
            if (hexagonal)
            {
                var rect = CellRect.CenteredOn(center, HexagonalRoomUtility.RoomCellSize.x, HexagonalRoomUtility.RoomCellSize.z);
                foreach (var c in rect.GetHexagonalEdges())
                {
                    walls.Add(new IntVec3(c.x, 0, c.z));
                }
                foreach (var c in rect.GetHexagonalFills())
                {
                    var cell = new IntVec3(c.x, 0, c.z);
                    if (!walls.Contains(cell)) { interior.Add(cell); }
                }
                doorCandidates.Add(new IntVec3(rect.minX + rect.Width / 2, 0, rect.minZ));
                doorCandidates.Add(new IntVec3(rect.minX + rect.Width / 2, 0, rect.maxZ));
            }
            else
            {
                var rect = CellRect.CenteredOn(center, 3);
                walls.AddRange(rect.EdgeCells);
                interior.AddRange(rect.ContractedBy(1).Cells);
                doorCandidates.Add(new IntVec3(rect.CenterCell.x, 0, rect.minZ));
                doorCandidates.Add(new IntVec3(rect.CenterCell.x, 0, rect.maxZ));
                doorCandidates.Add(new IntVec3(rect.minX, 0, rect.CenterCell.z));
                doorCandidates.Add(new IntVec3(rect.maxX, 0, rect.CenterCell.z));
            }

            var trees = 0;
            foreach (var cell in interior)
            {
                if (!IsExpansionCell(cell, ref trees)) { return false; }
            }
            foreach (var cell in walls)
            {
                if (!IsExpansionCell(cell, ref trees)) { return false; }
                if (!GenConstruct.CanPlaceBlueprintAt(VVThingDefOf.VV_ViviCreamWall, cell, Rot4.North, map).Accepted) { return false; }
            }

            // 문은 둥지 입구에 가까운 쪽, 바깥 칸으로 드나들 수 있어야 한다.
            var bestDoorDistance = float.MaxValue;
            foreach (var candidate in doorCandidates)
            {
                if (!walls.Contains(candidate)) { continue; }

                var outside = IntVec3.Invalid;
                foreach (var dir in GenAdj.CardinalDirections)
                {
                    var adj = candidate + dir;
                    if (!walls.Contains(adj) && !interior.Contains(adj)) { outside = adj; break; }
                }
                if (!outside.IsValid || !outside.InBounds(map) || !outside.Standable(map)) { continue; }
                if (!GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Door, candidate, Rot4.North, map, stuffDef: VVThingDefOf.VV_Viviwax).Accepted) { continue; }

                var distance = candidate.DistanceToSquared(origin);
                if (distance < bestDoorDistance)
                {
                    bestDoorDistance = distance;
                    door = candidate;
                }
            }
            if (!door.IsValid) { return false; }

            score = -center.DistanceTo(origin) - trees * 2f;
            return true;
        }

        // 증축 자리: 채굴·산 지붕·물·구역·다른 건물이 없고 플레이어 건물과 붙지 않은 칸
        private bool IsExpansionCell(IntVec3 cell, ref int trees)
        {
            var map = Map;
            if (!cell.InBounds(map) || cell.InNoBuildEdgeArea(map) || cell.Fogged(map) || cell.Roofed(map)) { return false; }
            if (cell.GetEdifice(map) != null || cell.GetTerrain(map).IsWater) { return false; }
            if (map.zoneManager.ZoneAt(cell) != null) { return false; }
            if (Territory.IsTerritoryCell(cell) || Territory.BoundaryCells.Contains(cell) || _owner.Garden.IsCottonCell(cell)) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (thing.def.category == ThingCategory.Item || thing is Building || thing.def.IsBlueprint || thing.def.IsFrame) { return false; }
                if (thing is Plant plant && plant.def.plant.IsTree) { trees++; }
            }

            foreach (var adj in GenAdj.AdjacentCells)
            {
                var near = cell + adj;
                if (!near.InBounds(map)) { continue; }
                foreach (var thing in near.GetThingList(map))
                {
                    if ((thing is Building || thing.def.IsBlueprint || thing.def.IsFrame) && thing.Faction == Faction.OfPlayer) { return false; }
                }
            }

            return true;
        }
        #endregion

        #region 지붕
        // 둥지 방 칸 중 지붕이 없는 칸(무너졌거나 새로 지은 방)
        public IEnumerable<IntVec3> CellsNeedingRoof()
        {
            var map = Map;
            foreach (var room in Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (!cell.Roofed(map)) { yield return cell; }
                }
            }
        }

        public bool NeedsRoof(IntVec3 cell) => !cell.Roofed(Map) && Territory.IsRoomCell(cell);

        // 바닐라 지붕 짓기와 같은 조건: 지붕 받침 범위 안, 지붕을 막는 것이 없을 것
        public static bool CanRoofNow(IntVec3 cell, Map map)
        {
            return !cell.Roofed(map) && RoofCollapseUtility.WithinRangeOfRoofHolder(cell, map) && RoofUtility.FirstBlockingThing(cell, map) == null;
        }
        #endregion

        #region 칸 고르기
        // 방 안에서 벽에 붙은 칸(가구 자리). 문 옆과 방 한가운데 통로는 비운다.
        private IEnumerable<IntVec3> WallSideRoomCells()
        {
            var map = Map;
            foreach (var room in Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (!IsFreeCell(cell, allowPlants: false)) { continue; }

                    var touchesWall = false;
                    foreach (var adj in GenAdj.CardinalDirections)
                    {
                        var edifice = (cell + adj).GetEdifice(map);
                        if (edifice != null && edifice.def.passability == Traversability.Impassable)
                        {
                            touchesWall = true;
                            break;
                        }
                    }
                    if (touchesWall) { yield return cell; }
                }
            }
        }

        // 문 밖 가까운 빈 땅(바깥 보관 칸·채집 장소·통로 제외)
        private IEnumerable<IntVec3> OutdoorBuildCells()
        {
            var origin = _doorCells.Count > 0 ? _doorCells[0] : _owner.NestCell;
            var cellCount = GenRadial.NumCellsInRadius(OutdoorBuildRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = origin + GenRadial.RadialPattern[i];
                if (IsFreeOutdoorCell(cell, allowPlants: false)) { yield return cell; }
            }
        }

        // 시체를 둘 바깥 칸: 둥지에서 떨어진 지붕 없는 빈 땅. 정착민 집 구역은 되도록 피한다.
        public bool TryFindCorpseDumpCell(Pawn pawn, out IntVec3 result)
        {
            var map = Map;
            var center = _owner.NestCell;
            var start = GenRadial.NumCellsInRadius(CorpseDumpMinDistance);
            var end = GenRadial.NumCellsInRadius(CorpseDumpMaxDistance);
            var fallback = IntVec3.Invalid;
            var offset = Rand.Range(0, end - start);
            for (int i = 0; i < end - start; ++i)
            {
                var cell = center + GenRadial.RadialPattern[start + (i + offset) % (end - start)];
                // 바닐라 운반 잡(HaulToCell)은 놓을 칸을 예약하므로 다른 폰이 예약해 둔 칸은 피한다(보관 칸 찾기와 같다).
                if (!IsFreeOutdoorCell(cell, allowPlants: true) || !pawn.CanReserve(cell) || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)) { continue; }

                if (!map.areaManager.Home[cell])
                {
                    result = cell;
                    return true;
                }
                if (!fallback.IsValid) { fallback = cell; }
            }

            result = fallback;
            return fallback.IsValid;
        }

        // 둥지 바깥 빈 땅: 지붕·구역·둥지 칸·목화밭이 아니고, 둥지 건물의 통로를 막지 않는 칸
        public bool IsFreeOutdoorCell(IntVec3 cell, bool allowPlants, bool ignoreCotton = false)
        {
            var map = Map;
            if (!cell.InBounds(map) || cell.Roofed(map) || cell.InNoBuildEdgeArea(map)) { return false; }
            if (Territory.IsTerritoryCell(cell) || Territory.BoundaryCells.Contains(cell)) { return false; }
            if (!ignoreCotton && _owner.Garden.IsCottonCell(cell)) { return false; }
            if (map.zoneManager.ZoneAt(cell) != null) { return false; }
            return IsFreeCell(cell, allowPlants);
        }

        private bool IsFreeCell(IntVec3 cell, bool allowPlants)
        {
            var map = Map;
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetEdifice(map) != null || cell.Fogged(map)) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (thing is Building || thing.def.IsBlueprint || thing.def.IsFrame) { return false; }
                if (thing.def.category == ThingCategory.Item) { return false; }
                if (thing is Plant plant && (!allowPlants || plant.def.plant.IsTree)) { return false; }
            }

            foreach (var door in Territory.BorderDoors)
            {
                if (cell.AdjacentTo8WayOrInside(door.Position)) { return false; }
            }

            // 다른 둥지 건물의 상호작용 칸을 막지 않는다.
            var production = _owner.Production;
            if (production.GatherSpot != null && production.GatherSpot.InteractionCell == cell) { return false; }
            var workSpots = production.WorkSpots;
            for (int i = 0; i < workSpots.Count; ++i)
            {
                if (workSpots[i].Spawned && workSpots[i].InteractionCell == cell) { return false; }
            }

            return true;
        }
        #endregion

        #region 청사진
        public Thing TryPlace(BuildableDef def, IntVec3 cell, Rot4 rot, ThingDef stuff)
        {
            var map = Map;
            if (!GenConstruct.CanPlaceBlueprintAt(def, cell, rot, map, stuffDef: stuff).Accepted) { return null; }

            // 플레이어 건물·청사진·구역과 겹치면 바닐라는 그것을 해체·치우려 들므로 놓지 않는다.
            foreach (var occupied in GenAdj.OccupiedRect(cell, rot, def.Size))
            {
                if (map.zoneManager.ZoneAt(occupied) != null) { return null; }
                foreach (var thing in occupied.GetThingList(map))
                {
                    if ((thing is Building || thing.def.IsBlueprint || thing.def.IsFrame) && thing.Faction == Faction.OfPlayer) { return null; }
                }
            }

            _resourceNeedTick = -99999;
            _constructionCacheTick = -99999;
            return GenConstruct.PlaceBlueprintForBuild(def, cell, map, rot, Faction, stuff);
        }

        private bool HasNestConstructionAt(IntVec3 cell)
        {
            foreach (var thing in cell.GetThingList(Map))
            {
                if ((thing.def.IsBlueprint || thing.def.IsFrame) && thing.Faction == Faction) { return true; }
            }
            return false;
        }

        public IEnumerable<Thing> NestConstructions()
        {
            var map = Map;
            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (thing.Faction == Faction) { yield return thing; }
            }
            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (thing.Faction == Faction) { yield return thing; }
            }
        }

        // 건설 잡용: 잠깐(60틱) 묶어 둔 둥지 골조·청사진. 그사이 사라진 것은 쓰는 쪽이 거른다.
        public List<Thing> CachedNestConstructions()
        {
            var ticks = Find.TickManager.TicksGame;
            if (ticks - _constructionCacheTick >= ConstructionCacheInterval)
            {
                _constructionCacheTick = ticks;
                _constructionCache.Clear();
                _constructionCache.AddRange(NestConstructions());
            }
            return _constructionCache;
        }

        // 설치 청사진(포장 식물)은 재료 없이 가져가 세우기만 하므로 다른 공사를 막지 않는다.
        private bool AnyPendingProject() => NestConstructions().Any(t => !(t is Blueprint_Install));

        // 둥지 청사진·골조에 아직 들어가야 할 재료 수(생산 목표에 더한다)
        public int ResourceNeeded(ThingDef def)
        {
            var ticks = Find.TickManager.TicksGame;
            if (ticks - _resourceNeedTick >= ResourceNeedInterval)
            {
                _resourceNeedTick = ticks;
                _resourceNeed.Clear();
                foreach (var thing in NestConstructions())
                {
                    // 설치 청사진은 재료가 없다(바닐라는 재료를 물으면 오류를 낸다).
                    if (!(thing is IConstructible constructible) || thing is Blueprint_Install) { continue; }
                    foreach (var cost in constructible.TotalMaterialCost())
                    {
                        var needed = constructible.ThingCountNeeded(cost.thingDef);
                        if (needed > 0)
                        {
                            _resourceNeed[cost.thingDef] = (_resourceNeed.TryGetValue(cost.thingDef, out var n) ? n : 0) + needed;
                        }
                    }
                }
            }
            return _resourceNeed.TryGetValue(def, out var count) ? count : 0;
        }

        private int CountNestBuildings(ThingDef def)
        {
            var count = 0;
            foreach (var building in Map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def == def && building.Faction == Faction) { count++; }
            }
            return count;
        }
        #endregion

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _doorCells, "doorCells", LookMode.Value);
            Scribe_Values.Look(ref _initialFacilitiesPlanned, "initialFacilitiesPlanned");
            Scribe_Collections.Look(ref _expansionInterior, "expansionInterior", LookMode.Value);
            Scribe_Values.Look(ref _expansionDoor, "expansionDoor", IntVec3.Invalid);
        }
    }
}
