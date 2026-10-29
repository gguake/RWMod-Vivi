using RimWorld;
using Verse;

namespace VVRace
{
    // 통신기 대화(비비 연방)의 〈고대 호박 코어 의뢰〉 선택지. 바닐라 "AI 코어 정보 요청"(FactionDialogMaker.RequestAICoreQuest)과 같은 꼴이며,
    // EndingCorePatch가 FactionDialogFor 결과의 "(통신 종료)" 앞에 끼운다.
    public static class AmberCoreCommsOption
    {
        // 보일 선택지(비활성일 수 있음). 밀서 전·엔딩 시작 뒤, 비비 연방이 아닌 세력, 정착지가 아닌 맵의 통신기에서는 null.
        public static DiaOption Make(Pawn negotiator, Faction faction)
        {
            var ending = ViviEndingQuest.Current;
            if (ending == null || !ending.CanOfferAmberCoreRequest) { return null; }
            if (faction == null || faction.def != EndingCoreDefOf.VV_ViviFederation) { return null; }

            var map = negotiator?.Map;
            if (map == null || !map.IsPlayerHome) { return null; }

            var option = new DiaOption(LocalizeString_EndingCore.VV_Comms_AmberCoreRequest.Translate());

            // 바닐라 요청 선택지처럼 사교가 완전히 불가능한 협상자는 의뢰할 수 없다.
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

            // 결과(퀘스트를 만들었는지)에 따라 다음 대화가 달라지므로 누르는 순간 만든다.
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
