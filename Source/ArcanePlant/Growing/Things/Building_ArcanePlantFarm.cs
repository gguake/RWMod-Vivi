using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Building_ArcanePlantFarm : Building, IThingHolder, INotifyHauledTo
    {
        public GrowingArcanePlantBill Bill => _bill;

        public float FarmTemperature => AmbientTemperature;

        public float FarmGlow
        {
            get
            {
                var glowGrid = Map.glowGrid;
                return this.OccupiedRect().Max(v => glowGrid.GroundGlowAt(v));
            }
        }

        public IEnumerable<ThingDefCount> RequiredThings
        {
            get
            {
                if (_bill == null) { yield break; }

                switch (_bill.Stage)
                {
                    case GrowingArcanePlantBillStage.Gathering:
                        foreach (var kv in _bill.Ingredients)
                        {
                            var requiredCount = kv.Value - _innerContainer.TotalStackCountOfDef(kv.Key);
                            if (requiredCount > 0)
                            {
                                yield return new ThingDefCount(kv.Key, requiredCount);
                            }
                        }
                        yield break;

                    case GrowingArcanePlantBillStage.Growing:
                        if (_bill.ManaPct <= _manaRefillPct)
                        {
                            var requiredFertilizerCount = Mathf.FloorToInt((_bill.Data.maxMana - _bill.Mana) / ArcanePlant.ManaByFertilizer);
                            if (requiredFertilizerCount <= 0) { yield break; }

                            yield return new ThingDefCount(VVThingDefOf.VV_Fertilizer, requiredFertilizerCount);
                        }
                        yield break;
                }
            }
        }

        private GrowingArcanePlantBill _bill;
        private ThingOwner _innerContainer;
        private float _manaRefillPct;

        public Building_ArcanePlantFarm()
        {
            _innerContainer = new ThingOwner<Thing>(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _bill, "bill", this);
            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref _manaRefillPct, "manaRefillPct");
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            EjectContents();
            base.DeSpawn(mode);
        }

        private static readonly Texture2D StartCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_Germinate");
        private static readonly Texture2D CancelCommandTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (_bill == null)
            {
                var commandNewBill = new Command_Action();
                commandNewBill.defaultLabel = LocalizeString_Command.CommandNewGrowArcanePlantBill.Translate();
                commandNewBill.defaultDesc = LocalizeString_Command.CommandNewGrowArcanePlantBillDesc.Translate();
                commandNewBill.icon = StartCommandTex;
                commandNewBill.action = () =>
                {
                    Find.WindowStack.Add(new Dialog_StartGrowingArcanePlant(this, (bill) => _bill = bill));
                };

                yield return commandNewBill;
            }
            else
            {
                var commandCancelBill = new Command_Action();
                commandCancelBill.defaultLabel = LocalizeString_Command.CommandCancelGrowArcanePlantBill.Translate();
                commandCancelBill.defaultDesc = LocalizeString_Command.CommandCancelGrowArcanePlantBillDesc.Translate();
                commandCancelBill.icon = CancelCommandTex;
                commandCancelBill.action = () =>
                {
                    switch (_bill.Stage)
                    {
                        case GrowingArcanePlantBillStage.Gathering:
                            EjectContents();
                            _bill = null;
                            break;

                        case GrowingArcanePlantBillStage.Growing:
                            Find.WindowStack.Add(new Dialog_Confirm(LocalizeString_Dialog.VV_DialogCancelGrowingArcanePlantBillInGrowing.Translate(), () =>
                            {
                                _bill = null;
                            }));
                            break;
                    }
                };

                yield return commandCancelBill;

                if (_bill.Stage == GrowingArcanePlantBillStage.Growing && DebugSettings.godMode)
                {
                    var commandDecreaseLife = new Command_Action();
                    commandDecreaseLife.defaultLabel = "Decrease Life -10%";
                    commandDecreaseLife.action = () =>
                    {
                        _bill.Health -= _bill.Data.maxHealth * 0.1f;
                    };
                    yield return commandDecreaseLife;

                    var commandDecreaseMana = new Command_Action();
                    commandDecreaseMana.defaultLabel = "Decrease Mana -10%";
                    commandDecreaseMana.action = () =>
                    {
                        _bill.Mana -= _bill.Data.maxMana * 0.1f;
                    };
                    yield return commandDecreaseMana;

                    var commandMakeManageZero = new Command_Action();
                    commandMakeManageZero.defaultLabel = "Make Management 0%";
                    commandMakeManageZero.action = () =>
                    {
                        _bill.Manage(1);
                    };
                    yield return commandMakeManageZero;
                }
            }
        }

        public override IEnumerable<InspectTabBase> GetInspectTabs()
        {
            return base.GetInspectTabs();
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (_bill?.Stage == GrowingArcanePlantBillStage.Gathering)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_GrowingArcanePlantReady.Translate(_bill.RecipeTarget.LabelCap));

                var requiredIngredients = RequiredThings.ToDictionary(tdc => tdc.ThingDef, tdc => tdc.Count);
                foreach (var kv in _bill.Ingredients)
                {
                    var holdings = kv.Value - (requiredIngredients.ContainsKey(kv.Key) ? requiredIngredients[kv.Key] : 0);
                    sb.AppendInNewLine($"{kv.Key.LabelCap}: {holdings}/{kv.Value}");
                }
            }

            return sb.ToString();
        }

        public override void Tick()
        {
            base.Tick();
            Tick(1);
        }

        public override void TickRare()
        {
            base.TickRare();
            Tick(GenTicks.TickRareInterval);
        }

        public override void TickLong()
        {
            base.TickLong();
            Tick(GenTicks.TickLongInterval);
        }

        private void Tick(int ticks)
        {
            if (Spawned && _bill != null)
            {
                switch (_bill.Stage)
                {
                    case GrowingArcanePlantBillStage.Gathering:
                        if (!RequiredThings.Any())
                        {
                            _innerContainer.ClearAndDestroyContents();
                            _bill.Start();
                        }
                        break;

                    case GrowingArcanePlantBillStage.Growing:
                        _bill.Tick(ticks);
                        break;

                    case GrowingArcanePlantBillStage.Complete:
                        var successChance = _bill.Data.successChanceCurve.Evaluate(_bill.Health);
                        if (Rand.ChanceSeeded(successChance, GenTicks.TicksGame))
                        {
                            var amount = _bill.Data.baseAmount;
                            if (_bill.Data.healthBonusAmountCurve != null)
                            {
                                amount += _bill.Data.healthBonusAmountCurve.Evaluate(_bill.Health);
                            }

                            var cleanliness = _bill.Cleanliness;
                            if (_bill.Data.cleanlinessBonusAmountCurve != null && cleanliness != null)
                            {
                                amount += _bill.Data.cleanlinessBonusAmountCurve.Evaluate(cleanliness.Value);
                            }

                            var amountInt = (int)amount + (Rand.ChanceSeeded(amount - (int)amount, GenTicks.TicksGame) ? 1 : 0);
                            var thing = ThingMaker.MakeThing(_bill.RecipeTarget);
                            thing.stackCount = amountInt;

                            if (!GenPlace.TryPlaceThing(thing, Position, Map, ThingPlaceMode.Direct))
                            {
                                if (!GenPlace.TryPlaceThing(thing, Position, Map, ThingPlaceMode.Near))
                                {
                                    Log.Error($"failed to place arcane plant from farm");
                                }
                            }

                            Messages.Message(LocalizeString_Message.VV_MessageCompleteGrowingArcanePlant.Translate(_bill.RecipeTarget.LabelCap, amountInt), this, MessageTypeDefOf.PositiveEvent);
                            _bill = null;
                        }
                        else
                        {
                            Notify_BillFailed();
                        }
                        break;
                }
            }
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerContainer;
        }

        public void Notify_HauledTo(Pawn hauler, Thing thing, int count)
        {
            if (_bill == null)
            {
                DropAll();
                return;
            }

            switch (_bill.Stage)
            {
                case GrowingArcanePlantBillStage.Gathering:
                    if (!RequiredThings.Any())
                    {
                        _innerContainer.ClearAndDestroyContents();
                        _bill.Start();
                    }
                    break;

                case GrowingArcanePlantBillStage.Growing:
                    if (thing.def == VVThingDefOf.VV_Fertilizer)
                    {
                        _innerContainer.ClearAndDestroyContents();
                        _bill.Mana += ArcanePlant.ManaByFertilizer * count;
                        break;
                    }
                    else
                    {
                        DropAll();
                    }
                    break;

                case GrowingArcanePlantBillStage.Complete:
                    DropAll();
                    break;
            }

            void DropAll()
            {
                _innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            }
        }

        public void Notify_BillFailed()
        {
            Messages.Message(LocalizeString_Message.VV_MessageFailedGrowingArcanePlant.Translate(_bill.RecipeTarget.LabelCap), this, MessageTypeDefOf.NegativeEvent);
            _bill = null;
        }

        private void EjectContents()
        {
            _innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
        }
    }
}
