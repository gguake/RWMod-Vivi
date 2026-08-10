using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Apparel_ScentBottle : Apparel
    {
        private Graphic filledGraphic;
        private Color filledColor;

        private CompScentBottle _cachedBottleComp;
        private CompScentBottle BottleComp
        {
            get
            {
                if (_cachedBottleComp == null)
                {
                    _cachedBottleComp = GetComp<CompScentBottle>();
                }
                return _cachedBottleComp;
            }
        }

        public override Graphic Graphic
        {
            get
            {
                var comp = BottleComp;
                if (comp?.HasScent != true)
                {
                    return base.Graphic;
                }

                if (filledGraphic == null || filledColor != comp.ScentColor)
                {
                    filledColor = comp.ScentColor;
                    filledGraphic = GraphicDatabase.Get<Graphic_Single>(
                        comp.Props.filledGraphicPath,
                        def.graphicData.shaderType.Shader,
                        def.graphicData.drawSize,
                        filledColor);
                }

                return filledGraphic;
            }
        }

        public override Color DrawColor
        {
            get
            {
                var comp = BottleComp;
                return comp?.HasScent == true ? comp.ScentColor : base.DrawColor;
            }
            set => base.DrawColor = value;
        }
    }
}
