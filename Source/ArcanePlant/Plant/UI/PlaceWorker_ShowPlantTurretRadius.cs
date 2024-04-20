using UnityEngine;
using Verse;

namespace VVRace
{
    public class PlaceWorker_ShowPlantTurretRadius : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            if (thing == null) { return; }

            VerbProperties verbProperties = def.building.turretGunDef.Verbs
                .Find((VerbProperties v) => typeof(Verb_ShootWithMana).IsAssignableFrom(v.verbClass) || typeof(Verb_Spray).IsAssignableFrom(v.verbClass));

            if (verbProperties.range > 0f)
            {
                GenDraw.DrawRadiusRing(center, verbProperties.range);
            }
            if (verbProperties.minRange > 0f)
            {
                GenDraw.DrawRadiusRing(center, verbProperties.minRange);
            }
        }
    }
}
