using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 아포크리파가 영원꽃 찬탈 채널링 중일 때의 치명 경고(Alert 파생 클래스는 자동 등록된다).
    public class Alert_ApocryphaChanneling : Alert_Critical
    {
        private readonly List<Pawn> _channelingBosses = new List<Pawn>();

        private List<Pawn> ChannelingBosses
        {
            get
            {
                _channelingBosses.Clear();

                var maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    foreach (var thing in maps[i].listerThings.ThingsOfDef(ApocryphaThingDefOf.VV_Apocrypha))
                    {
                        if (thing is Pawn boss && boss.TryGetComp<CompApocrypha>() is CompApocrypha comp && comp.IsChanneling)
                        {
                            _channelingBosses.Add(boss);
                        }
                    }
                }

                return _channelingBosses;
            }
        }

        private CompApocrypha FirstChannelingComp
        {
            get
            {
                var bosses = ChannelingBosses;
                return bosses.Count > 0 ? bosses[0].TryGetComp<CompApocrypha>() : null;
            }
        }

        public Alert_ApocryphaChanneling()
        {
            defaultPriority = AlertPriority.Critical;
        }

        public override AlertReport GetReport()
        {
            var bosses = ChannelingBosses;
            if (bosses.Count == 0)
            {
                return false;
            }

            return AlertReport.CulpritsAre(bosses);
        }

        public override string GetLabel()
        {
            var comp = FirstChannelingComp;
            return LocalizeString_EndingBoss.VV_Alert_ApocryphaChanneling.Translate((comp?.ChannelProgress ?? 0f).ToStringPercent());
        }

        public override TaggedString GetExplanation()
        {
            var comp = FirstChannelingComp;
            return LocalizeString_EndingBoss.VV_Alert_ApocryphaChannelingDesc.Translate(
                (comp?.ChannelProgress ?? 0f).ToStringPercent().Named("PROGRESS"),
                (comp?.ChannelTicksLeft ?? 0).ToStringTicksToPeriod().Named("TIMELEFT"));
        }
    }
}
