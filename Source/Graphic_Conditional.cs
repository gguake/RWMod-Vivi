using UnityEngine;
using Verse;

namespace VVRace
{
    public interface IConditionalGraphicProvider
    {
        int GraphicIndex { get; }
    }

    public class Graphic_Conditional : Graphic_Collection
    {
        public override Material MatSingle => subGraphics[0].MatSingle;

        public int SubGraphicsCount => subGraphics.Length;

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            if (newColorTwo != Color.white)
            {
                Log.ErrorOnce("Cannot use Graphic_Conditional.GetColoredVersion with a non-white colorTwo.", 9910251);
            }
            return GraphicDatabase.Get<Graphic_Conditional>(path, newShader, drawSize, newColor, Color.white, data);
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

            return SubGraphicFor(thing).MatSingle;
        }

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            Graphic graphic = ((thing == null) ? subGraphics[0] : SubGraphicFor(thing));
            graphic.DrawWorker(loc, rot, thingDef, thing, extraRotation);
            if (base.ShadowGraphic != null)
            {
                base.ShadowGraphic.DrawWorker(loc, rot, thingDef, thing, extraRotation);
            }
        }

        public Graphic SubGraphicFor(Thing thing)
        {
            if (thing == null || !(thing is IConditionalGraphicProvider graphicProvider))
            {
                return subGraphics[0];
            }

            return subGraphics[graphicProvider.GraphicIndex];
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            Graphic graphic = ((thing == null) ? subGraphics[0] : SubGraphicFor(thing));
            graphic.Print(layer, thing, extraRotation);

            if (base.ShadowGraphic != null && thing != null)
            {
                base.ShadowGraphic.Print(layer, thing, extraRotation);
            }
        }

        public override string ToString()
        {
            return "Conditional(path=" + path + ", count=" + subGraphics.Length + ")";
        }
    }
}
