using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Building_GatherWorkTable : Building_WorkTable
    {
        private static readonly Texture2D ToggleGatherPollensCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_TogglePollen");

        public const float GatherMaxRadius = 15.9f;
        public const float GatherMinRadius = 1.9f;
        public const float GatherDefaultRadius = 10.9f;

        public event Action<Building_GatherWorkTable> workTableGatherRadiusChanged;

        public float GatherRadius
        {
            get
            {
                return _gatherRadius;
            }
            set
            {
                value = Mathf.Clamp(value, GatherMinRadius, GatherMaxRadius);
                if (Spawned && value != _gatherRadius)
                {
                    workTableGatherRadiusChanged(this);
                }

                _gatherRadius = value;
            }
        }

        public bool CanGatherFilth => _canGatherFilth;

        public IntVec3 CenterCell => (def.hasInteractionCell ? InteractionCell : Position);

        private float _gatherRadius = GatherDefaultRadius;
        private bool _canGatherFilth = true;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _gatherRadius, "gatherRadius", GatherDefaultRadius);
            Scribe_Values.Look(ref _canGatherFilth, "canGatherFilth", _canGatherFilth);
        }

        // 맵별로 모든 채집 건물은 캐시를 공유한다
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            Map.GetComponent<GatheringMapComponent>().Notify_WorkTableSpawned(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var map = Map;
            base.DeSpawn(mode);

            map.GetComponent<GatheringMapComponent>().Notify_WorkTableDespawned(this);
        }

        private List<IntVec3> _tmpOverlayCells = new List<IntVec3>();
        public override void DrawExtraSelectionOverlays()
        {
            if (Spawned)
            {
                _tmpOverlayCells.Clear();
                _tmpOverlayCells.AddRange(Map.GetComponent<GatheringMapComponent>().GetGatherableCellsForWorkTable(this));
                GenDraw.DrawFieldEdges(_tmpOverlayCells);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Spawned && Faction == Faction.OfPlayerSilentFail)
            {
                var commandToggleGatherFilth = new Command_Toggle();
                commandToggleGatherFilth.defaultLabel = LocalizeString_Command.VV_Command_CanGatherPollens.Translate();
                commandToggleGatherFilth.defaultDesc = LocalizeString_Command.VV_Command_CanGatherPollensDesc.Translate();
                commandToggleGatherFilth.icon = ToggleGatherPollensCommandTex;
                commandToggleGatherFilth.isActive = () => _canGatherFilth;
                commandToggleGatherFilth.toggleAction = () => _canGatherFilth = !_canGatherFilth;

                yield return commandToggleGatherFilth;
            }
        }
    }
}
