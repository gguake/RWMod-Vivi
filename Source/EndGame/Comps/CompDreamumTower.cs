using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_DreamumTower : CompProperties
    {
        public GraphicData graphicData;

        public CompProperties_DreamumTower()
        {
            compClass = typeof(CompDreamumTower);
        }
    }

    public class CompDreamumTower : ThingComp
    {
        private CompProperties_DreamumTower Props => (CompProperties_DreamumTower)props;

        public override void PostDraw()
        {
            base.PostDraw();

            //if (parent is Building_DreamumAltar altar && altar.Stage < DreamumProjectStage.InProgress)
            //{
            //    return;
            //}

            var mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
            var drawPos = parent.DrawPos;
            drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            var mat = Props.graphicData.Graphic.MatAt(parent.Rotation, parent);
            Graphics.DrawMesh(
                mesh,
                drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation),
                Quaternion.identity,
                mat,
                layer: 0);
        }
    }
}
