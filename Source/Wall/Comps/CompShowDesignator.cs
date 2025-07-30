using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_ShowDesignator : CompProperties
    {
        public Designator Designator
        {
            get
            {
                if (_designator == null)
                {
                    _designator = (Designator)Activator.CreateInstance(designatorType);
                }

                return _designator;
            }
        }
        private Designator _designator;

        public Type designatorType;
        public DesignationDef designationDef;
        public ResearchProjectDef researchProjectDef;

        public CompProperties_ShowDesignator()
        {
            compClass = typeof(CompShowDesignator);
        }
    }

    public class CompShowDesignator : ThingComp
    {
        public CompProperties_ShowDesignator Props => (CompProperties_ShowDesignator)props;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayerSilentFail)
            {
                yield break;
            }

            if (Props.researchProjectDef != null && !Props.researchProjectDef.IsFinished)
            {
                yield break;
            }

            if (parent.Map.designationManager.DesignationOn(parent, Props.designationDef) != null)
            {
                yield break;
            }

            yield return Props.Designator;
        }
    }
}
