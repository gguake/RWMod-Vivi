using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    // 진행 단계에 맞는 목표 안내를 설명란에 붙인다. 단계 상태는 퀘스트가 아니라 ViviEndingQuest가 가진다.
    public class QuestPart_ViviEndingJourney : QuestPart
    {
        // 퀘스트 탭은 매 프레임 설명을 읽는다. 틱이 흐르거나 진행 상태가 바뀔 때만 다시 만든다(일시정지 중 재계산 방지).
        private string _cachedDescription;
        private int _cachedTick = -1;
        private int _cachedKey;

        private ViviEndingQuest Ending
        {
            get
            {
                var ending = ViviEndingQuest.Current;
                return ending != null && ending.Quest == quest ? ending : null;
            }
        }

        public override string DescriptionPart
        {
            get
            {
                if (quest.State != QuestState.Ongoing) { return null; }

                var ending = Ending;
                if (ending == null) { return null; }

                var now = Find.TickManager.TicksGame;
                var key = DescriptionKey(ending);
                if (_cachedDescription == null || now != _cachedTick || key != _cachedKey)
                {
                    _cachedDescription = BuildDescription(ending) ?? string.Empty;
                    _cachedTick = now;
                    _cachedKey = key;
                }

                return _cachedDescription.Length > 0 ? _cachedDescription : null;
            }
        }

        private static int DescriptionKey(ViviEndingQuest ending)
        {
            return Gen.HashCombineInt(
                Gen.HashCombineInt((int)ending.Stage, ending.Tales.Delivered),
                Gen.HashCombineInt(ending.Ruins.Investigated, ViviEndingUtility.FindFederation(requireNonHostile: true) != null ? 1 : 0));
        }

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                foreach (var target in base.QuestLookTargets)
                {
                    yield return target;
                }

                var ending = Ending;
                if (ending == null) { yield break; }

                var site = ending.Ruins.Site;
                if (site != null && ending.Stage == ViviEndingStage.Ruins)
                {
                    yield return site;
                }
            }
        }

        private static string BuildDescription(ViviEndingQuest ending)
        {
            var sb = new StringBuilder();
            var stage = ending.Stage;
            switch (stage)
            {
                case ViviEndingStage.Tales:
                    sb.Append(ending.Tales.AllDelivered
                        ? LocalizeString_Etc.VV_Quest_EndingStageMissiveWait.Translate().Resolve()
                        : LocalizeString_Etc.VV_Quest_EndingStageTales.Translate(ending.Tales.Delivered, ViviEndingTales.VolumeCount).Resolve());
                    AppendHostility(sb);
                    break;

                case ViviEndingStage.Missive:
                    sb.Append(LocalizeString_Etc.VV_Quest_EndingStageCoordinates.Translate().Resolve());
                    sb.Append("\n\n").Append(ViviEndingRequirements.ConditionsText());
                    AppendHostility(sb);
                    break;

                case ViviEndingStage.Ruins:
                    sb.Append(ending.Ruins.Investigated < ViviAncientRuins.EchoCount
                        ? LocalizeString_Etc.VV_Quest_EndingStageRuins.Translate(
                            ViviAncientRuins.RequiredAttunementLevel, ending.Ruins.Investigated, ViviAncientRuins.EchoCount).Resolve()
                        : LocalizeString_Etc.VV_Quest_EndingStageRuinsExtract.Translate().Resolve());
                    break;

                case ViviEndingStage.AltarUnlocked:
                    sb.Append(LocalizeString_Etc.VV_Quest_EndingStageAltar.Translate(Building_EverflowerAltar.RequiredAttunementLevel).Resolve());
                    sb.Append(ViviEndingRequirements.AltarStepsText());
                    if (ending.IsCoreLost)
                    {
                        sb.Append("\n\n").Append(LocalizeString_Etc.VV_Quest_EndingStageAltarCoreLost.Translate().Resolve());
                    }
                    break;

                case ViviEndingStage.Siege:
                case ViviEndingStage.Calm:
                case ViviEndingStage.Boss:
                case ViviEndingStage.Coronation:
                    sb.Append(LocalizeString_Etc.VV_Quest_EndingStageBloom.Translate().Resolve());
                    break;

                default:
                    return null;
            }

            return sb.ToString();
        }

        private static void AppendHostility(StringBuilder sb)
        {
            if (ViviEndingUtility.FindFederation(requireNonHostile: true) != null) { return; }

            sb.Append("\n\n").Append(LocalizeString_Etc.VV_Quest_EndingStageHostile.Translate().Resolve().Colorize(ColorLibrary.RedReadable));
        }
    }
}
