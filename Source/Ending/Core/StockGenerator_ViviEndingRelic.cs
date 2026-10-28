using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 비비 연방 상인의 고대 호박 코어 재고(Patches/Ending_Traders.xml).
    // 연방의 서신을 받은 뒤, 엔딩이 끝나지 않았고 플레이어가 코어나 제단(청사진/프레임 포함)을 갖고 있지 않을 때만 판다.
    public class StockGenerator_ViviEndingRelic : StockGenerator_SingleDef
    {
        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            if (!CanOfferRelic()) { yield break; }

            foreach (var thing in base.GenerateThings(forTile, faction))
            {
                yield return thing;
            }
        }

        private static bool CanOfferRelic()
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || !quest.IsAltarUnlocked) { return false; }
            if (quest.Stage == ViviEndingStage.Completed || quest.Stage == ViviEndingStage.Failed) { return false; }

            return !PlayerHasAmberCoreOrAltar();
        }

        // 플레이어 정착지 맵(코어·제단·제단 청사진/틀, 정착민 소지품)이나 캐러밴에 호박 코어 또는 제단이 있는지. 코어 재전달 판정에도 쓴다.
        internal static bool PlayerHasAmberCoreOrAltar()
        {
            var coreDef = EndingCoreDefOf.VV_AncientAmberCore;
            var altarDef = EndingCoreDefOf.VV_DreamumAltar;

            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) { continue; }

                if (HasAny(map, coreDef) || HasAny(map, altarDef) || HasAny(map, altarDef?.blueprintDef) || HasAny(map, altarDef?.frameDef))
                {
                    return true;
                }

                if (coreDef != null)
                {
                    foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                    {
                        if (pawn.inventory != null && pawn.inventory.innerContainer.TotalStackCountOfDef(coreDef) > 0)
                        {
                            return true;
                        }
                    }
                }
            }

            if (coreDef != null)
            {
                foreach (var caravan in Find.WorldObjects.Caravans)
                {
                    if (!caravan.IsPlayerControlled) { continue; }

                    foreach (var item in CaravanInventoryUtility.AllInventoryItems(caravan))
                    {
                        if (item.def == coreDef)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasAny(Map map, ThingDef def)
        {
            return def != null && map.listerThings.ThingsOfDef(def).Count > 0;
        }
    }
}
