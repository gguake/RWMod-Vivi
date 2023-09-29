using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Command_CancelGerminateSchedule : Command
    {
        public Building_SeedlingGerminator building;
        private List<Building_SeedlingGerminator> buildings;

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);

            if (buildings == null)
            {
                buildings = new List<Building_SeedlingGerminator>();
            }

            if (!buildings.Contains(building))
            {
                buildings.Add(building);
            }

            var hasAnyStartedSchedule = buildings.Any(v => v.CurrentSchedule != null && !v.CurrentSchedule.CanStopInstantly);
            if (hasAnyStartedSchedule)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    LocalizeTexts.WarnCancelGerminateSchedule.Translate(), 
                    "Confirm".Translate(),
                    CancelGerminate,
                    "GoBack".Translate(),
                    buttonADestructive: true));
            }
            else
            {
                CancelGerminate();
            }
        }

        public override bool InheritInteractionsFrom(Gizmo other)
        {
            if (buildings == null)
            {
                buildings = new List<Building_SeedlingGerminator>();
            }

            buildings.Add(((Command_CancelGerminateSchedule)other).building);
            return false;
        }

        private void CancelGerminate()
        {
            if (building != null)
            {
                building.CancelSchedule();
            }

            if (buildings != null)
            {
                foreach (var building in buildings)
                {
                    building.CancelSchedule();
                }
            }
        }
    }
}
