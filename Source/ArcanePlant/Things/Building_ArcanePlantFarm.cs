using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace VVRace
{
    public class Building_ArcanePlantFarm : Building, IThingHolder, INotifyHauledTo
    {
        public GrowArcanePlantBill Bill => _bill;

        public float FarmTemperature => AmbientTemperature;

        public float FarmGlow
        {
            get
            {
                var glowGrid = Map.glowGrid;
                return this.OccupiedRect().Average(v => glowGrid.GroundGlowAt(v));
            }
        }

        public IEnumerable<ThingDefCount> RequiredIngredients
        {
            get
            {
                if (_bill == null || _bill.IsStarted) { yield break; }

                foreach (var kv in _bill.Ingredients)
                {
                    var requiredCount = kv.Value - _innerContainer.TotalStackCountOfDef(kv.Key);
                    if (requiredCount > 0)
                    {
                        yield return new ThingDefCount(kv.Key, requiredCount);
                    }
                }
            }
        }

        private GrowArcanePlantBill _bill;
        public ThingOwner _innerContainer;

        public Building_ArcanePlantFarm()
        {
            _innerContainer = new ThingOwner<Thing>(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _bill, "bill", this);
            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            EjectContents();
            base.DeSpawn(mode);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (_bill == null)
            {
                var commandNewBill = new Command_Action();
                commandNewBill.defaultLabel = LocalizeTexts.CommandNewGrowArcanePlantBill;
                commandNewBill.defaultDesc = LocalizeTexts.CommandNewGrowArcanePlantBillDesc;
                commandNewBill.action = () =>
                {
                    Find.WindowStack.Add(new Dialog_StartGrowingArcanePlant(
                        this, 
                        (bill) =>
                        {
                            _bill = bill;
                        }));
                };

                yield return commandNewBill;
            }
            else
            {
                var commandCancelBill = new Command_Action();
                commandCancelBill.defaultLabel = LocalizeTexts.CommandCancelGrowArcanePlantBill;
                commandCancelBill.defaultDesc = LocalizeTexts.CommandCancelGrowArcanePlantBillDesc;
                commandCancelBill.action = () =>
                {
                    if (_bill.IsStarted)
                    {

                    }
                    else
                    {
                        EjectContents();
                        _bill = null;
                    }
                };

                yield return commandCancelBill;
            }
        }

        public override IEnumerable<InspectTabBase> GetInspectTabs()
        {
            return base.GetInspectTabs();
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (_bill != null && !_bill.IsStarted)
            {
                var requiredIngredients = RequiredIngredients.ToDictionary(tdc => tdc.ThingDef, tdc => tdc.Count);
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

            if (Spawned)
                _bill?.Tick(1);
        }

        public override void TickRare()
        {
            base.TickRare();

            if (Spawned)
                _bill?.Tick(GenTicks.TickRareInterval);
        }

        public override void TickLong()
        {
            base.TickLong();

            if (Spawned)
                _bill?.Tick(GenTicks.TickLongInterval);
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
            if (_bill != null && !_bill.IsStarted && !RequiredIngredients.Any())
            {
                _bill.Start();
            }
        }

        public void Notify_BillCompleted()
        {
            _bill = null;
        }

        public void Notify_BillFailed()
        {
            _bill = null;
        }

        private void EjectContents()
        {
            _innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
        }
    }
}
