using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompViviHolder : ThingComp, IThingHolder
    {
        public int InnerViviCount => _innerViviContainer.Count;

        private ThingOwner _innerViviContainer;

        public CompViviHolder()
        {
            _innerViviContainer = new ThingOwner<Pawn>(this);
        }

        public override void PostExposeData()
        {
            Scribe_Deep.Look(ref _innerViviContainer, "innerViviContainer", new object[] { this });
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (mode != DestroyMode.WillReplace)
            {
                if (_innerViviContainer.Count > 0)
                {
                    _innerViviContainer.ClearAndDestroyContents(mode);
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (_innerViviContainer.Count > 0)
            {
                return $"count: {_innerViviContainer.Count}";
            }

            return base.CompInspectStringExtra();
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return _innerViviContainer;
        }

        public void JoinVivi(Pawn vivi)
        {
            if (!vivi.IsVivi()) { return; }

            if (vivi.Spawned)
            {
                vivi.DeSpawn();
            }

            _innerViviContainer.TryAddOrTransfer(vivi);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_FairyficationCompleteLabel.Translate(vivi.Named("TARGET")),
                LocalizeString_Letter.VV_Letter_FairyficationComplete.Translate(vivi.Named("TARGET"), parent.Named("CASTER")),
                LetterDefOf.NeutralEvent,
                parent);
        }
    }
}
