using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static RimWorld.ColonistBar;

namespace VVRace
{
    public class EnergyRule_Sunlight : EnergyRule
    {
        public FloatRange energyFromSunlightRange;

        public override IntRange ApproximateEnergy => new IntRange((int)energyFromSunlightRange.min, (int)energyFromSunlightRange.max);

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            if (plant.Map.roofGrid.Roofed(plant.Position))
            {
                return 0f;
            }

            return energyFromSunlightRange.LerpThroughRange(plant.Map.skyManager.CurSkyGlow) / 60000f * ticks;
        }
    }
}
