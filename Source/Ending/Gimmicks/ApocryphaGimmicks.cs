using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    // [계약] 아포크리파 1페이즈 기믹(요정 비비 사출 포격, 멸망꽃/요정꽃, 요정 간섭, 영혼 연결선) 정적 API.
    // 보스 쪽(Task D)이 타이머/조준을 관리하고 실제 발사·생성·연출은 이 모듈(Task E)이 담당한다.
    public static class ApocryphaGimmicks
    {
        // 씨앗 착탄 칸: 맵 무작위 칸, 보스로부터 8칸 이상
        private const float MinSeedDistanceFromBoss = 8f;

        // 한 번에 흩뿌린 씨앗끼리 떨어져야 하는 거리
        private const float MinVolleySeedSpacing = 5f;

        // 착탄 순간 칸이 막혀 있으면(비행 중 건물/폰이 들어온 경우) 주변에서 다시 찾는 범위
        private const int BloomCellFallbackRadius = 4;

        // 보스 몸통 중심(연결선·사출탄의 기준점): DrawPos에서 화면 위쪽으로. 1페이즈 여왕은 가슴 브로치, 2페이즈 말벌은 가슴께.
        private static readonly Vector3 BoundBodyOffset = new Vector3(0f, 0f, 0.6f);
        private static readonly Vector3 UnboundBodyOffset = new Vector3(0f, 0f, 1.5f);

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

        // [시험용] 보스는 TryLaunchSeedVolley만 쓴다. 아래 두 메서드는 자동 테스트 스크립트가 리플렉션으로 부르는 진입점이므로
        // 이름·시그니처를 바꿀 때는 스크립트도 함께 고친다.

        // 맵의 무작위 빈 바닥(물/깊은 물 제외)으로 멸망꽃 씨앗 투사체를 날린다. 착탄 시 멸망꽃이 피어난다.
        public static bool TryLaunchDoomSeed(Pawn boss)
        {
            TryLaunchSeedVolley(boss, 1, false, out var doomLaunched, out _);
            return doomLaunched > 0;
        }

        // 맵의 무작위 빈 바닥으로 요정꽃 씨앗 투사체를 날린다. 착탄 시 요정꽃이 피어난다(요정꽃이 이미 최대 개수면 아무것도 쏘지 않는다).
        public static bool TryLaunchFairySeed(Pawn boss)
        {
            TryLaunchSeedVolley(boss, 0, true, out _, out var fairyLaunched);
            return fairyLaunched > 0;
        }

        // 보스의 멸망꽃 사출 1회: 멸망꽃 씨앗 doomCount개를 서로 떨어진 빈 바닥으로 흩뿌린다. withFairy면 요정꽃 씨앗 1개를 더한다.
        // 맵의 꽃과 날아가는 씨앗을 합친 수는 보스의 maxDoomFlowers/maxFairyFlowers를 넘지 않는다(요정꽃이 꽉 찼으면 요정꽃 씨앗은 쏘지 않는다).
        public static void TryLaunchSeedVolley(Pawn boss, int doomCount, bool withFairy, out int doomLaunched, out int fairyLaunched)
        {
            doomLaunched = 0;
            fairyLaunched = 0;
            if (!IsBoundBoss(boss, out var comp) || (doomCount <= 0 && !withFairy)) { return; }

            var map = boss.Map;
            var doomSeedDef = ApocryphaGimmickDefOf.VV_Projectile_DoomSeed;
            var fairySeedDef = ApocryphaGimmickDefOf.VV_Projectile_FairySeed;
            var doomRoom = comp.Props.maxDoomFlowers - CountFlowersAndSeeds(map, ApocryphaGimmickDefOf.VV_DoomFlower, doomSeedDef);
            var fairyRoom = comp.Props.maxFairyFlowers - CountFlowersAndSeeds(map, ApocryphaGimmickDefOf.VV_FairyFlower, fairySeedDef);

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

        // targets: 같은 사출에서 이미 고른 착탄 칸(새 칸은 이 칸들과 떨어진 곳에서 고르고, 고른 칸을 덧붙인다).
        private static bool TryLaunchSeedTo(Pawn boss, ThingDef seedDef, List<IntVec3> targets)
        {
            if (!TryFindSeedTargetCell(boss, targets, out var cell)) { return false; }

            targets.Add(cell);
            var projectile = (Projectile)GenSpawn.Spawn(seedDef, boss.Position, boss.Map);
            projectile.Launch(boss, GetBossBodyPos(boss), cell, cell, ProjectileHitFlags.None);
            return true;
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
        // 지금 그려지는 몸(생애 단계 0 = 1페이즈 그림)에 맞춘 몸통 중심 오프셋
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
            var altar = Building_DreamumAltar.FindOnMap(map);
            var altarRect = GetAltarRect(altar);
            var colonyAnchor = GetColonyAnchor(map, altar);
            if (IsValidBloomCell(cell, map, altarRect, colonyAnchor))
            {
                result = cell;
                return true;
            }

            return CellFinder.TryFindRandomCellNear(cell, map, BloomCellFallbackRadius, c => IsValidBloomCell(c, map, altarRect, colonyAnchor), out result);
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

        // avoid: 같은 차례에 이미 고른 착탄 칸(이 칸들과 MinVolleySeedSpacing 이상 떨어진 곳만 고른다)
        // 맵 무작위 칸을 고른다. 조건에 맞는 칸이 드물어 무작위 시도가 모두 빗나가면 바닐라 TryFindRandomCell이 맵 전체를 훑어 찾는다.
        private static bool TryFindSeedTargetCell(Pawn boss, List<IntVec3> avoid, out IntVec3 result)
        {
            var map = boss.Map;
            var bossCell = boss.Position;
            var altar = Building_DreamumAltar.FindOnMap(map);
            var altarRect = GetAltarRect(altar);
            var colonyAnchor = GetColonyAnchor(map, altar);
            var minDistanceSquared = MinSeedDistanceFromBoss * MinSeedDistanceFromBoss;

            // 싼 검사부터: 보스와의 거리 → 같은 사출의 다른 착탄 칸과의 간격 → 칸 상태 → 정착지에서의 도달 여부
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

        // 꽃이 필 수 있는 빈 바닥: 서 있을 수 있고, 물(깊은 물 포함)이 아니며, 건물·아이템·폰·청사진이 없고,
        // 두꺼운 지붕 아래나 안개 속에 있지 않으며, 제단 발판이 아니고, 정착지에서 걸어갈 수 있는 칸.
        // 일반 흙·암석 바닥·인공 바닥은 모두 허용한다(야생 풀은 피어날 때 치우지만, 재배 구역의 작물은 건드리지 않는다).
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

        // 정착지에서 이 칸까지 걸어갈 수 있는지. 바닐라 CanReachColony는 칸마다 모든 정착민·정착지 건물과의 도달을 따진 뒤
        // 그래도 안 되면 '가장 큰 맵 가장자리 구역'에서 닿는지로 넘어가, 정착민이 못 가는 땅(물·용암 건너편 섬 등)도 통과시킨다.
        // 대신 정착지 기준점 한 곳과의 도달만 본다(구역 쌍 단위로 캐시되므로 칸마다 불러도 싸다).
        private static bool CanReachFromColony(IntVec3 cell, Map map, LocalTargetInfo colonyAnchor)
        {
            if (!colonyAnchor.IsValid) { return map.reachability.CanReachColony(cell); }

            return map.reachability.CanReach(cell, colonyAnchor, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors));
        }

        // 정착지 기준점: 엔딩 제단(보스전이 벌어지는 곳). 제단이 없으면 아무 자유 정착민, 그마저 없으면 Invalid(바닐라 판정으로 대신).
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
