using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class MinifiedArcanePlant : MinifiedThing
    {
        public override Graphic Graphic
        {
            get
            {
                if (cachedGraphic == null)
                {
                    cachedGraphic = base.InnerThing.Graphic.ExtractInnerGraphicFor(base.InnerThing);
                    Vector2 minifiedDrawSize = GetMinifiedDrawSize(base.InnerThing.def.size.ToVector2(), 1.1f);
                    Vector2 newDrawSize = new Vector2(minifiedDrawSize.x / (float)base.InnerThing.def.size.x * cachedGraphic.drawSize.x, minifiedDrawSize.y / (float)base.InnerThing.def.size.z * cachedGraphic.drawSize.y);
                    cachedGraphic = cachedGraphic.GetCopy(newDrawSize, ShaderTypeDefOf.Cutout.Shader);
                }
                return cachedGraphic;
            }
        }

        protected override Graphic LoadCrateFrontGraphic()
        {
            return GraphicDatabase.Get<Graphic_Single>("Things/Item/Minified/BurlapBag", ShaderDatabase.Cutout, GetMinifiedDrawSize(base.InnerThing.def.size.ToVector2(), 1.1f) * 1.16f, Color.white);
        }

        public override void PreTraded(TradeAction action, Pawn playerNegotiator, ITrader trader)
        {
            base.PreTraded(action, playerNegotiator, trader);

            InnerThing?.PreTraded(action, playerNegotiator, trader);
        }
    }
}
