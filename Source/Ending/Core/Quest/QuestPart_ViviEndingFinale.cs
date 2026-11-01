using RimWorld;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class QuestPart_ViviEndingFinale : QuestPartActivable
    {
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
        private const float EndingCameraShake = 1.5f;

        private static readonly Color[] PetalColors =
        {
            new Color(1f, 0.93f, 0.7f),
            new Color(1f, 0.82f, 0.5f),
            new Color(1f, 0.76f, 0.82f),
            new Color(1f, 0.98f, 0.9f),
        };

        public ArcanePlant_Everflower anchor;

        private Pawn _queen;
        private int _startTick = -1;

        private Map AnchorMap => anchor != null && !anchor.Destroyed ? anchor.MapHeld : null;

        private Vector3 BudOffset => anchor?.EverflowerComp?.RitualEffecterOffset ?? Vector3.zero;

        protected override void Enable(SignalArgs receivedArgs)
        {
            base.Enable(receivedArgs);

            _startTick = Find.TickManager.TicksGame;
            if (!receivedArgs.TryGetArg("PAWN", out _queen))
            {
                _queen = null;
            }

            Begin();
        }

        // 크레딧 예약은 _startTick 기준이라 이후 연출이 실패해도 엔딩은 끝까지 진행된다.
        private void Begin()
        {
            var map = AnchorMap;

            ViviEndingUtility.RunStep("finale: cleanup gimmicks", ViviEndingUtility.CleanupGimmicks);

            var boss = quest.GetFirstPartOfType<QuestPart_ViviApocrypha>()?.Boss;
            ViviEndingUtility.RunStep("finale: vanish boss", () => ViviEndingUtility.VanishBoss(boss));

            ViviEndingUtility.RunStep("finale: end ominous silence", ViviEndingUtility.EndOminousSilence);
            ViviEndingUtility.RunStep("finale: golden bloom fade", ViviEndingUtility.FadeGoldenBloom);

            if (anchor != null && !anchor.Destroyed)
            {
                ViviEndingUtility.RunStep("finale: final bloom", () => anchor.EverflowerComp?.SetAttunementLevel(FinalAttunementLevel));
            }

            if (map != null)
            {
                ViviEndingUtility.RunStep("finale: purify pollution", () => PurifyPollution(map));
                ViviEndingUtility.RunStep("finale: bloom vivi flowers", () => BloomViviFlowers(map, GetBloomCenter(map)));
            }

            ViviEndingUtility.RunStep("finale: ending effects", () => StartEndingEffects(map));
        }

        public override void QuestPartTick()
        {
            base.QuestPartTick();

            var elapsed = Find.TickManager.TicksGame - _startTick;
            var map = AnchorMap;

            if (map != null && anchor.Spawned)
            {
                if (elapsed > 0 && elapsed < GoldenRingIntervalTicks * GoldenRingCount && elapsed % GoldenRingIntervalTicks == 0)
                {
                    ViviEndingUtility.RunStep("finale: golden ring", () => SpawnGoldenWaveRing(map));
                }

                if (elapsed < PetalStormTicks)
                {
                    ViviEndingUtility.RunStep("finale: petal storm", () => SpawnPetalStorm(map));
                }
            }

            if (elapsed >= CreditsDelayTicks)
            {
                ViviEndingUtility.RunStep("finale: ending letter and credits", SendEndingLetterAndCredits);
                Complete();
            }
        }

        private IntVec3 GetBloomCenter(Map map)
        {
            var altar = ViviEndingUtility.AltarOf(anchor);
            if (altar != null && altar.Spawned && altar.Map == map)
            {
                return altar.Position;
            }

            if (anchor != null && anchor.Spawned && anchor.Map == map)
            {
                return anchor.Position;
            }

            return map.Center;
        }

        private void StartEndingEffects(Map map)
        {
            ViviEndingQuest.Current?.PlayEndingFlash();

            if (map == null || anchor == null || !anchor.Spawned) { return; }

            if (Find.CurrentMap == map)
            {
                Find.CameraDriver.shaker.DoShake(EndingCameraShake);
            }

            EndingCoreDefOf.VV_Effecter_EndingGoldenWave?.Spawn(anchor.Position, map, BudOffset).Cleanup();
        }

        private void SpawnGoldenWaveRing(Map map)
        {
            var ringDef = EndingCoreDefOf.VV_Fleck_GoldenWaveRing;
            if (ringDef == null) { return; }

            map.flecks.CreateFleck(new FleckCreationData()
            {
                def = ringDef,
                spawnPosition = anchor.DrawPos + BudOffset,
                scale = 1f,
                rotation = Rand.Range(0f, 360f),
                ageTicksOverride = -1,
            });
        }

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
            var ending = BuildEndingText(ResolveQueen());

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingVictoryLabel.Translate(),
                ending + "\n\n" + LocalizeString_EndingCore.VV_Letter_EndingVictoryContinue.Translate().Resolve(),
                LetterDefOf.PositiveEvent,
                anchor != null && anchor.Spawned ? new LookTargets(anchor) : LookTargets.Invalid,
                null,
                quest);

            var credits = new StringBuilder(ending);

            var memory = GameVictoryUtility.InMemoryOfSection();
            if (!memory.NullOrEmpty())
            {
                credits.Append("\n\n").Append(memory);
            }

            GameVictoryUtility.ShowCredits(credits.ToString(), SongDefOf.EndCreditsSong, exitToMainMenu: false);
        }

        private Pawn ResolveQueen()
        {
            if (_queen != null && !_queen.Destroyed) { return _queen; }

            return ViviEndingUtility.ResonatorOf(anchor) ?? PawnsFinder.AllMaps_FreeColonists.FirstOrDefault();
        }

        private enum EndingScenarioKind : byte
        {
            None,
            Exiled,
            Refugee,
        }

        // 빠른 테스트 게임 등에는 시나리오 세력이 적용되지 않아 세력만으로 시나리오를 판별할 수 없다.
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

            // 시나리오 이름은 게임 생성 당시 언어로 저장되므로 같은 언어로 플레이 중일 때만 유효한 최후의 판별 수단이다.
            if (!scenario.name.NullOrEmpty())
            {
                if (scenario.name == DefDatabase<ScenarioDef>.GetNamedSilentFail("VV_ViviStart")?.scenario?.name) { return EndingScenarioKind.Exiled; }
                if (scenario.name == DefDatabase<ScenarioDef>.GetNamedSilentFail("VV_ViviRefugeeStart")?.scenario?.name) { return EndingScenarioKind.Refugee; }
            }

            return EndingScenarioKind.None;
        }

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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref anchor, "anchor");
            Scribe_References.Look(ref _queen, "queen");
            Scribe_Values.Look(ref _startTick, "startTick", -1);
        }
    }
}
