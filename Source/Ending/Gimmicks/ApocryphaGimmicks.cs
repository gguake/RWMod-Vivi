using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public static class ApocryphaGimmicks
    {
        private const float MinBloomSpacing = 5f;

        private const int BloomCellFallbackRadius = 4;

        private static readonly Vector3 QueenBodyOffset = new Vector3(0f, 0f, 1.5f);
        private static readonly Vector3 AsphodelBodyOffset = new Vector3(0f, 0f, 0.28f);

        public static void DrawLinks(Pawn boss)
        {
            if (boss == null || !boss.Spawned) { return; }

            var map = boss.Map;
            var bossPos = GetBossBodyPos(boss);

            DrawFlowerLinks(bossPos, map.listerThings.ThingsOfDef(VVThingDefOf.VV_DoomFlower));
            DrawFlowerLinks(bossPos, map.listerThings.ThingsOfDef(VVThingDefOf.VV_FairyFlower));
        }

        public static void CleanupAll(Map map)
        {
            if (map == null) { return; }

            ClearGimmickThings(map);
            RemoveFairyAttunement(map);
            DoomBlightUtility.ClearDoomBlights(map);
        }

        #region 내부 공용

        internal static bool IsBossFightActive => ViviEndingQuest.Current?.Stage == ViviEndingStage.Boss;

        internal static bool IsDoomGimmickActive(Pawn boss)
        {
            if (!IsBossFightActive || boss == null || !boss.Spawned || boss.Dead) { return false; }

            return boss.TryGetComp<CompApocrypha>()?.Phase == ApocryphaPhase.Queen;
        }

        internal static Pawn FindBoss(Map map)
        {
            if (map == null) { return null; }

            var questBoss = ViviEndingQuest.Current?.Boss;
            if (questBoss != null && questBoss.Spawned && questBoss.Map == map) { return questBoss; }

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i].TryGetComp<CompApocrypha>() != null) { return pawns[i]; }
            }

            return null;
        }

        internal static Vector3 GetBossBodyOffset(Pawn boss)
        {
            return boss.ageTracker.CurLifeStageIndex == 0 ? QueenBodyOffset : AsphodelBodyOffset;
        }

        internal static Vector3 GetBossBodyPos(Pawn boss)
        {
            var drawPos = boss.DrawPos;
            var bodyPos = drawPos + GetBossBodyOffset(boss);
            return boss.Map != null && bodyPos.ToIntVec3().InBounds(boss.Map) ? bodyPos : drawPos;
        }

        internal static void PlayEffect(EffecterDef effecterDef, IntVec3 cell, Map map, Vector3 offset = default, float scale = 1f)
        {
            if (effecterDef == null || map == null || !cell.InBounds(map)) { return; }

            var effecter = effecterDef.Spawn(cell, map, offset, scale);
            if (effecterDef.maintainTicks > 0)
            {
                map.effecterMaintainer.AddEffecterToMaintain(effecter, cell, effecterDef.maintainTicks);
            }
            else
            {
                effecter.Cleanup();
            }
        }

        internal static List<Thing> GetBlastIgnoredThings(Map map, Thing boss)
        {
            var ignored = new List<Thing>();
            if (boss != null) { ignored.Add(boss); }

            var anchor = ViviEndingQuest.Current?.AnchorEverflower;
            if (anchor != null && anchor.Spawned && anchor.Map == map) { ignored.Add(anchor); }

            var altar = Building_EverflowerAltar.FindOnMap(map);
            if (altar != null) { ignored.Add(altar); }

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn == boss) { continue; }
                if (pawn.kindDef == VVPawnKindDefOf.VV_TitanicHornet || pawn.TryGetComp<CompApocrypha>() != null)
                {
                    ignored.Add(pawn);
                }
            }

            ignored.AddRange(map.listerThings.ThingsOfDef(VVThingDefOf.VV_DoomFlower));
            ignored.AddRange(map.listerThings.ThingsOfDef(VVThingDefOf.VV_FairyFlower));
            return ignored;
        }

        internal static bool TryFindBloomCellNear(IntVec3 cell, Map map, out IntVec3 result)
        {
            var altar = Building_EverflowerAltar.FindOnMap(map);
            var altarRect = GetAltarRect(altar);
            var colonyAnchor = GetColonyAnchor(map, altar);
            if (IsValidBloomCell(cell, map, altarRect, colonyAnchor))
            {
                result = cell;
                return true;
            }

            return CellFinder.TryFindRandomCellNear(cell, map, BloomCellFallbackRadius, c => IsValidBloomCell(c, map, altarRect, colonyAnchor), out result);
        }

        internal static bool TryGetEverflowerBudPos(Map map, out Vector3 pos)
        {
            var everflower = ViviEndingQuest.Current?.AnchorEverflower;
            if (everflower == null || !everflower.Spawned || everflower.Map != map)
            {
                pos = Vector3.zero;
                return false;
            }

            pos = EverflowerBudPos(everflower);
            return true;
        }

        internal static Vector3 EverflowerBudPos(ArcanePlant_Everflower everflower)
        {
            return (everflower.DrawPos + (everflower.EverflowerComp?.RitualEffecterOffset ?? Vector3.zero)).Yto0();
        }

        internal static void ThrowFairyLight(Map map, Vector3 from, Vector3 to, float speed, float scale, Color color)
        {
            var travel = (to - from).Yto0();
            var distance = travel.magnitude;
            if (distance < 0.05f || !from.ShouldSpawnMotesAt(map)) { return; }

            var def = VVFleckDefOf.VV_Fleck_ApocryphaFairyLight;
            var data = FleckMaker.GetDataStatic(from, map, def, scale);
            data.velocityAngle = travel.AngleFlat();
            data.velocitySpeed = speed;
            data.solidTimeOverride = Mathf.Max(0f, distance / speed - def.fadeInTime - def.fadeOutTime);
            data.instanceColor = color;
            map.flecks.CreateFleck(data);
        }

        internal static void SpawnFlower(Thing flower, IntVec3 cell, Map map, Pawn boss)
        {
            var plant = cell.GetPlant(map);
            if (plant != null && !plant.Destroyed)
            {
                plant.Destroy(DestroyMode.Vanish);
            }

            if (flower.def == VVThingDefOf.VV_DoomFlower)
            {
                flower.SetFaction(boss?.Faction ?? Faction.OfInsects);
            }

            GenSpawn.Spawn(flower, cell, map, WipeMode.Vanish);
        }

        #endregion

        #region 멸망꽃 위치

        internal static List<IntVec3> FindBloomCells(Pawn boss, int count, FloatRange distance)
        {
            var result = new List<IntVec3>();
            if (count <= 0) { return result; }

            var map = boss.Map;
            var bossCell = boss.Position;
            var altar = Building_EverflowerAltar.FindOnMap(map);
            var altarRect = GetAltarRect(altar);
            var colonyAnchor = GetColonyAnchor(map, altar);
            var minDistanceSquared = distance.min * distance.min;
            var maxDistanceSquared = distance.max * distance.max;

            for (int i = 0; i < count; i++)
            {
                if (!CellFinder.TryFindRandomCellNear(bossCell, map, Mathf.CeilToInt(distance.max), c =>
                    c.DistanceToSquared(bossCell) >= minDistanceSquared
                    && c.DistanceToSquared(bossCell) <= maxDistanceSquared
                    && !IsNearAny(result, c, MinBloomSpacing)
                    && IsValidBloomCell(c, map, altarRect, colonyAnchor), out var cell))
                {
                    break;
                }

                result.Add(cell);
            }

            return result;
        }

        private static bool IsNearAny(List<IntVec3> cells, IntVec3 cell, float radius)
        {
            if (cells == null) { return false; }

            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].InHorDistOf(cell, radius)) { return true; }
            }

            return false;
        }

        private static bool IsValidBloomCell(IntVec3 cell, Map map, CellRect altarRect, LocalTargetInfo colonyAnchor)
        {
            if (!cell.InBounds(map)) { return false; }
            if (!cell.Standable(map)) { return false; }
            if (cell.Fogged(map)) { return false; }
            if (cell.GetTerrain(map).IsWater) { return false; }

            var roof = cell.GetRoof(map);
            if (roof != null && roof.isThickRoof) { return false; }

            if (altarRect.Area > 0 && altarRect.Contains(cell)) { return false; }
            if (map.zoneManager.ZoneAt(cell) is Zone_Growing) { return false; }

            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (thing is Building || thing is Pawn) { return false; }
                if (thing.def.category == ThingCategory.Item) { return false; }
                if (thing.def.IsBlueprint || thing.def.IsFrame) { return false; }
            }

            return CanReachFromColony(cell, map, colonyAnchor);
        }

        private static bool CanReachFromColony(IntVec3 cell, Map map, LocalTargetInfo colonyAnchor)
        {
            if (!colonyAnchor.IsValid) { return map.reachability.CanReachColony(cell); }

            return map.reachability.CanReach(cell, colonyAnchor, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors));
        }

        private static LocalTargetInfo GetColonyAnchor(Map map, Building altar)
        {
            if (altar != null && altar.Spawned) { return altar; }

            var colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists.Count > 0) { return colonists[0]; }

            return LocalTargetInfo.Invalid;
        }

        private static CellRect GetAltarRect(Building altar)
        {
            return altar != null && altar.Spawned ? altar.OccupiedRect() : CellRect.Empty;
        }

        #endregion

        #region 연결선

        private static void DrawFlowerLinks(Vector3 bossPos, List<Thing> flowers)
        {
            for (int i = 0; i < flowers.Count; i++)
            {
                if (!(flowers[i] is Building_ApocryphaFlower flower) || !flower.Spawned) { continue; }

                var extension = flower.Extension;
                if (extension == null) { continue; }

                var color = extension.linkColor;
                color.a *= flower.LinkAlpha;
                PetalLinkRenderer.Draw(bossPos, flower.LinkAnchor, extension.linkWidth, color, flower.thingIDNumber);
            }
        }

        #endregion

        #region 정리

        private static void ClearGimmickThings(Map map)
        {
            WitherAll(map, VVThingDefOf.VV_FairyFlower);
            WitherAll(map, VVThingDefOf.VV_DoomFlower);
        }

        private static void WitherAll(Map map, ThingDef def)
        {
            var flowers = new List<Thing>(map.listerThings.ThingsOfDef(def));
            for (int i = 0; i < flowers.Count; i++)
            {
                if (flowers[i] is Building_ApocryphaFlower flower)
                {
                    flower.Wither();
                }
                else if (!flowers[i].Destroyed)
                {
                    flowers[i].Destroy(DestroyMode.Vanish);
                }
            }
        }

        private static void RemoveFairyAttunement(Map map)
        {
            var pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < pawns.Count; i++)
            {
                var hediff = pawns[i].health?.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyAttunement);
                if (hediff != null)
                {
                    pawns[i].health.RemoveHediff(hediff);
                }
            }
        }

        #endregion
    }
}
