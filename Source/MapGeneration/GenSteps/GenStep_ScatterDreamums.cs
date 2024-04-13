using RimWorld.BaseGen;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GenStep_ScatterDreamums : GenStep_Scatterer
    {
        public override int SeedPart => 2024041311;

        protected override bool CanScatterAt(IntVec3 c, Map map)
        {
            if (!base.CanScatterAt(c, map))
            {
                return false;
            }

            if (!c.Standable(map))
            {
                return false;
            }
            if (c.Roofed(map))
            {
                return false;
            }
            if (!map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)))
            {
                return false;
            }

            var cellRect = CellRect.CenteredOn(c, 7, 7);
            if (!cellRect.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z)))
            {
                return false;
            }

            foreach (var cell in cellRect)
            {
                if (!c.Standable(map) || cell.Roofed(map))
                {
                    return false;
                }
            }

            return true;
        }

        protected override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int count = 1)
        {
            var radius = Rand.Range(4, 7);
            var rect = CellRect.CenteredOn(c, radius, radius);
            rect.ClipInsideMap(map);

            var resolveParams = default(ResolveParams);
            resolveParams.rect = rect;
            resolveParams.faction = null;
            BaseGen.globalSettings.map = map;
            BaseGen.symbolStack.Push("vv_dreamum_garden", resolveParams);

            BaseGen.Generate();
        }
    }
}
