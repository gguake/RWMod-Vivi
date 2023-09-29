using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ManageSeedling : JobDriver
    {
        public GerminateScheduleDef GerminateScheduleDef { get; set; }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            throw new NotImplementedException();
        }

        protected override string ReportStringProcessed(string str)
        {
            return base.ReportStringProcessed(str);
        }
    }
}
