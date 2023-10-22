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

        public const int TotalScheduleCount = 6;
        public static int TotalGerminateDays = TotalScheduleCount;

        public bool CanStopInstantly => _germinateNextManageTick == 0;

        public int TicksToCompleteGerminate => Mathf.Max(_germinateCompleteTick - GenTicks.TicksGame, 0);

        public bool CanManageJob => Stage == GerminateStage.GerminateInProgress && GenTicks.TicksGame >= _germinateNextManageTick;
        public bool HasNextManageJob => _currentScheduleIndex < TotalScheduleCount && _germinateNextManageTick < _germinateCompleteTick;
        public int TicksToNextManageJob => Mathf.Max(_germinateNextManageTick - GenTicks.TicksGame, 0);

        public GerminateScheduleDef CurrentManageScheduleDef => Stage == GerminateStage.GerminateInProgress && _currentScheduleIndex < TotalScheduleCount ? _schedules[_currentScheduleIndex] : null;

        public float ExpectedGerminateBonusCount
        {
            get
            {
                return _schedules.Sum(v => v.bonusAddGerminateResult.LerpThroughRange(1f / TotalScheduleCount));
            }
        }
        public float ExpectedGerminateBonusSuccessChance
        {
            get
            {
                return 1f + _schedules.Sum(v => v.bonusMultiplierGerminateSuccessChance.LerpThroughRange(1f / TotalScheduleCount));
            }
        }
        public float ExpectedGerminateBonusRareChance
        {
            get
            {
                return 1f + _schedules.Sum(v => v.bonusMultiplierGerminateRareChance.LerpThroughRange(1f / TotalScheduleCount));
            }
        }
        public float ExpectedFixedGerminateMutateAnotherArtificialPlantChance
        {
            get
            {
                return _schedules.Sum(v => v.bonusMutateAnotherArtificialPlantChance.LerpThroughRange(1f / TotalScheduleCount));
            }
        }

        public bool IsFixedGerminate => _fixedGerminateResult != null;
        public ThingDef FixedGerminateResult => _fixedGerminateResult;
        private ThingDef _fixedGerminateResult;

        public GerminateStage Stage => _stage;
        private GerminateStage _stage = GerminateStage.None;

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

        public GerminateSchedule(ThingDef buildingDef, ThingDef fixedGerminateResult = null)
        {
            _buildingDef = buildingDef;
            _fixedGerminateResult = fixedGerminateResult;
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
            var schedule = new GerminateSchedule(_buildingDef, _fixedGerminateResult);
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
            Scribe_Defs.Look(ref _fixedGerminateResult, "fixedGerminateResult");
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

        public float GetGerminateQuality(int index)
        {
            return _scheduleQuality[index];
        }

        public void StartGerminate()
        {
            if (Stage != GerminateStage.None) { return; }

            _seed = Rand.Int;
            _germinateNextManageTick = GenTicks.TicksGame + GerminatorModExtension.scheduleCooldown;
            _germinateCompleteTick = GenTicks.TicksGame + 24 * 2500 * TotalGerminateDays;
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

            var min = _schedules[_currentScheduleIndex].curveQualityMinBySkillLevel.Evaluate(plantSkillLevel);
            var max = _schedules[_currentScheduleIndex].curveQualityMaxBySkillLevel.Evaluate(plantSkillLevel);
            AdvanceGerminateSchedule(germinator, Rand.Range(min, max));
        }

        public void AdvanceGerminateSchedule(Building_SeedlingGerminator germinator, float quality = 1f)
        {
            _scheduleQuality[_currentScheduleIndex] = Mathf.Clamp(quality, 0.001f, 1f);

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

            var qualityDict = new Dictionary<GerminateScheduleDef, float>();
            for (int i = 0; i < TotalScheduleCount; ++i)
            {
                var def = _schedules[i];
                if (def == VVGerminateScheduleDefOf.VV_DoNothing)
                {
                    continue;
                }

                var quality = Mathf.Clamp01(_scheduleQuality[i] / TotalScheduleCount);
                if (!qualityDict.ContainsKey(def))
                {
                    qualityDict.Add(def, quality);
                }
                else
                {
                    qualityDict[def] += quality;
                }
            }

            // 결과가 음수라면 퀄리티 값을 반대로 적용
            float bonusProductCount = qualityDict.Sum(kv => kv.Key.bonusAddGerminateResult.LerpThroughRange(kv.Key.bonusAddGerminateResult.max >= 0 ? kv.Value : 1f - kv.Value));
            float bonusSuccessChanceMultiplier = 1f + qualityDict.Sum(kv => kv.Key.bonusMultiplierGerminateSuccessChance.LerpThroughRange(kv.Key.bonusAddGerminateResult.max >= 0 ? kv.Value : 1f - kv.Value));
            float bonusRareChanceMultiplier = 1f + qualityDict.Sum(kv => kv.Key.bonusMultiplierGerminateRareChance.LerpThroughRange(kv.Key.bonusAddGerminateResult.max >= 0 ? kv.Value : 1f - kv.Value));
            float bonusMutateAnotherArtificialPlantChance = qualityDict.Sum(kv => kv.Key.bonusMutateAnotherArtificialPlantChance.LerpThroughRange(kv.Key.bonusAddGerminateResult.max >= 0 ? kv.Value : 1f - kv.Value));

            var germinatorData = _buildingDef.GetModExtension<GerminatorModExtension>();
            if (germinatorData == null)
            {
                throw new Exception("germinatorData is null");
            }

            var table = new List<(float weight, ThingDef thingDef)>();
            Rand.PushState();
            try
            {
                Rand.Seed = _seed;

                var successChance = Mathf.Clamp01((IsFixedGerminate ? germinatorData.fixedGerminateSuccessChance : germinatorData.germinateSuccessChance) * bonusSuccessChanceMultiplier);
                
                var expectProductCount = (IsFixedGerminate ? Rand.Range(2, 4) : Rand.Range(3, 5)) + bonusProductCount;
                var actualProductCount = (int)expectProductCount;

                if (expectProductCount > 0 && Rand.Chance(expectProductCount - actualProductCount))
                {
                    actualProductCount++;
                }

                if (Rand.Chance(successChance) && actualProductCount > 0)
                {
                    if (IsFixedGerminate)
                    {
                        if (Rand.Chance(bonusMutateAnotherArtificialPlantChance))
                        {
                            // 변이한 경우
                            foreach (var plant in ArtificialPlant.AllArtificialPlantDefs)
                            {
                                var plantData = plant.GetModExtension<ArtificialPlantModExtension>();
                                table.Add((plantData.germinateWeight, plant));
                            }
                        }
                        else
                        {
                            // 고정 배양의 경우 결과 고정
                            table.Add((99999f, FixedGerminateResult));
                        }
                    }
                    else
                    {
                        // 희귀 판정에 따라 결과 테이블 설정
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
                }
                else
                {
                    // 실패시 랜덤 결과
                    table.Add((germinatorData.germinateFailureCriticalWeight, null));

                    var failureCropsWeightSum = germinatorData.germinateFailureCropsTable.Sum(tdc => tdc.count);
                    for (int i = 0; i < germinatorData.germinateFailureCropsTable.Count; ++i)
                    {
                        var tdc = germinatorData.germinateFailureCropsTable[i];
                        table.Add((tdc.count / (float)failureCropsWeightSum * germinatorData.germinateFailureCropsWeight, tdc.thingDef));
                    }
                }

                _stage = GerminateStage.GerminateComplete;

                var result = table.RandomElementByWeight(v => v.weight).thingDef;
                germinator.Notify_ScheduleComplete(result, result != null ? actualProductCount : 0);
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
