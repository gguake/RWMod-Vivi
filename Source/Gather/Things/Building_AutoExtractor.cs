using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Building_AutoHoneyExtractor : Building
    {
        public const float DailyExtractRawHoneyAmount = 300;
        public const float ForceEjectTick = 5000;

        public CompRefuelable RefuelableComp
        {
            get
            {
                if (_compRefuelable == null)
                {
                    _compRefuelable = GetComp<CompRefuelable>();
                }
                return _compRefuelable;
            }
        }
        private CompRefuelable _compRefuelable;

        public CompPowerTrader PowerTraderComp
        {
            get
            {
                if (_compPowerTrader == null)
                {
                    _compPowerTrader = GetComp<CompPowerTrader>();
                }
                return _compPowerTrader;
            }
        }
        private CompPowerTrader _compPowerTrader;

        public float FilterEfficiencyRatio
        {
            get
            {
                if (_filterEfficiencyRatio == null)
                {
                    var rawHoneyCount = VVRecipeDefOf.VV_FilteringHoney.ingredients.FirstOrDefault(ic => ic.filter.AllowedThingDefs.Any(v => v == VVThingDefOf.VV_RawHoney))?.GetBaseCount() ?? 1;
                    var filteredCount = VVRecipeDefOf.VV_FilteringHoney.products.FirstOrDefault(v => v.thingDef == VVThingDefOf.VV_FilteredHoney)?.count ?? 2;

                    _filterEfficiencyRatio = filteredCount / rawHoneyCount * 1.2f;
                }
                return _filterEfficiencyRatio.Value;
            }
        }
        private float? _filterEfficiencyRatio;

        private float _filteredHoneyAmount;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _filteredHoneyAmount, "filteredHoneyAmount");
        }

        public override void TickLong()
        {
            base.TickLong();

            if (!PowerTraderComp.Off && RefuelableComp.HasFuel && _filteredHoneyAmount < VVThingDefOf.VV_FilteredHoney.stackLimit)
            {
                var rawHoneyAmount = Mathf.Clamp(GenTicks.TickLongInterval * DailyExtractRawHoneyAmount / 60000f, 0f, RefuelableComp.Fuel);
                RefuelableComp.ConsumeFuel(rawHoneyAmount);

                _filteredHoneyAmount += rawHoneyAmount * FilterEfficiencyRatio;
                if (_filteredHoneyAmount >= 1)
                {
                    var stackCount = Mathf.Clamp((int)_filteredHoneyAmount, 1, VVThingDefOf.VV_FilteredHoney.stackLimit);

                    var honey = ThingMaker.MakeThing(VVThingDefOf.VV_FilteredHoney);
                    honey.stackCount = stackCount;

                    if (!GenPlace.TryPlaceThing(honey, InteractionCell, Map, ThingPlaceMode.Direct, placedAction: (_, actualPlaced) => { _filteredHoneyAmount -= actualPlaced; }))
                    {
                        honey.Destroy();
                    }
                }
            }
        }
    }
}
