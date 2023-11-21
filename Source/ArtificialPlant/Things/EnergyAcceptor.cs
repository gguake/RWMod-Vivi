using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public static class EnergyAcceptorOverlayMats
    {
        public static readonly Graphic LinkedOverlayGraphic;

        static EnergyAcceptorOverlayMats()
        {
            Graphic graphic = GraphicDatabase.Get<Graphic_Single>("Things/Building/VV_EnergyTube_OverlayAtlas", ShaderDatabase.MetaOverlay);
            LinkedOverlayGraphic = GraphicUtility.WrapLinked(graphic, LinkDrawerType.Basic);
        }
    }

    [StaticConstructorOnStartup]
    public abstract class EnergyAcceptor : Building
    {
        public abstract EnergyFluxNetwork EnergyFluxNetwork { get; set; }
        public abstract EnergyFluxNetworkNode EnergyFluxNode { get; }

        protected EnergyFluxGrid EnergyFluxGrid { get; private set; }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            EnergyFluxGrid.Notify_SpawnEnergyAcceptor(this);
            EnergyFluxGrid = EnergyFluxGrid.GetFluxGrid(map);

            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlag.PowerGrid, true, false);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlag.PowerGrid, true, false);

            EnergyFluxGrid.Notify_DespawnEnergyAcceptor(this);
            EnergyFluxGrid = null;

            base.DeSpawn(mode);
        }

        public override void Tick()
        {
            if (Spawned && EnergyFluxGrid != null && EnergyFluxGrid.ShouldRefreshNetwork)
            {
                EnergyFluxGrid.RefreshNetworks();
            }

            base.Tick();
        }

        public override void TickRare()
        {
            if (Spawned && EnergyFluxGrid != null && EnergyFluxGrid.ShouldRefreshNetwork)
            {
                EnergyFluxGrid.RefreshNetworks();
            }

            base.TickRare();
        }

        public override void Print(SectionLayer layer)
        {
            base.Print(layer);
            PostPrint(layer);
        }

        public virtual void PostPrint(SectionLayer layer)
        {
        }

        public virtual void PrintForEnergyGrid(SectionLayer layer)
        {
        }

        private static readonly Material WireMat = MaterialPool.MatFrom("Things/Special/Power/Wire");
        public static void PrintEnergyWirePieceConnecting(SectionLayer layer, Thing A, Thing B, bool forOverlay)
        {
            var mat = WireMat;
            var y = AltitudeLayer.SmallWire.AltitudeFor();
            if (forOverlay)
            {
                mat = PowerOverlayMats.MatConnectorLine;
                y = AltitudeLayer.MapDataOverlay.AltitudeFor();
            }

            var center = (A.TrueCenter() + B.TrueCenter()) / 2f;
            center.y = y;

            var v = B.TrueCenter() - A.TrueCenter();
            Printer_Plane.PrintPlane(layer, center, new Vector2(1f, v.MagnitudeHorizontal()), mat, v.AngleFlat());
        }
    }

}
