using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public static class ApocryphaGimmicks
    {
        private const float MinSeedDistanceFromBoss = 8f;

        private const float MinVolleySeedSpacing = 5f;

        private const int BloomCellFallbackRadius = 4;

        private static readonly Vector3 BoundBodyOffset = new Vector3(0f, 0f, 0.28f);
        private static readonly Vector3 UnboundBodyOffset = new Vector3(0f, 0f, 1.5f);

        private static readonly Vector3 PawnLinkOffset = new Vector3(0f, 0f, 0.35f);
        private static readonly Color InterferenceLinkColor = new Color(1f, 0.9f, 0.6f);
        private const float InterferenceLinkWidth = 1f;
        private const float InterferenceLinkFadeInTicks = 45f;

        private const float ManaBackflowBeamWidth = 1.6f;
        private const float ManaBackflowCameraShake = 1.5f;

        public static bool TryFireFairyShell(Pawn boss, LocalTargetInfo target)
        {
            if (!IsBoundBoss(boss, out var comp)) { return false; }
            if (!target.IsValid) { return false; }

            var map = boss.Map;
            var cell = target.Cell;
            if (!cell.InBounds(map)) { return false; }

            var shellDef = VVThingDefOf.VV_Projectile_FairyShell;
            var projectile = (Projectile)GenSpawn.Spawn(shellDef, boss.Position, map);
            projectile.Launch(boss, GetBossBodyPos(boss), target, target, ProjectileHitFlags.IntendedTarget);

            PlayLaunchEffect(shellDef, boss);
            comp.Notify_FairyShellFired();
            return true;
        }

        // 보스는 TryLaunchSeedVolley만 사용한다. 아래 두 메서드는 자동 테스트가 리플렉션으로 호출하므로
        // 이름이나 시그니처를 바꿀 때 테스트 스크립트도 함께 수정해야 한다.

        public static bool TryLaunchDoomSeed(Pawn boss)
        {
            TryLaunchSeedVolley(boss, 1, false, out var doomLaunched, out _);
            return doomLaunched > 0;
        }

        public static bool TryLaunchFairySeed(Pawn boss)
        {
            TryLaunchSeedVolley(boss, 0, true, out _, out var fairyLaunched);
            return fairyLaunched > 0;
        }

        public static void TryLaunchSeedVolley(Pawn boss, int doomCount, bool withFairy, out int doomLaunched, out int fairyLaunched)
        {
            doomLaunched = 0;
            fairyLaunched = 0;
            if (!IsBoundBoss(boss, out var comp) || (doomCount <= 0 && !withFairy)) { return; }

            var map = boss.Map;
            var doomSeedDef = VVThingDefOf.VV_Projectile_DoomSeed;
            var fairySeedDef = VVThingDefOf.VV_Projectile_FairySeed;
            var doomRoom = comp.Props.maxDoomFlowers - CountFlowersAndSeeds(map, VVThingDefOf.VV_DoomFlower, doomSeedDef);
            var fairyRoom = comp.Props.maxFairyFlowers - CountFlowersAndSeeds(map, VVThingDefOf.VV_FairyFlower, fairySeedDef);

            var fairyCount = withFairy && fairyRoom > 0 ? 1 : 0;
            doomCount = Mathf.Min(doomCount, doomRoom);

            var targets = new List<IntVec3>();
            if (fairyCount > 0 && TryLaunchSeedTo(boss, fairySeedDef, targets))
            {
                fairyLaunched = 1;
            }

            for (int i = 0; i < doomCount; i++)
            {
                if (!TryLaunchSeedTo(boss, doomSeedDef, targets)) { break; }
                doomLaunched++;
            }

            if (fairyLaunched > 0)
            {
                PlayLaunchEffect(fairySeedDef, boss);
            }

            if (doomLaunched > 0)
            {
                PlayLaunchEffect(doomSeedDef, boss);
            }
        }

        private static int CountFlowersAndSeeds(Map map, ThingDef flowerDef, ThingDef seedDef)
        {
            return map.listerThings.ThingsOfDef(flowerDef).Count + map.listerThings.ThingsOfDef(seedDef).Count;
        }

        private static bool TryLaunchSeedTo(Pawn boss, ThingDef seedDef, List<IntVec3> targets)
        {
            if (!TryFindSeedTargetCell(boss, targets, out var cell)) { return false; }

            targets.Add(cell);
            var projectile = (Projectile)GenSpawn.Spawn(seedDef, boss.Position, boss.Map);
            projectile.Launch(boss, GetBossBodyPos(boss), cell, cell, ProjectileHitFlags.None);
            return true;
        }

        public static void DrawLinks(Pawn boss)
        {
            if (boss == null || !boss.Spawned) { return; }

            var map = boss.Map;
            var bossPos = GetBossBodyPos(boss);

            DrawFlowerLinks(bossPos, map.listerThings.ThingsOfDef(VVThingDefOf.VV_DoomFlower));
            DrawFlowerLinks(bossPos, map.listerThings.ThingsOfDef(VVThingDefOf.VV_FairyFlower));
            DrawSeedLinks(bossPos, map.listerThings.ThingsOfDef(VVThingDefOf.VV_Projectile_DoomSeed));
            DrawSeedLinks(bossPos, map.listerThings.ThingsOfDef(VVThingDefOf.VV_Projectile_FairySeed));

            var pawns = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < pawns.Count; i++)
            {
                var hediff = pawns[i].health?.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyInterference);
                if (hediff == null) { continue; }

                var color = InterferenceLinkColor;
                color.a *= Mathf.Clamp01(hediff.ageTicks / InterferenceLinkFadeInTicks);
                PetalLinkRenderer.Draw(bossPos, pawns[i].DrawPos + PawnLinkOffset, InterferenceLinkWidth, color, pawns[i].thingIDNumber);
            }
        }

        public static void Notify_BossPhaseChanged(Pawn boss, ApocryphaPhase phase)
        {
            if (phase == ApocryphaPhase.Bound) { return; }

            var map = boss?.MapHeld;
            if (map == null) { return; }

            ClearGimmickThings(map);
            RemoveFairyInterference(map);
            DoomBlightUtility.ClearDoomBlights(map);
        }

        public static void CleanupAll(Map map)
        {
            if (map == null) { return; }

            ClearGimmickThings(map);
            RemoveFairyInterference(map);
            DoomBlightUtility.ClearDoomBlights(map);
        }

        // a와 b는 월드 좌표이며 width는 칸 단위다.
        public static void DrawSoulLink(Vector3 a, Vector3 b, float width, Color color)
        {
            SoulLinkRenderer.Draw(a, b, width, color);
        }

        #region 내부 공용

        internal static bool IsBoundBoss(Pawn boss, out CompApocrypha comp)
        {
            comp = null;
            if (boss == null || !boss.Spawned || boss.Dead) { return false; }

            comp = boss.TryGetComp<CompApocrypha>();
            return comp != null && comp.Phase == ApocryphaPhase.Bound;
        }

        internal static bool IsBoundBoss(Pawn boss)
        {
            return IsBoundBoss(boss, out _);
        }

        internal static bool IsBossFightActive => ViviEndingQuest.Current?.Stage == ViviEndingStage.Boss;

        internal static bool IsGimmickActive(Pawn boss)
        {
            return IsBossFightActive && IsBoundBoss(boss);
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
            return boss.ageTracker.CurLifeStageIndex == 0 ? BoundBodyOffset : UnboundBodyOffset;
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

        internal static void PlayManaBackflow(Pawn vivi, Pawn boss)
        {
            var map = boss.Map;
            var bossPos = GetBossBodyPos(boss);
            var offset = bossPos - boss.Position.ToVector3Shifted();
            offset.y = 0f;

            PlayEffect(VVEffecterDefOf.VV_Effecter_ManaBackflow, boss.Position, map, offset);

            if (vivi != null && vivi.Spawned && vivi.Map == map && bossPos.ShouldSpawnMotesAt(map))
            {
                FleckMaker.ConnectingLine(vivi.DrawPos, bossPos, VVFleckDefOf.VV_Fleck_GimmickBackflowBeam, map, ManaBackflowBeamWidth);
            }

            if (map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(ManaBackflowCameraShake);
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

        // linkSeed는 씨앗에서 이어받을 연결선 무늬다(-1이면 꽃 자신의 무늬로 새로 나타난다).
        internal static Thing SpawnFlower(ThingDef flowerDef, IntVec3 cell, Map map, Pawn boss, int linkSeed = -1)
        {
            var plant = cell.GetPlant(map);
            if (plant != null && !plant.Destroyed)
            {
                plant.Destroy(DestroyMode.Vanish);
            }

            var flower = ThingMaker.MakeThing(flowerDef);
            if (flowerDef == VVThingDefOf.VV_DoomFlower)
            {
                flower.SetFaction(boss?.Faction ?? Faction.OfInsects);
            }

            if (linkSeed >= 0 && flower is Building_ApocryphaFlower apocryphaFlower)
            {
                apocryphaFlower.InheritLink(linkSeed);
            }

            return GenSpawn.Spawn(flower, cell, map, WipeMode.Vanish);
        }

        #endregion

        #region 씨앗 발사

        private static bool TryFindSeedTargetCell(Pawn boss, List<IntVec3> avoid, out IntVec3 result)
        {
            var map = boss.Map;
            var bossCell = boss.Position;
            var altar = Building_EverflowerAltar.FindOnMap(map);
            var altarRect = GetAltarRect(altar);
            var colonyAnchor = GetColonyAnchor(map, altar);
            var minDistanceSquared = MinSeedDistanceFromBoss * MinSeedDistanceFromBoss;

            if (CellFinder.TryFindRandomCell(map, cell =>
                cell.DistanceToSquared(bossCell) >= minDistanceSquared
                && !IsNearAny(avoid, cell, MinVolleySeedSpacing)
                && IsValidBloomCell(cell, map, altarRect, colonyAnchor), out result))
            {
                return true;
            }

            result = IntVec3.Invalid;
            return false;
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

        // CanReachColony는 정착지와 닿지 않아도 맵 가장자리의 가장 큰 구역과 연결되면 통과시켜 외딴 섬도 허용한다.
        // 이를 막으려고 정착지 기준점과의 도달 여부를 확인한다. 구역 쌍별로 캐시되므로 칸마다 조회해도 비용이 작다.
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

        private static void PlayLaunchEffect(ThingDef projectileDef, Pawn boss)
        {
            var effecterDef = projectileDef.GetModExtension<ApocryphaProjectileExtension>()?.launchEffecter;
            if (effecterDef == null) { return; }

            var offset = GetBossBodyPos(boss) - boss.Position.ToVector3Shifted();
            offset.y = 0f;
            PlayEffect(effecterDef, boss.Position, boss.Map, offset);
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
                PetalLinkRenderer.Draw(bossPos, flower.LinkAnchor, extension.linkWidth, color, flower.LinkSeed);
            }
        }

        // 날아가는 씨앗까지 피워 낼 꽃의 연결선을 미리 이어 투사체가 잘 보이게 한다. 착탄하면 꽃이 같은 무늬로 이어받는다.
        private static void DrawSeedLinks(Vector3 bossPos, List<Thing> seeds)
        {
            for (int i = 0; i < seeds.Count; i++)
            {
                if (!(seeds[i] is Projectile_ApocryphaSeed seed) || !seed.Spawned) { continue; }

                var extension = seed.def.projectile.spawnsThingDef?.GetModExtension<ApocryphaFlowerExtension>();
                if (extension == null) { continue; }

                PetalLinkRenderer.Draw(bossPos, seed.ArcDrawPos, extension.linkWidth, extension.linkColor, seed.thingIDNumber);
            }
        }

        #endregion

        #region 정리

        private static void ClearGimmickThings(Map map)
        {
            DestroyAll(map, VVThingDefOf.VV_Projectile_DoomSeed);
            DestroyAll(map, VVThingDefOf.VV_Projectile_FairySeed);

            WitherAll(map, VVThingDefOf.VV_FairyFlower);
            WitherAll(map, VVThingDefOf.VV_DoomFlower);
        }

        private static void DestroyAll(Map map, ThingDef def)
        {
            var things = new List<Thing>(map.listerThings.ThingsOfDef(def));
            for (int i = 0; i < things.Count; i++)
            {
                if (!things[i].Destroyed)
                {
                    things[i].Destroy(DestroyMode.Vanish);
                }
            }
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

        private static void RemoveFairyInterference(Map map)
        {
            var pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < pawns.Count; i++)
            {
                var hediff = pawns[i].health?.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyInterference);
                if (hediff != null)
                {
                    pawns[i].health.RemoveHediff(hediff);
                }
            }
        }

        #endregion
    }
}
