using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ComplexWorker_MechanoidScoutingBase : ComplexWorker
    {
        private static SimpleCurve EntranceCountOverAreaCurve = new SimpleCurve
        {
            new CurvePoint(0f, 1f),
            new CurvePoint(1000f, 1f),
            new CurvePoint(1500f, 2f),
            new CurvePoint(5000f, 3f),
            new CurvePoint(10000f, 4f)
        };

        public override ComplexSketch GenerateSketch(IntVec2 size, Faction faction = null)
        {
            var sketch = new Sketch();

            int entranceCount = GenMath.RoundRandom(EntranceCountOverAreaCurve.Evaluate(size.Area));
            var complexLayout = ComplexLayoutGenerator.GenerateRandomLayout(new CellRect(0, 0, size.x, size.z), 9, 9, 0.2f, null, entranceCount);

            for (int x = complexLayout.container.minX; x <= complexLayout.container.maxX; x++)
            {
                for (int z = complexLayout.container.minZ; z <= complexLayout.container.maxZ; z++)
                {
                    var intVec = new IntVec3(x, 0, z);
                    int roomIdAt = complexLayout.GetRoomIdAt(intVec);
                    if (complexLayout.IsWallAt(intVec))
                    {
                        sketch.AddThing(ThingDefOf.Wall, intVec, Rot4.North, ThingDefOf.Steel);
                    }

                    if (complexLayout.IsFloorAt(intVec) || complexLayout.IsDoorAt(intVec))
                    {
                        sketch.AddTerrain(TerrainDefOf.PavedTile, intVec);
                    }

                    if (complexLayout.IsDoorAt(intVec))
                    {
                        sketch.AddThing(ThingDefOf.Door, intVec, Rot4.North, ThingDefOf.Steel);
                    }
                }
            }

            var roomParams = default(ComplexRoomParams);
            roomParams.sketch = sketch;

            if (!def.roomDefs.NullOrEmpty())
            {
                var usedDefs = new List<ComplexRoomDef>();
                foreach (var room in complexLayout.Rooms)
                {
                    roomParams.room = room;
                    if (def.roomDefs
                        .Where(def => def.CanResolve(roomParams) && usedDefs.Count(usedRoomDef => usedRoomDef == def) < def.maxCount)
                        .TryRandomElementByWeight(def => def.selectionWeight, out var result))
                    {
                        room.def = result;
                        usedDefs.Add(room.def);
                    }
                }
            }

            foreach (var room in complexLayout.Rooms)
            {
                if (room.def != null)
                {
                    roomParams.room = room;
                    room.def.ResolveSketch(roomParams);
                }
            }

            return new ComplexSketch
            {
                structure = sketch,
                layout = complexLayout,
                complexDef = def
            };
        }

        public override void Spawn(ComplexSketch sketch, Map map, IntVec3 center, float? threatPoints = null, List<Thing> allSpawnedThings = null)
        {
            base.Spawn(sketch, map, center, threatPoints, allSpawnedThings);
        }

        public override Faction GetFixedHostileFactionForThreats()
        {
            return Faction.OfMechanoids;
        }
    }
}
