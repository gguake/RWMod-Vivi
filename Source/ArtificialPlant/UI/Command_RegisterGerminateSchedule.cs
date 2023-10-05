using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Command_RegisterGerminateSchedule : Command
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

            Find.WindowStack.Add(new Dialog_GerminateSchedule(buildings));
        }

        public override bool InheritInteractionsFrom(Gizmo other)
        {
            if (buildings == null)
            {
                buildings = new List<Building_SeedlingGerminator>();
            }

            buildings.Add(((Command_RegisterGerminateSchedule)other).building);
            return false;
        }
    }
}
