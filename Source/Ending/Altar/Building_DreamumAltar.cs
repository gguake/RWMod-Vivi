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
    [StaticConstructorOnStartup]
    public class Building_DreamumAltar : ArcanePlantPot
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
