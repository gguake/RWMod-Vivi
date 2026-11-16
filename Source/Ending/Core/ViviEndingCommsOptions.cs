using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 비비 연방 통신 선택지: 옛 제단 좌표 요청, 동화책 재구매, 대체 호박 코어 복구.
    public static class ViviEndingCommsOptions
    {
        public static IEnumerable<DiaOption> Make(Pawn negotiator, Faction faction)
        {
            var ending = ViviEndingQuest.Current;
            if (ending == null || faction == null || faction.def != VVFactionDefOf.VV_ViviFederation) { yield break; }

            var map = negotiator?.Map;
            if (map == null || !map.IsPlayerHome) { yield break; }

            var hostile = faction.HostileTo(Faction.OfPlayer);

            if (ending.Ruins.MissiveReceived && !ending.Ruins.HasCoordinates)
            {
                yield return MakeCoordinatesOption(ending, negotiator, faction, hostile);
            }

            if (ending.Tales.Delivered > 0)
            {
                yield return MakeRepurchaseOption(ending, negotiator, faction, hostile, map);
            }

            if (ending.Stage == ViviEndingStage.AltarUnlocked && !AmberCoreUtility.AnyAmberCoreExists())
            {
                yield return MakeReplacementCoreOption(ending, negotiator, faction, hostile, map);
            }
        }

        private static DiaOption MakeCoordinatesOption(ViviEndingQuest ending, Pawn negotiator, Faction faction, bool hostile)
        {
            var option = new DiaOption(LocalizeString_Dialog.VV_Comms_RuinsCoordinatesRequest.Translate());
            if (TryDisable(option, negotiator, hostile)) { return option; }

            var report = ending.Ruins.CanRequestCoordinates();
            if (!report.Accepted)
            {
                option.Disable(report.Reason);
                return option;
            }

            option.linkLateBind = () =>
            {
                var text = ending.Ruins.RevealRuins()
                    ? LocalizeString_Dialog.VV_Comms_RuinsCoordinatesResult.Translate(LeaderName(faction).Named("LEADER")).Resolve()
                    : LocalizeString_Dialog.VV_Comms_RuinsCoordinatesNoTile.Translate().Resolve();
                return ResultNode(text, negotiator, faction);
            };
            return option;
        }

        private static DiaOption MakeRepurchaseOption(ViviEndingQuest ending, Pawn negotiator, Faction faction, bool hostile, Map map)
        {
            var option = new DiaOption(LocalizeString_Dialog.VV_Comms_TaleRepurchase.Translate());
            if (TryDisable(option, negotiator, hostile)) { return option; }

            option.linkLateBind = () =>
            {
                var leader = LeaderName(faction).Named("LEADER");
                var node = new DiaNode(LocalizeString_Dialog.VV_Comms_TaleRepurchaseIntro.Translate(leader).CapitalizeFirst());
                var cost = ending.Tales.RepurchaseCost;
                var report = ending.Tales.CanRepurchase(map);

                for (int volume = 1; volume <= ending.Tales.Delivered; ++volume)
                {
                    var def = ViviEndingTales.VolumeDef(volume);
                    if (def == null) { continue; }

                    var captured = volume;
                    var volumeOption = new DiaOption(LocalizeString_Dialog.VV_Comms_TaleRepurchaseOption.Translate(def.LabelCap, cost));
                    if (!report.Accepted)
                    {
                        volumeOption.Disable(report.Reason);
                    }
                    else
                    {
                        volumeOption.linkLateBind = () =>
                        {
                            ending.Tales.Repurchase(captured, map);
                            return ResultNode(LocalizeString_Dialog.VV_Comms_TaleRepurchaseResult.Translate(def.label, leader).Resolve(), negotiator, faction);
                        };
                    }
                    node.options.Add(volumeOption);
                }

                node.options.Add(new DiaOption("GoBack".Translate())
                {
                    linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator),
                });
                return node;
            };
            return option;
        }

        private static DiaOption MakeReplacementCoreOption(ViviEndingQuest ending, Pawn negotiator, Faction faction, bool hostile, Map map)
        {
            var option = new DiaOption(LocalizeString_Dialog.VV_Comms_ReplacementCore.Translate());
            if (TryDisable(option, negotiator, hostile)) { return option; }

            option.linkLateBind = () =>
            {
                ending.Ruins.GrantReplacementCore(map);
                return ResultNode(LocalizeString_Dialog.VV_Comms_ReplacementCoreResult.Translate().Resolve(), negotiator, faction);
            };
            return option;
        }

        private static bool TryDisable(DiaOption option, Pawn negotiator, bool hostile)
        {
            if (hostile)
            {
                option.Disable(LocalizeString_Dialog.VV_Comms_FederationHostile.Translate());
                return true;
            }

            var social = negotiator.skills?.GetSkill(SkillDefOf.Social);
            if (social == null || social.TotallyDisabled)
            {
                option.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));
                return true;
            }

            return false;
        }

        private static string LeaderName(Faction faction)
        {
            return faction.leader?.LabelShort ?? faction.Name;
        }

        private static DiaNode ResultNode(string text, Pawn negotiator, Faction faction)
        {
            var node = new DiaNode(text.CapitalizeFirst());
            node.options.Add(new DiaOption("OK".Translate())
            {
                linkLateBind = FactionDialogMaker.ResetToRoot(faction, negotiator),
            });
            return node;
        }
    }
}
