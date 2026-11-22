using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 같은 신호로 켜지는 QuestPart_FactionGoodwillLocked보다 먼저 추가해야 잠금 전에 우호도가 바뀐다.
    public class QuestPart_ViviFederationAlliance : QuestPart_ViviEndingLetter
    {
        public Faction faction;

        private bool _allied;

        public override IEnumerable<Faction> InvolvedFactions
        {
            get
            {
                foreach (var involvedFaction in base.InvolvedFactions)
                {
                    yield return involvedFaction;
                }

                if (faction != null)
                {
                    yield return faction;
                }
            }
        }

        protected override string ExtraText
        {
            get
            {
                if (faction == null || faction.defeated || faction.HostileTo(Faction.OfPlayer)) { return null; }

                return (_allied ? LocalizeString_Letter.VV_Letter_EndingSiegeFederation : LocalizeString_Letter.VV_Letter_EndingSiegeFederationSupport).Translate().Resolve();
            }
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            if (signal.tag == inSignal)
            {
                _allied = TryAlly();
            }

            base.Notify_QuestSignalReceived(signal);
        }

        private bool TryAlly()
        {
            if (faction == null || faction.defeated || faction.PlayerRelationKind != FactionRelationKind.Neutral) { return false; }

            var change = DiplomacyTuning.BecomeAllyThreshold - Faction.OfPlayer.BaseGoodwillWith(faction);
            if (change > 0)
            {
                Faction.OfPlayer.TryAffectGoodwillWith(faction, change, canSendMessage: false, canSendHostilityLetter: false);
            }

            return faction.PlayerRelationKind == FactionRelationKind.Ally;
        }

        public override void Notify_FactionRemoved(Faction removed)
        {
            base.Notify_FactionRemoved(removed);
            if (removed == faction)
            {
                faction = null;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref faction, "faction");
        }
    }
}
