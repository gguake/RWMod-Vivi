using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class PawnTable_Vivis : PawnTable
    {
        public PawnTable_Vivis(PawnTableDef def, Func<IEnumerable<Pawn>> pawnsGetter, int uiWidth, int uiHeight) : 
            base(def, pawnsGetter, uiWidth, uiHeight)
        {
        }

        protected override IEnumerable<Pawn> LabelSortFunction(IEnumerable<Pawn> input)
        {
            return input
                .OrderBy(v => v.GetMindLinkMaster()?.thingIDNumber ?? int.MaxValue)
                .ThenBy(v => v.KindLabel)
                .ThenBy(v => v.Label);
        }
    }
}
