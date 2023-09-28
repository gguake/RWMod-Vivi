using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class GerminateSchedule : IExposable
    {
        public const int ScheduleDayCount = 6;

        public bool CanStopInstantly { get; }

        public GerminateScheduleDef this[int index]
        {
            get
            {
                return _schedules[index];
            }
            set
            {
                _schedules[index] = value;
            }
        }

        private List<GerminateScheduleDef> _schedules = Enumerable.Repeat(VVGerminateScheduleDefOf.VV_DoNothing, ScheduleDayCount).ToList();
        private List<bool> _scheduleProcessed = Enumerable.Repeat(false, ScheduleDayCount).ToList();
        private int _currentScheduleIndex = 0;
        private int _germinateStartTick;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _schedules, "schedules", LookMode.Def);
            Scribe_Collections.Look(ref _scheduleProcessed, "scheduleProcessed", LookMode.Value);

            Scribe_Values.Look(ref _currentScheduleIndex, "currentScheduleIndex");
            Scribe_Values.Look(ref _germinateStartTick, "germinateStartTick");
        }

        public GerminateSchedule()
        {
        }
    }
}
