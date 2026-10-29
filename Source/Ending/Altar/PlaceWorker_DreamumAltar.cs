using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 영원꽃 제단 배치 규칙: 지역마다 하나(완성 건물·청사진·틀 모두), 발판 안 마법 식물 금지(중앙의 영원꽃 포함).
    // 제단 틀은 발판의 건물(마법 식물 포함)을 해체해 버리므로 영원꽃 둘레에 제단을 지을 수 없다.
    // 영원꽃은 제단을 완공한 뒤 순간이동으로만 안치한다. 비비 연방의 밀서를 받기 전에는 건축 메뉴에 보이지 않는다.
    public class PlaceWorker_DreamumAltar : PlaceWorker
    {
        // 갓 모드에서는 바닐라 Designator_Build.Visible이 이 검사 전에 보여 준다.
        public override bool IsBuildDesignatorVisible(BuildableDef def)
        {
            return ViviEndingQuest.Current?.IsAltarUnlocked == true;
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (checkingDef is ThingDef altarDef)
            {
                if (AnyOtherOfDef(map, altarDef, thingToIgnore, thing)
                    || AnyOtherOfDef(map, altarDef.blueprintDef, thingToIgnore, thing)
                    || AnyOtherOfDef(map, altarDef.frameDef, thingToIgnore, thing))
                {
                    return new AcceptanceReport(LocalizeString_EndingAltar.VV_FailReason_AltarOnePerMap.Translate());
                }
            }

            foreach (var cell in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
            {
                if (!cell.InBounds(map)) { continue; }

                var thingList = cell.GetThingList(map);
                for (int i = 0; i < thingList.Count; ++i)
                {
                    var other = thingList[i];
                    if (other == thingToIgnore || other == thing) { continue; }

                    // 이미 심긴 마법 식물이 있거나 옮겨 심기(설치) 청사진으로 마법 식물이 예약되어 있으면 배치를 막는다.
                    var isArcanePlant = other is ArcanePlant
                        || (other is Blueprint_Install installBlueprint && installBlueprint.ThingToInstall is ArcanePlant);
                    if (isArcanePlant)
                    {
                        return new AcceptanceReport(LocalizeString_EndingAltar.VV_FailReason_AltarBlockedByArcanePlant.Translate());
                    }
                }
            }

            return true;
        }

        private static bool AnyOtherOfDef(Map map, ThingDef def, Thing thingToIgnore, Thing thing)
        {
            if (def == null) { return false; }

            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; ++i)
            {
                if (things[i] != thingToIgnore && things[i] != thing)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
