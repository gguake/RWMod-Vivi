using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    public class ViviEggHatchery : Building, IThingHolder
    {
        public bool CanHatchNow => HatchingDisabledReason.Accepted;
        public AcceptanceReport HatchingDisabledReason
        {
            get
            {
                if (!Spawned) { return false; }

                if (Position.GetRoof(Map) == null)
                {
                    return LocalizeString_Inspector.VV_Inspector_HatcheryDisabledReasonNoRoof.Translate();
                }

                if (Position.UsesOutdoorTemperature(Map))
                {
                    return LocalizeString_Inspector.VV_Inspector_HatcheryDisabledReasonOutdoor.Translate();
                }

                return true;
            }
        }

        public bool CanLayHere
        {
            get
            {
                if (!CanHatchNow || ViviEgg != null) { return false; }

                var compForbiddable = GetComp<CompForbiddable>();
                if (compForbiddable != null && compForbiddable.Forbidden)
                {
                    return false;
                }

                if (Position.GetFirstPawn(Map) != null) { return false; }

                return true;
            }
        }
        public Thing ViviEgg
        {
            get
            {
                if (_innerContainer.Count > 0)
                {
                    return _innerContainer[0];
                }

                return null;
            }
            set
            {
                if (_innerContainer.Count > 0)
                {
                    _innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
                }

                if (value.Spawned) { value.DeSpawn(); }
                if (!_innerContainer.TryAddOrTransfer(value, canMergeWithExistingStacks: false))
                {
                    Log.Error($"failed to set egg");
                }
            }
        }

        private ThingOwner _innerContainer;

        public ViviEggHatchery()
        {
            _innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Deep.Look(ref _innerContainer, "innerContainer", this);
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

        public override void Tick()
        {
            base.Tick();
            _innerContainer.ThingOwnerTick();

            if (ViviEgg != null)
            {
                var comp = ViviEgg.TryGetComp<CompViviHatcher>();
                if (comp.TemperatureDamaged)
                {
                    _innerContainer.ClearAndDestroyContents();
                }
            }
        }

        public override void TickRare()
        {
            base.TickRare();
            _innerContainer.ThingOwnerTickRare();

            if (ViviEgg != null)
            {
                var comp = ViviEgg.TryGetComp<CompViviHatcher>();
                if (comp.TemperatureDamaged)
                {
                    _innerContainer.ClearAndDestroyContents();
                }
            }
        }

        public override void TickLong()
        {
            base.TickLong();
            _innerContainer.ThingOwnerTickLong();

            if (ViviEgg != null)
            {
                var comp = ViviEgg.TryGetComp<CompViviHatcher>();
                if (comp.TemperatureDamaged)
                {
                    _innerContainer.ClearAndDestroyContents();
                }
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            var hatchingDisabledReason = HatchingDisabledReason;
            if (!hatchingDisabledReason.Accepted)
            {
                sb.Append(LocalizeString_Inspector.VV_Inspector_HatcheryDisabled.Translate());

                if (hatchingDisabledReason.Reason != null)
                {
                    sb.Append(": ").Append(hatchingDisabledReason.Reason);
                }

                return sb.ToString();
            }
            else
            {
                if (ViviEgg != null)
                {
                    sb.Append(ViviEgg.GetInspectString());
                }
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (ViviEgg != null)
            {
                foreach (var gizmo in ViviEgg.GetGizmos())
                {
                    yield return gizmo;
                }
            }
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
