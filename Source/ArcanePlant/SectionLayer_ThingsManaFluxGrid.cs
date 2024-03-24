using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class SectionLayer_ThingsManaFluxGrid : SectionLayer_Things
    {
        private static int lastPowerGridDrawFrame;

        public SectionLayer_ThingsManaFluxGrid(Section section) 
            : base(section)
        {
            requireAddToMapMesh = false;
            relevantChangeTypes = VVMapMeshFlagDefOf.VV_ManaFluxGrid;
        }

        public static void DrawManaFluxGridOverlayThisFrame()
        {

            lastPowerGridDrawFrame = Time.frameCount;
        }

        public override void DrawLayer()
        {
            if (lastPowerGridDrawFrame + 1 >= Time.frameCount)
            {
                base.DrawLayer();
            }
        }

        protected override void TakePrintFrom(Thing thing)
        {
            if ((thing.Faction == null || thing.Faction == Faction.OfPlayer) && thing is ManaAcceptor acceptor)
            {
                acceptor.PrintForManaFluxGrid(this);
            }
        }
    }
}
