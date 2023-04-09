using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ViviHatcheryEggDrawer : CompProperties
    {
        public GraphicData graphicData;

        public CompProperties_ViviHatcheryEggDrawer()
        {
            compClass = typeof(CompViviHatcheryEggDrawer);
        }
    }

    [StaticConstructorOnStartup]
    public class CompViviHatcheryEggDrawer : ThingComp
    {
        private CompProperties_ViviHatcheryEggDrawer Props => (CompProperties_ViviHatcheryEggDrawer)props;

        public ViviEggHatchery Hatchery => parent as ViviEggHatchery;

        public override void PostDraw()
        {
            var hatchery = Hatchery;
            if (hatchery == null) { return; }

            var egg = hatchery.ViviEgg;
            if (egg == null) { return; }

            var drawPos = hatchery.DrawPos;

            drawPos += Altitudes.AltIncVect;
            egg.DrawAt(drawPos);

            drawPos += Altitudes.AltIncVect;
            var mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
            Graphics.DrawMesh(
                mesh, 
                drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation), 
                Quaternion.identity, 
                Props.graphicData.Graphic.MatAt(parent.Rotation), 
                0);
        }
    }
}
