using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class EnergyAcceptorOverlayMats
    {
        public static readonly Graphic LinkedOverlayGraphic;

        public static readonly Material MatWire;
        public static readonly Material MatConnectorBase;
        public static readonly Material MatConnectorLine;


        static EnergyAcceptorOverlayMats()
        {
            Graphic graphic = GraphicDatabase.Get<Graphic_Single>("Things/Special/VV_EnergyTube_OverlayAtlas", ShaderDatabase.MetaOverlay);
            LinkedOverlayGraphic = GraphicUtility.WrapLinked(graphic, LinkDrawerType.Basic);

            MatWire = MaterialPool.MatFrom("Things/Special/VV_EnergyTubeWire");
            MatConnectorBase = MaterialPool.MatFrom("Things/Special/VV_EnergyOverlayBase", ShaderDatabase.MetaOverlay);
            MatConnectorLine = MaterialPool.MatFrom("Things/Special/VV_EnergyOverlayLine", ShaderDatabase.MetaOverlay);
        }
    }

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

        protected void PrintOverlayConnectorBaseFor(SectionLayer layer)
        {
            var center = this.TrueCenter();
            center.y = AltitudeLayer.MapDataOverlay.AltitudeFor();

            Printer_Plane.PrintPlane(layer, center, new Vector2(1f, 1f), EnergyAcceptorOverlayMats.MatConnectorBase);
        }

        protected void PrintEnergyWirePieceConnecting(SectionLayer layer, Thing A, bool forOverlay)
        {
            var mat = EnergyAcceptorOverlayMats.MatWire;
            var y = AltitudeLayer.SmallWire.AltitudeFor();
            if (forOverlay)
            {
                mat = EnergyAcceptorOverlayMats.MatConnectorLine;
                y = AltitudeLayer.MapDataOverlay.AltitudeFor();
            }

            var center = (this.TrueCenter() + A.TrueCenter()) / 2f;
            center.y = y;

            var v = A.TrueCenter() - this.TrueCenter();
            Printer_Plane.PrintPlane(layer, center, new Vector2(1f, v.MagnitudeHorizontal()), mat, v.AngleFlat());
        }
    }

}
