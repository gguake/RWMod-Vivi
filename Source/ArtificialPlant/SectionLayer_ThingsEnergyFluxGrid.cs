using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class SectionLayer_ThingsEnergyFluxGrid : SectionLayer_Things
    {
        private static int lastPowerGridDrawFrame;

        public SectionLayer_ThingsEnergyFluxGrid(Section section) 
            : base(section)
        {
            requireAddToMapMesh = false;
            relevantChangeTypes = (MapMeshFlag)0x2000;
        }

        public static bool ShouldDrawGrid => lastPowerGridDrawFrame + 1 >= Time.frameCount;

        public static void DrawEnergyFluxGridOverlayThisFrame()
        {
            lastPowerGridDrawFrame = Time.frameCount;
        }

        public override void DrawLayer()
        {
            if (ShouldDrawGrid)
            {
                base.DrawLayer();
            }
        }

        protected override void TakePrintFrom(Thing thing)
        {
            if ((thing.Faction == null || thing.Faction == Faction.OfPlayer) && thing is EnergyAcceptor energyAcceptor)
            {
                energyAcceptor.PrintForEnergyGrid(this);
            }
        }
    }
}
