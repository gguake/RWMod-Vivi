using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 같은 신호로 켜지는 QuestPart_FactionGoodwillLocked보다 먼저 추가해야 잠금 전에 우호도가 바뀐다.
    public class QuestPart_ViviFederationAlliance : QuestPart
    {
        private const int AllyGoodwill = 75;

        public string inSignal;
        public Faction faction;

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

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag != inSignal) { return; }

            if (faction == null || faction.defeated || faction.HostileTo(Faction.OfPlayer)) { return; }

            var change = AllyGoodwill - Faction.OfPlayer.GoodwillWith(faction);
            if (change <= 0) { return; }

            Faction.OfPlayer.TryAffectGoodwillWith(faction, change);
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
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_References.Look(ref faction, "faction");
        }
    }
}
