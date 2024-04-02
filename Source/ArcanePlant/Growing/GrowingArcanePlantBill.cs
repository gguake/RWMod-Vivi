using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum GrowingArcanePlantBillStage
    {
        Gathering,
        Growing,
        Complete,
    }

    public class GrowingArcanePlantBill : IExposable
    {
        public GrowingArcanePlantData Data
        {
            get
            {
                if (_data == null)
                {
                    _data = _recipeTargetDef.GetModExtension<GrowingArcanePlantData>();
                }
                return _data;
            }
        }
        [Unsaved]
        private GrowingArcanePlantData _data;

        public ThingDef RecipeTarget => _recipeTargetDef;
        public GrowingArcanePlantBillStage Stage
        {
            get
            {
                if (_billStartTicks < 0) { return GrowingArcanePlantBillStage.Gathering; }
                else if (GenTicks.TicksGame - _billStartTicks > TotalGrowTicks) { return GrowingArcanePlantBillStage.Complete; }

                return GrowingArcanePlantBillStage.Growing;
            }
        }

        public int TotalGrowTicks => (int)(Data.totalGrowDays * 60000) + 1;

        public float HealthPct => _health / Data.maxHealth;
        public float Health
        {
            get => _health;
            set
            {
                _health = Mathf.Clamp(value, 0f, Data.maxHealth);
            }
        }

        public float ManaPct => _mana / Data.maxMana;
        public float Mana
        {
            get => _mana;
            set
            {
                _mana = Mathf.Clamp(value, 0f, Data.maxMana);
                if (_badManaStartTicks >= 0)
                {
                    _badManaStartTicks = -1;
                }
            }
        }

        public Dictionary<ThingDef, int> Ingredients
        {
            get
            {
                if (_ingredients == null)
                {
                    _ingredients = Data.ingredients.ToDictionary(tdcc => tdcc.thingDef, tdcc => tdcc.count);
                }

                return _ingredients;
            }
        }
        private Dictionary<ThingDef, int> _ingredients;

        public IntRange ExpectedAmount
        {
            get
            {
                var data = Data;

                var cleanliness = Cleanliness;
                var minimum = data.baseAmount + (data.healthBonusAmountCurve?.Evaluate(0f) ?? 0f) + (data.cleanlinessBonusAmountCurve?.MinY ?? 0f);
                var maximum = data.baseAmount + (data.healthBonusAmountCurve?.Evaluate(Health) ?? 0f) + (cleanliness != null ? (data.cleanlinessBonusAmountCurve?.Evaluate(cleanliness.Value) ?? 0f) : 0f);

                return new IntRange((int)minimum, (int)maximum);
            }
        }

        private Building_ArcanePlantFarm _billOwner;
        private ThingDef _recipeTargetDef;

        private float _health;
        private float _mana;
        private float _cleanlinessBonus;

        private int _billStartTicks = -1;
        private int _lastManagementTicks = -1;

        private int _badTemperatureStartTicks = -1;
        private int _badGlowStartTicks = -1;
        private int _badManageStartTicks = -1;
        private int _badManaStartTicks = -1;

        public bool IsGoodTemperature => Data.optimalTemperatureRange.IncludesEpsilon(_billOwner.FarmTemperature);

        public bool IsGoodGlow => Data.optimalGlowRange.IncludesEpsilon(_billOwner.FarmGlow);

        public bool ManagementRequired => GenTicks.TicksGame - _lastManagementTicks >= Data.manageIntervalTicks;

        public float? Cleanliness
        {
            get
            {
                var room = _billOwner.GetRoom();
                if (room != null && room.Role != null && room.Role != RoomRoleDefOf.None && !room.PsychologicallyOutdoors)
                {
                    var cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
                    return cleanliness;
                }

                return null;
            }
        }

        public GrowingArcanePlantBill(Building_ArcanePlantFarm billOwner)
        {
            _billOwner = billOwner;
        }

        public GrowingArcanePlantBill(Building_ArcanePlantFarm billOwner, ThingDef target)
        {
            _billOwner = billOwner;
            _recipeTargetDef = target;

            _health = Data.maxHealth;
            _mana = 0f;
            _cleanlinessBonus = 0f;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref _recipeTargetDef, "recipeTargetDef");

            Scribe_Values.Look(ref _health, "health");
            Scribe_Values.Look(ref _mana, "mana");
            Scribe_Values.Look(ref _cleanlinessBonus, "cleanlinessBonus");

            Scribe_Values.Look(ref _billStartTicks, "billStartTicks");
            Scribe_Values.Look(ref _lastManagementTicks, "lastManagementTicks");

            Scribe_Values.Look(ref _badTemperatureStartTicks, "badTemperatureStartTicks");
            Scribe_Values.Look(ref _badGlowStartTicks, "badGlowStartTicks");
            Scribe_Values.Look(ref _badManageStartTicks, "badManageStartTicks");
            Scribe_Values.Look(ref _badManaStartTicks, "badManaStartTicks");
        }

        public void Start()
        {
            if (Stage != GrowingArcanePlantBillStage.Gathering) { return; }

            _billStartTicks = GenTicks.TicksGame;
            _lastManagementTicks = GenTicks.TicksGame;
        }

        public void Tick(int ticks)
        {
            var data = Data;
            var room = _billOwner.GetRoom();
            if (room != null && room.Role != null && room.Role != RoomRoleDefOf.None && !room.PsychologicallyOutdoors)
            {
                var cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
                if (data.cleanlinessBonusAmountCurve != null)
                {
                    _cleanlinessBonus += data.cleanlinessBonusAmountCurve.Evaluate(cleanliness) * ticks / TotalGrowTicks;
                }
            }
            else
            {
                if (data.cleanlinessBonusAmountCurve != null)
                {
                    _cleanlinessBonus += data.cleanlinessBonusAmountCurve.MinY * ticks / TotalGrowTicks;
                }
            }

            var consumed = data.consumedManaByDay * ticks / 60000f;
            var waterdropCount = room.ContainedThings(VVThingDefOf.VV_Waterdrops).Count(v => v.Spawned);
            if (waterdropCount > 0)
            {
                consumed /= Mathf.Log10(10 + waterdropCount * 5);
            }

            Mana -= consumed;

            var damaged = false;
            if (IsGoodTemperature)
            {
                if (_badTemperatureStartTicks >= 0)
                {
                    _badTemperatureStartTicks = -1;
                }
            }
            else
            {
                damaged = true;
                if (_badTemperatureStartTicks >= 0 && data.badTemperatureDamageByDayCurve != null)
                {
                    var badDays = (GenTicks.TicksGame - _badTemperatureStartTicks) / 60000f - data.badTemperatureThresholdDay;
                    if (badDays > 0f)
                    {
                        Health -= data.badTemperatureDamageByDayCurve.Evaluate(badDays) * ticks / 60000f;
                    }
                }
                else
                {
                    _badTemperatureStartTicks = GenTicks.TicksGame;
                }
            }

            if (IsGoodGlow)
            {
                if (_badGlowStartTicks >= 0)
                {
                    _badGlowStartTicks = -1;
                }
            }
            else
            {
                damaged = true;
                if (_badGlowStartTicks >= 0 && data.badGlowDamageByDayCurve != null)
                {
                    var badDays = (GenTicks.TicksGame - _badGlowStartTicks) / 60000f - data.badGlowThresholdDay;
                    if (badDays > 0f)
                    {
                        Health -= data.badGlowDamageByDayCurve.Evaluate(badDays) * ticks / 60000f;
                    }
                }
                else
                {
                    _badGlowStartTicks = GenTicks.TicksGame;
                }
            }

            if (!ManagementRequired)
            {
                if (_badManageStartTicks >= 0)
                {
                    _badManageStartTicks = -1;
                }
            }
            else
            {
                damaged = true;
                if (_badManageStartTicks >= 0 && data.badManageDamageByDayCurve != null)
                {
                    var badDays = (GenTicks.TicksGame - _badManageStartTicks) / 60000f - data.badManageThresholdDay;
                    if (badDays > 0f)
                    {
                        Health -= data.badManageDamageByDayCurve.Evaluate(badDays) * ticks / 60000f;
                    }
                }
                else
                {
                    _badManageStartTicks = GenTicks.TicksGame;
                }
            }

            if (Mana < Data.requiredMinMana)
            {
                if (_badManaStartTicks >= 0)
                {
                    _badManaStartTicks = -1;
                }
            }
            else
            {
                damaged = true;
                if (_badManaStartTicks >= 0 && data.badManaDamageByDayCurve != null)
                {
                    var badDays = (GenTicks.TicksGame - _badManaStartTicks) / 60000f - data.badManaThresholdDay;
                    if (badDays > 0f)
                    {
                        Health -= data.badManaDamageByDayCurve.Evaluate(badDays) * ticks / 60000f;
                    }
                }
                else
                {
                    _badManaStartTicks = GenTicks.TicksGame;
                }
            }

            if (!damaged)
            {
                Health += data.healthRegenNoDamagedByDays * ticks / 60000f;
            }

            if (Health <= 0f)
            {
                _billOwner.Notify_BillFailed();
            }
        }

        public void Manage()
        {
            _lastManagementTicks = GenTicks.TicksGame;
            if (_badManageStartTicks >= 0)
            {
                _badManageStartTicks = -1;
            }
        }
    }
}
