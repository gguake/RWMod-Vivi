using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class ManaAcceptorOverlayMats
    {
        public static readonly Graphic LinkedOverlayGraphic;

        public static readonly Material MatWire;
        public static readonly Material MatConnectorBase;
        public static readonly Material MatConnectorLine;


        static ManaAcceptorOverlayMats()
        {
            Graphic graphic = GraphicDatabase.Get<Graphic_Single>("Things/Special/VV_ManaFluxTube_OverlayAtlas", ShaderDatabase.MetaOverlay);
            LinkedOverlayGraphic = GraphicUtility.WrapLinked(graphic, LinkDrawerType.Basic);

            MatWire = MaterialPool.MatFrom("Things/Special/VV_ManaFluxTubeWire");
            MatConnectorBase = MaterialPool.MatFrom("Things/Special/VV_ManaOverlayBase", ShaderDatabase.MetaOverlay);
            MatConnectorLine = MaterialPool.MatFrom("Things/Special/VV_ManaOverlayLine", ShaderDatabase.MetaOverlay);
        }
    }

    public abstract class ManaAcceptor : Building
    {
        public abstract ManaFluxNetwork ManaFluxNetwork { get; set; }
        public abstract ManaFluxNetworkNode ManaFluxNode { get; }

        protected ManaFluxGrid ManaFluxGrid { get; private set; }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            ManaFluxGrid.Notify_SpawnManaAcceptor(this);
            ManaFluxGrid = ManaFluxGrid.GetFluxGrid(map);

            Map.mapDrawer.MapMeshDirty(Position, VVMapMeshFlagDefOf.VV_ManaFluxGrid, true, false);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map.mapDrawer.MapMeshDirty(Position, VVMapMeshFlagDefOf.VV_ManaFluxGrid, true, false);

            ManaFluxGrid.Notify_DespawnManaAcceptor(this);
            ManaFluxGrid = null;

            base.DeSpawn(mode);
        }

        public override void Tick()
        {
            if (Spawned && ManaFluxGrid != null && ManaFluxGrid.ShouldRefreshNetwork)
            {
                ManaFluxGrid.RefreshNetworks();
            }

            base.Tick();
        }

        public override void TickRare()
        {
            if (Spawned && ManaFluxGrid != null && ManaFluxGrid.ShouldRefreshNetwork)
            {
                ManaFluxGrid.RefreshNetworks();
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

        public virtual void PrintForManaFluxGrid(SectionLayer layer)
        {
        }

        protected void PrintOverlayConnectorBaseFor(SectionLayer layer)
        {
            var center = this.TrueCenter();
            center.y = AltitudeLayer.MapDataOverlay.AltitudeFor();

            Printer_Plane.PrintPlane(layer, center, new Vector2(1f, 1f), ManaAcceptorOverlayMats.MatConnectorBase);
        }

        protected void PrintManaFluxWirePieceConnecting(SectionLayer layer, Thing A, bool forOverlay)
        {
            var mat = ManaAcceptorOverlayMats.MatWire;
            var y = AltitudeLayer.SmallWire.AltitudeFor();
            if (forOverlay)
            {
                mat = ManaAcceptorOverlayMats.MatConnectorLine;
                y = AltitudeLayer.MapDataOverlay.AltitudeFor();
            }

            var center = (this.TrueCenter() + A.TrueCenter()) / 2f;
            center.y = y;

            var v = A.TrueCenter() - this.TrueCenter();
            Printer_Plane.PrintPlane(layer, center, new Vector2(1f, v.MagnitudeHorizontal()), mat, v.AngleFlat());
        }
    }

}
