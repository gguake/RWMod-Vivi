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
        // 성장(사용자 요구 "확장하는지"): 목표 성장 속도는 로열의 알 속도(알 게이지 하루 0.125, 8일에 하나)다.
        // 알은 7~13일(따뜻한 실내 보너스로 장기 계측 6~8일)에 깨고, 아기는 아기 시기 내내 부화기를 아기 침대로 쓴다
        // (어린이 노화 4배 × 비비 유전자 2~3배로 약 18~22일). 부화기 하나는 알·아기로 25~30일 쓰이므로 8일마다 낳으려면 부화기가 3~4개 든다.
        // 방 크기 제한(20칸당 하나)으로 처음 방(36칸)에는 하나뿐이지만, 증축은 앞으로 깰 구성원까지 세어(ExpectedMembers) 아기가 깨기 전에 하므로
        // 첫 아기가 부화기를 쓰는 동안 다음 알 게이지가 차면 증축 방(47칸)이 서서 넷까지 짓는다.
        // 어린이(몸 크기 0.28)도 부화기(아기 침대, 0.3 이하)에 들어가지만 쓰지 않는다(LordJob_ViviNest.FindBedFor): 계측(probe_growth)에서 어린이가 된 첫 아기가
        // 낮잠·밤잠마다 부화기를 다시 차지했고(바닐라는 생일마다 소유를 풀 뿐이다), 어린이 시기(12살까지, 약 45일) 동안 로열이 알을 못 낳았다.
        public const int CellsPerHatchery = 20;
        public const int MaxHatcheries = 4;
        public const int FloorTilesPerProject = 4;
        public const int WaxForFloor = 40;
        public const int IdleForHorseshoes = 2;
        // 방 수용량: 방 6칸당 1명(앞으로 깰 구성원 포함). 넘으면 증축하고, 증축할 자리가 없으면 수용량을 넘겨 알을 낳지 않는다(EggAllowed).
        public const int RoomCellsPerMember = 6;
        // 알 낳기 식량 하한: 남은 둥지 식량이 이만큼(일)보다 적으면 알을 낳지 않는다. 알은 7일 넘게 지나야 깨고 깬 아기도 둥지 꿀을 먹으므로
        // 식량이 바닥나 가는 둥지가 식구를 늘려 굶주림 이주(1일 미만)에 몰리지 않게 한다. 장기 계측에서 정착 5일 뒤 둥지 식량은 대개 6~25일 치였다(한랭 겨울 끝 6일).
        // 알은 꿀 꽃이 자라는 철(FlowerSeason)에만 낳는다. 꽃이 자라지 않는 겨울에는 둥지 식량이 비축 꿀로만 줄어든다.
        // 장기 계측(final_cold60 북방림, 겨울 바깥 -48°C): 가을·겨울(16~42일)에도 알을 낳아 6→10명이 됐고, 비축 꿀(걸러낸 꿀 831·경단 1012)이
        // 겨울 동안 바닥나 50일째 굶주림으로 떠났다(식량 하한 5일은 알을 낳을 때 넘었다). 예전 한랭 계측은 겨울 내내 5~7명으로 식량 7.7일 치 이상을 지켰다.
        public const float MinFoodDaysForEgg = 5f;

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
        // 계획 주기마다 다시 잰다(저장하지 않는다): 로열이 알을 낳아도 되는지, 마지막 증축 자리 찾기가 실패했는지
        private bool _eggAllowed = true;
        private bool _noExpansionSite;

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

        public bool EggAllowed => _eggAllowed;

        public void Tick()
        {
            _eggAllowed = ComputeEggAllowed();
            ReleaseHatcheryOwners();
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

            // 잔불꽃은 구조물(둥지 방)에 먼저 둔다: 둘 잔불꽃이 받아 줄 빈 화분(방마다 상한까지)보다 많으면 다른 공사보다 먼저 화분을 짓는다.
            if (TryPlanEmberPot()) { return; }
            if (TryPlanExpansion()) { return; }
            // 알 게이지가 찬 로열이 낳을 부화기는 선반보다 먼저 짓는다(증축 뒤 늘어난 선반 몫을 다 지을 때까지 기다리지 않게).
            if (TryPlanHatchery()) { return; }
            if (TryPlanShelf()) { return; }
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

        #region 포장 식물 설치·잔불꽃 배치
        // 둥지 몫 포장 마법식물(정착대가 가져온 잔불꽃·풍년화)은 바닐라 설치 청사진(플레이어의 다시 심기와 같은 것)을 놓아 다 자란 채로 세운다.
        // 포장을 가져가 그 자리에 세우는 일은 바닐라 건설 워크기버가 한다(설치 청사진의 자재 운반이 곧 설치다). 화분 위 설치도 기존 마법식물 시스템이 받는다.
        // 경계 수리처럼 다른 공사를 기다리지 않고, 설치 청사진은 다른 공사를 막지 않는다(AnyPendingProject).
        // 생존용 꽃(잔불꽃)은 둥지 방 화분에(잔불꽃이 없는 방부터, 그다음 추운 방부터, FindEmberPotCell), 나머지는 비옥한 바깥 땅에 둔다. 둘 곳이 없으면 포장한 채 둥지에 남겨 둔다(씨앗과 같다).
        // 바깥에서 자란 둥지 잔불꽃(미지의 씨앗은 기존 마법식물 시스템대로 무작위 마법식물로 자란다)은 방마다 상한(MaxEmberPots)까지 빈 화분이 있으면
        // 바닐라 다시 설치 청사진(플레이어의 다시 심기와 같다)으로 그 방 화분에 옮긴다. 뽑아 든 포장은 둥지 몫이다(BeekeepingPatch, MakeMinified).
        private void PlanInstalls()
        {
            var map = Map;
            var minis = map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing);
            for (int i = 0; i < minis.Count; ++i)
            {
                if (!(minis[i] is MinifiedThing mini) || !(mini.InnerThing is ArcanePlant plant)) { continue; }
                if (!_owner.Stock.IsNestOwned(mini) || InstallBlueprintUtility.ExistingBlueprintFor(mini) != null) { continue; }

                var def = plant.def;
                var cell = JobGiver_ViviNestPlantSeed.IsPotPlant(def)
                    ? FindEmberPotCell(c => ArcanePlantUtility.CanPlaceArcanePlantToCell(map, c, def).Accepted)
                    : FindOutdoorInstallCell(def);
                if (!cell.IsValid) { continue; }

                MarkConstructionsDirty();
                GenConstruct.PlaceBlueprintForInstall(mini, cell, map, Rot4.North, Faction);
            }

            foreach (var plant in OutdoorEmbersToMove())
            {
                var cell = FindEmberPotCell(c => ArcanePlantUtility.CanPlaceArcanePlantToCell(map, c, plant.def, plant).Accepted);
                if (!cell.IsValid) { break; }

                MarkConstructionsDirty();
                GenConstruct.PlaceBlueprintForReinstall(plant, cell, map, Rot4.North, Faction);
            }
        }

        // 잔불꽃은 구조물(둥지 방)에 먼저 둔다(사용자 요구 "구조물에 잔불꽃을 우선 배치"). 잔불꽃의 열은 제 방만 데우므로
        // 증축 방에 잔불꽃이 없으면 겨울에 영하로 내려간다(장기 계측: 온대 겨울 증축 방 -17°C). 잔불꽃이 없는 방부터 하나씩 두고, 남는 잔불꽃
        // (바깥에서 자란 것·씨앗·포장)도 방마다 상한(MaxEmberPots)까지 가장 추운 방부터 둔다. 화분은 잔불꽃만 쓰고(씨앗 심기·설치 모두)
        // 다른 마법식물(미지의 씨앗·전투·장식 식물)은 바깥 땅에 둔다.
        // 증축 방에는 방출기를 두지 않는다: 방 안 마법식물은 저장 마나가 절반 아래면 예비분 제한 없이 비료를 받으므로(JobGiver_ViviNestFertilizePlant)
        // 잔불꽃은 마나 소모(하루 200)만큼 비료 하루 2개로 지내지만, 방출기는 하루 8개를 태워 마나 대부분을 둘레로 흩는다.
        // 방마다 잔불꽃(화분) 상한은 방 18칸당 하나(최소 하나)다. 잔불꽃 하나의 열(초당 19)은 방 칸 수로 나뉘어 오르고 바닐라 방 기온은 지붕·벽·열린 문으로
        // 바깥과 섞인다. 장기 계측(한랭 60일): 잔불꽃 하나인 처음 방(36칸)·육각 증축 방(47칸)이 바깥 -24~-27°C에 12°C까지 내려갔다(바깥보다 35~37°C 높음).
        // 잔불꽃이 둘이면 열이 두 배라 잔불꽃이 데우는 상한(29°C)을 지키므로 두 방은 둘, 사각 증축 방(25칸)은 하나까지 둔다.
        public const int CellsPerEmberPot = 18;

        private readonly Dictionary<Room, int> _emberCounts = new Dictionary<Room, int>();
        private readonly List<ArcanePlant> _tmpOutdoorEmbers = new List<ArcanePlant>();
        private readonly List<Room> _tmpPotRooms = new List<Room>();
        // 화분 청사진을 놓을 벽 쪽 칸이 없던 잔불꽃 없는 방(저장하지 않는다, 계획 주기마다 다시 해 본다). 이런 방은 다른 방의 빈 화분을 막지 않는다.
        private readonly HashSet<Room> _noPotRooms = new HashSet<Room>();

        private static ThingDef EmberSeedDef => VVThingDefOf.VV_EmberBloom.GetModExtension<ArcaneSeedExtension>()?.seedDef;

        public static int MaxEmberPots(Room room) => System.Math.Max(1, room.CellCount / CellsPerEmberPot);

        // 잔불꽃을 둘 둥지 화분 칸: 잔불꽃이 없는 둥지 방이 있으면 그 방들의 빈 화분만 쓰고, 그 방에 빈 화분이 없으면 설계자가 화분을 지을 때까지 기다린다.
        // 그다음(잔불꽃 없는 방이 없거나 화분을 지을 자리가 없으면) 잔불꽃이 상한보다 적은 다른 방의 빈 화분을 쓴다. 같은 쪽에서는 추운 방부터.
        // 자동 테스트(final_full3 p3 B12): 잔불꽃이 이미 있는 처음 방의 빈 화분이 바깥 잔불꽃을 먼저 받아 가 잔불꽃 없는 증축 방에는 끝내 화분을 짓지 않았다.
        // 빈 화분은 식물·새싹이 없는 둥지 세력 화분이고, 설치 청사진이 놓인 칸 등은 validator(기존 마법식물 놓기·씨앗 심기 판정)가 거른다.
        public IntVec3 FindEmberPotCell(System.Predicate<IntVec3> validator)
        {
            var map = Map;
            var counts = EmberCounts();
            var emberlessFirst = AnyOpenEmberlessRoom(counts);
            var best = IntVec3.Invalid;
            var bestEmberless = false;
            var bestTemperature = float.MaxValue;
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!(building is ArcanePlantPot pot) || pot.Faction != Faction || !Territory.IsRoomCell(pot.Position)) { continue; }

                var cell = pot.Position;
                if (cell.GetFirstThing<ArcanePlant>(map) != null) { continue; }

                var room = cell.GetRoom(map);
                if (room == null) { continue; }
                var embers = EmberCount(counts, room);
                if (embers >= MaxEmberPots(room)) { continue; }

                var emberless = embers == 0;
                if (emberlessFirst && !emberless) { continue; }
                var temperature = room.Temperature;
                if (best.IsValid && ((bestEmberless && !emberless) || (bestEmberless == emberless && temperature >= bestTemperature))) { continue; }
                if (!validator(cell)) { continue; }

                best = cell;
                bestEmberless = emberless;
                bestTemperature = temperature;
            }
            return best;
        }

        // 잔불꽃을 받을 수 있는 잔불꽃 없는 둥지 방이 있는지(화분을 지을 자리가 없던 방은 빼되, 그 방에 빈 화분이 있으면 FindEmberPotCell이 먼저 고른다)
        private bool AnyOpenEmberlessRoom(Dictionary<Room, int> counts)
        {
            var rooms = Territory.Rooms;
            for (int i = 0; i < rooms.Count; ++i)
            {
                if (EmberCount(counts, rooms[i]) == 0 && !_noPotRooms.Contains(rooms[i])) { return true; }
            }
            return false;
        }

        // 둥지 방마다 잔불꽃 수: 다 자란 잔불꽃, 잔불꽃 새싹(화분에 심은 잔불꽃 씨앗), 잔불꽃 설치·다시 설치 청사진, 구성원이 심으러 가는 잔불꽃 씨앗의 자리
        private Dictionary<Room, int> EmberCounts()
        {
            var map = Map;
            _emberCounts.Clear();
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.Faction != Faction) { continue; }
                if (building.def == VVThingDefOf.VV_EmberBloom || (building is ArcanePlant_Seedling seedling && seedling.MaturePlantDef == VVThingDefOf.VV_EmberBloom))
                {
                    AddEmber(building.Position);
                }
            }

            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (thing.Faction == Faction && thing is Blueprint_Install install && install.ThingToInstall?.def == VVThingDefOf.VV_EmberBloom)
                {
                    AddEmber(thing.Position);
                }
            }

            var pawns = _owner.lord?.ownedPawns;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; ++i)
                {
                    if (IsPlantingEmberSeed(pawns[i].CurJob)) { AddEmber(pawns[i].CurJob.targetB.Cell); }
                }
            }
            return _emberCounts;
        }

        private void AddEmber(IntVec3 cell)
        {
            var room = cell.GetRoom(Map);
            if (room != null && Territory.Rooms.Contains(room))
            {
                _emberCounts[room] = EmberCount(_emberCounts, room) + 1;
            }
        }

        private static int EmberCount(Dictionary<Room, int> counts, Room room) => counts.TryGetValue(room, out var count) ? count : 0;

        private static bool IsPlantingEmberSeed(Job job)
        {
            return job?.def == VVJobDefOf.VV_PlantArcaneSeed && JobGiver_ViviNestPlantSeed.IsPotPlant(job.targetA.Thing?.TryGetComp<CompArcaneSeed>()?.Props.targetPlantDef);
        }

        // 둥지 방 밖에 선 둥지 잔불꽃 중 아직 옮기기로 하지 않은 것(다시 설치 청사진이 없는 것)
        private List<ArcanePlant> OutdoorEmbersToMove()
        {
            var map = Map;
            _tmpOutdoorEmbers.Clear();
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def != VVThingDefOf.VV_EmberBloom || building.Faction != Faction || !(building is ArcanePlant plant)) { continue; }
                if (Territory.IsNestRoomCell(plant.Position) || map.listerBuildings.TryGetReinstallBlueprint(plant, out _)) { continue; }

                _tmpOutdoorEmbers.Add(plant);
            }
            return _tmpOutdoorEmbers;
        }

        // 아직 자리를 받지 않은 둥지 잔불꽃 수: 둥지 몫 잔불꽃 씨앗(심으러 가는 것 제외), 설치 청사진이 없는 둥지 몫 포장 잔불꽃, 옮기지 않은 바깥 둥지 잔불꽃
        private int EmbersToPlace()
        {
            var map = Map;
            var count = 0;
            var seedDef = EmberSeedDef;
            if (seedDef != null)
            {
                foreach (var seed in map.listerThings.ThingsOfDef(seedDef))
                {
                    if (_owner.Stock.IsNestOwned(seed)) { count += seed.stackCount; }
                }

                var pawns = _owner.lord?.ownedPawns;
                if (pawns != null)
                {
                    for (int i = 0; i < pawns.Count; ++i)
                    {
                        if (IsPlantingEmberSeed(pawns[i].CurJob) && pawns[i].CurJob.targetA.Thing.Spawned) { count--; }
                    }
                }
            }

            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
            {
                if (thing is MinifiedThing mini && mini.InnerThing?.def == VVThingDefOf.VV_EmberBloom && _owner.Stock.IsNestOwned(mini)
                    && InstallBlueprintUtility.ExistingBlueprintFor(mini) == null)
                {
                    count++;
                }
            }

            return count + OutdoorEmbersToMove().Count;
        }

        // 둘 잔불꽃이 받아 줄 빈 화분보다 많으면 밀랍 화분을 하나 짓는다(초기 설비의 첫 화분과 같다). 잔불꽃이 없는 방부터: 그 방들의 빈 화분이 받고 남는
        // 잔불꽃이 있으면 빈 화분이 없는 잔불꽃 없는 방(추운 방부터)에 짓는다. 그래도 남으면 잔불꽃·화분이 상한보다 적은 다른 방(추운 방부터)에 짓는다.
        // 둘 잔불꽃이 없거나 모든 방이 상한이면 짓지 않는다(씨앗·포장·바깥 잔불꽃으로 남는다). 벽 쪽 칸이 없어 못 지은 잔불꽃 없는 방은 기억해 두고
        // 그동안은 다른 방의 빈 화분을 막지 않는다(FindEmberPotCell).
        private bool TryPlanEmberPot()
        {
            var waiting = EmbersToPlace();
            if (waiting <= 0) { return false; }

            var counts = EmberCounts();
            foreach (var emberless in new[] { true, false })
            {
                _tmpPotRooms.Clear();
                foreach (var room in Territory.Rooms)
                {
                    var cap = MaxEmberPots(room);
                    var embers = EmberCount(counts, room);
                    if (embers >= cap || (embers == 0) != emberless) { continue; }

                    var pots = CountNestPots(room, out var freePots);
                    waiting -= System.Math.Min(freePots, cap - embers);
                    if (pots < cap && freePots == 0) { _tmpPotRooms.Add(room); }
                }
                if (waiting <= 0) { return false; }

                _tmpPotRooms.SortBy(r => r.Temperature);
                foreach (var room in _tmpPotRooms)
                {
                    foreach (var cell in WallSideRoomCells(room))
                    {
                        if (TryPlace(VVThingDefOf.VV_ArcanePlantPot, cell, Rot4.North, VVThingDefOf.VV_Viviwax) != null)
                        {
                            _noPotRooms.Remove(room);
                            return true;
                        }
                    }
                    if (emberless) { _noPotRooms.Add(room); }
                }
            }
            return false;
        }

        // 그 방의 둥지 화분 수와 그중 빈 화분(식물·새싹이 없는 것) 수
        private int CountNestPots(Room room, out int free)
        {
            var map = Map;
            var count = 0;
            free = 0;
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!(building is ArcanePlantPot pot) || pot.Faction != Faction || pot.Position.GetRoom(map) != room) { continue; }

                count++;
                if (pot.Position.GetFirstThing<ArcanePlant>(map) == null) { free++; }
            }
            return count;
        }

        // 바깥 마법식물(풍년화 등)을 둘 칸: 바깥 씨앗 자리(JobGiver_ViviNestPlantSeed)처럼 둥지 반경 12칸 안 바깥 빈 땅 중 비옥도가 가장 높은 칸
        // (같으면 환경 마나가 많은 칸, 비옥도 0.7 미만 제외). 다 자란 식물은 새싹처럼 제 칸 마나를 하루 80씩 받아야 자라는 것이 아니라 마나 하한은 두지 않는다.
        // 풍년화는 흙 비옥도로 마나를 만들고(비옥도 1 → 하루 150, 1.4 → 1050, 소모 300, 남는 마나는 둘레에 내놓고 제 칸 흙도 차츰 기름지게 한다)
        // 꿀이 많이 나는 꽃이라 둥지가 거둘 수 있게 둥지 채집 범위 안 칸만 쓴다. 설치하면 그 칸 식물이 지워지므로 꿀 꽃이 자라는 칸은 피한다.
        private IntVec3 FindOutdoorInstallCell(ThingDef plantDef)
        {
            var map = Map;
            var mana = map.GetManaComponent();
            var production = _owner.Production;
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
                if (!production.InGatherRange(cell)) { continue; }
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

        // 로열의 알 게이지가 다 찼는데 알을 낳을 부화기가 없으면 방 안에 부화기를 짓는다(방 크기에 맞춰 개수 제한, 알을 낳아도 될 때만).
        // 알은 19~39°C(가운데에 가까울수록 빨리 깬다)를 벗어나면 상하므로 기온이 그 가운데에 가까운 방(잔불꽃이 데우는 방)부터 고른다.
        private bool TryPlanHatchery()
        {
            if (!_eggAllowed || !AnyRoyalReadyToLay()) { return false; }

            var hatcheries = NestHatcheries();
            if (hatcheries.Any(h => h.CanLayHere)) { return false; }

            var roomCells = Territory.Rooms.Sum(r => r.CellCount);
            if (hatcheries.Count >= System.Math.Min(MaxHatcheries, roomCells / CellsPerHatchery)) { return false; }

            var ideal = EggIdealTemperature();
            foreach (var room in Territory.Rooms.OrderBy(r => System.Math.Abs(r.Temperature - ideal)))
            {
                foreach (var cell in WallSideRoomCells(room))
                {
                    if (TryPlace(VVThingDefOf.VV_ViviHatchery, cell, Rot4.South, null) != null) { return true; }
                }
            }
            return false;
        }

        // 부화기(아기 침대)는 알과 아기 몫이라 아기가 아닌 소유자(어린이가 된 아기)는 풀어 준다(어린이는 둥지 방 바닥에서 잔다, LordJob_ViviNest.FindBedFor).
        // 바닐라는 생애 단계의 몸 크기 계수가 침대 한도를 넘는 생일에만 소유를 푸는데, 비비 아기가 어린이가 되는 3살 생일에는 생애 단계가 바뀌기 전에 판정해
        // 풀리지 않았다(계측 probe_growth: 어린이가 된 뒤에도 소유, 다음 생일까지 약 5일).
        private void ReleaseHatcheryOwners()
        {
            foreach (var hatchery in NestHatcheries())
            {
                var owners = hatchery.OwnersForReading;
                for (int i = owners.Count - 1; i >= 0; --i)
                {
                    if (!owners[i].DevelopmentalStage.Baby()) { owners[i].ownership?.UnclaimBed(); }
                }
            }
        }

        private List<ViviEggHatchery> NestHatcheries()
        {
            return Map.listerBuildings.allBuildingsNonColonist.OfType<ViviEggHatchery>().Where(h => h.Faction == Faction).ToList();
        }

        // 알이 상하지 않는 기온 범위(기존 알 정의)의 가운데
        private static float EggIdealTemperature()
        {
            var props = VVThingDefOf.VV_ViviEgg.GetCompProperties<CompProperties_TemperatureRuinable>();
            return props != null ? (props.minSafeTemperature + props.maxSafeTemperature) / 2f : 29f;
        }

        // 앞으로 늘 구성원까지 센 수: 지금 구성원 + 둥지 부화기의 알 + 알 게이지가 찬 로열이 곧 낳을 알(낳아도 될 때만)
        public int ExpectedMembers() => PendingMembers() + (_eggAllowed && AnyRoyalReadyToLay() ? 1 : 0);

        // 지금 구성원 + 둥지 부화기의 알
        private int PendingMembers()
        {
            var count = _owner.lord?.ownedPawns.Count ?? 0;
            foreach (var hatchery in NestHatcheries())
            {
                if (hatchery.ViviEgg != null) { count++; }
            }
            return count;
        }

        // 로열이 알을 낳아도 되는지(JobGiver_ViviNestLayEgg): 꿀 꽃이 자라는 철이고, 둥지 식량이 MinFoodDaysForEgg 이상이며, 새 알까지 센 구성원이
        // 방 수용량 안이거나 증축할 자리가 있을 것(마지막 증축 자리 찾기가 실패하지 않았을 것). 증축 자리가 없으면 수용량을 넘겨 낳지 않는다.
        private bool ComputeEggAllowed()
        {
            if (!FlowerSeason(Map) || _owner.Production.FoodReserveDays() < MinFoodDaysForEgg) { return false; }

            return (PendingMembers() + 1) * RoomCellsPerMember <= Territory.Rooms.Sum(r => r.CellCount) || !_noExpansionSite;
        }

        // 꿀 꽃이 자라는 철: 계절 기온(바닐라 SeasonalTemp, 하루 기온 출렁임을 뺀 값)이 꿀 꽃(민들레)이 자라는 기온 범위 안.
        // 바닐라 성장 계절 판정(PlantUtility.GrowthSeasonNow)은 지금 바깥 기온을 봐서 봄·가을 밤마다 바뀌므로 계절 기온으로 본다.
        public static bool FlowerSeason(Map map)
        {
            var plant = ThingDefOf.Plant_Dandelion.plant;
            var seasonal = map.mapTemperature.SeasonalTemp;
            return seasonal > plant.minGrowthTemperature && seasonal < plant.maxGrowthTemperature;
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

        // 한가한 어른: 산책·낮잠·빈둥거리기 중인 구성원(편자 막대가 없을 때 놀이 묶음은 낮잠·빈둥거리기만 고른다)
        private int IdleMemberCount()
        {
            var count = 0;
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return 0; }

            foreach (var pawn in pawns)
            {
                var job = pawn.CurJobDef;
                if (pawn.DevelopmentalStage.Adult() && (job == JobDefOf.GotoWander || job == JobDefOf.Wait_Wander || job == VVJobDefOf.VV_ViviNestNap || job == VVJobDefOf.VV_ViviNestLoaf))
                {
                    count++;
                }
            }
            return count;
        }
        #endregion

        #region 증축
        // 앞으로 늘 구성원(부화기의 알·곧 낳을 알 포함, ExpectedMembers)에 비해 방이 좁으면 둥지 가까운 빈터에 육각 방(안 되면 사각 방)을 크림 벽·밀랍 문으로 짓는다.
        // 깰 아기까지 세어 미리 짓는다: 예전에는 지금 구성원만 세어 방 하나(36칸)에 6명이면 짓지 않았는데, 그때 하나뿐인 부화기를 아기가 쓰고 있어
        // 7번째가 깨지 못했다(장기 계측: 온대 60일 내내 6명, 다른 계측 6→7에 30일).
        // 채굴이 필요한 자리(바위·광맥)와 산 지붕 아래는 후보에서 뺀다. 지붕은 방이 닫히면 둥지 지붕 잡이 얹는다.
        private bool TryPlanExpansion()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null || pawns.Count == 0) { return false; }
            if (Territory.Rooms.Sum(r => r.CellCount) >= ExpectedMembers() * RoomCellsPerMember) { return false; }

            _noExpansionSite = !TryFindExpansionSite(out var walls, out var interior, out var door);
            if (_noExpansionSite) { return false; }

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
            foreach (var room in Territory.Rooms)
            {
                foreach (var cell in WallSideRoomCells(room))
                {
                    yield return cell;
                }
            }
        }

        private IEnumerable<IntVec3> WallSideRoomCells(Room room)
        {
            var map = Map;
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

        // 문 밖 가까운 빈 땅(바깥 보관 칸·통로 제외)
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
            var workSpots = _owner.Production.WorkSpots;
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

            MarkConstructionsDirty();
            return GenConstruct.PlaceBlueprintForBuild(def, cell, map, rot, Faction, stuff);
        }

        private void MarkConstructionsDirty()
        {
            _resourceNeedTick = -99999;
            _constructionCacheTick = -99999;
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
