using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 영원꽃 제단. ArcanePlantPot을 상속해 중앙 칸(Position)에만 영원꽃을 안치할 수 있다(5x5, Standable).
    // [계약] public 시그니처는 다른 모듈이 호출하므로 바꾸지 말 것.
    // 화분을 상속하므로 식물 대신 피해를 받고, 제단이 사라지면 영원꽃을 순간이동시키며, 식물과 제단이 서로를 제거하지 않도록 하는 처리를 그대로 이어받는다.
    // 〈영원꽃 완전 동화〉 중인 대조율 공명자를 동면관처럼 안에 보관한다(보관된 폰은 정지되고, 모습은 영원꽃이 공중에 그린다).
    [StaticConstructorOnStartup]
    public class Building_DreamumAltar : ArcanePlantPot, IThingHolder, ISuspendableThingHolder
    {
        // 제단에 안치하려면 영원꽃 조율이 이 단계 이상이어야 한다.
        public const int RequiredAttunementLevel = 4;

        // 바닐라가 자동으로 만드는 건설 틀 def(Frame_VV_DreamumAltar)는 제단 def의 building 설정을 물려받지 않는다.
        // 틀이 공격으로 부서지면(KillFinalize) 바닐라는 넣어 둔 재료의 25%만 남기고 forcedCostLeavings도 보지 않아 코어를 잃을 수 있으므로,
        // 틀도 완성된 제단처럼 공격으로 부서지지 않고 습격자가 노리지 않게 한다(틀 def는 정의를 모두 불러온 뒤에 생기므로 여기서 고친다).
        static Building_DreamumAltar()
        {
            var frameBuilding = AltarDefOf.VV_DreamumAltar?.frameDef?.building;
            if (frameBuilding != null)
            {
                frameBuilding.canBeDamagedByAttacks = false;
                frameBuilding.ai_neverTrashThis = true;
            }
        }

        // 〈영원꽃 완전 동화〉 중인 공명자 보관함. 동면관처럼 내용물이 정지된다(욕구·나이가 멈추고 피해·지시를 받지 않음).
        private ThingOwner<Pawn> _resonatorHolder;

        public Building_DreamumAltar()
        {
            _resonatorHolder = new ThingOwner<Pawn>(this, oneStackOnly: true);
        }

        // 보관 중인 공명자(없으면 null).
        public Pawn HeldResonator => _resonatorHolder.Count > 0 ? _resonatorHolder[0] : null;

        public bool IsContentsSuspended => true;

        public ThingOwner GetDirectlyHeldThings() => _resonatorHolder;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        // 공명자를 맵에서 꺼내 보관한다. 의식 로드에는 그대로 남는다(대조율 의식은 공명자가 보관된 채 끝까지 진행된다).
        public bool HoldResonator(Pawn pawn)
        {
            if (pawn == null || HeldResonator != null) { return false; }

            if (pawn.Spawned)
            {
                pawn.DeSpawnOrDeselect();
            }

            return _resonatorHolder.TryAdd(pawn);
        }

        // 보관한 공명자를 cell(영원꽃 앞 칸) 근처로 내보낸다.
        public void EjectResonator(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || !_resonatorHolder.Contains(pawn) || !Spawned) { return; }

            var target = cell.IsValid && cell.InBounds(Map) ? cell : FrontCell;
            if (_resonatorHolder.TryDrop(pawn, target, Map, ThingPlaceMode.Near, out var dropped) && dropped != null)
            {
                dropped.Rotation = Rot4.South;
            }
        }

        // 제단이 사라지면 보관 중인 공명자를 밖으로 내보낸다(동화는 창구 틱에서 풀린다).
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (_resonatorHolder.Any && Spawned)
            {
                _resonatorHolder.TryDropAll(Position + IntVec3.South, Map, ThingPlaceMode.Near);
            }

            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref _resonatorHolder, "resonatorHolder", this);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && _resonatorHolder == null)
            {
                _resonatorHolder = new ThingOwner<Pawn>(this, oneStackOnly: true);
            }
        }

        // 중앙 칸에 안치된 영원꽃(없으면 null). thingGrid로 찾는다.
        public ArcanePlant_Everflower EnshrinedEverflower => Spawned ? Position.GetFirstThing<ArcanePlant_Everflower>(Map) : null;

        // 제단 바로 앞(남쪽)에서 서 있을 수 있는 칸. 요정 간섭 비비가 귀환하는 위치 등에 쓴다.
        public IntVec3 FrontCell
        {
            get
            {
                if (!Spawned) { return Position; }

                var map = Map;
                var rect = this.OccupiedRect();

                // 벽 너머 칸이 뽑히지 않도록 제단 발판(올라설 수 있다)의 남쪽 가장자리에서 걸어서 갈 수 있는 칸만 고른다.
                var origin = new IntVec3(Position.x, 0, rect.minZ);
                var traverseParms = TraverseParms.For(TraverseMode.PassDoors);
                bool Reachable(IntVec3 c) => c.InBounds(map) && map.reachability.CanReach(origin, c, PathEndMode.OnCell, traverseParms);

                // 발판 남쪽 가장자리 바로 아래부터 가까운 칸을 찾되, 가능하면 발판 밖을 고른다.
                var front = new IntVec3(Position.x, 0, rect.minZ - 1);
                var cell = CellFinder.StandableCellNear(front, map, 4f, c => !rect.Contains(c) && Reachable(c));
                if (!cell.IsValid)
                {
                    cell = CellFinder.StandableCellNear(Position, map, 6f, Reachable);
                }

                return cell.IsValid ? cell : Position;
            }
        }

        // 제단을 지을 수 있고 엔딩을 치를 수 있는 지역: 플레이어 정착지. 야영지·임시 사건 지역·포켓 지역은 뺀다.
        public static bool IsValidAltarMap(Map map)
        {
            if (map == null || !map.IsPlayerHome || map.IsPocketMap) { return false; }

            return map.Parent == null || !map.IsTempIncidentMap;
        }

        // 중력선 기반(오디세이 서브스트럭처) 위의 칸인지. 중력선이 떠나면 함께 실려 가므로 제단을 둘 수 없다.
        public static bool IsOnSubstructure(IntVec3 cell, Map map)
        {
            return map.terrainGrid.FoundationAt(cell)?.IsSubstructure == true;
        }

        // 이 제단이 엔딩을 치를 수 있는 자리에 있는지(배치 규칙이 생기기 전의 세이브에서 중력선 위·임시 지역에 지은 제단은 쓸 수 없다).
        public AcceptanceReport LocationReport
        {
            get
            {
                if (!Spawned) { return false; }
                if (!IsValidAltarMap(Map)) { return LocalizeString_EndingAltar.VV_FailReason_AltarNotHomeMap.Translate(); }

                foreach (var cell in this.OccupiedRect())
                {
                    if (IsOnSubstructure(cell, Map))
                    {
                        return LocalizeString_EndingAltar.VV_FailReason_AltarOnSubstructure.Translate();
                    }
                }

                return true;
            }
        }

        public static Building_DreamumAltar FindOnMap(Map map)
        {
            if (map == null || AltarDefOf.VV_DreamumAltar == null) { return null; }

            var altars = map.listerThings.ThingsOfDef(AltarDefOf.VV_DreamumAltar);
            for (int i = 0; i < altars.Count; ++i)
            {
                if (altars[i] is Building_DreamumAltar altar && altar.Spawned)
                {
                    return altar;
                }
            }

            return null;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 불러올 때는 1x1 영원꽃이 5x5 제단보다 먼저 스폰되므로, 제단 쪽에서 중앙 영원꽃의 안치 캐시를 알림 없이 다시 갱신한다.
            // (발판에 마법 식물이 있으면 제단을 지을 수 없으므로 새로 완공될 때 중앙에 영원꽃이 있는 경우는 없다. 안치는 순간이동으로만 한다)
            EnshrinedEverflower?.EverflowerComp.RefreshEnshrinement(silent: true);
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction)
        {
            var everflower = EnshrinedEverflower;
            if (everflower != null)
            {
                var quest = ViviEndingQuest.Current;
                if (quest != null && (quest.IsEndingActive || quest.IsCompleted) && quest.IsAnchor(everflower))
                {
                    return new AcceptanceReport(LocalizeString_EndingAltar.VV_FailReason_AltarEndingInProgress.Translate());
                }

                return new AcceptanceReport(LocalizeString_EndingAltar.VV_FailReason_AltarHasEnshrinedEverflower.Translate());
            }

            return base.DeconstructibleBy(faction);
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            var everflower = EnshrinedEverflower;
            if (everflower != null)
            {
                sb.AppendInNewLine(LocalizeString_EndingAltar.VV_Inspector_AltarEnshrinedEverflower.Translate(everflower.AttunementLevel.Named("LEVEL")));
            }

            var resonator = HeldResonator;
            if (resonator != null)
            {
                sb.AppendInNewLine(LocalizeString_EndingAltar.VV_Inspector_AltarHeldResonator.Translate(resonator.Named("PAWN")));
            }
            else
            {
                sb.AppendInNewLine(LocalizeString_EndingAltar.VV_Inspector_AltarNoEverflower.Translate(RequiredAttunementLevel.Named("LEVEL")));
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode && Spawned && EnshrinedEverflower == null)
            {
                // 자동 테스트용: 공명자의 순간이동 작업 없이 맵에서 단계가 가장 높은 영원꽃 하나를 바로 중앙에 안치한다.
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Enshrine everflower now",
                    action = () =>
                    {
                        var everflower = Map.listerThings.ThingsOfDef(VVThingDefOf.VV_Everflower)
                            .OfType<ArcanePlant_Everflower>()
                            .Where(v => v.Spawned && !v.EverflowerComp.IsEnshrined)
                            .OrderByDescending(v => v.AttunementLevel)
                            .FirstOrDefault();

                        everflower?.Teleport(Position);
                    }
                };
            }
        }
    }
}
