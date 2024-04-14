using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class WorldObjectCompProperties_ViviRuins : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_ViviRuins()
        {
            compClass = typeof(WorldObjectCompViviRuins);
        }
    }

    public class WorldObjectCompViviRuins : WorldObjectComp
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            return CaravanArrivalActionUtility.GetFloatMenuOptions(
                () => CaravanArrivalAction_VisitViviRuins.CanVisit((MapParent)parent), 
                () => new CaravanArrivalAction_VisitViviRuins(this), 
                LocalizeString_Gizmo.VV_Gizmo_VisitViviRuins.Translate(parent.Label), 
                caravan, 
                parent.Tile,
                parent);
        }
    }
}
