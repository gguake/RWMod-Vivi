using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GrowArcanePlantBill : IExposable
    {
        public GrowArcanePlantData Data
        {
            get
            {
                if (_data == null)
                {
                    _data = _targetDef.GetModExtension<GrowArcanePlantData>();
                }
                return _data;
            }
        }
        [Unsaved]
        private GrowArcanePlantData _data;

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

        public bool IsStarted => _billStartTicks >= 0;

        public bool IsCompleted
        {
            get
            {
                if (_billStartTicks < 0) { return false; }
                return GenTicks.TicksGame - _billStartTicks > TotalGrowTicks;
            }
        }

        public IntRange ExpectedAmount
        {
            get
            {
                var data = Data;
                var expected = data.baseAmount + (data.healthBonusAmountCurve?.Evaluate(HealthPct) ?? 0f) + _cleanlinessBonus;
                var expectedInt = (int)expected;

                return new IntRange(expectedInt, expectedInt + (expected - expectedInt) > 0.0001f ? 1 : 0);
            }
        }
        public float Amount => IsCompleted ? ExpectedAmount.RandomInRange : 0;

        private Building_ArcanePlantFarm _billOwner;
        private ThingDef _targetDef;

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

        public bool IsLowMana => Mana < Data.requiredMinMana;

        public GrowArcanePlantBill(Building_ArcanePlantFarm billOwner)
        {
            _billOwner = billOwner;
        }

        public GrowArcanePlantBill(Building_ArcanePlantFarm billOwner, ThingDef target)
        {
            _billOwner = billOwner;
            _targetDef = target;

            _health = Data.maxHealth;
            _mana = 0f;
            _cleanlinessBonus = 0f;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref _targetDef, "targetDef");

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
            if (IsStarted) { return; }

            _billStartTicks = GenTicks.TicksGame;
            _lastManagementTicks = GenTicks.TicksGame;
        }

        public void Tick(int ticks)
        {
            if (!IsStarted)
            {
                return;
            }
            else if (IsCompleted)
            {
                _billOwner.Notify_BillCompleted();
                return;
            }

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

            if (!IsLowMana)
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
