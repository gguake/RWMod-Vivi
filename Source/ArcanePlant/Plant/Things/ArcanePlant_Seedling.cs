using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ArcanePlant_Seedling : ArcanePlant, IThingHolder
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

        private ThingOwner _innerContainer;

        public ThingDef MaturePlantDef => _innerContainer.Count > 0 ? _innerContainer[0].TryGetComp<CompArcaneSeed>()?.Props.targetPlantDef : null;

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
                return Extension.dailyGrowth / 60000f * ((thingIDNumber % 30) + 85) / 100f;
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
            _innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
        }

        public override void PostMake()
        {
            base.PostMake();

            _growth = Rand.Range(0.01f, 0.03f);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            _innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            _innerContainer.ClearAndDestroyContents();

            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            _innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            _innerContainer.ClearAndDestroyContents();

            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);

            Scribe_Values.Look(ref _growth, "growth");
            Scribe_Values.Look(ref _lastZeroNutritionTick, "lastZeroNutritionTick");
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (Spawned)
            {
                if (RefuelableComp.Fuel > 0f)
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

                            var plantDef = MaturePlantDef;
                            if (plantDef == null)
                            {
                                try
                                {
                                    Rand.PushState(thingIDNumber);
                                    plantDef = AllArcanePlantDefs.Except(VVThingDefOf.VV_Everflower).RandomElement();
                                }
                                finally
                                {
                                    Rand.PopState();
                                }
                            }

                            if (_innerContainer.Count > 0)
                            {
                                _innerContainer.ClearAndDestroyContents();
                            }

                            var healthPct = (float)HitPoints / MaxHitPoints;
                            Destroy(DestroyMode.WillReplace);

                            var plant = ThingMaker.MakeThing(plantDef);
                            plant.SetFaction(Faction ?? Faction.OfPlayer);
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

                                _innerContainer.ClearAndDestroyContents();
                                Destroy();
                                return;
                            }
                            else
                            {
                                HitPoints--;
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

            return sb.ToString();
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }
    }
}
