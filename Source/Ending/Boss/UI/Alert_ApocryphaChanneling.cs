using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Alert_ApocryphaChanneling : Alert_Critical
    {
        private readonly List<Pawn> _channelingBosses = new List<Pawn>();

        private CompApocrypha _firstChannelingComp;

        public Alert_ApocryphaChanneling()
        {
            defaultPriority = AlertPriority.Critical;
        }

        private void CollectChannelingBosses()
        {
            _channelingBosses.Clear();
            _firstChannelingComp = null;

            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                foreach (var thing in maps[i].listerThings.ThingsOfDef(VVThingDefOf.VV_Apocrypha))
                {
                    if (thing is Pawn boss && boss.TryGetComp<CompApocrypha>() is CompApocrypha comp && comp.Channel.IsChanneling)
                    {
                        _channelingBosses.Add(boss);
                        _firstChannelingComp ??= comp;
                    }
                }
            }
        }

        public override AlertReport GetReport()
        {
            CollectChannelingBosses();
            if (_channelingBosses.Count == 0)
            {
                return false;
            }

            return AlertReport.CulpritsAre(_channelingBosses);
        }

        public override string GetLabel()
        {
            return LocalizeString_Alert.VV_Alert_ApocryphaChanneling.Translate((_firstChannelingComp?.Channel.Progress ?? 0f).ToStringPercent());
        }

        public override TaggedString GetExplanation()
        {
            var comp = _firstChannelingComp;
            return LocalizeString_Alert.VV_Alert_ApocryphaChannelingDesc.Translate(
                (comp?.Channel.Progress ?? 0f).ToStringPercent().Named("PROGRESS"),
                (comp?.Channel.TicksLeft ?? 0).ToStringTicksToPeriod().Named("TIMELEFT"));
        }
    }
}
