using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            var hasAnySchedule = buildings.Any(v => v.CurrentSchedule != null);
            if (hasAnySchedule)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    LocalizeTexts.WarnCancelGerminateSchedule.Translate(), 
                    "Yes".Translate(),
                    () =>
                    {
                        CancelGerminate();
                    }));
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
