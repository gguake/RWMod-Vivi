using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PlaceWorker_TurretPlantTop : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 loc, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            var turretTopGraphic = GraphicDatabase.Get<Graphic_Single>(
                def.building.turretGunDef.graphicData.texPath, 
                ShaderDatabase.Cutout, 
                new Vector2(def.building.turretTopDrawSize, def.building.turretTopDrawSize), 
                Color.white);

            var ghostGraphic = GhostUtility.GhostGraphicFor(turretTopGraphic, def, ghostCol);

            ghostGraphic.DrawFromDef(GenThing.TrueCenter(loc, rot, def.Size, AltitudeLayer.MetaOverlays.AltitudeFor()), rot, def, TurretTop.ArtworkRotation);
        }
    }
}
