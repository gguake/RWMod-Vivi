using UnityEngine;
using Verse;

namespace VVRace
{
    public class Building_RuinSpatialBarrier : Building
    {
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Comps_PostDraw();
        }
    }
}
