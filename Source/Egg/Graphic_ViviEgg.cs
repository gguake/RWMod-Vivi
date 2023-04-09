using UnityEngine;
using Verse;

namespace VVRace
{
    public class Graphic_ViviEgg : Graphic_Collection
    {
        public override Material MatSingle => subGraphics[0].MatSingle;

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_StackCount>(path, newShader, drawSize, newColor, newColorTwo, data);
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            if (thing == null)
            {
                return MatSingle;
            }

            return MatSingleFor(thing);
        }

        public override Material MatSingleFor(Thing thing)
        {
            if (thing == null)
            {
                return MatSingle;
            }

            return subGraphics[GetSubGraphicIndex(thing)].MatSingle;
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            Graphic graphic = ((thing == null) ? subGraphics[0] : subGraphics[GetSubGraphicIndex(thing)]);
            graphic.DrawWorker(loc, rot, thingDef, thing, extraRotation);
        }

        private int GetSubGraphicIndex(Thing thing)
        {
            var egg = thing.TryGetComp<CompViviHatcher>();
            if (egg != null)
            {
                if (egg.hatchProgress >= 0.7f)
                {
                    return 2;
                }
                else if (egg.hatchProgress >= 0.3f)
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}
