using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    // [계약] 아포크리파 1페이즈 기믹(요정 비비 사출 포격, 멸망꽃/요정꽃, 요정 간섭, 영혼 연결선) 정적 API.
    // 보스 쪽(Task D)이 타이머/조준을 관리하고 실제 발사·생성·연출은 이 모듈(Task E)이 담당한다.
    public static class ApocryphaGimmicks
    {
        // 동시에 맵에 있을 수 있는 꽃 수(날아가는 중인 씨앗 포함). 보스 타이머에서 개수 제한을 지키지 못하더라도 여기서 한 번 더 확인해 생성을 막는다.
        public const int MaxDoomFlowers = 9;
        public const int MaxFairyFlowers = 2;

        // 씨앗 착탄 칸: 맵 무작위 칸을 최대 200번 시도, 보스로부터 8칸 이상
        private const int SeedCellSearchTries = 200;
        private const float MinSeedDistanceFromBoss = 8f;

        // 한 번에 흩뿌린 씨앗끼리 떨어져야 하는 거리
        private const float MinVolleySeedSpacing = 5f;

        // 착탄 순간 칸이 막혀 있으면(비행 중 건물/폰이 들어온 경우) 주변에서 다시 찾는 범위
        private const int BloomCellFallbackRadius = 4;

        // 보스 몸통 중심(연결선·사출탄의 기준점): DrawPos에서 화면 위쪽으로 약 1.5칸
        public static readonly Vector3 BossBodyOffset = new Vector3(0f, 0f, 1.5f);

        // 요정 간섭 비비 쪽 연결선 끝(가슴 높이)과 연결선 모양
        private static readonly Vector3 PawnLinkOffset = new Vector3(0f, 0f, 0.35f);
        private static readonly Color InterferenceLinkColor = new Color(1f, 0.9f, 0.6f);
        private const float InterferenceLinkWidth = 0.9f;
        private const float InterferenceLinkFadeInTicks = 45f;

        // 결속 해방(마나 역류) 연출
        private const float ManaBackflowBeamWidth = 1.6f;
        private const float ManaBackflowCameraShake = 1.5f;

        // 조준(경고 연출)이 끝난 뒤 보스가 target으로 요정 비비를 사출한다. 성공 시 보스 결속 -1(Notify_FairyShellFired)까지 처리.
        public static bool TryFireFairyShell(Pawn boss, LocalTargetInfo target)
        {
            if (!IsBoundBoss(boss, out var comp)) { return false; }
            if (!target.IsValid) { return false; }

            var map = boss.Map;
            var cell = target.Cell;
            if (!cell.InBounds(map)) { return false; }

            var shellDef = ApocryphaGimmickDefOf.VV_Projectile_FairyShell;
            var projectile = (Projectile)GenSpawn.Spawn(shellDef, boss.Position, map);
            projectile.Launch(boss, GetBossBodyPos(boss), target, target, ProjectileHitFlags.IntendedTarget);

            PlayLaunchEffect(shellDef, boss);
            comp.Notify_FairyShellFired();
            return true;
        }

        // 맵의 무작위 빈 바닥(물/깊은 물 제외)으로 멸망꽃 씨앗 투사체를 날린다. 착탄 시 멸망꽃이 피어난다.
        public static bool TryLaunchDoomSeed(Pawn boss)
        {
            return TryLaunchDoomSeeds(boss, 1) > 0;
        }

        // 멸망꽃 씨앗을 한 번에 count개까지 서로 떨어진 빈 바닥으로 흩뿌린다(맵의 멸망꽃·날아가는 씨앗 합계는 최대 MaxDoomFlowers).
        // 실제로 날린 씨앗 수를 반환한다.
        public static int TryLaunchDoomSeeds(Pawn boss, int count)
        {
            if (!IsBoundBoss(boss, out _) || count <= 0) { return 0; }

            var seedDef = ApocryphaGimmickDefOf.VV_Projectile_DoomSeed;
            var flowerDef = ApocryphaGimmickDefOf.VV_DoomFlower;
            var map = boss.Map;
            var room = MaxDoomFlowers - map.listerThings.ThingsOfDef(flowerDef).Count - map.listerThings.ThingsOfDef(seedDef).Count;

            var targets = new List<IntVec3>();
            for (int i = 0; i < Mathf.Min(count, room); i++)
            {
                if (!TryFindSeedTargetCell(boss, targets, out var cell)) { break; }

                targets.Add(cell);
                var projectile = (Projectile)GenSpawn.Spawn(seedDef, boss.Position, map);
                projectile.Launch(boss, GetBossBodyPos(boss), cell, cell, ProjectileHitFlags.None);
            }

            if (targets.Count > 0)
            {
                PlayLaunchEffect(seedDef, boss);
            }

            return targets.Count;
        }

        // 맵의 무작위 빈 바닥으로 요정꽃 씨앗 투사체를 날린다. 착탄 시 요정꽃이 피어난다.
        public static bool TryLaunchFairySeed(Pawn boss)
        {
            return TryLaunchSeed(boss, ApocryphaGimmickDefOf.VV_Projectile_FairySeed, ApocryphaGimmickDefOf.VV_FairyFlower, MaxFairyFlowers);
        }

        // 보스의 매 프레임 그리기 훅에서 호출: 보스↔꽃, 보스↔요정 간섭 비비 사이의 영혼 연결선을 그린다.
        // 짧은 목록만 훑으므로 카메라 밖이어도 그대로 그린다.
        public static void DrawLinks(Pawn boss)
        {
            if (boss == null || !boss.Spawned) { return; }

            var map = boss.Map;
            var bossPos = GetBossBodyPos(boss);

            DrawFlowerLinks(bossPos, map.listerThings.ThingsOfDef(ApocryphaGimmickDefOf.VV_DoomFlower));
            DrawFlowerLinks(bossPos, map.listerThings.ThingsOfDef(ApocryphaGimmickDefOf.VV_FairyFlower));

            // 요정 간섭은 플레이어 비비만 얻을 수 있으므로 플레이어 세력 폰만 본다.
            var pawns = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < pawns.Count; i++)
            {
                var hediff = pawns[i].health?.hediffSet.GetFirstHediffOfDef(ApocryphaGimmickDefOf.VV_FairyInterference);
                if (hediff == null) { continue; }

                var color = InterferenceLinkColor;
                color.a *= Mathf.Clamp01(hediff.ageTicks / InterferenceLinkFadeInTicks);
                DrawSoulLink(bossPos, pawns[i].DrawPos + PawnLinkOffset, InterferenceLinkWidth, color);
            }
        }

        // 보스 페이즈가 바뀔 때 호출(2페이즈 진입 시 남은 요정꽃/요정 간섭 정리 등).
        // 페널티(멸망꽃 저주)는 유지하고, 엔딩에서 CleanupAll이 해제한다.
        public static void Notify_BossPhaseChanged(Pawn boss, ApocryphaPhase phase)
        {
            if (phase == ApocryphaPhase.Bound) { return; }

            var map = boss?.MapHeld;
            if (map == null) { return; }

            ClearGimmickThings(map);
            RemoveFairyInterference(map);
        }

        // 엔딩 완료/실패 시 호출: 맵의 멸망꽃·요정꽃·멸망꽃 디버프·요정 간섭을 모두 정리한다.
        public static void CleanupAll(Map map)
        {
            if (map == null) { return; }

            ClearGimmickThings(map);
            RemoveFairyInterference(map);
            DoomBlightUtility.ClearDoomBlights(map);
        }

        // 영혼 연결 빔 하나를 그린다(VV_SoulLink 셰이더). a, b는 월드 좌표, width는 칸 단위.
        public static void DrawSoulLink(Vector3 a, Vector3 b, float width, Color color)
        {
            SoulLinkRenderer.Draw(a, b, width, color);
        }

        #region 내부 공용

        // 보스가 스폰되어 있고 1페이즈(결속 상태)인지
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

        // 엔딩 퀘스트가 보스 단계인지. 싸움이 끝난 뒤(완료·실패) 착탄한 씨앗·만개한 멸망꽃·요정 간섭은 아무 효과도 내지 않는다.
        internal static bool IsBossFightActive => ViviEndingQuest.Current?.Stage == ViviEndingStage.Boss;

        // 기믹이 실제로 효과를 내도 되는지: 보스 단계이고, 해당 보스가 스폰된 1페이즈(결속 상태)일 때.
        internal static bool IsGimmickActive(Pawn boss)
        {
            return IsBossFightActive && IsBoundBoss(boss);
        }

        // 맵 위의 아포크리파. 퀘스트가 기억하는 보스를 먼저 보고, 없으면(개발자 도구로 직접 소환 등) 맵 폰을 훑는다.
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

        // 보스 몸통 중심(연결선·사출 기준). 맵 위쪽 가장자리라 기준점이 맵 밖이면 보스 위치를 그대로 쓴다.
        internal static Vector3 GetBossBodyPos(Pawn boss)
        {
            var drawPos = boss.DrawPos;
            var bodyPos = drawPos + BossBodyOffset;
            return boss.Map != null && bodyPos.ToIntVec3().InBounds(boss.Map) ? bodyPos : drawPos;
        }

        // 한 번 재생되는 이펙트. maintainTicks가 있으면 지연 스프레이까지 끝나도록 맵에 맡긴다.
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

        // 결속 해방 연출: 보스 위치의 거대한 마나 역류 섬광 + 비비→보스 빔 플래시 + 카메라 흔들림
        internal static void PlayManaBackflow(Pawn vivi, Pawn boss)
        {
            var map = boss.Map;
            var bossPos = GetBossBodyPos(boss);
            var offset = bossPos - boss.Position.ToVector3Shifted();
            offset.y = 0f;

            PlayEffect(ApocryphaGimmickDefOf.VV_Effecter_ManaBackflow, boss.Position, map, offset);

            if (vivi != null && vivi.Spawned && vivi.Map == map && bossPos.ShouldSpawnMotesAt(map))
            {
                FleckMaker.ConnectingLine(vivi.DrawPos, bossPos, ApocryphaGimmickDefOf.VV_Fleck_GimmickBackflowBeam, map, ManaBackflowBeamWidth);
            }

            if (map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(ManaBackflowCameraShake);
            }
        }

        // 사출탄 폭발에서 제외할 것: 보스, 맵의 거대 말벌 전부, 보스가 피운 꽃.
        // 엔딩에 연결된 제단과 영원꽃도 보스 자신의 포격으로 피해를 받지 않게 한다.
        internal static List<Thing> GetBlastIgnoredThings(Map map, Thing boss)
        {
            var ignored = new List<Thing>();
            if (boss != null) { ignored.Add(boss); }

            var anchor = ViviEndingQuest.Current?.AnchorEverflower;
            if (anchor != null && anchor.Spawned && anchor.Map == map) { ignored.Add(anchor); }

            var altar = Building_DreamumAltar.FindOnMap(map);
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

            ignored.AddRange(map.listerThings.ThingsOfDef(ApocryphaGimmickDefOf.VV_DoomFlower));
            ignored.AddRange(map.listerThings.ThingsOfDef(ApocryphaGimmickDefOf.VV_FairyFlower));
            return ignored;
        }

        // 씨앗이 떨어진 칸(또는 그 근처)에 꽃을 피울 칸을 다시 고른다.
        internal static bool TryFindBloomCellNear(IntVec3 cell, Map map, out IntVec3 result)
        {
            var altarRect = GetAltarRect(map);
            if (IsValidBloomCell(cell, map, altarRect))
            {
                result = cell;
                return true;
            }

            return CellFinder.TryFindRandomCellNear(cell, map, BloomCellFallbackRadius, c => IsValidBloomCell(c, map, altarRect), out result);
        }

        // 꽃을 피운다. 멸망꽃은 적대 표시(아군 자동 공격)를 위해 보스 세력에 속한다.
        internal static Thing SpawnFlower(ThingDef flowerDef, IntVec3 cell, Map map, Pawn boss)
        {
            // 풀 같은 기존 식물은 먼저 치운다(생성 시 지워지지 않는 식물이 남지 않게).
            var plant = cell.GetPlant(map);
            if (plant != null && !plant.Destroyed)
            {
                plant.Destroy(DestroyMode.Vanish);
            }

            var flower = ThingMaker.MakeThing(flowerDef);
            if (flowerDef == ApocryphaGimmickDefOf.VV_DoomFlower)
            {
                flower.SetFaction(boss?.Faction ?? Faction.OfInsects);
            }

            return GenSpawn.Spawn(flower, cell, map, WipeMode.Vanish);
        }

        #endregion

        #region 씨앗 발사

        private static bool TryLaunchSeed(Pawn boss, ThingDef seedDef, ThingDef flowerDef, int maxCount)
        {
            if (!IsBoundBoss(boss, out _)) { return false; }

            var map = boss.Map;
            var count = map.listerThings.ThingsOfDef(flowerDef).Count + map.listerThings.ThingsOfDef(seedDef).Count;
            if (count >= maxCount) { return false; }

            if (!TryFindSeedTargetCell(boss, null, out var cell)) { return false; }

            var projectile = (Projectile)GenSpawn.Spawn(seedDef, boss.Position, map);
            projectile.Launch(boss, GetBossBodyPos(boss), cell, cell, ProjectileHitFlags.None);

            PlayLaunchEffect(seedDef, boss);
            return true;
        }

        // avoid: 같은 차례에 이미 고른 착탄 칸(이 칸들과 MinVolleySeedSpacing 이상 떨어진 곳만 고른다)
        private static bool TryFindSeedTargetCell(Pawn boss, List<IntVec3> avoid, out IntVec3 result)
        {
            var map = boss.Map;
            var altarRect = GetAltarRect(map);
            var minDistanceSquared = MinSeedDistanceFromBoss * MinSeedDistanceFromBoss;

            for (int i = 0; i < SeedCellSearchTries; i++)
            {
                var cell = CellFinder.RandomCell(map);
                if (cell.DistanceToSquared(boss.Position) < minDistanceSquared) { continue; }
                if (avoid != null && avoid.Any(v => v.InHorDistOf(cell, MinVolleySeedSpacing))) { continue; }
                if (!IsValidBloomCell(cell, map, altarRect)) { continue; }

                result = cell;
                return true;
            }

            result = IntVec3.Invalid;
            return false;
        }

        // 꽃이 필 수 있는 빈 바닥: 서 있을 수 있고, 물(깊은 물 포함)이 아니며, 건물·아이템·폰·청사진이 없고,
        // 두꺼운 지붕 아래나 안개 속에 있지 않으며, 제단 발판이 아니고, 정착지에서 걸어갈 수 있는 칸.
        // 일반 흙·암석 바닥·인공 바닥은 모두 허용한다(야생 풀은 피어날 때 치우지만, 재배 구역의 작물은 건드리지 않는다).
        private static bool IsValidBloomCell(IntVec3 cell, Map map, CellRect altarRect)
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

            return map.reachability.CanReachColony(cell);
        }

        private static CellRect GetAltarRect(Map map)
        {
            var altar = Building_DreamumAltar.FindOnMap(map);
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
                DrawSoulLink(bossPos, flower.LinkAnchor, extension.linkWidth, color);
            }
        }

        #endregion

        #region 정리

        // 비행 중인 씨앗, 요정꽃, 멸망꽃(페널티 없이 시듦)을 모두 치운다.
        private static void ClearGimmickThings(Map map)
        {
            DestroyAll(map, ApocryphaGimmickDefOf.VV_Projectile_DoomSeed);
            DestroyAll(map, ApocryphaGimmickDefOf.VV_Projectile_FairySeed);

            WitherAll(map, ApocryphaGimmickDefOf.VV_FairyFlower);
            WitherAll(map, ApocryphaGimmickDefOf.VV_DoomFlower);
        }

        private static void DestroyAll(Map map, ThingDef def)
        {
            // 파괴하면 목록이 줄어들므로 복사본을 순회한다.
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
            // 파괴하면 목록이 줄어들므로 복사본을 순회한다.
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
                var hediff = pawns[i].health?.hediffSet.GetFirstHediffOfDef(ApocryphaGimmickDefOf.VV_FairyInterference);
                if (hediff != null)
                {
                    pawns[i].health.RemoveHediff(hediff);
                }
            }
        }

        #endregion
    }
}
