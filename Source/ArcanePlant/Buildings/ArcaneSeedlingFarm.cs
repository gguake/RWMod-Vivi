using RimWorld;
using Verse;

namespace VVRace
{
    public class ArcaneSeedlingFarm : ArcanePlantPot
    {
        public CompRefuelable CompRefuelable
        {
            get
            {
                if (_compRefuelable == null)
                {
                    _compRefuelable = GetComp<CompRefuelable>();
                }
                return _compRefuelable;
            }
        }
        private CompRefuelable _compRefuelable;

        public override int UpdateRateTicks => 500;
        protected override int MinTickIntervalRate => 500;
        protected override int MaxTickIntervalRate => 500;

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (!Spawned || Destroyed)
            {
                return;
            }

            var compRefuelable = CompRefuelable;
            foreach (var cell in this.OccupiedRect())
            {
                if (compRefuelable.Fuel < 1f) { break; }

                var plant = _arcanePlantMapComp.GetArcanePlantAtCell(cell);
                if (plant is ArcanePlant_Seedling seedling)
                {
                    var seedlingRefuelable = seedling.GetComp<CompRefuelable>();
                    var requiredFuel = seedlingRefuelable.Props.fuelCapacity - seedlingRefuelable.Fuel;
                    if (requiredFuel > 1.5f)
                    {
                        compRefuelable.ConsumeFuel(1f);
                        seedlingRefuelable.Refuel(1.5f);
                    }
                }
            }
        }
    }
}
