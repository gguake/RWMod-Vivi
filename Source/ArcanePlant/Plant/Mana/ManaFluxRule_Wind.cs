﻿using Verse;

namespace VVRace
{
    public class ManaFluxRule_Wind : ManaFluxRule
    {
        public SimpleCurve manaFromWindSpeed;

        public override IntRange ApproximateManaFlux => new IntRange((int)manaFromWindSpeed.MinY, (int)manaFromWindSpeed.MaxY);

        public override float CalcManaFlux(ManaAcceptor plant, int ticks)
        {
            if (!plant.IsOutside()) { return 0f; }

            return manaFromWindSpeed.Evaluate(plant.Map.windManager.WindSpeed) / 60000f * ticks;
        }
    }
}