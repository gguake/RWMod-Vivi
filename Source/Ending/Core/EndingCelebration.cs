using RimWorld;
using System;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 【종막】 아포크리파 토벌 → 〈여왕의 대관식〉 대기 → 〈영원한 봄의 제국〉: 6단계 완전 개화·황금 파동·꽃잎 폭풍·정화·크레딧,
    // 그리고 〈영원꽃 찬탈〉 등 실패 처리
    public partial class ViviEndingQuest
    {
        private enum EndingFailReason : byte
        {
            Stolen,
            AnchorLost,
        }

        private const int FinalAttunementLevel = 6;
        private const int CreditsDelayTicks = 360;
        private const int GoldenRingIntervalTicks = 20;
        private const int GoldenRingCount = 3;
        private const int PetalStormTicks = 240;
        private const int PetalCellsPerFleck = 400;
        private const int PetalMinPerTick = 4;
        private const int PetalMaxPerTick = 16;
        private const float BloomFlowerRadius = 45f;
        private const int BloomFlowerCount = 150;
        private const float EndingFlashFadeInSeconds = 0.6f;
        private const float EndingFlashHoldSeconds = 0.67f;     // 40틱(1배속) 뒤 걷어낸다. 일시정지와 무관하게 실시간으로 처리한다.
        private const float EndingFlashFadeOutSeconds = 3f;
        private const float EndingCameraShake = 1.5f;

        private static readonly Color EndingFlashColor = new Color(1f, 0.85f, 0.45f, 0.55f);

        private static readonly Color[] PetalColors =
        {
            new Color(1f, 0.93f, 0.7f),
            new Color(1f, 0.82f, 0.5f),
            new Color(1f, 0.76f, 0.82f),
            new Color(1f, 0.98f, 0.9f),
        };

        // 비저장: 금빛 섬광을 걷어낼 실시간(Time.realtimeSinceStartup). 음수면 대기 중인 섬광 없음.
        private float _fadeBackRealtime = -1f;

        // 꽃봉오리 위치 오프셋(제단 안치·단계별 의식 이펙트 오프셋). 황금 파동이 여기서 퍼진다.
        private Vector3 BudOffset => _anchor?.EverflowerComp?.RitualEffecterOffset ?? Vector3.zero;

        // 보스 토벌 → 응축 100%, 기믹·습격 비콘 정리. 영원꽃은 5단계에 머물고 공명자의 〈여왕의 대관식〉(1회)을 기다린다.
        private void EnterCoronation()
        {
            _stage = ViviEndingStage.Coronation;
            _bloomProgress = 1f;
            _boss = null;
            _bossDeferred = false;
            _bossDeferredMessageSent = false;
            _bossMissingSinceTick = -1;
            _calmEndTick = -1;

            CleanupGimmicks();
            EndOminousSilence();

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingCoronationLabel.Translate(),
                TranslateWithResonator(LocalizeString_EndingCore.VV_Letter_EndingCoronation, AnchorResonator).Resolve(),
                LetterDefOf.PositiveEvent,
                _anchor != null && _anchor.Spawned ? new LookTargets(_anchor) : LookTargets.Invalid);
        }

        // 대관식 완료(또는 개발자 도구) → 6단계 개화, 기믹 정리, 정화·개화, 연출 시작. 잠시 뒤 엔딩 편지와 크레딧.
        private void CompleteEnding(bool removeBoss, Pawn queen)
        {
            var map = AnchorMap ?? _lastAnchorMap;
            var boss = _boss;

            _stage = ViviEndingStage.Completed;
            _bloomProgress = 1f;
            _boss = null;
            _queen = queen;
            _bossDeferred = false;
            _bossDeferredMessageSent = false;
            _bossMissingSinceTick = -1;
            _calmEndTick = -1;

            // 연출·크레딧 예약을 먼저 해 두고, 이후 단계(다른 모듈 코드 포함)가 실패해도 엔딩 자체는 끝까지 진행되게 한다.
            var now = Find.TickManager.TicksGame;
            _endingEffectTick = now;
            _creditsTick = now + CreditsDelayTicks;

            // 기믹(꽃·간섭·저주)을 먼저 정리한 뒤, 살아 있는 보스가 있으면(개발자 도구) 호위 말벌과 함께 사라지게 한다.
            CleanupGimmicks();
            if (removeBoss)
            {
                RunEndingStep("vanish boss", () => VanishBoss(boss));
            }

            EndOminousSilence();
            RunEndingStep("golden bloom fade", FadeGoldenBloom);

            if (_anchor != null && !_anchor.Destroyed)
            {
                RunEndingStep("final bloom", () => _anchor.EverflowerComp?.SetAttunementLevel(FinalAttunementLevel));
            }

            if (map != null)
            {
                RunEndingStep("purify pollution", () => PurifyPollution(map));
                RunEndingStep("bloom vivi flowers", () => BloomViviFlowers(map, GetBloomCenter(map)));
            }

            RunEndingStep("ending effects", () => StartEndingEffects(map));
        }

        private static void RunEndingStep(string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] ending step '{label}' failed: {ex}");
            }
        }

        private IntVec3 GetBloomCenter(Map map)
        {
            var altar = AnchorAltar;
            if (altar != null && altar.Spawned && altar.Map == map)
            {
                return altar.Position;
            }

            if (_anchor != null && _anchor.Spawned && _anchor.Map == map)
            {
                return _anchor.Position;
            }

            return map.Center;
        }

        private void StartEndingEffects(Map map)
        {
            ScreenFader.StartFade(EndingFlashColor, EndingFlashFadeInSeconds);
            _fadeBackRealtime = Time.realtimeSinceStartup + EndingFlashHoldSeconds;

            if (map == null || _anchor == null || !_anchor.Spawned) { return; }

            if (Find.CurrentMap == map)
            {
                Find.CameraDriver.shaker.DoShake(EndingCameraShake);
            }

            EndingCoreDefOf.VV_Effecter_EndingGoldenWave?.Spawn(_anchor.Position, map, BudOffset).Cleanup();
        }

        // 매 틱: 황금 파동 고리 3겹(20틱 간격, 첫 겹은 이펙터가 낸다)과 240틱 동안의 꽃잎 폭풍.
        private void TickEndingEffects(int now)
        {
            var elapsed = now - _endingEffectTick;
            var map = AnchorMap;

            if (map != null && _anchor != null && _anchor.Spawned)
            {
                if (elapsed > 0 && elapsed < GoldenRingIntervalTicks * GoldenRingCount && elapsed % GoldenRingIntervalTicks == 0)
                {
                    SpawnGoldenWaveRing(map);
                }

                if (elapsed < PetalStormTicks)
                {
                    SpawnPetalStorm(map);
                }
            }

            if (elapsed >= PetalStormTicks)
            {
                _endingEffectTick = -1;
            }
        }

        private void UpdateEndingScreenFade()
        {
            if (_fadeBackRealtime < 0f || Time.realtimeSinceStartup < _fadeBackRealtime) { return; }

            _fadeBackRealtime = -1f;
            ScreenFader.StartFade(Color.clear, EndingFlashFadeOutSeconds);
        }

        private void SpawnGoldenWaveRing(Map map)
        {
            var ringDef = EndingCoreDefOf.VV_Fleck_GoldenWaveRing;
            if (ringDef == null) { return; }

            map.flecks.CreateFleck(new FleckCreationData()
            {
                def = ringDef,
                spawnPosition = _anchor.DrawPos + BudOffset,
                scale = 1f,
                rotation = Rand.Range(0f, 360f),
                ageTicksOverride = -1,
            });
        }

        // 카메라 시야 사각형 곳곳에서 영원꽃 꽃잎이 떨어지며 회전한다(보고 있는 맵일 때만).
        private static void SpawnPetalStorm(Map map)
        {
            var petalDef = EndingCoreDefOf.VV_Fleck_EndingPetal;
            if (petalDef == null || Find.CurrentMap != map) { return; }

            var view = Find.CameraDriver.CurrentViewRect.ExpandedBy(2).ClipInsideMap(map);
            if (view.Area <= 0) { return; }

            var count = Mathf.Clamp(view.Area / PetalCellsPerFleck, PetalMinPerTick, PetalMaxPerTick);
            for (int i = 0; i < count; ++i)
            {
                var position = new Vector3(Rand.Range(view.minX, view.maxX + 1f), 0f, Rand.Range(view.minZ, view.maxZ + 1f));
                map.flecks.CreateFleck(new FleckCreationData()
                {
                    def = petalDef,
                    spawnPosition = position,
                    scale = Rand.Range(0.6f, 1.3f),
                    rotation = Rand.Range(0f, 360f),
                    rotationRate = Rand.Range(-240f, 240f),
                    velocityAngle = Rand.Range(150f, 210f),
                    velocitySpeed = Rand.Range(0.8f, 2.4f),
                    instanceColor = PetalColors.RandomElement(),
                    ageTicksOverride = -1,
                });
            }
        }

        // Biotech: 맵 전역의 오염을 정화한다(한 번만 실행).
        private static int PurifyPollution(Map map)
        {
            if (!ModsConfig.BiotechActive || map.pollutionGrid == null) { return 0; }

            var purified = 0;
            foreach (var cell in map.AllCells)
            {
                if (!map.pollutionGrid.IsPolluted(cell)) { continue; }

                cell.Unpollute(map);
                purified++;
            }

            return purified;
        }

        // 제단 반경 45칸 안의 비옥한 토양에 비비아 꽃 약 150송이를 다 자란 상태로 피운다.
        private static int BloomViviFlowers(Map map, IntVec3 center)
        {
            var spawned = 0;
            foreach (var cell in GenRadial.RadialCellsAround(center, BloomFlowerRadius, false).InRandomOrder())
            {
                if (!cell.InBounds(map) || cell.Fogged(map) || cell.Roofed(map)) { continue; }
                if (!ArcanePlantUtility.TrySpawnViviFlower(map, cell, out var flower)) { continue; }

                if (flower is Plant plant)
                {
                    plant.Growth = 1f;
                    plant.DirtyMapMesh(map);
                }

                if (++spawned >= BloomFlowerCount) { break; }
            }

            return spawned;
        }

        private void SendEndingLetterAndCredits()
        {
            var queen = ResolveQueen();
            _queen = null;

            var ending = BuildEndingText(queen);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingVictoryLabel.Translate(),
                ending + "\n\n" + LocalizeString_EndingCore.VV_Letter_EndingVictoryContinue.Translate().Resolve(),
                LetterDefOf.PositiveEvent,
                _anchor != null && _anchor.Spawned ? new LookTargets(_anchor) : LookTargets.Invalid);

            // 바닐라 엔딩 크레딧처럼 제목 없이 이야기 본문으로 시작하고, 뒤에 '기억 속에 남겨짐' 명단을 붙인다.
            var credits = new StringBuilder(ending);

            var memory = GameVictoryUtility.InMemoryOfSection();
            if (!memory.NullOrEmpty())
            {
                credits.Append("\n\n").Append(memory);
            }

            // 크레딧이 닫힌 뒤에도 게임은 계속된다(메인 메뉴로 나가지 않음).
            GameVictoryUtility.ShowCredits(credits.ToString(), SongDefOf.EndCreditsSong, exitToMainMenu: false);
        }

        private enum EndingScenarioKind : byte
        {
            None,
            Exiled,
            Refugee,
        }

        // 시작 시나리오 판별: 플레이어 세력 → 시작 폰 구성 → 시나리오 이름 순.
        // 시나리오 세력이 적용되지 않은 게임(빠른 테스트 게임 등)도 있어서 세력만으로는 부족하다.
        private static EndingScenarioKind DetectEndingScenario()
        {
            var playerFactionDef = Faction.OfPlayer?.def;
            if (playerFactionDef != null && playerFactionDef == EndingCoreDefOf.VV_PlayerExiledVivi) { return EndingScenarioKind.Exiled; }
            if (playerFactionDef != null && playerFactionDef == EndingCoreDefOf.VV_PlayerViviRefugee) { return EndingScenarioKind.Refugee; }

            var scenario = Find.Scenario;
            if (scenario == null) { return EndingScenarioKind.None; }

            foreach (var part in scenario.AllParts)
            {
                if (part is ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs kindDefsPart
                    && kindDefsPart.kindCounts.Any(v => v.kindDef != null && v.kindDef.defName == "VV_PlayerExiledRoyalVivi"))
                {
                    return EndingScenarioKind.Exiled;
                }
            }

            // 세이브에는 시나리오 이름이 게임을 만든 시점의 언어로 남는다. 같은 언어로 플레이 중일 때만 유효한 최후의 판별 수단이다.
            if (!scenario.name.NullOrEmpty())
            {
                if (scenario.name == DefDatabase<ScenarioDef>.GetNamedSilentFail("VV_ViviStart")?.scenario?.name) { return EndingScenarioKind.Exiled; }
                if (scenario.name == DefDatabase<ScenarioDef>.GetNamedSilentFail("VV_ViviRefugeeStart")?.scenario?.name) { return EndingScenarioKind.Refugee; }
            }

            return EndingScenarioKind.None;
        }

        // 즉위한 여왕: 대관식을 치른 공명자 → 앵커 공명자 → 아무 자유 정착민(개발자 도구로 끝낸 경우) 순.
        private Pawn ResolveQueen()
        {
            if (_queen != null && !_queen.Destroyed) { return _queen; }

            return AnchorResonator ?? PawnsFinder.AllMaps_FreeColonists.FirstOrDefault();
        }

        // 바닐라 엔딩 크레딧(공허 노드·세레브렉스 코어)과 같은 짜임: 마지막 행위 → 세계의 변화 → "{PAWN}(은)는 ~했습니다."
        // → 시나리오 문단(추방된 비비/비비 피난민) → "이야기는 계속됩니다..."
        private static string BuildEndingText(Pawn queen)
        {
            var sb = new StringBuilder();
            sb.Append(queen != null
                ? LocalizeString_EndingCore.VV_EndingCredits.Translate(queen.Named("PAWN")).Resolve()
                : LocalizeString_EndingCore.VV_EndingCreditsNoQueen.Translate().Resolve());

            string scenarioKey = null;
            switch (DetectEndingScenario())
            {
                case EndingScenarioKind.Exiled:
                    scenarioKey = LocalizeString_EndingCore.VV_EndingCreditsExiled;
                    break;
                case EndingScenarioKind.Refugee:
                    scenarioKey = LocalizeString_EndingCore.VV_EndingCreditsRefugee;
                    break;
            }

            if (scenarioKey != null)
            {
                sb.Append("\n\n").Append(scenarioKey.Translate().Resolve());
            }

            sb.Append("\n\n").Append(LocalizeString_EndingCore.VV_EndingCreditsOutro.Translate().Resolve());
            return sb.ToString();
        }

        // 엔딩 실패: 상태·편지·정리만 한다. 찬탈(Stolen)의 영원꽃 파괴와 보스 퇴장은 보스 모듈이 이 호출 전후에 처리한다.
        private void FailEnding(EndingFailReason reason, bool removeBoss)
        {
            var boss = _boss;
            var map = AnchorMap ?? _lastAnchorMap;
            var altar = map != null ? Building_DreamumAltar.FindOnMap(map) : null;

            _stage = ViviEndingStage.Failed;
            _boss = null;
            _bossDeferred = false;
            _bossDeferredMessageSent = false;
            _bossMissingSinceTick = -1;
            _calmEndTick = -1;
            _endingEffectTick = -1;
            _creditsTick = -1;

            // 찬탈 이외의 실패(앵커 소실·개발자 도구)는 기믹을 정리한 뒤 살아 있는 보스와 호위 말벌을 사라지게 한다.
            // 찬탈(Stolen)은 보스 모듈이 이 호출 뒤에 영원꽃·호위·보스를 직접 지운다.
            CleanupGimmicks();
            if (removeBoss)
            {
                RunEndingStep("vanish boss", () => VanishBoss(boss));
            }

            EndOminousSilence();
            RunEndingStep("golden bloom end", EndGoldenBloom);

            var stolen = reason == EndingFailReason.Stolen;
            Find.LetterStack.ReceiveLetter(
                (stolen ? LocalizeString_EndingCore.VV_Letter_EndingFailureLabel : LocalizeString_EndingCore.VV_Letter_EndingFailureLostLabel).Translate(),
                (stolen ? LocalizeString_EndingCore.VV_Letter_EndingFailureStolen : LocalizeString_EndingCore.VV_Letter_EndingFailureLost).Translate(),
                LetterDefOf.NegativeEvent,
                altar != null ? new LookTargets(altar) : LookTargets.Invalid);
        }

        // 살아 있는 보스를 호위 거대 말벌과 함께 순간 소실 연출로 없앤다(시체 없음). 엔딩이 보스 생존 중에 끝났을 때만 쓴다.
        private static void VanishBoss(Pawn boss)
        {
            if (boss == null || boss.Destroyed || !boss.Spawned) { return; }

            var map = boss.Map;
            var vanishEffect = ApocryphaEffecterDefOf.VV_Effecter_ApocryphaVanish ?? EffecterDefOf.Skip_EntryNoDelay;
            foreach (var hornet in ApocryphaUtility.GetEscorts(boss))
            {
                if (hornet == null || hornet.Destroyed) { continue; }

                if (hornet.Spawned)
                {
                    vanishEffect.Spawn(hornet.Position, hornet.Map).Cleanup();
                }

                hornet.Destroy(DestroyMode.Vanish);
            }

            vanishEffect.Spawn(boss.Position, map, 2.5f).Cleanup();
            boss.Destroy(DestroyMode.Vanish);
        }

        // 멸망꽃·요정꽃·요정 간섭·멸망꽃 페널티를 모든 맵에서 정리한다(앵커가 이미 사라졌어도 빠짐없이).
        private static void CleanupGimmicks()
        {
            foreach (var map in Find.Maps.ToList())
            {
                try
                {
                    ApocryphaGimmicks.CleanupAll(map);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to clean up Apocrypha gimmicks on {map}: {ex}");
                }
            }
        }
    }
}
