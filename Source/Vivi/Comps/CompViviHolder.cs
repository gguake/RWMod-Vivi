using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompViviHolder : ThingComp, IThingHolder
    {
        public bool CanJoin => _innerViviContainer.Count < 10;
        public int InnerViviCount => _innerViviContainer.Count;

        private ThingOwner _innerViviContainer;
        private List<MoteViviFairy> _motes;

        public CompViviHolder()
        {
            _innerViviContainer = new ThingOwner<Pawn>(this);
        }

        public override void PostExposeData()
        {
            Scribe_Deep.Look(ref _innerViviContainer, "innerViviContainer", new object[] { this });
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (_motes == null)
            {
                _motes = new List<MoteViviFairy>();
            }

            for (int i = 0; i < InnerViviCount; ++i)
            {
                var mote = (MoteViviFairy)ThingMaker.MakeThing(VVThingDefOf.VV_Mote_FairyVivi);
                mote.Initialize((Pawn)parent);
                GenSpawn.Spawn(mote, parent.Position, parent.Map);
                _motes.Add(mote);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            foreach (var mote in _motes)
            {
                if (!mote.Destroyed)
                {
                    mote.Destroy();
                }
            }
            _motes.Clear();
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

            Refresh();
        }

        public Pawn DetachVivi()
        {
            var vivi = _innerViviContainer.RandomElement();
            if (_innerViviContainer.TryDrop(vivi, ThingPlaceMode.Near, 1, out _))
            {
                Refresh();
                return (Pawn)vivi;
            }

            return null;
        }

        private void Refresh()
        {
            var pawn = (Pawn)parent;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_ViviFairyFollow);
            var severity = _innerViviContainer.Count;
            if (severity > 0)
            {
                if (hediff == null)
                {
                    hediff = pawn.health.AddHediff(VVHediffDefOf.VV_ViviFairyFollow);
                }

                hediff.Severity = severity;
            }
            else
            {
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }
    }
}
