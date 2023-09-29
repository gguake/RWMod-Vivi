using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum GerminateStage
    {
        None,
        GerminateInProgress,
        GerminateCompleted,
    }

    public class GerminateSchedule : IExposable, IEnumerable<GerminateScheduleDef>
    {
        public const int TotalScheduleCount = 6;

        public bool CanStopInstantly => _germinateNextManageTick == 0;

        public int TicksToCompleteGerminate => Mathf.Max(_germinateCompleteTick - GenTicks.TicksGame, 0);

        public bool CanManageJob => Stage == GerminateStage.GerminateInProgress && GenTicks.TicksGame >= _germinateNextManageTick;
        public bool HasNextManageJob => _currentScheduleIndex < TotalScheduleCount && _germinateNextManageTick < _germinateCompleteTick;
        public int TicksToNextManageJob => Mathf.Max(_germinateNextManageTick - GenTicks.TicksGame, 0);


        public GerminateScheduleDef CurrentManageScheduleDef => Stage == GerminateStage.GerminateInProgress ? _schedules[_currentScheduleIndex] : null;

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

        public float ExpectedGerminateBonusCount
        {
            get
            {
                float count = 0;
                for (int i = 0; i < _schedules.Count; ++i)
                {
                    count += _schedules[i].bonusAddGerminateResult.Average;
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
                    if (bonus != FloatRange.Zero)
                    {
                        baseProb *= bonus.Average;
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
                    if (bonus != FloatRange.Zero)
                    {
                        baseProb *= bonus.Average;
                    }
                }

                return baseProb;
            }
        }

        private GerminateStage _stage = GerminateStage.None;
        public GerminateStage Stage => _stage;

        public int CurrentScheduleNumber => _currentScheduleIndex + 1;

        private int _seed;
        private List<GerminateScheduleDef> _schedules = Enumerable.Repeat(VVGerminateScheduleDefOf.VV_DoNothing, TotalScheduleCount).ToList();
        private List<float> _scheduleQuality = Enumerable.Repeat(0f, TotalScheduleCount).ToList();
        private int _currentScheduleIndex = 0;
        private int _germinateNextManageTick;
        private int _germinateCompleteTick;

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
            schedule._germinateNextManageTick = _germinateNextManageTick;

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
            Scribe_Values.Look(ref _seed, "seed");
            Scribe_Collections.Look(ref _schedules, "schedules", LookMode.Def);
            Scribe_Collections.Look(ref _scheduleQuality, "scheduleQuality", LookMode.Value);

            Scribe_Values.Look(ref _currentScheduleIndex, "currentScheduleIndex");
            Scribe_Values.Look(ref _germinateNextManageTick, "germinateNextManageTick");
            Scribe_Values.Look(ref _germinateCompleteTick, "germinateCompleteTick");
            Scribe_Defs.Look(ref _buildingDef, "buildingDef");
        }

        public void Tick()
        {
            if (CanManageJob && CurrentManageScheduleDef == VVGerminateScheduleDefOf.VV_DoNothing)
            {
                AdvanceGerminateSchedule();
            }

            if (Stage == GerminateStage.GerminateInProgress && GenTicks.TicksGame >= _germinateCompleteTick)
            {
                _stage = GerminateStage.GerminateCompleted;
            }
        }

        public void StartGerminate()
        {
            if (Stage != GerminateStage.None) { return; }

            _seed = Rand.Int;
            _germinateNextManageTick = GenTicks.TicksGame + GerminatorModExtension.scheduleCooldown;
            _germinateCompleteTick = GenTicks.TicksGame + 24 * 2500 * (TotalScheduleCount + 1);
            _stage = GerminateStage.GerminateInProgress;

            _currentScheduleIndex = 0;
            for (int i = 0; i < _scheduleQuality.Count; ++i)
            {
                _scheduleQuality[i] = 0f;
            }
        }

        public void AdvanceGerminateSchedule(float quality = 1f)
        {
            _scheduleQuality[_currentScheduleIndex] = quality;

            _currentScheduleIndex++;
            _germinateNextManageTick = GenTicks.TicksGame + GerminatorModExtension.scheduleCooldown;
        }

        public void Debug_ReduceNextManageTick(int ticks)
        {
            _germinateNextManageTick = Mathf.Max(GenTicks.TicksGame, _germinateNextManageTick - ticks);
        }

        public void Debug_ReduceCompleteTick(int ticks)
        {
            _germinateCompleteTick = Mathf.Max(GenTicks.TicksGame, _germinateCompleteTick - ticks);
        }
    }
}
