using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace VVRace
{
    public enum PeacebloomProjectStage
    {
        NotStarted,
        InProgress,
        Completed,
    }

    public class PeacebloomAltar : EnergyAcceptor
    {
        private bool _projectStarted;
        private float _projectProgress;

        public bool EnergySupplied => true;

        public override EnergyFluxNetwork EnergyFluxNetwork { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override EnergyFluxNetworkNode EnergyFluxNode => throw new NotImplementedException();

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _projectStarted, "projectStarted");
            Scribe_Values.Look(ref _projectProgress, "projectProgress");
        }

        public override void Tick()
        {
            base.Tick();

            if (EnergySupplied)
            {
            }
        }
    }
}
