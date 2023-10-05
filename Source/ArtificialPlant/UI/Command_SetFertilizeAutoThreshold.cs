using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Command_SetFertilizeAutoThreshold : Command
    {
        public ArtificialPlant plant;
        private List<ArtificialPlant> plants;

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);

            if (plants == null)
            {
                plants = new List<ArtificialPlant>();
            }

            if (!plants.Contains(plant))
            {
                plants.Add(plant);
            }

            var dialog_Slider = new Dialog_Slider(
                (x) => LocalizeTexts.GizmoSetFertilizeAutoThreshold.Translate(x), 
                0, 
                plants.Min(v => v.ArtificialPlantModExtension.energyCapacity), 
                (value) => {
                    for (int i = 0; i < plants.Count; ++i)
                    {
                        plants[i].FertilizeAutoThreshold = value;
                    }
                }, 
                plants.Min(v => v.FertilizeAutoThreshold));

            Find.WindowStack.Add(dialog_Slider);
        }


        public override bool InheritInteractionsFrom(Gizmo other)
        {
            if (plants == null)
            {
                plants = new List<ArtificialPlant>();
            }

            plants.Add(((Command_SetFertilizeAutoThreshold)other).plant);
            return false;
        }
    }
}
