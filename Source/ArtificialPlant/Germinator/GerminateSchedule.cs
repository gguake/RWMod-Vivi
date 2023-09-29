using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public enum GerminateStage
    {
        None,
        Germating,
        GerminateCompleted,
    }

    public class GerminateSchedule : IExposable, IEnumerable<GerminateScheduleDef>
    {
        public const int TotalScheduleCount = 6;

        public bool CanStopInstantly => _germinateStartTick == 0;

        private GerminatorModExtension _defModExtension;
        public GerminatorModExtension GerminatorModExtension
        {
            get
            {
                if (_defModExtension == null)
                {
                    _defModExtension = _buildingDef.GetModExtension<GerminatorModExtension>();
                }

                return _defModExtension;
            }
        }

        public int ExpectedGerminateBonusCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _schedules.Count; ++i)
                {
                    count += _schedules[i].bonusAddGerminateResult;
                }

                return count;
            }
        }
        public float ExpectedGerminateBonusSuccessChance
        {
            get
            {
                float baseProb = 1f;
                for (int i = 0; i < _schedules.Count; ++i)
                {
                    var bonus = _schedules[i].bonusMultiplierGerminateSuccessChance;
                    if (bonus > 0f)
                    {
                        baseProb *= bonus;
                    }
                }

                return baseProb;
            }
        }
        public float ExpectedGerminateBonusRareChance
        {
            get
            {
                float baseProb = 1f;
                for (int i = 0; i < _schedules.Count; ++i)
                {
                    var bonus = _schedules[i].bonusMultiplierGerminateRareChance;
                    if (bonus > 0f)
                    {
                        baseProb *= bonus;
                    }
                }

                return baseProb;
            }
        }

        private GerminateStage _stage = GerminateStage.None;
        public GerminateStage Stage => _stage;

        private List<GerminateScheduleDef> _schedules = Enumerable.Repeat(VVGerminateScheduleDefOf.VV_DoNothing, TotalScheduleCount).ToList();
        private List<float> _scheduleQuality = Enumerable.Repeat(0f, TotalScheduleCount).ToList();
        private int _currentScheduleIndex = 0;
        private int _germinateStartTick;
        private ThingDef _buildingDef;

        public GerminateSchedule()
        {
        }

        public GerminateSchedule(ThingDef buildingDef)
        {
            _buildingDef = buildingDef;
        }

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

        public void ResolveBuildingDef(ThingDef buildingDef)
        {
            _buildingDef = buildingDef;
        }

        public GerminateSchedule Clone()
        {
            var schedule = new GerminateSchedule(_buildingDef);
            schedule._schedules = _schedules.ToList();
            schedule._scheduleQuality = _scheduleQuality.ToList();
            schedule._currentScheduleIndex = _currentScheduleIndex;
            schedule._germinateStartTick = _germinateStartTick;

            return schedule;
        }

        public IEnumerator<GerminateScheduleDef> GetEnumerator()
        {
            return _schedules.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _schedules.GetEnumerator();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _stage, "germinateStage");
            Scribe_Collections.Look(ref _schedules, "schedules", LookMode.Def);
            Scribe_Collections.Look(ref _scheduleQuality, "scheduleQuality", LookMode.Value);

            Scribe_Values.Look(ref _currentScheduleIndex, "currentScheduleIndex");
            Scribe_Values.Look(ref _germinateStartTick, "germinateStartTick");
            Scribe_Defs.Look(ref _buildingDef, "buildingDef");
        }

        public void StartGerminate()
        {
            _germinateStartTick = GenTicks.TicksGame;
        }
    }
}
