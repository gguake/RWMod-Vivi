using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ArcanePlant_Seedling : ArcanePlant
    {
        public SeedlingExtension Extension
        {
            get
            {
                if (_seedlingExtension == null)
                {
                    _seedlingExtension = def.GetModExtension<SeedlingExtension>();
                }
                return _seedlingExtension;
            }
        }
        private SeedlingExtension _seedlingExtension;

        public CompRefuelable RefuelableComp
        {
            get
            {
                if (_refuelableComp == null)
                {
                    _refuelableComp = GetComp<CompRefuelable>();
                }
                return _refuelableComp;
            }
        }
        private CompRefuelable _refuelableComp;

        public ThingDef MaturePlantDef => _maturePlantDef;
        private ThingDef _maturePlantDef;

        private ThingDef _seedDef;

        public float GrowthBonusCached
        {
            get
            {
                if (_growthBonusCached == null)
                {
                    var growthBonus = 1f;
                    foreach (var cell in GenAdjFast.AdjacentCellsCardinal(Position))
                    {
                        if (!cell.InBounds(Map)) { continue; }

                        var accelerator = cell.GetFirstThingWithComp<CompSeedlingGrowthAccelerator>(Map);
                        if (accelerator != null && accelerator.TryGetComp<CompSeedlingGrowthAccelerator>(out var comp))
                        {
                            growthBonus *= comp.Props.growthBonus;
                        }
                    }

                    _growthBonusCached = growthBonus;
                }
                return _growthBonusCached.Value;
            }
        }
        private float? _growthBonusCached;

        public float Growth
        {
            get => _growth;
            set
            {
                _growth = Mathf.Clamp01(value);
                if (Spawned)
                {
                    DirtyMapMesh(Map);
                }
            }
        }
        private float _growth;

        public float GrowthRate
        {
            get
            {
                return Extension.dailyGrowth / 60000f * ((thingIDNumber % 30) + 85) / 100f * GrowthBonusCached;
            }
        }

        private int _lastZeroNutritionTick;

        public override string LabelNoCount
        {
            get
            {
                if (MaturePlantDef != null)
                {
                    return LocalizeString_Etc.VV_Thing_Seedling.Translate(MaturePlantDef.LabelCap);
                }
                else
                {
                    return base.LabelNoCount;
                }
            }
        }

        public override int? OverrideGraphicIndex
        {
            get
            {
                return _growth >= 0.45f ? (thingIDNumber % 4) + 4 : (thingIDNumber % 4);
            }
        }

        public ArcanePlant_Seedling()
        {
        }

        public override void PostMake()
        {
            base.PostMake();

            _growth = Rand.Range(0.01f, 0.03f);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var map = Map;
            var position = Position;

            base.DeSpawn(mode);
            if (mode == DestroyMode.Deconstruct && _seedDef != null)
            {
                if (GenSpawn.CanSpawnAt(_seedDef, position, map, canWipeEdifices: false))
                {
                    GenSpawn.TrySpawn(_seedDef, position, map, out _, canWipeEdifices: false);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Defs.Look(ref _maturePlantDef, "maturePlantDef");

            Scribe_Values.Look(ref _growth, "growth");
            Scribe_Values.Look(ref _lastZeroNutritionTick, "lastZeroNutritionTick");
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (Spawned)
            {
                if (RefuelableComp.HasFuel)
                {
                    if (_lastZeroNutritionTick > 0)
                    {
                        _lastZeroNutritionTick = 0;
                    }

                    if (ManaComp.Active)
                    {
                        Growth += GrowthRate * delta;
                        if (Growth >= 1f)
                        {
                            var map = Map;
                            var position = Position;
                            var rotation = Rotation;
                            var faction = Faction;

                            var plantDef = MaturePlantDef;
                            if (plantDef == null)
                            {
                                try
                                {
                                    Rand.PushState(thingIDNumber);

                                    if (Rand.Chance(0.015f))
                                    {
                                        plantDef = VVThingDefOf.VV_Everflower;
                                    }
                                    else
                                    {
                                        plantDef = AllGeneratableArcanePlantDefs.RandomElement();
                                    }
                                }
                                finally
                                {
                                    Rand.PopState();
                                }
                            }

                            var healthPct = (float)HitPoints / MaxHitPoints;
                            Destroy(DestroyMode.WillReplace);

                            var plant = ThingMaker.MakeThing(plantDef);
                            plant.SetFaction(faction ?? Faction.OfPlayerSilentFail);
                            plant.HitPoints = Mathf.Clamp(Mathf.CeilToInt(healthPct * plant.MaxHitPoints), 1, plant.MaxHitPoints);
                            GenSpawn.Spawn(plant, position, map, rotation);
                            return;
                        }
                    }
                }
                else
                {
                    if (_lastZeroNutritionTick == 0)
                    {
                        _lastZeroNutritionTick = GenTicks.TicksGame;
                    }
                    else if (GenTicks.TicksGame - _lastZeroNutritionTick > Extension.ticksToDamageFromZeroNutrition)
                    {
                        if (GenTicks.TicksGame % 7 == 0)
                        {
                            if (HitPoints == 1)
                            {
                                Messages.Message(
                                    LocalizeString_Message.VV_Message_ArcanePlantSeedlingDeadCausedNoNutrition.Translate(LabelCap), 
                                    MessageTypeDefOf.NegativeHealthEvent);

                                Destroy();
                                return;
                            }
                            else
                            {
                                HitPoints--;
                                _lastDamagedTick = GenTicks.TicksGame;
                            }
                        }
                    }
                }
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode)
            {
                Command_Action command_addGrowth = new Command_Action();
                command_addGrowth.defaultLabel = "DEV: Growth +10%";
                command_addGrowth.action = () =>
                {
                    Growth += 0.1f;
                };

                yield return command_addGrowth;
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            sb.AppendInNewLine("PercentGrowth".Translate(Growth.ToStringPercent()));

            if (!RefuelableComp.HasFuel)
            {
                sb.Append(", ");
                sb.Append(LocalizeString_Inspector.VV_Inspector_SeedlingOutOfFertilzier.Translate());
            }

            if (!ManaComp.Active)
            {
                sb.Append(", ");
                sb.Append(LocalizeString_Inspector.VV_Inspector_SeedlingOutOfMana.Translate());
            }

            return sb.ToString();
        }

        public void SetMaturePlant(ThingDef seedDef, ThingDef plantDef)
        {
            _seedDef = seedDef;
            _maturePlantDef = plantDef;
        }

        public void ResetGrowthBonusCache()
        {
            _growthBonusCached = null;
        }
    }
}
