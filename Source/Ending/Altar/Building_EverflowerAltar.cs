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
            var map = Map;
            var everflower = EnshrinedEverflower;
            var wasEnshrined = everflower != null && everflower.EverflowerComp.IsEnshrined;

            if (_resonatorHolder.Any && Spawned)
            {
                _resonatorHolder.TryDropAll(Position + IntVec3.South, Map, ThingPlaceMode.Near);
            }

            base.DeSpawn(mode);

            if (everflower != null && everflower.Spawned && everflower.Map == map && map.edificeGrid[everflower.Position] != everflower)
            {
                map.edificeGrid.Register(everflower);
            }

            if (wasEnshrined)
            {
                ViviEndingQuest.Current?.Notify_AnchorAltarLost(everflower);
            }
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            var ending = ViviEndingQuest.Current;
            if (ending != null && ending.IsEndingActive && ending.IsAnchor(EnshrinedEverflower))
            {
                absorbed = true;
                return;
            }

            base.PreApplyDamage(ref dinfo, out absorbed);
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

        public static bool CanEnshrine(ArcanePlant_Everflower everflower)
        {
            return everflower.AttunementLevel >= RequiredAttunementLevel;
        }

        public static string CannotEnshrineReason => LocalizeString_Message.VV_Message_EverflowerAltarRequiresLevel.Translate(RequiredAttunementLevel.Named("LEVEL"));

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

            EnshrinedEverflower?.EverflowerComp.RefreshEnshrinement(silent: true);
        }

        public override AcceptanceReport DeconstructibleBy(Faction faction)
        {
            if (EnshrinedEverflower?.IsEnshrinementLocked == true)
            {
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
