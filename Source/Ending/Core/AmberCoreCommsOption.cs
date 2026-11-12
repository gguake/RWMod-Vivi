using RimWorld;
using Verse;

namespace VVRace
{
    public static class AmberCoreCommsOption
    {
        public static DiaOption Make(Pawn negotiator, Faction faction)
        {
            var ending = ViviEndingQuest.Current;
            if (ending == null || !ending.AmberCoreRequest.CanOffer) { return null; }
            if (faction == null || faction.def != VVFactionDefOf.VV_ViviFederation) { return null; }

            var map = negotiator?.Map;
            if (map == null || !map.IsPlayerHome) { return null; }

            var option = new DiaOption(LocalizeString_Dialog.VV_Comms_AmberCoreRequest.Translate());

            var social = negotiator.skills?.GetSkill(SkillDefOf.Social);
            if (social == null || social.TotallyDisabled)
            {
                option.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));
                return option;
            }

            var report = ending.AmberCoreRequest.CanRequest(faction);
            if (!report.Accepted)
            {
                option.Disable(report.Reason);
                return option;
            }

            option.linkLateBind = () =>
            {
                var leader = faction.leader?.LabelShort ?? faction.Name;
                var quest = ending.AmberCoreRequest.Request(faction);
                var key = quest != null ? LocalizeString_Dialog.VV_Comms_AmberCoreRequestResult : LocalizeString_Dialog.VV_Comms_AmberCoreRequestNoQuest;
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
