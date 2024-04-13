using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class SymbolResolver_DreamumGarden : SymbolResolver
    {
        private static List<IntVec3> _tmpRadialCells = new List<IntVec3>();
        public override void Resolve(ResolveParams rp)
        {
            var map = BaseGen.globalSettings.map;
            var center = rp.rect.CenterCell;

            var radius = Math.Min(rp.rect.Width, rp.rect.Height);
            try
            {
                _tmpRadialCells.AddRange(GenRadial.RadialPatternInRadius(radius + 0.5f).Select(c => center + c).Where(c => c.InBounds(map) && !c.Impassable(map) && c.GetFertility(map) > 0f));
                foreach (var cell in _tmpRadialCells)
                {
                    if (Rand.Chance(0.1f))
                    {
                        var p = rp;
                        p.rect = new CellRect(cell.x, cell.z, 1, 1);
                        p.singleThingDef = VVThingDefOf.VV_Dreamum;
                        BaseGen.symbolStack.Push("thing", p);
                    }
                }
            }
            finally
            {
                _tmpRadialCells.Clear();
            }
        }
    }
}
