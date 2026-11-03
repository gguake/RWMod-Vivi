using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Building_EverflowerAltar : ArcanePlantPot, IThingHolder, ISuspendableThingHolder
    {
        public const int RequiredAttunementLevel = 4;

        // 자동 생성되는 Frame_VV_EverflowerAltar는 제단의 building 설정을 상속하지 않는다.
        // 틀이 파괴되면 KillFinalize가 재료의 25%만 남기고 forcedCostLeavings도 무시해 코어를 잃을 수 있다.
        static Building_EverflowerAltar()
        {
            var frameBuilding = VVThingDefOf.VV_EverflowerAltar?.frameDef?.building;
            if (frameBuilding != null)
            {
                frameBuilding.canBeDamagedByAttacks = false;
                frameBuilding.ai_neverTrashThis = true;
            }
        }

        private ThingOwner<Pawn> _resonatorHolder;

        public Building_EverflowerAltar()
        {
            _resonatorHolder = new ThingOwner<Pawn>(this, oneStackOnly: true);
        }

        public Pawn HeldResonator => _resonatorHolder.Count > 0 ? _resonatorHolder[0] : null;

        public bool IsContentsSuspended => true;

        public ThingOwner GetDirectlyHeldThings() => _resonatorHolder;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        // 공명자를 보관해도 의식 로드에는 남겨 만개 의식이 끝까지 진행되게 한다.
        public bool HoldResonator(Pawn pawn)
        {
            if (pawn == null || HeldResonator != null) { return false; }

            if (pawn.Spawned)
            {
                pawn.DeSpawnOrDeselect();
            }

            return _resonatorHolder.TryAdd(pawn);
        }

        public void EjectResonator(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || !_resonatorHolder.Contains(pawn) || !Spawned) { return; }

            var target = cell.IsValid && cell.InBounds(Map) ? cell : FrontCell;
            if (_resonatorHolder.TryDrop(pawn, target, Map, ThingPlaceMode.Near, out var dropped) && dropped != null)
            {
                dropped.Rotation = Rot4.South;
            }
        }

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
        }

        public ArcanePlant_Everflower EnshrinedEverflower => Spawned ? Position.GetFirstThing<ArcanePlant_Everflower>(Map) : null;

        public IntVec3 FrontCell
        {
            get
            {
                if (!Spawned) { return Position; }

                var map = Map;
                var rect = this.OccupiedRect();

                var origin = new IntVec3(Position.x, 0, rect.minZ);
                var traverseParms = TraverseParms.For(TraverseMode.PassDoors);
                bool Reachable(IntVec3 c) => c.InBounds(map) && map.reachability.CanReach(origin, c, PathEndMode.OnCell, traverseParms);

                var front = new IntVec3(Position.x, 0, rect.minZ - 1);
                var cell = CellFinder.StandableCellNear(front, map, 4f, c => !rect.Contains(c) && Reachable(c));
                if (!cell.IsValid)
                {
                    cell = CellFinder.StandableCellNear(Position, map, 6f, Reachable);
                }

                return cell.IsValid ? cell : Position;
            }
        }

        public static bool IsValidAltarMap(Map map)
        {
            if (map == null || !map.IsPlayerHome || map.IsPocketMap) { return false; }

            return map.Parent == null || !map.IsTempIncidentMap;
        }

        // 중력선 기반 위의 제단은 이륙 시 함께 실려 가므로 해당 칸에는 배치할 수 없다.
        public static bool IsOnSubstructure(IntVec3 cell, Map map)
        {
            return map.terrainGrid.FoundationAt(cell)?.IsSubstructure == true;
        }

        public AcceptanceReport LocationReport
        {
            get
            {
                if (!Spawned) { return false; }
                if (!IsValidAltarMap(Map)) { return LocalizeString_Etc.VV_FailReason_AltarNotHomeMap.Translate(); }

                foreach (var cell in this.OccupiedRect())
                {
                    if (IsOnSubstructure(cell, Map))
                    {
                        return LocalizeString_Etc.VV_FailReason_AltarOnSubstructure.Translate();
                    }
                }

                return true;
            }
        }

        public static Building_EverflowerAltar FindOnMap(Map map)
        {
            if (map == null || VVThingDefOf.VV_EverflowerAltar == null) { return null; }

            var altars = map.listerThings.ThingsOfDef(VVThingDefOf.VV_EverflowerAltar);
            for (int i = 0; i < altars.Count; ++i)
            {
                if (altars[i] is Building_EverflowerAltar altar && altar.Spawned)
                {
                    return altar;
                }
            }

            return null;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 불러올 때 영원꽃이 제단보다 먼저 스폰되므로 제단에서도 꽃의 안치 캐시를 알림 없이 갱신한다.
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
                    return new AcceptanceReport(LocalizeString_Etc.VV_FailReason_AltarEndingInProgress.Translate());
                }

                return new AcceptanceReport(LocalizeString_Etc.VV_FailReason_AltarHasEnshrinedEverflower.Translate());
            }

            return base.DeconstructibleBy(faction);
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            var everflower = EnshrinedEverflower;
            if (everflower != null)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_AltarEnshrinedEverflower.Translate(everflower.AttunementLevel.Named("LEVEL")));
            }

            var resonator = HeldResonator;
            if (resonator != null)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_AltarHeldResonator.Translate(resonator.Named("PAWN")));
            }
            else if (everflower == null)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_AltarNoEverflower.Translate(RequiredAttunementLevel.Named("LEVEL")));
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            foreach (var gizmo in GetEnshrinedEverflowerGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode && Spawned && EnshrinedEverflower == null)
            {
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

        // 중앙 한 칸의 영원꽃을 고르기 어려워 제단에서도 꽃의 기즈모를 표시한다.
        private IEnumerable<Gizmo> GetEnshrinedEverflowerGizmos()
        {
            var everflower = EnshrinedEverflower;
            if (everflower == null || !everflower.Spawned || Find.Selector.IsSelected(everflower)) { yield break; }

            foreach (var gizmo in everflower.GetGizmos())
            {
                if (gizmo is Designator) { continue; }
                if (gizmo is Command command && (command.hotKey == KeyBindingDefOf.Designator_Deconstruct || command.hotKey == KeyBindingDefOf.Command_ItemForbid)) { continue; }

                yield return gizmo;
            }
        }
    }
}
