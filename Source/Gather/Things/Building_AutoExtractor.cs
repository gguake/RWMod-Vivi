using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Building_AutoHoneyExtractor : Building, IThingHolder
    {
        private static readonly Texture2D EjectCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_EjectFiltered");

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

        private ThingOwner<Thing> _innerContainer;

        public Building_AutoHoneyExtractor()
        {
            _innerContainer = new ThingOwner<Thing>(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _filteredHoneyAmount, "filteredHoneyAmount");
            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
        }

        public override void TickLong()
        {
            base.TickLong();


            if (!PowerTraderComp.Off)
            {
                if (RefuelableComp.HasFuel)
                {
                    var rawHoneyAmount = Mathf.Clamp(GenTicks.TickLongInterval * DailyExtractRawHoneyAmount / 60000f, 0f, RefuelableComp.Fuel);
                    RefuelableComp.ConsumeFuel(rawHoneyAmount);

                    _filteredHoneyAmount += rawHoneyAmount * FilterEfficiencyRatio;
                }

                if (_filteredHoneyAmount >= 1f)
                {
                    var honey = ThingMaker.MakeThing(VVThingDefOf.VV_FilteredHoney);
                    honey.stackCount = (int)_filteredHoneyAmount;
                    _filteredHoneyAmount -= honey.stackCount;

                    _innerContainer.TryAdd(honey);
                }

                if (_innerContainer.TotalStackCountOfDef(VVThingDefOf.VV_FilteredHoney) >= VVThingDefOf.VV_FilteredHoney.stackLimit || RefuelableComp.Fuel < 1f)
                {
                    _innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
                }
            }
            else
            {
                if (_innerContainer.Count > 0)
                {
                    _innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
                }
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace)
            {
                if (_innerContainer.Count > 0)
                {
                    _innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
                    _innerContainer.ClearAndDestroyContents(mode);
                }
            }

            base.DeSpawn(mode);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Spawned && _innerContainer.Count > 0)
            {
                var commandEject = new Command_Action();
                commandEject.defaultLabel = LocalizeString_Command.VV_Command_EjectFilteredHoney.Translate();
                commandEject.defaultDesc = LocalizeString_Command.VV_Command_EjectFilteredHoneyDesc.Translate();
                commandEject.icon = EjectCommandTex;
                commandEject.action = () =>
                {
                    _innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
                };

                yield return commandEject;
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (_innerContainer.Count > 0)
            {
                sb.AppendInNewLine("StoresThings".Translate());
                sb.Append(": ");
                sb.Append($"{VVThingDefOf.VV_FilteredHoney.LabelCap} x{_innerContainer.TotalStackCountOfDef(VVThingDefOf.VV_FilteredHoney)}");
            }

            return sb.ToString();
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }
    }
}
