using RimWorld;
using System.Collections.Generic;
using System.Linq;
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

            ;

            Find.WindowStack.Add(new FloatMenu(GetFloatMenuOptionForGerminate().ToList()));
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

        private IEnumerable<FloatMenuOption> GetFloatMenuOptionForGerminate()
        {
            var fixedGerminateCandidate = new List<ThingDef>() { null };

            if (VVResearchProjectDefOf.VV_AncientPlantCuttings.IsFinished)
            {
                fixedGerminateCandidate.AddRange(building.Map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing)
                    .Where(v => v.GetInnerIfMinified() is ArtificialPlant)
                    .Select(v => v.GetInnerIfMinified().def)
                    .Distinct());
            }

            var floatMenuOptions = new List<FloatMenuOption>();
            foreach (var candidate in fixedGerminateCandidate)
            {
                if (candidate == null)
                {
                    yield return new FloatMenuOption(
                        label: LocalizeTexts.FloatMenuOptionDefaultGerminate.Translate(), 
                        action: delegate
                        {
                            Find.WindowStack.Add(new Dialog_GerminateSchedule(buildings, null));
                        },
                        priority: MenuOptionPriority.High);
                }
                else
                {
                    yield return new FloatMenuOption(
                        label: LocalizeTexts.FloatMenuOptionFixedGerminate.Translate(candidate.LabelCap),
                        action: delegate
                        {
                            Find.WindowStack.Add(new Dialog_GerminateSchedule(buildings, candidate));
                        });
                }
            }
        }
    }
}
