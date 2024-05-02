using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_FarmSeedlingDrawer : CompProperties
    {
        public GraphicData graphicData;

        public CompProperties_FarmSeedlingDrawer()
        {
            compClass = typeof(CompFarmSeedlingDrawer);
        }
    }

    [StaticConstructorOnStartup]
    public class CompFarmSeedlingDrawer : ThingComp
    {
        public CompProperties_FarmSeedlingDrawer Props => (CompProperties_FarmSeedlingDrawer)props;

        public override void PostDraw()
        {
            if (parent is Building_ArcanePlantFarm farm && farm.Bill?.Stage == GrowingArcanePlantBillStage.Growing)
            {
                var mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
                var mat = Props.graphicData.Graphic.MatAt(parent.Rotation, parent);

                var drawPos = parent.DrawPos.SetToAltitude(AltitudeLayer.BuildingOnTop);
                Graphics.DrawMesh(
                    mesh,
                    drawPos,
                    Quaternion.identity,
                    mat,
                    layer: 0);
            }
        }
    }
}
