using System.Collections.Generic;
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

    public class Building_SeedlingGerminator : Building
    {
        public GerminateSchedule CurrentSchedule => _currentSchedule;

        private GerminateSchedule _currentSchedule;

        private GerminateSchedule _lastCompletedSchedule;
        private GerminateSchedule _reservedSchedule;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _currentSchedule, "currentSchedule");

            Scribe_Deep.Look(ref _lastCompletedSchedule, "lastCompletedSchedule");
            Scribe_Deep.Look(ref _reservedSchedule, "reservedSchedule");
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Spawned)
            {
                if (_currentSchedule == null)
                {
                    var commandRegisterSchedule = new Command_RegisterGerminateSchedule();
                    commandRegisterSchedule.defaultLabel = LocalizeTexts.CommandRegisterGerminateSchedule.Translate();
                    commandRegisterSchedule.defaultDesc = LocalizeTexts.CommandRegisterGerminateScheduleDesc.Translate();
                    yield return commandRegisterSchedule;
                }
                else
                {
                    var commandCancelSchedule = new Command_CancelGerminateSchedule();
                    commandCancelSchedule.defaultLabel = LocalizeTexts.CommandCancelGerminateSchedule.Translate();
                    commandCancelSchedule.defaultDesc = LocalizeTexts.CommandCancelGerminateScheduleDesc.Translate();
                    yield return commandCancelSchedule;
                }
            }
        }

        public void ReserveSchedule(GerminateSchedule schedule)
        {
            _reservedSchedule = schedule;
        }

        public void CancelSchedule()
        {
            _currentSchedule = null;
        }
    }
}
