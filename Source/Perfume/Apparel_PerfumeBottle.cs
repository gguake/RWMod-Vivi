using UnityEngine;
using RimWorld;
using Verse;

namespace VVRace
{
    public class Apparel_PerfumeBottle : Apparel
    {
        public override Color DrawColor
        {
            get
            {
                var comp = GetComp<CompPerfumeBottle>();
                return comp?.IsComplete == true ? comp.BlendColor : base.DrawColor;
            }
            set => base.DrawColor = value;
        }
    }
}
