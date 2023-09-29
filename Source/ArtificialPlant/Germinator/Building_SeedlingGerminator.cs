using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace VVRace
{
    public enum GerminateScheduleJob
    {
        None,
        Repotting,
        PestControl,
        Weeding,
    }

    public class GerminatorModExtension : DefModExtension
    {
        public List<ThingDefCountClass> germinateIngredients;
        public int scheduleCooldown;
    }

    public class Building_SeedlingGerminator : Building
    {
        public GerminateSchedule CurrentSchedule => _currentSchedule;

        private GerminateSchedule _currentSchedule;
        private GerminateSchedule _lastCompletedSchedule;

        private GerminatorModExtension _defModExtension;
        public GerminatorModExtension GerminatorModExtension
        {
            get
            {
                if (_defModExtension == null)
                {
                    _defModExtension = def.GetModExtension<GerminatorModExtension>();
                }

                return _defModExtension;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _currentSchedule, "currentSchedule");
            Scribe_Deep.Look(ref _lastCompletedSchedule, "lastCompletedSchedule");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _currentSchedule?.ResolveBuildingDef(def);
                _lastCompletedSchedule?.ResolveBuildingDef(def);
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (_currentSchedule != null)
            {
                switch (_currentSchedule.Stage)
                {
                    case GerminateStage.None:
                        {
                            sb.AppendLine();
                            sb.Append(LocalizeTexts.InspectorViviGerminatorReserved.Translate());
                        }
                        break;

                    case GerminateStage.Germating:
                        break;
                }
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Spawned)
            {
                if (_currentSchedule == null && Find.Selector.SelectedObjectsListForReading
                    .Where(v => v is Building_SeedlingGerminator)
                    .Cast<Building_SeedlingGerminator>()
                    .GroupBy(v => v.def)
                    .Count() == 1)
                {
                    var commandRegisterSchedule = new Command_RegisterGerminateSchedule();
                    commandRegisterSchedule.building = this;
                    commandRegisterSchedule.defaultLabel = LocalizeTexts.CommandRegisterGerminateSchedule.Translate();
                    commandRegisterSchedule.defaultDesc = LocalizeTexts.CommandRegisterGerminateScheduleDesc.Translate();
                    yield return commandRegisterSchedule;
                }
                else
                {
                    var commandCancelSchedule = new Command_CancelGerminateSchedule();
                    commandCancelSchedule.building = this;
                    commandCancelSchedule.defaultLabel = LocalizeTexts.CommandCancelGerminateSchedule.Translate();
                    commandCancelSchedule.defaultDesc = LocalizeTexts.CommandCancelGerminateScheduleDesc.Translate();
                    yield return commandCancelSchedule;
                }
            }
        }

        public void ReserveSchedule(GerminateSchedule schedule)
        {
            _currentSchedule = schedule;
            
        }

        public void CancelSchedule()
        {
            _currentSchedule = null;
        }
    }
}
