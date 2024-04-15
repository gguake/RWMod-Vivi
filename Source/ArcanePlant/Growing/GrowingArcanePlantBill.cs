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
                else if (GenTicks.TicksGame - _billStartTicks > TotalGrowthTicks) { return GrowingArcanePlantBillStage.Complete; }

                return GrowingArcanePlantBillStage.Growing;
            }
        }

        public int TotalGrowthTicks => (int)(Data.totalGrowDays * 60000) + 1;
        public float TotalGrowthPct => (float)(GenTicks.TicksGame - _billStartTicks) / TotalGrowthTicks;

        public float HealthPct => _health / Data.maxHealth;
        public float Health
        {
            get => _health;
            set
            {
                _health = Mathf.Clamp(value, 0f, Data.maxHealth);
            }
        }

        public float LastHealthChangeOffset { get; private set; }

        public float ManaPct => _mana / Data.maxMana;
        public float Mana
        {
            get => _mana;
            set
            {
                _mana = Mathf.Clamp(value, 0f, Data.maxMana);
            }
        }

        public float ManagePct => 1f - Mathf.Min(Data.RealManageIntervalTicks, GenTicks.TicksGame - _lastManagementTicks) / (float)Data.RealManageIntervalTicks;

        public float TemperatureSeverity => _temperatureSeverity;

        public float GlowSeverity => _glowSeverity;

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

        private float _temperatureSeverity;
        private float _glowSeverity;

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
            _mana = Data.maxMana;
            _cleanlinessBonus = 0f;

            _temperatureSeverity = 1f;
            _glowSeverity = 1f;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref _recipeTargetDef, "recipeTargetDef");

            Scribe_Values.Look(ref _health, "health");
            Scribe_Values.Look(ref _mana, "mana");
            Scribe_Values.Look(ref _cleanlinessBonus, "cleanlinessBonus");

            Scribe_Values.Look(ref _billStartTicks, "billStartTicks");
            Scribe_Values.Look(ref _lastManagementTicks, "lastManagementTicks");
            Scribe_Values.Look(ref _temperatureSeverity, "temperatureSeverity");
            Scribe_Values.Look(ref _glowSeverity, "glowSeverity");
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

            RefreshPlantNeeds(data, ticks);

            var damage = CalcPlantDamageFromNeeds(data, ticks);
            if (damage > 0f)
            {
                LastHealthChangeOffset = -damage;
            }
            else
            {
                if (HealthPct < 1f)
                {
                    var heal = data.healthRegenNoDamagedByDays * ticks / 60000f;
                    LastHealthChangeOffset = heal;
                }
                else
                {
                    LastHealthChangeOffset = 0f;
                }
            }

            if (LastHealthChangeOffset != 0f)
            {
                Health += LastHealthChangeOffset;
            }

            if (Health <= 0f)
            {
                _billOwner.Notify_BillFailed();
            }
        }

        public void Manage(int? forceTick = null)
        {
            _lastManagementTicks = forceTick == null ? GenTicks.TicksGame : forceTick.Value;
        }

        private void RefreshPlantNeeds(GrowingArcanePlantData data, int ticks)
        {
            var room = _billOwner.GetRoom();
            if (room != null && room.Role != null && room.Role != RoomRoleDefOf.None && !room.PsychologicallyOutdoors)
            {
                var cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
                if (data.cleanlinessBonusAmountCurve != null)
                {
                    _cleanlinessBonus += data.cleanlinessBonusAmountCurve.Evaluate(cleanliness) * ticks / TotalGrowthTicks;
                }
            }
            else
            {
                if (data.cleanlinessBonusAmountCurve != null)
                {
                    _cleanlinessBonus += data.cleanlinessBonusAmountCurve.MinY * ticks / TotalGrowthTicks;
                }
            }

            var consumed = data.consumedManaByDay * ticks / 60000f;
            var waterdropCount = room.ContainedThings(VVThingDefOf.VV_Waterdrops).Count(v => v.Spawned);
            if (waterdropCount > 0)
            {
                consumed /= Mathf.Log10(10 + waterdropCount * 5);
            }

            Mana -= consumed;

            if (IsGoodTemperature)
            {
                if (_temperatureSeverity < 1f)
                {
                    _temperatureSeverity = Mathf.Clamp01(_temperatureSeverity + ticks / 60000f);
                }
            }
            else
            {
                _temperatureSeverity = Mathf.Clamp01(_temperatureSeverity - ticks / 45000f);
            }

            if (IsGoodGlow)
            {
                if (_glowSeverity < 1f)
                {
                    _glowSeverity = Mathf.Clamp01(_glowSeverity + ticks / 60000f);
                }
            }
            else
            {
                _glowSeverity = Mathf.Clamp01(_glowSeverity - ticks / 45000f);
            }

        }

        private float CalcPlantDamageFromNeeds(GrowingArcanePlantData data, int ticks)
        {
            float damage = 0f;
            if (data.manaSensitivity > GrowingArcanePlantSensitivity.None)
            {
                damage += data.manaSensitivity.CalcTickDamage(ManaPct) * ticks;
            }

            if (data.manageSensitivity > GrowingArcanePlantSensitivity.None)
            {
                damage += data.manageSensitivity.CalcTickDamage(ManagePct) * ticks;
            }

            if (data.temperatureSensitivity > GrowingArcanePlantSensitivity.None)
            {
                damage += data.temperatureSensitivity.CalcTickDamage(_temperatureSeverity) * ticks;
            }

            if (data.glowSensitivity > GrowingArcanePlantSensitivity.None)
            {
                damage += data.glowSensitivity.CalcTickDamage(_glowSeverity) * ticks;
            }

            return damage;
        }
    }
}
