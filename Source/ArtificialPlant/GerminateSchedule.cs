using RimWorld;
using System;
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
        GerminateComplete,
    }

    public class GerminateSchedule : IExposable, IEnumerable<GerminateScheduleDef>
    {
        public const int TotalScheduleCount = 6;

        public bool CanStopInstantly => _germinateNextManageTick == 0;

        public int TicksToCompleteGerminate => Mathf.Max(_germinateCompleteTick - GenTicks.TicksGame, 0);

        public bool CanManageJob => Stage == GerminateStage.GerminateInProgress && GenTicks.TicksGame >= _germinateNextManageTick;
        public bool HasNextManageJob => _currentScheduleIndex < TotalScheduleCount && _germinateNextManageTick < _germinateCompleteTick;
        public int TicksToNextManageJob => Mathf.Max(_germinateNextManageTick - GenTicks.TicksGame, 0);


        public GerminateScheduleDef CurrentManageScheduleDef => Stage == GerminateStage.GerminateInProgress && _currentScheduleIndex < TotalScheduleCount ? _schedules[_currentScheduleIndex] : null;

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
        private List<float> _scheduleQuality = Enumerable.Repeat(-1f, TotalScheduleCount).ToList();
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

        public void Tick(Building_SeedlingGerminator building)
        {
            if (Stage == GerminateStage.GerminateInProgress)
            {
                if (GenTicks.TicksGame >= _germinateCompleteTick)
                {
                    CompleteGerminate(building);
                }
                else if (CanManageJob && CurrentManageScheduleDef == VVGerminateScheduleDefOf.VV_DoNothing)
                {
                    AdvanceGerminateSchedule(building);
                }
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
                _scheduleQuality[i] = -1f;
            }
        }

        public void AdvanceGerminateSchedule(Pawn actor, Building_SeedlingGerminator germinator)
        {
            var plantSkillLevel = actor.skills.GetSkill(SkillDefOf.Plants).Level;
            var range = new FloatRange(Mathf.Clamp01(plantSkillLevel * 0.05f), Mathf.Clamp01(0.5f + plantSkillLevel * 0.05f));
            AdvanceGerminateSchedule(germinator, range.RandomInRange);
        }

        public void AdvanceGerminateSchedule(Building_SeedlingGerminator germinator, float quality = 1f)
        {
            _scheduleQuality[_currentScheduleIndex] = quality;

            _currentScheduleIndex++;
            _germinateNextManageTick = GenTicks.TicksGame + GerminatorModExtension.scheduleCooldown;

            germinator.Notify_RefreshDrawer();
        }

        public void CompleteGerminate(Building_SeedlingGerminator germinator)
        {
            if (Stage != GerminateStage.GerminateInProgress)
            {
                return;
            }

            float bonusProductCount = 0f;
            float bonusSuccessChanceMultiplier = 1f;
            float bonusRareChanceMultiplier = 1f;
            for (int i = 0; i < TotalScheduleCount; ++i)
            {
                var def = _schedules[i];
                var quality = _scheduleQuality[i];
                if (quality < 0f)
                {
                    continue;
                }

                if (def.bonusAddGerminateResult != FloatRange.Zero)
                {
                    bonusProductCount += def.bonusAddGerminateResult.LerpThroughRange(quality);
                }

                if (def.bonusMultiplierGerminateSuccessChance != FloatRange.Zero)
                {
                    bonusSuccessChanceMultiplier *= def.bonusMultiplierGerminateSuccessChance.LerpThroughRange(quality);
                }

                if (def.bonusMultiplierGerminateRareChance != FloatRange.Zero)
                {
                    bonusRareChanceMultiplier *= def.bonusMultiplierGerminateRareChance.LerpThroughRange(quality);
                }
            }

            var germinatorData = _buildingDef.GetModExtension<GerminatorModExtension>();
            if (germinatorData == null)
            {
                throw new System.Exception("germinatorData is null");
            }

            var table = new List<(float weight, ThingDef thingDef)>();
            Rand.PushState();
            try
            {
                Rand.Seed = _seed;

                var successChance = Mathf.Clamp01(germinatorData.germinateSuccessChance * bonusSuccessChanceMultiplier);
                if (Rand.Chance(successChance))
                {
                    var rare = Rand.Chance(Mathf.Clamp01(germinatorData.germinateRareChance * bonusRareChanceMultiplier));
                    foreach (var plant in ArtificialPlant.AllArtificialPlantDefs)
                    {
                        var plantData = plant.GetModExtension<ArtificialPlantModExtension>();
                        if (plantData.germinateRare == rare)
                        {
                            table.Add((plantData.germinateWeight, plant));
                        }
                    }
                }
                else
                {
                    table.Add((germinatorData.germinateFailureCriticalWeight, null));

                    var failureCropsWeightSum = germinatorData.germinateFailureCropsTable.Sum(tdc => tdc.count);
                    for (int i = 0; i < germinatorData.germinateFailureCropsTable.Count; ++i)
                    {
                        var tdc = germinatorData.germinateFailureCropsTable[i];
                        table.Add((tdc.count / (float)failureCropsWeightSum * germinatorData.germinateFailureCropsWeight, tdc.thingDef));
                    }
                }

                var actualBonusProductCount = Rand.Range(3, 5) + (int)bonusProductCount + (Rand.Chance(bonusProductCount - (int)bonusProductCount) ? 1 : 0);
                var result = table.RandomElementByWeight(v => v.weight).thingDef;

                _stage = GerminateStage.GerminateComplete;

                germinator.Notify_ScheduleComplete(result, result != null ? actualBonusProductCount : 0);
            }
            catch (Exception ex)
            {
                Log.Error("Error in CompleteGerminate: " + ex);
            }

            Rand.PopState();

            germinator.Notify_RefreshDrawer();
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
