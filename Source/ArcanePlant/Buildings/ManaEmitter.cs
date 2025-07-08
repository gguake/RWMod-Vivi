using RimWorld;
using Verse;

namespace VVRace
{
    public class ManaEmitter : Building
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

        public override int? OverrideGraphicIndex => CompRefuelable.HasFuel ? 1 : 0;

        public override int UpdateRateTicks => 1200;
        protected override int MaxTickIntervalRate => 1000;

        protected override void ReceiveCompSignal(string signal)
        {
            if (Spawned)
            {
                if (signal == "RanOutOfFuel" || signal == "Refueled")
                {
                    DirtyMapMesh(Map);
                }
            }
        }
    }
}
