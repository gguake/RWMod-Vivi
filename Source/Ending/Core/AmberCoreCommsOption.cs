using RimWorld;
using Verse;

namespace VVRace
{
    public static class AmberCoreCommsOption
    {
        public static DiaOption Make(Pawn negotiator, Faction faction)
        {
            var ending = ViviEndingQuest.Current;
            if (ending == null || !ending.CanOfferAmberCoreRequest) { return null; }
            if (faction == null || faction.def != EndingCoreDefOf.VV_ViviFederation) { return null; }

            var map = negotiator?.Map;
            if (map == null || !map.IsPlayerHome) { return null; }

            var option = new DiaOption(LocalizeString_EndingCore.VV_Comms_AmberCoreRequest.Translate());

            var social = negotiator.skills?.GetSkill(SkillDefOf.Social);
            if (social == null || social.TotallyDisabled)
            {
                option.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));
                return option;
            }

            var report = ending.CanRequestAmberCoreQuest(faction);
            if (!report.Accepted)
            {
                option.Disable(report.Reason);
                return option;
            }

            // 퀘스트 생성 결과에 따라 다음 대화가 달라지므로 선택지를 누른 시점에 생성한다.
            option.linkLateBind = () =>
            {
                var leader = faction.leader?.LabelShort ?? faction.Name;
                var quest = ending.RequestAmberCoreQuest(faction);
                var key = quest != null ? LocalizeString_EndingCore.VV_Comms_AmberCoreRequestResult : LocalizeString_EndingCore.VV_Comms_AmberCoreRequestNoQuest;
                var node = new DiaNode(key.Translate(leader.Named("LEADER")).CapitalizeFirst());
                node.options.Add(new DiaOption("OK".Translate())
                {
                    linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator),
                });
                return node;
            };

            return option;
        }
    }
}
