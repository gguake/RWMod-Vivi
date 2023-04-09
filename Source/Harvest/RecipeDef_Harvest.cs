using System;
using Verse;

namespace VVRace
{
    public class RecipeDef_Harvest : RecipeDef
    {
        public Type harvestWorkerType;

        [NonSerialized]
        public HarvestWorker harvestWorker;

        public Danger maxPathDanger;

        public override void ResolveReferences()
        {
            base.ResolveReferences();

            if (harvestWorkerType != null)
            {
                harvestWorker = (HarvestWorker)Activator.CreateInstance(harvestWorkerType);
            }
        }
    }
}
