using System.Linq;
using Verse;

namespace VVRace
{
    public class ViviControlSettings : IExposable
    {
        public int MindLinkElapsedTicks => mindLinkStartTicks >= 0 ? Find.TickManager.TicksGame - mindLinkStartTicks : 0;

        public ViviWorkModeDef AssignedWorkMode
        {
            get
            {
                return assignedWorkMode ?? VVWorkModeDefOf.VV_Work;
            }
            set
            {
                assignedWorkMode = value;

                if (value != null)
                {
                    foreach (var item in value.AllWorkTypePriority.Where(v => !gene.pawn.WorkTypeIsDisabled(v.workTypeDef)))
                    {
                        gene.pawn.workSettings.SetPriority(item.workTypeDef, item.priority);
                    }

                    UpdateWorkSettingByRestFirstSetting();
                }

                gene.pawn.InterruptCurrentJob();
            }
        }

        public bool UnassignRequested
        {
            get => unassignRequested;
            set => unassignRequested = value;
        }

        public bool DoRestFirst
        {
            get => doRestFirst;
            set
            {
                doRestFirst = value;
                UpdateWorkSettingByRestFirstSetting();
            }
        }

        public Gene_Vivi gene;
        private int mindLinkStartTicks = -1;
        private ViviWorkModeDef assignedWorkMode;
        private bool doRestFirst = false;
        private bool unassignRequested = false;

        public ViviControlSettings(Gene_Vivi gene)
        {
            this.gene = gene;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref mindLinkStartTicks, "linkStartTicks");
            Scribe_Defs.Look(ref assignedWorkMode, "assignedWorkMode");
            Scribe_Values.Look(ref doRestFirst, "doRestFirst");
            Scribe_Values.Look(ref unassignRequested, "unassignRequested");
        }

        public void Notify_NewMindLink()
        {
            mindLinkStartTicks = Find.TickManager.TicksGame;
        }

        public void ResetWorkSettings()
        {
            assignedWorkMode = null;
            doRestFirst = false;

            if (gene.pawn.workSettings != null && gene.pawn.workSettings.EverWork)
            {
                foreach (var def in DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(def => !gene.pawn.WorkTypeIsDisabled(def)))
                {
                    gene.pawn.workSettings.SetPriority(def, 3);
                }
            }
        }


        private void UpdateWorkSettingByRestFirstSetting()
        {
            if (gene.pawn.WorkTypeIsDisabled(VVWorkTypeDefOf.Patient) || gene.pawn.WorkTypeIsDisabled(VVWorkTypeDefOf.PatientBedRest))
            {
                return;
            }

            if (doRestFirst)
            {
                gene.pawn.workSettings.SetPriority(VVWorkTypeDefOf.Patient, 1);
                gene.pawn.workSettings.SetPriority(VVWorkTypeDefOf.PatientBedRest, 1);
            }
            else
            {
                gene.pawn.workSettings.SetPriority(VVWorkTypeDefOf.Patient, 2);
                gene.pawn.workSettings.SetPriority(VVWorkTypeDefOf.PatientBedRest, 3);
            }
        }

    }
}
