using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_FarmSeedlingDrawer : CompProperties
    {
        public GraphicData graphicData;
        public IntRange seedlingRange;

        public CompProperties_FarmSeedlingDrawer()
        {
            compClass = typeof(CompFarmSeedlingDrawer);
        }
    }

    [StaticConstructorOnStartup]
    public class CompFarmSeedlingDrawer : ThingComp
    {
        public CompProperties_FarmSeedlingDrawer Props => (CompProperties_FarmSeedlingDrawer)props;

        private static List<int> _tmpIndices = Enumerable.Range(0, 16).ToList();
        private List<Vector3> _seedlingVisualCoordinates = new List<Vector3>();

        public void RefreshSeedlingVisualCoordinates()
        {
            _seedlingVisualCoordinates.Clear();

            var parentRect = parent.OccupiedRect();
            var count = Props.seedlingRange.RandomInRange;
            var randoms = _tmpIndices.TakeRandomDistinct(count);

            for (int i = 0; i < count; ++i)
            {
                var index = randoms[i];
                var x = ((index % 4) + Rand.Range(0.35f, 0.65f)) / 4f * parentRect.Width;
                var z = ((index / 4) + Rand.Range(0.35f, 0.65f)) / 4f * parentRect.Width;
                _seedlingVisualCoordinates.Add(new Vector3(x, 0, z));
            }
        }

        public override void PostExposeData()
        {
            Scribe_Collections.Look(ref _seedlingVisualCoordinates, "seedlingVisualCoordinates", LookMode.Value);
        }

        public override void PostDraw()
        {
            if (parent is Building_ArcanePlantFarm farm && farm.Bill?.Stage == GrowingArcanePlantBillStage.Growing)
            {
                var mesh = Props.graphicData.Graphic.MeshAt(parent.Rotation);
                var mat = Props.graphicData.Graphic.MatAt(parent.Rotation, parent);

                foreach (var coordinate in _seedlingVisualCoordinates)
                {
                    var rect = parent.OccupiedRect();
                    var drawPos = new Vector3(rect.minX + coordinate.x, AltitudeLayer.BuildingBelowTop.AltitudeFor(), rect.minZ + coordinate.z);

                    Graphics.DrawMesh(
                        mesh,
                        drawPos + Props.graphicData.drawOffset.RotatedBy(parent.Rotation),
                        Quaternion.identity,
                        mat,
                        layer: 0);
                }
            }
        }

    }
}
