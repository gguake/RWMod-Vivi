using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 엔딩 공성·소강·보스 단계 동안 거점 맵을 습격 신호기 맵(Map_RaidBeacon)으로 만들어 스토리텔러가 습격을 더 자주 고르게 한다.
    // 맵의 사건 태그는 그 맵 부모 월드 오브젝트의 태그와 comp 태그를 그대로 쓰므로, 모든 MapParent 계열 정의에 상태 없는 이 comp를 붙인다.
    // comp는 저장되지 않고 월드 오브젝트를 만들거나 불러올 때마다 정의에서 다시 생기므로 기존 세이브에도 그대로 적용된다.
    public class WorldObjectComp_ViviEndingRaidBeacon : WorldObjectComp
    {
        private static readonly IncidentTargetTagDef[] RaidBeaconTags = { IncidentTargetTagDefOf.Map_RaidBeacon };

        // 모든 맵의 사건 판정마다 불리므로 비활성일 때는 반복기를 만들지 않는다.
        public override IEnumerable<IncidentTargetTagDef> IncidentTargetTags()
        {
            var quest = ViviEndingQuest.Current;
            if (quest != null && parent is MapParent mapParent && mapParent.HasMap && quest.IsRaidBeaconActive(mapParent.Map))
            {
                return RaidBeaconTags;
            }

            return Enumerable.Empty<IncidentTargetTagDef>();
        }
    }

    [StaticConstructorOnStartup]
    internal static class WorldObjectComp_ViviEndingRaidBeaconInjector
    {
        static WorldObjectComp_ViviEndingRaidBeaconInjector()
        {
            var compClass = typeof(WorldObjectComp_ViviEndingRaidBeacon);
            foreach (var def in DefDatabase<WorldObjectDef>.AllDefsListForReading)
            {
                try
                {
                    if (def.worldObjectClass == null || !typeof(MapParent).IsAssignableFrom(def.worldObjectClass)) { continue; }

                    if (def.comps == null)
                    {
                        def.comps = new List<WorldObjectCompProperties>();
                    }
                    else if (def.comps.Any(c => c?.compClass == compClass))
                    {
                        continue;
                    }

                    def.comps.Add(new WorldObjectCompProperties { compClass = compClass });
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to add the ending raid beacon comp to {def.defName}: {ex}");
                }
            }
        }
    }
}
