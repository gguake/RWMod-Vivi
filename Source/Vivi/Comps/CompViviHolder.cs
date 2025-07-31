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

    public class CompViviHolder : ThingComp
    {
        public CompProperties_ViviHolder Props => (CompProperties_ViviHolder)props;

        public bool CanJoin => FairyficatedPawnCount < Props.maxCount;

        public IEnumerable<Pawn> FairyficatedPawns => Current.Game.GetComponent<GameComponent_Mana>().GetFairyficatedPawns((Pawn)parent);
        public int FairyficatedPawnCount => FairyficatedPawns.Count();

        public CompViviHolder()
        {
        }

        public override void PostExposeData()
        {
            ThingOwner<Pawn> innerContainer = null;
            Scribe_Deep.Look(ref innerContainer, "innerViviContainer", new object[] { this });
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (mode != DestroyMode.WillReplace)
            {
                Current.Game.GetComponent<GameComponent_Mana>().UnregisterAllFairyByLinker((Pawn)parent);
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            var gameCompMana = Current.Game.GetComponent<GameComponent_Mana>();
            foreach (var fairy in gameCompMana.GetFairyficatedPawns((Pawn)parent))
            {
                fairy.Kill(dinfo);
            }

            gameCompMana.UnregisterAllFairyByLinker((Pawn)parent);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (Props.preventDamageChanceByInnerCount != null && Rand.Chance(Props.preventDamageChanceByInnerCount.Evaluate(FairyficatedPawnCount)))
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

        public void JoinVivi(Pawn vivi)
        {
            if (!vivi.IsVivi()) { return; }

            if (vivi.Spawned)
            {
                vivi.jobs.StopAll();
                vivi.DeSpawnOrDeselect(DestroyMode.Vanish);
            }

            Current.Game.GetComponent<GameComponent_Mana>().RegisterFairy((Pawn)parent, vivi);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_FairyficationCompleteLabel.Translate(vivi.Named("TARGET")),
                LocalizeString_Letter.VV_Letter_FairyficationComplete.Translate(vivi.Named("TARGET"), parent.Named("CASTER")),
                LetterDefOf.NeutralEvent,
                parent);

            Refresh();
        }

        public Pawn DetachVivi()
        {
            var gameCompMana = Current.Game.GetComponent<GameComponent_Mana>();
            var vivi = gameCompMana.GetFairyficatedPawns((Pawn)parent).RandomElement();

            if (GenPlace.TryPlaceThing(vivi, parent.Position, parent.Map, ThingPlaceMode.Near))
            {
                gameCompMana.UnregisterFairy(vivi);
                return (Pawn)vivi;
            }

            return null;
        }

        private void Refresh()
        {
            var pawn = (Pawn)parent;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_ViviFairyFollow);
            var severity = FairyficatedPawnCount;
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
