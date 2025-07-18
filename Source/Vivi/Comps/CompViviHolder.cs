using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ViviHolder : CompProperties
    {
        public int maxCount;

        public SimpleCurve preventDamageChanceByInnerCount;
        public EffecterDef preventDamageEffect;

        public CompProperties_ViviHolder()
        {
            compClass = typeof(CompViviHolder);
        }
    }

    public class CompViviHolder : ThingComp, IThingHolder
    {
        public CompProperties_ViviHolder Props => (CompProperties_ViviHolder)props;

        public bool CanJoin => _innerViviContainer.Count < Props.maxCount;
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

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            for (int i = 0; i < _innerViviContainer.Count; ++i)
            {
                _innerViviContainer.FirstOrDefault().Kill(dinfo);
            }

            _innerViviContainer.ClearAndDestroyContents();
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (Props.preventDamageChanceByInnerCount != null && Rand.Chance(Props.preventDamageChanceByInnerCount.Evaluate(InnerViviCount)))
            {
                if (Props.preventDamageEffect != null)
                {
                    Props.preventDamageEffect.SpawnAttached(parent, parent.Map);
                }

                MoteMaker.ThrowText(
                    new Vector3(parent.Position.x + 1f, parent.Position.y, parent.Position.z + 1f), 
                    parent.Map, 
                    LocalizeString_Etc.VV_TextMote_DamageBlockedByFairy.Translate());

                absorbed = true;
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
