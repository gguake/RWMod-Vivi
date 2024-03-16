using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class Filth_Pollen : Filth
    {
        private string LabelThing
        {
            get
            {
                if (stackCount > 1)
                {
                    return LabelNoCount + " x" + stackCount.ToStringCached();
                }
                return LabelNoCount;
            }
        }

        public override string Label
        {
            get
            {
                return "FilthLabel".Translate(
                    LabelThing, 
                    thickness.ToString());
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void ThickenFilth()
        {
            base.ThickenFilth();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!Spawned) { return; }

            // 만약 자연적으로 소멸했다면 낮은 확률로 식물을 복제한다.
            if (sources != null && DisappearAfterTicks != 0 && TicksSinceThickened > DisappearAfterTicks && Rand.Chance(0.3f))
            {
                var plantDefs = DefDatabase<ThingDef>.AllDefsListForReading.Where(v => sources.Contains(v.defName) && v.CanEverPlantAt(Position, Map)).ToList();
                if (plantDefs.Count > 0)
                {
                    var def = plantDefs.RandomElement();
                    var plant = GenSpawn.Spawn(def, Position, Map) as Plant;
                    if (plant != null)
                    {
                        plant.Growth = 0.0001f;
                        base.Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
                    }
                    else
                    {
                        plant.Destroy();
                    }
                }
            }

            base.Destroy(mode);
        }
    }
}
