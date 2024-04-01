using RimWorld.BaseGen;
using System.Linq;
using Verse;

namespace VVRace
{
    public class SymbolResolver_ViviEggSpawn : SymbolResolver
    {
        public override void Resolve(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var hatcheries = map.listerBuildings.AllBuildingsNonColonistOfDef(VVThingDefOf.VV_ViviHatchery).Cast<ViviEggHatchery>().ToList();
            if (hatcheries.Count() == 0)
            {
                return;
            }

            var royalVivis = map.mapPawns.AllPawns.Where(pawn => pawn.Faction == resolveParams.faction && pawn.IsRoyalVivi()).ToList();
            if (royalVivis.Count() == 0)
            {
                return;
            }

            var vivi = royalVivis.RandomElement();
            foreach (var hatchery in hatcheries)
            {
                if (Rand.Bool)
                {
                    var egg = vivi.GetCompViviEggLayer().ProduceEgg(force: true) as ViviEgg;
                    if (egg != null)
                    {
                        egg.CompViviHatcher.hatchProgress = Rand.Range(0.2f, 0.7f);
                        hatchery.ViviEgg = egg;
                    }
                }
            }
        }
    }
}
