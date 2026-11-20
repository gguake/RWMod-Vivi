using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VVRace
{
    public class RuinGalleryBlastAim : IExposable
    {
        public IntVec3 cell = IntVec3.Invalid;
        public int detonateTick = -1;

        private Mote _targetMote;
        private Effecter _effecter;

        public RuinGalleryBlastAim()
        {
        }

        public RuinGalleryBlastAim(IntVec3 cell, int detonateTick)
        {
            this.cell = cell;
            this.detonateTick = detonateTick;
        }

        public void TickVisuals(Map map, int aimTicks)
        {
            if (map == null || !cell.InBounds(map)) { return; }

            var aimStartTick = Mathf.Min(detonateTick - aimTicks, Find.TickManager.TicksGame);
            if (_targetMote == null || _targetMote.Destroyed)
            {
                _targetMote = MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, VVThingDefOf.VV_Mote_ApocryphaAimTarget, 1f, makeOffscreen: true);
                _targetMote?.ForceSpawnTick(aimStartTick);
            }

            _targetMote?.Maintain();

            var target = new TargetInfo(cell, map);
            EndingVfxUtility.MaintainEffecter(ref _effecter, VVEffecterDefOf.VV_Effecter_ApocryphaAim, target, target);
        }

        public void CleanupVisuals()
        {
            EndingVfxUtility.CleanupEffecter(ref _effecter);
            _targetMote = null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref detonateTick, "detonateTick", -1);
        }
    }

    public class MapComponent_ViviAncientRuins : MapComponent
    {
        public const int DoomRespawnDelayTicks = 90;
        public const int PuzzleTetherDurationTicks = 3600;
        public const int UnsealFadeDurationTicks = 90;
        public const int ResealDurationTicks = 180;
        public const int TrialBlightDurationTicks = GenDate.TicksPerDay;
        public const int GalleryBlastAimTicks = 240;
        private const int MaxConcurrentGalleryBlasts = 3;
        private const float GalleryBlastMinSpacing = 3.0f;
        private const int GalleryTargetRescanTicks = 30;
        private static readonly IntRange GalleryBlastIntervalTicks = new IntRange(120, 210);
        private const float PathGuideCellsPerTick = 0.14f;
        private const int TrapCooldownTicks = 15;

        private static readonly Color AlignedLinkColor = new Color(1f, 0.88f, 0.46f, 0.92f);
        private static readonly Color TangledLinkColor = new Color(0.56f, 0.85f, 1f, 0.78f);
        private static readonly Color SolvedLinkColor = new Color(1f, 0.92f, 0.56f, 0.95f);
        private static readonly Color TetherLinkColor = new Color(1f, 0.95f, 0.72f, 0.92f);
        private static readonly Vector3 PawnTetherOffset = new Vector3(0f, 0f, 0.35f);

        private bool _active;
        private IntVec3 _origin = IntVec3.Invalid;
        private int _layoutSeed;
        private int _unsealedMask;
        private bool _trialCleared;
        private bool _hexagonSolved;
        private int _doomRespawnTick = -1;
        private int _trapCooldownUntilTick = -1;
        private int _resealStartTick = -1;

        private List<IntVec3> _safePath = new List<IntVec3>();
        private List<int> _guideStartTicks = new List<int>();
        private List<int> _guideLastStepIndices = new List<int>();

        private List<Building_RuinPuzzleFairyFlower> _puzzleFlowers = new List<Building_RuinPuzzleFairyFlower>();
        private Building_RuinPuzzleFairyFlower _selectedFlower;
        private Pawn _tetheredPawn;
        private int _tetherExpireTick = -1;

        private bool _galleryAlarmActive;
        private int _nextGalleryBlastTick = -1;
        private List<RuinGalleryBlastAim> _galleryAims = new List<RuinGalleryBlastAim>();
        private Effecter _galleryAimSustainer;

        private readonly int[] _unsealStartTicks = { -1, -1, -1, -1 };
        private HashSet<IntVec3> _safePathSet = new HashSet<IntVec3>();
        private Dictionary<IntVec3, int> _potIndexByCell = new Dictionary<IntVec3, int>();

        public bool IsActiveRuinsMap => _active;
        public IntVec3 Origin => _origin;
        public bool IsTrialCleared => _trialCleared;
        public bool IsHexagonSolved => _hexagonSolved;
        public bool IsResealing => _resealStartTick >= 0;
        public bool IsGalleryAlarmActive => _galleryAlarmActive;
        public int ActiveGalleryBlastAimCount => _galleryAims.Count;
        public IReadOnlyList<IntVec3> SafePath => _safePath;
        public IReadOnlyList<Building_RuinPuzzleFairyFlower> PuzzleFlowers => _puzzleFlowers;
        public Building_RuinPuzzleFairyFlower SelectedFlower => _selectedFlower;
        public Pawn TetheredPawn => _tetheredPawn;
        public int ActiveGuideCount => _guideStartTicks.Count;

        public MapComponent_ViviAncientRuins(Map map) : base(map)
        {
        }

        public bool IsStageUnsealed(int stage)
        {
            if (stage <= 0) { return true; }
            if (stage > RuinLayoutUtility.SealStageCount) { return false; }
            return (_unsealedMask & (1 << (stage - 1))) != 0;
        }

        private void MarkStageUnsealed(int stage, bool animate)
        {
            if (stage < 1 || stage > RuinLayoutUtility.SealStageCount) { return; }

            var bit = 1 << (stage - 1);
            if ((_unsealedMask & bit) != 0) { return; }

            _unsealedMask |= bit;
            _unsealStartTicks[stage - 1] = animate ? Find.TickManager.TicksGame : -1;
        }

        public void InitializeFromGenStep(IntVec3 origin, int seed, int investigated, bool hexagonSolved, bool coreExtracted)
        {
            _active = true;
            _origin = origin;
            _layoutSeed = seed;
            _unsealedMask = 0;
            _trialCleared = investigated >= 3;
            _hexagonSolved = hexagonSolved || investigated >= 4 || coreExtracted;
            _doomRespawnTick = -1;
            _trapCooldownUntilTick = -1;
            _resealStartTick = -1;
            _guideStartTicks.Clear();
            _guideLastStepIndices.Clear();
            _puzzleFlowers.Clear();
            ClearPuzzleTether(notify: false);
            _galleryAlarmActive = false;
            _nextGalleryBlastTick = -1;
            CancelGallerySecurityAims();

            _safePath = RuinLayoutUtility.GenerateHiddenPath(origin, seed);
            RebuildCaches();

            RuinLayoutUtility.PrepareFootprintGround(map, origin);
            RuinLayoutUtility.BuildZone1Forecourt(map, origin, investigated, coreExtracted, spawnHornets: true);

            if (investigated >= 1)
            {
                MarkStageUnsealed(1, animate: false);
                RuinLayoutUtility.BuildZone2Maze(map, origin, investigated, zone3Open: investigated >= 2, seed);
            }

            if (investigated >= 2)
            {
                MarkStageUnsealed(2, animate: false);
                RuinLayoutUtility.BuildZone3Hall(map, origin, investigated, zone4Open: investigated >= 3, _safePath);
            }

            if (investigated >= 3)
            {
                MarkStageUnsealed(3, animate: false);
                _puzzleFlowers = RuinLayoutUtility.BuildZone4Ring(map, origin, _hexagonSolved, seed);
            }

            if (_hexagonSolved)
            {
                MarkStageUnsealed(4, animate: false);
                RuinLayoutUtility.BuildZone5AltarSanctum(map, origin, investigated, coreExtracted);
            }

            RuinLayoutUtility.RefreshSpatialBarriers(map, origin, IsStageUnsealed);
            RuinLayoutUtility.UnfogSanctuary(map, origin);
        }

        public void PostMapGenerationCleanup()
        {
            if (!_active) { return; }

            RuinLayoutUtility.CleanupPostGenDebris(map, _origin, IsStageUnsealed);
            RuinLayoutUtility.RefreshSpatialBarriers(map, _origin, IsStageUnsealed);
            RuinLayoutUtility.EnsureExteriorApproachReachability(map, _origin);
            RuinLayoutUtility.UnfogSanctuary(map, _origin, forceMeshDirty: false);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (!_active) { return; }

            RebuildCaches();
            RuinLayoutUtility.UnfogSanctuary(map, _origin, forceMeshDirty: true);
        }

        private void RebuildCaches()
        {
            _safePathSet.Clear();
            if (_safePath != null)
            {
                for (int i = 0; i < _safePath.Count; i++)
                {
                    _safePathSet.Add(_safePath[i]);
                }
            }

            _potIndexByCell.Clear();
            if (_origin.IsValid)
            {
                var pots = RuinLayoutUtility.GetHexagonPotCells(_origin);
                for (int i = 0; i < pots.Length; i++)
                {
                    _potIndexByCell[pots[i]] = i;
                }
            }
        }

        public void Notify_EchoInvestigated(int echoIndex)
        {
            if (!_active) { return; }

            var ruins = ViviEndingQuest.Current?.Ruins;
            var investigated = Mathf.Max(echoIndex, ruins?.Investigated ?? echoIndex);
            var coreExtracted = ruins?.CoreExtracted ?? false;

            if (echoIndex >= 1 && !IsStageUnsealed(1))
            {
                MarkStageUnsealed(1, animate: true);
                RuinLayoutUtility.BuildZone2Maze(map, _origin, investigated, zone3Open: investigated >= 2, _layoutSeed);
                PlayUnsealEffects(new IntVec3(_origin.x, 0, _origin.z - 57));
            }

            if (echoIndex >= 2 && !IsStageUnsealed(2))
            {
                MarkStageUnsealed(2, animate: true);
                RuinLayoutUtility.BuildZone3Hall(map, _origin, investigated, zone4Open: investigated >= 3, _safePath);
                PlayUnsealEffects(new IntVec3(_origin.x, 0, _origin.z + 50));
            }

            if (echoIndex >= 3 && !IsStageUnsealed(3))
            {
                DisarmTrialZone();
                MarkStageUnsealed(3, animate: true);
                _puzzleFlowers = RuinLayoutUtility.BuildZone4Ring(map, _origin, _hexagonSolved, _layoutSeed);
                PlayUnsealEffects(new IntVec3(_origin.x, 0, _origin.z + 21));
            }

            if (echoIndex >= 4 && !_hexagonSolved)
            {
                CompleteHexagonPuzzle(playEffects: false);
            }

            RuinLayoutUtility.RefreshSpatialBarriers(map, _origin, IsStageUnsealed);
            RuinLayoutUtility.UnfogSanctuary(map, _origin, forceMeshDirty: true);
        }

        private void DisarmTrialZone()
        {
            _trialCleared = true;
            _doomRespawnTick = -1;
            _guideStartTicks.Clear();
            _guideLastStepIndices.Clear();

            var doomFlowers = map.listerThings.ThingsOfDef(VVThingDefOf.VV_RuinDoomFlower).ToList();
            for (int i = 0; i < doomFlowers.Count; i++)
            {
                if (!doomFlowers[i].Destroyed)
                {
                    ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_SeedBloomFairy, doomFlowers[i].Position, map);
                    doomFlowers[i].Destroy(DestroyMode.Vanish);
                }
            }

            RuinLayoutUtility.RevealSafePathFloor(map, _safePath);
        }

        private void PlayUnsealEffects(IntVec3 focusCell)
        {
            if (focusCell.InBounds(map))
            {
                ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellImpact, focusCell, map);
                ApocryphaUtility.ShakeCamera(map, 0.45f);
            }

            Messages.Message(
                LocalizeString_Message.VV_Message_RuinZoneUnsealed.Translate(),
                focusCell.InBounds(map) ? new LookTargets(focusCell, map) : LookTargets.Invalid,
                MessageTypeDefOf.PositiveEvent);
        }

        #region 제3구간: 잔향의 멸망꽃 & 숨겨진 길 시련

        public void Notify_RuinDoomFlowerDestroyed(IntVec3 pos)
        {
            if (!_active || _trialCleared) { return; }

            _doomRespawnTick = Find.TickManager.TicksGame + DoomRespawnDelayTicks;
            SpawnPathGuide();

            Messages.Message(
                LocalizeString_Message.VV_Message_RuinPathGuideSpawned.Translate(),
                new LookTargets(pos, map),
                MessageTypeDefOf.NeutralEvent,
                historical: false);
        }

        public void SpawnPathGuide()
        {
            if (!_active || _safePath == null || _safePath.Count == 0) { return; }

            _guideStartTicks.Add(Find.TickManager.TicksGame);
            _guideLastStepIndices.Add(-1);
        }

        private void TickTrial(int now)
        {
            if (!IsStageUnsealed(2) || _trialCleared) { return; }

            if (_doomRespawnTick >= 0 && now >= _doomRespawnTick)
            {
                _doomRespawnTick = -1;
                if (map.listerThings.ThingsOfDef(VVThingDefOf.VV_RuinDoomFlower).Count == 0)
                {
                    var doomCell = RuinLayoutUtility.RuinDoomFlowerCell(_origin);
                    if (doomCell.InBounds(map))
                    {
                        RuinLayoutUtility.SpawnBuildingClean(map, VVThingDefOf.VV_RuinDoomFlower, doomCell);
                        var bloomPos = doomCell.ToVector3Shifted();
                        if (bloomPos.ShouldSpawnMotesAt(map))
                        {
                            if (VVFleckDefOf.VV_Fleck_GimmickPhaseRing != null)
                            {
                                var ring = FleckMaker.GetDataStatic(bloomPos, map, VVFleckDefOf.VV_Fleck_GimmickPhaseRing, 0.9f);
                                ring.instanceColor = new Color(0.95f, 0.42f, 0.68f, 0.88f);
                                map.flecks.CreateFleck(ring);
                            }
                            if (VVFleckDefOf.VV_Fleck_GimmickHalo != null)
                            {
                                var halo = FleckMaker.GetDataStatic(bloomPos, map, VVFleckDefOf.VV_Fleck_GimmickHalo, 1.05f);
                                halo.instanceColor = new Color(0.92f, 0.35f, 0.62f, 0.80f);
                                map.flecks.CreateFleck(halo);
                            }
                        }
                    }
                }
            }

            TickPathGuides(now);

            if (now < _trapCooldownUntilTick) { return; }

            var trialRect = RuinLayoutUtility.TrialRect(_origin);
            Pawn violator = null;

            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (p == null || p.Dead || !p.Spawned) { continue; }
                if (!trialRect.Contains(p.Position)) { continue; }
                if (_safePathSet.Contains(p.Position)) { continue; }

                violator = p;
                break;
            }

            if (violator != null)
            {
                TriggerTrialBacklash(now);
            }
        }

        private void TickPathGuides(int now)
        {
            if (_guideStartTicks.Count == 0 || _safePath.Count == 0) { return; }

            for (int i = _guideStartTicks.Count - 1; i >= 0; i--)
            {
                var dist = (now - _guideStartTicks[i]) * PathGuideCellsPerTick;
                int segIndex = Mathf.FloorToInt(dist);
                if (segIndex >= _safePath.Count)
                {
                    _guideStartTicks.RemoveAt(i);
                    while (_guideLastStepIndices.Count > _guideStartTicks.Count)
                    {
                        _guideLastStepIndices.RemoveAt(Mathf.Min(i, _guideLastStepIndices.Count - 1));
                    }
                    continue;
                }

                while (_guideLastStepIndices.Count <= i)
                {
                    _guideLastStepIndices.Add(-1);
                }

                var curCell = _safePath[segIndex];
                var nextCell = segIndex + 1 < _safePath.Count ? _safePath[segIndex + 1] : curCell + IntVec3.South;
                var frac = dist - segIndex;
                var wispPos = Vector3.Lerp(curCell.ToVector3Shifted(), nextCell.ToVector3Shifted(), frac);

                if (now % 2 == 0 && wispPos.ShouldSpawnMotesAt(map) && VVFleckDefOf.VV_Fleck_GimmickTwinkle != null)
                {
                    var fleck = FleckMaker.GetDataStatic(wispPos, map, VVFleckDefOf.VV_Fleck_GimmickTwinkle, 0.85f);
                    fleck.instanceColor = new Color(1f, 0.97f, 0.86f, 0.98f);
                    fleck.rotation = Rand.Range(0f, 360f);
                    map.flecks.CreateFleck(fleck);
                }

                if (_guideLastStepIndices[i] != segIndex)
                {
                    _guideLastStepIndices[i] = segIndex;
                    var cellCenter = curCell.ToVector3Shifted();
                    if (cellCenter.ShouldSpawnMotesAt(map))
                    {
                        if (VVFleckDefOf.VV_Fleck_GimmickHalo != null)
                        {
                            var halo = FleckMaker.GetDataStatic(cellCenter, map, VVFleckDefOf.VV_Fleck_GimmickHalo, 0.92f);
                            halo.instanceColor = new Color(1f, 0.95f, 0.78f, 0.92f);
                            map.flecks.CreateFleck(halo);
                        }

                        if (VVFleckDefOf.VV_Fleck_GimmickPhaseRing != null)
                        {
                            var ring = FleckMaker.GetDataStatic(cellCenter, map, VVFleckDefOf.VV_Fleck_GimmickPhaseRing, 0.65f);
                            ring.instanceColor = new Color(0.75f, 0.88f, 1f, 0.90f);
                            map.flecks.CreateFleck(ring);
                        }
                    }
                }
            }
        }

        private void TriggerTrialBacklash(int now)
        {
            _trapCooldownUntilTick = now + TrapCooldownTicks;

            var trialRect = RuinLayoutUtility.TrialRect(_origin);
            var doomCell = RuinLayoutUtility.RuinDoomFlowerCell(_origin);
            var returnAnchor = RuinLayoutUtility.RuinDoomReturnCell(_origin);

            // 잔향의 멸망꽃 파동 연출 (폭발 피해 없음)
            var pulsePos = doomCell.InBounds(map) ? doomCell.ToVector3Shifted() : trialRect.CenterVector3;
            if (pulsePos.ShouldSpawnMotesAt(map))
            {
                if (VVFleckDefOf.VV_Fleck_GimmickDoomLaunchWave != null)
                {
                    map.flecks.CreateFleck(FleckMaker.GetDataStatic(pulsePos, map, VVFleckDefOf.VV_Fleck_GimmickDoomLaunchWave, 1.15f));
                }
                if (VVFleckDefOf.VV_Fleck_GimmickDoomRing != null)
                {
                    map.flecks.CreateFleck(FleckMaker.GetDataStatic(pulsePos, map, VVFleckDefOf.VV_Fleck_GimmickDoomRing, 1.6f));
                }
            }

            VVSoundDefOf.VV_FairyTeleport?.PlayOneShot(new TargetInfo(trialRect.CenterCell, map));
            ApocryphaUtility.ShakeCamera(map, 0.4f);

            // 전체 정착민 대상 랜덤 멸망꽃 페널티 적용 (1일 지속)
            var blightDef = DoomBlightUtility.ApplyTimedDoomBlight(map, TrialBlightDurationTicks);

            // 시련 격자 위 폰들을 멸망꽃 앞으로 강제 이동
            var candidates = map.mapPawns.AllPawnsSpawned.ToList();
            int offsetIdx = 0;
            IntVec2[] returnOffsets =
            {
                new IntVec2(0, 0), new IntVec2(-1, 0), new IntVec2(1, 0),
                new IntVec2(-2, 0), new IntVec2(2, 0), new IntVec2(0, -1),
                new IntVec2(-1, -1), new IntVec2(1, -1),
            };

            for (int i = 0; i < candidates.Count; i++)
            {
                var pawn = candidates[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned) { continue; }
                if (!trialRect.Contains(pawn.Position)) { continue; }

                var oldPos = pawn.Position;
                var dest = returnAnchor;
                for (int r = 0; r < returnOffsets.Length; r++)
                {
                    var cand = new IntVec3(
                        returnAnchor.x + returnOffsets[(offsetIdx + r) % returnOffsets.Length].x,
                        0,
                        returnAnchor.z + returnOffsets[(offsetIdx + r) % returnOffsets.Length].z);
                    if (cand.InBounds(map) && cand.Standable(map) && !trialRect.Contains(cand))
                    {
                        dest = cand;
                        offsetIdx++;
                        break;
                    }
                }

                if (VVFleckDefOf.VV_Fleck_GimmickPhaseRing != null)
                {
                    map.flecks.CreateFleck(FleckMaker.GetDataStatic(oldPos.ToVector3Shifted(), map, VVFleckDefOf.VV_Fleck_GimmickPhaseRing, 1.2f));
                    map.flecks.CreateFleck(FleckMaker.GetDataStatic(dest.ToVector3Shifted(), map, VVFleckDefOf.VV_Fleck_GimmickPhaseRing, 1.2f));
                }

                pawn.pather?.StopDead();
                pawn.jobs?.StopAll();
                pawn.Position = dest;
                pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: true);
            }

            var blightLabel = blightDef != null ? blightDef.label : string.Empty;
            Messages.Message(
                LocalizeString_Message.VV_Message_RuinTrialTriggered.Translate(blightLabel.Named("BLIGHT")),
                new LookTargets(returnAnchor, map),
                MessageTypeDefOf.NegativeEvent,
                historical: false);
        }

        #endregion

        #region 제4구간: 육각형 요정꽃 재정렬 퍼즐

        public Pawn GetTetheredPawnFor(Building_RuinPuzzleFairyFlower flower)
        {
            if (flower == null || _selectedFlower != flower || !HasValidTether) { return null; }
            return _tetheredPawn;
        }

        private bool HasValidTether =>
            _tetheredPawn != null
            && !_tetheredPawn.Dead
            && !_tetheredPawn.Downed
            && _tetheredPawn.Spawned
            && _tetheredPawn.Map == map
            && _selectedFlower != null
            && _selectedFlower.Spawned
            && _selectedFlower.Map == map
            && Find.TickManager.TicksGame <= _tetherExpireTick;

        public void InteractPuzzleFlower(Pawn vivi, Building_RuinPuzzleFairyFlower flower)
        {
            if (!_active || _hexagonSolved || vivi == null || flower == null || !flower.Spawned) { return; }

            var now = Find.TickManager.TicksGame;
            if (!_galleryAlarmActive)
            {
                ActivateGallerySecurityAlarm(now);
            }

            if (!HasValidTether || _tetheredPawn != vivi)
            {
                _tetheredPawn = vivi;
                _selectedFlower = flower;
                _tetherExpireTick = now + PuzzleTetherDurationTicks;
                ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_SeedBloomFairy, flower.Position, map);
                Messages.Message(
                    LocalizeString_Message.VV_Message_RuinPuzzleTethered.Translate(vivi.Named("PAWN")),
                    flower,
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
                return;
            }

            if (_selectedFlower == flower)
            {
                ClearPuzzleTether(notify: true);
                return;
            }

            SwapPuzzleFlowers(_selectedFlower, flower);
            ClearPuzzleTether(notify: false);

            if (CheckIsHexagonAligned())
            {
                CompleteHexagonPuzzle(playEffects: true);
            }
            else
            {
                Messages.Message(
                    LocalizeString_Message.VV_Message_RuinPuzzleSwapped.Translate(),
                    flower,
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
            }
        }

        public void SwapPuzzleFlowers(Building_RuinPuzzleFairyFlower flowerA, Building_RuinPuzzleFairyFlower flowerB)
        {
            if (flowerA == null || flowerB == null || flowerA == flowerB || !flowerA.Spawned || !flowerB.Spawned) { return; }

            var drawA = flowerA.DrawPos;
            var drawB = flowerB.DrawPos;
            var cellA = flowerA.Position;
            var cellB = flowerB.Position;

            flowerA.DeSpawn(DestroyMode.WillReplace);
            flowerB.DeSpawn(DestroyMode.WillReplace);

            GenSpawn.Spawn(flowerA, cellB, map, WipeMode.Vanish);
            GenSpawn.Spawn(flowerB, cellA, map, WipeMode.Vanish);

            flowerA.BeginSwapGlide(drawA);
            flowerB.BeginSwapGlide(drawB);

            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellLaunch, cellA, map);
            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellLaunch, cellB, map);
        }

        public bool CheckIsHexagonAligned()
        {
            RefreshPuzzleFlowerList();
            if (_puzzleFlowers.Count != 6) { return false; }

            int[] potIndices = new int[6];
            int usedMask = 0;
            for (int i = 0; i < 6; i++)
            {
                var flower = _puzzleFlowers[i];
                if (flower == null || !flower.Spawned || flower.NodeIndex < 0 || flower.NodeIndex >= 6) { return false; }
                if (!_potIndexByCell.TryGetValue(flower.Position, out var potIdx)) { return false; }
                if ((usedMask & (1 << potIdx)) != 0) { return false; }

                usedMask |= 1 << potIdx;
                potIndices[flower.NodeIndex] = potIdx;
            }

            for (int i = 0; i < 6; i++)
            {
                int diff = Mathf.Abs(potIndices[(i + 1) % 6] - potIndices[i]);
                if (diff != 1 && diff != 5)
                {
                    return false;
                }
            }

            return true;
        }

        public void DebugSolveHexagon()
        {
            if (!_active || _hexagonSolved) { return; }

            RefreshPuzzleFlowerList();
            var potCells = RuinLayoutUtility.GetHexagonPotCells(_origin);
            if (_puzzleFlowers.Count == 6 && potCells.Length == 6)
            {
                var ordered = _puzzleFlowers.OrderBy(f => f.NodeIndex).ToList();
                var oldDraws = ordered.Select(f => f.DrawPos).ToArray();
                for (int i = 0; i < 6; i++)
                {
                    if (ordered[i].Spawned)
                    {
                        ordered[i].DeSpawn(DestroyMode.WillReplace);
                    }
                }
                for (int i = 0; i < 6; i++)
                {
                    GenSpawn.Spawn(ordered[i], potCells[i], map, WipeMode.Vanish);
                    ordered[i].BeginSwapGlide(oldDraws[i]);
                }
            }

            CompleteHexagonPuzzle(playEffects: true);
        }

        public void CompleteHexagonPuzzle(bool playEffects)
        {
            if (_hexagonSolved && IsStageUnsealed(4)) { return; }

            _hexagonSolved = true;
            _galleryAlarmActive = false;
            _nextGalleryBlastTick = -1;
            CancelGallerySecurityAims();
            ClearPuzzleTether(notify: false);
            ViviEndingQuest.Current?.Ruins.Notify_HexagonSolved();

            var ruins = ViviEndingQuest.Current?.Ruins;
            int investigated = ruins?.Investigated ?? 3;
            bool coreExtracted = ruins?.CoreExtracted ?? false;

            MarkStageUnsealed(4, animate: playEffects);
            RuinLayoutUtility.BuildZone5AltarSanctum(map, _origin, investigated, coreExtracted);
            RuinLayoutUtility.RefreshSpatialBarriers(map, _origin, IsStageUnsealed);
            RuinLayoutUtility.UnfogSanctuary(map, _origin, forceMeshDirty: true);

            if (playEffects)
            {
                var ringCenter = RuinLayoutUtility.RingCenter(_origin);
                VVEffecterDefOf.VV_Effecter_EndingGoldenWave?.Spawn(ringCenter, map).Cleanup();
                ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellImpact, ringCenter, map);

                RefreshPuzzleFlowerList();
                for (int i = 0; i < _puzzleFlowers.Count; i++)
                {
                    if (_puzzleFlowers[i] != null && _puzzleFlowers[i].Spawned)
                    {
                        ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellLaunch, _puzzleFlowers[i].Position, map);
                    }
                }

                ApocryphaUtility.ShakeCamera(map, 0.85f);
                Messages.Message(
                    LocalizeString_Message.VV_Message_RuinHexagonCompleted.Translate(),
                    new LookTargets(ringCenter, map),
                    MessageTypeDefOf.PositiveEvent);
            }
        }

        private void ClearPuzzleTether(bool notify)
        {
            bool hadTether = _tetheredPawn != null || _selectedFlower != null;
            _tetheredPawn = null;
            _selectedFlower = null;
            _tetherExpireTick = -1;

            if (notify && hadTether)
            {
                Messages.Message(
                    LocalizeString_Message.VV_Message_RuinPuzzleUntethered.Translate(),
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
            }
        }

        private void RefreshPuzzleFlowerList()
        {
            _puzzleFlowers.RemoveAll(f => f == null || f.Destroyed);
            if (_puzzleFlowers.Count == 6) { return; }

            _puzzleFlowers.Clear();
            var list = map.listerThings.ThingsOfDef(VVThingDefOf.VV_RuinPuzzleFairyFlower);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Building_RuinPuzzleFairyFlower pf && pf.Spawned)
                {
                    _puzzleFlowers.Add(pf);
                }
            }
        }

        private void ActivateGallerySecurityAlarm(int now)
        {
            if (_galleryAlarmActive || _hexagonSolved) { return; }

            _galleryAlarmActive = true;
            _nextGalleryBlastTick = now + 30;

            var ringCenter = RuinLayoutUtility.RingCenter(_origin);
            if (ringCenter.InBounds(map))
            {
                if (ringCenter.ToVector3Shifted().ShouldSpawnMotesAt(map) && VVFleckDefOf.VV_Fleck_GimmickDoomRing != null)
                {
                    map.flecks.CreateFleck(FleckMaker.GetDataStatic(ringCenter.ToVector3Shifted(), map, VVFleckDefOf.VV_Fleck_GimmickDoomRing, 2.2f));
                }

                SoundDefOf.MechanoidsWakeUp?.PlayOneShot(new TargetInfo(ringCenter, map));
                ApocryphaUtility.ShakeCamera(map, 0.7f);
            }

            Messages.Message(
                LocalizeString_Message.VV_Message_RuinGalleryAlarm.Translate(),
                ringCenter.InBounds(map) ? new LookTargets(ringCenter, map) : LookTargets.Invalid,
                MessageTypeDefOf.ThreatBig);
        }

        private void TickGallerySecurity(int now)
        {
            if (!_galleryAlarmActive || _hexagonSolved || !IsStageUnsealed(3) || IsResealing)
            {
                if (_galleryAims.Count > 0 || _galleryAimSustainer != null)
                {
                    CancelGallerySecurityAims();
                }
                return;
            }

            for (int i = _galleryAims.Count - 1; i >= 0; i--)
            {
                var aim = _galleryAims[i];
                if (now < aim.detonateTick) { continue; }

                _galleryAims.RemoveAt(i);
                aim.CleanupVisuals();
                DetonateGalleryBlast(aim.cell);
            }

            if (_galleryAims.Count < MaxConcurrentGalleryBlasts && now >= _nextGalleryBlastTick)
            {
                if (TryPickGalleryBlastTarget(out var targetCell))
                {
                    _galleryAims.Add(new RuinGalleryBlastAim(targetCell, now + GalleryBlastAimTicks));
                    _nextGalleryBlastTick = now + GalleryBlastIntervalTicks.RandomInRange;
                }
                else
                {
                    _nextGalleryBlastTick = now + GalleryTargetRescanTicks;
                }
            }

            if (_galleryAims.Count == 0)
            {
                EndingVfxUtility.CleanupEffecter(ref _galleryAimSustainer);
                return;
            }

            for (int i = 0; i < _galleryAims.Count; i++)
            {
                _galleryAims[i].TickVisuals(map, GalleryBlastAimTicks);
            }

            var originTarget = new TargetInfo(_origin, map);
            EndingVfxUtility.MaintainEffecter(ref _galleryAimSustainer, VVEffecterDefOf.VV_Effecter_ApocryphaAimSustain, originTarget, originTarget);
        }

        private bool TryPickGalleryBlastTarget(out IntVec3 result)
        {
            var candidates = new List<Pawn>();
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (p == null || p.Dead || p.Downed || !p.Spawned) { continue; }
                if (p.Faction != Faction.OfPlayer && !p.IsPrisonerOfColony) { continue; }
                if (RuinLayoutUtility.GetSealStage(p.Position, _origin) != 3) { continue; }
                candidates.Add(p);
            }

            if (candidates.Count == 0)
            {
                result = IntVec3.Invalid;
                return false;
            }

            // 회랑 내 랜덤 대상을 고르되 기존 예고 장판과 완전히 겹치지 않는 위치를 우선 선택한다.
            var picked = candidates.RandomElement();
            if (!IsNearGalleryAim(picked.Position))
            {
                result = picked.Position;
                return true;
            }

            if (CellFinder.TryFindRandomCellNear(picked.Position, map, 4,
                c => c.InBounds(map) && c.Standable(map)
                    && RuinLayoutUtility.GetSealStage(c, _origin) == 3
                    && !IsNearGalleryAim(c),
                out result))
            {
                return true;
            }

            result = picked.Position;
            return true;
        }

        private bool IsNearGalleryAim(IntVec3 cell)
        {
            for (int i = 0; i < _galleryAims.Count; i++)
            {
                if (_galleryAims[i].cell.InHorDistOf(cell, GalleryBlastMinSpacing))
                {
                    return true;
                }
            }
            return false;
        }

        private void DetonateGalleryBlast(IntVec3 cell)
        {
            var shellDef = VVThingDefOf.VV_Projectile_FairyShell;
            if (shellDef?.projectile == null || !cell.InBounds(map)) { return; }

            var impactEffecter = shellDef.GetModExtension<ApocryphaProjectileExtension>()?.impactEffecter
                ?? VVEffecterDefOf.VV_Effecter_FairyShellImpact;
            ApocryphaGimmicks.PlayEffect(impactEffecter, cell, map);

            var ignored = new List<Thing>();
            ignored.AddRange(map.listerThings.ThingsOfDef(VVThingDefOf.VV_RuinPuzzlePot));
            ignored.AddRange(map.listerThings.ThingsOfDef(VVThingDefOf.VV_RuinPuzzleFairyFlower));
            ignored.AddRange(map.listerThings.ThingsOfDef(VVThingDefOf.VV_AncientSanctumWall));
            ignored.AddRange(map.listerThings.ThingsOfDef(VVThingDefOf.VV_AncientSanctumLamp));
            ignored.AddRange(map.listerThings.ThingsOfDef(VVThingDefOf.VV_RuinSpatialBarrier));

            GenExplosion.DoExplosion(
                cell,
                map,
                shellDef.projectile.explosionRadius,
                shellDef.projectile.damageDef,
                null,
                shellDef.projectile.GetDamageAmount((Thing)null),
                shellDef.projectile.GetArmorPenetration(),
                projectile: shellDef,
                ignoredThings: ignored,
                screenShakeFactor: shellDef.projectile.screenShakeFactor);
        }

        private void CancelGallerySecurityAims()
        {
            for (int i = 0; i < _galleryAims.Count; i++)
            {
                _galleryAims[i].CleanupVisuals();
            }

            _galleryAims.Clear();
            EndingVfxUtility.CleanupEffecter(ref _galleryAimSustainer);
        }

        #endregion

        #region 코어 추출 후 재봉인 연출 및 월드 추방

        public void BeginResealSequence()
        {
            if (!_active || _resealStartTick >= 0) { return; }

            _resealStartTick = Find.TickManager.TicksGame;
            _galleryAlarmActive = false;
            _nextGalleryBlastTick = -1;
            CancelGallerySecurityAims();
            ClearPuzzleTether(notify: false);

            var altarCell = RuinLayoutUtility.CentralAltarCell(_origin);
            if (altarCell.InBounds(map))
            {
                ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellImpact, altarCell, map);
                ApocryphaUtility.ShakeCamera(map, 0.55f);
            }

            Messages.Message(
                LocalizeString_Message.VV_Message_RuinResealing.Translate(),
                altarCell.InBounds(map) ? new LookTargets(altarCell, map) : LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
        }

        private void TickReseal(int now)
        {
            if (_resealStartTick < 0) { return; }

            var elapsed = now - _resealStartTick;

            if (elapsed == 45)
            {
                var ringCenter = RuinLayoutUtility.RingCenter(_origin);
                if (ringCenter.InBounds(map))
                {
                    ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellImpact, ringCenter, map);
                    ApocryphaUtility.ShakeCamera(map, 0.45f);
                }

                var pots = RuinLayoutUtility.GetHexagonPotCells(_origin);
                for (int i = 0; i < pots.Length; i++)
                {
                    if (pots[i].InBounds(map) && VVFleckDefOf.VV_Fleck_GimmickPhaseRing != null)
                    {
                        var ring = FleckMaker.GetDataStatic(pots[i].ToVector3Shifted(), map, VVFleckDefOf.VV_Fleck_GimmickPhaseRing, 1.35f);
                        ring.instanceColor = new Color(1f, 0.90f, 0.55f, 0.92f);
                        map.flecks.CreateFleck(ring);
                    }
                }
            }
            else if (elapsed == 90)
            {
                var hallMid = new IntVec3(_origin.x, 0, _origin.z + 36);
                if (hallMid.InBounds(map))
                {
                    ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellImpact, hallMid, map);
                    ApocryphaUtility.ShakeCamera(map, 0.48f);
                }
            }
            else if (elapsed == 135)
            {
                var southGate = new IntVec3(_origin.x, 0, _origin.z - 56);
                if (southGate.InBounds(map))
                {
                    ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_FairyShellImpact, southGate, map);
                    ApocryphaUtility.ShakeCamera(map, 0.65f);
                }
            }
            else if (elapsed == 165)
            {
                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    var p = pawns[i];
                    if (p == null || p.Dead || !p.Spawned || !IsExpellablePawn(p)) { continue; }
                    if (p.DrawPos.ShouldSpawnMotesAt(map))
                    {
                        FleckMaker.Static(p.DrawPos, map, FleckDefOf.PsycastSkipFlashEntry, 2.2f);
                    }
                }
                SoundDefOf.Psycast_Skip_Exit?.PlayOneShot(new TargetInfo(_origin, map));
                ApocryphaUtility.ShakeCamera(map, 0.75f);
            }

            if (elapsed >= ResealDurationTicks)
            {
                FinishResealAndExpelToWorld();
            }
        }

        private static bool IsExpellablePawn(Pawn p)
        {
            if (p == null || p.Dead) { return false; }
            if (p.Faction == Faction.OfPlayer) { return true; }
            if (p.IsPrisonerOfColony) { return true; }
            if (p.HostFaction == Faction.OfPlayer && !p.HostileTo(Faction.OfPlayer)) { return true; }
            return false;
        }

        private void FinishResealAndExpelToWorld()
        {
            _resealStartTick = -1;

            // 오디세이 중력선이 맵에 착륙해 있는 경우: 함선 손실을 막기 위해 거대 육각형만 완전 재봉인하고 내부 인원을 중력선 곁으로 밀어낸다.
            if (ModsConfig.OdysseyActive && GravshipUtility.PlayerHasGravEngine(map))
            {
                ExpelInsideHexagonNearGravship();
                return;
            }

            // 운반 중인 폰·아이템이 소실되지 않도록 먼저 정리한다.
            var snapshot = map.mapPawns.AllPawnsSpawned.ToList();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var p = snapshot[i];
                if (!IsExpellablePawn(p)) { continue; }

                var carried = p.carryTracker?.CarriedThing;
                if (carried == null) { continue; }

                if (carried is Pawn)
                {
                    p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
                }
                else if (p.inventory?.innerContainer != null)
                {
                    p.carryTracker.innerContainer.TryTransferToContainer(carried, p.inventory.innerContainer);
                }
            }

            var expelPawns = new List<Pawn>();
            var allSpawned = map.mapPawns.AllPawnsSpawned.ToList();
            for (int i = 0; i < allSpawned.Count; i++)
            {
                var p = allSpawned[i];
                if (!IsExpellablePawn(p)) { continue; }

                p.pather?.StopDead();
                p.jobs?.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                expelPawns.Add(p);
            }

            bool wasCurrentMap = Find.CurrentMap == map;
            var tile = map.Tile;

            Caravan caravan = null;
            if (expelPawns.Count > 0)
            {
                caravan = CaravanExitMapUtility.ExitMapAndCreateCaravan(
                    expelPawns,
                    Faction.OfPlayer,
                    tile,
                    tile,
                    PlanetTile.Invalid,
                    sendMessage: false);
            }

            Messages.Message(
                LocalizeString_Message.VV_Message_RuinResealedExpelled.Translate(),
                caravan != null ? new LookTargets(caravan) : LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);

            if (wasCurrentMap && caravan != null)
            {
                CameraJumper.TryJumpAndSelect(caravan);
            }
        }

        private void ExpelInsideHexagonNearGravship()
        {
            var gravEngine = GravshipUtility.GetPlayerGravEngine_NewTemp(map);
            var anchor = gravEngine != null && gravEngine.Spawned
                ? gravEngine.Position
                : new IntVec3(_origin.x, 0, _origin.z - 64).ClampInsideMap(map);

            _unsealedMask = 0;
            _trialCleared = false;
            _puzzleFlowers.Clear();
            ClearPuzzleTether(notify: false);

            var pawns = map.mapPawns.AllPawnsSpawned.ToList();
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (!IsExpellablePawn(p)) { continue; }
                if (RuinLayoutUtility.GetSealStage(p.Position, _origin) <= 0) { continue; }

                if (CellFinder.TryFindRandomCellNear(anchor, map, 12,
                    c => c.InBounds(map) && c.Standable(map) && RuinLayoutUtility.GetSealStage(c, _origin) == 0,
                    out var safe))
                {
                    p.pather?.StopDead();
                    p.Position = safe;
                    p.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                    if (p.DrawPos.ShouldSpawnMotesAt(map))
                    {
                        FleckMaker.Static(p.DrawPos, map, FleckDefOf.PsycastSkipOuterRingExit, 1.5f);
                    }
                }
            }

            RuinLayoutUtility.ResealAllStagesToNaturalGround(map, _origin);

            Messages.Message(
                LocalizeString_Message.VV_Message_RuinResealedGravship.Translate(),
                new LookTargets(anchor, map),
                MessageTypeDefOf.NeutralEvent);
        }

        #endregion

        #region 매 틱·매 프레임 갱신 및 렌더링

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!_active) { return; }

            var now = Find.TickManager.TicksGame;
            if (_resealStartTick >= 0)
            {
                TickReseal(now);
                return;
            }

            TickTrial(now);
            TickGallerySecurity(now);

            if ((_selectedFlower != null || _tetheredPawn != null) && !HasValidTether)
            {
                ClearPuzzleTether(notify: false);
            }

            // 봉인된 영역 안으로 도약/수송 포드/순간이동 등이 들어갔을 때 바깥으로 밀어낸다.
            if (now % 15 == 0 && _unsealedMask != 0b1111)
            {
                var forecourtFallback = RuinLayoutUtility.ForecourtAnchorCell(_origin).ClampInsideMap(map);
                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    var p = pawns[i];
                    if (p == null || !p.Spawned) { continue; }
                    var st = RuinLayoutUtility.GetSealStage(p.Position, _origin);
                    if (st > 0 && !IsStageUnsealed(st))
                    {
                        if (!CellFinder.TryFindRandomCellNear(p.Position, map, 16,
                                c => c.InBounds(map) && c.Standable(map) && IsStageUnsealed(RuinLayoutUtility.GetSealStage(c, _origin)),
                                out var safe)
                            && !CellFinder.TryFindRandomCellNear(forecourtFallback, map, 8,
                                c => c.InBounds(map) && c.Standable(map) && RuinLayoutUtility.GetSealStage(c, _origin) == 0,
                                out safe))
                        {
                            safe = forecourtFallback;
                        }

                        p.pather?.StopDead();
                        p.Position = safe;
                        p.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                        if (p.DrawPos.ShouldSpawnMotesAt(map))
                        {
                            FleckMaker.Static(p.DrawPos, map, FleckDefOf.PsycastSkipFlashEntry, 1.5f);
                        }
                    }
                }
            }
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (!_active || Find.CurrentMap != map || WorldRendererUtility.WorldSelected) { return; }

            DrawDistortionSeals();
            DrawHexagonPuzzleLinks();
        }

        private void DrawDistortionSeals()
        {
            var now = Find.TickManager.TicksGame;
            Vector4 state = Vector4.zero;
            float distortionStrength = 13.5f;

            if (_resealStartTick >= 0)
            {
                var elapsed = Mathf.Max(0, now - _resealStartTick);
                // 중앙 제단(Stage 4) -> 원형 회랑(Stage 3) -> 장대 복도(Stage 2) -> 거대 육각형 미로(Stage 1) 순으로 역방향 재봉인 파동
                state.w = SmoothStep01(0f, 55f, elapsed);
                state.z = SmoothStep01(35f, 95f, elapsed);
                state.y = SmoothStep01(70f, 135f, elapsed);
                state.x = SmoothStep01(105f, 170f, elapsed);
                distortionStrength = Mathf.Lerp(13.5f, 27.5f, Mathf.Clamp01(elapsed / (float)ResealDurationTicks));
            }
            else
            {
                for (int i = 0; i < RuinLayoutUtility.SealStageCount; i++)
                {
                    float val;
                    if (!IsStageUnsealed(i + 1))
                    {
                        val = 1f;
                    }
                    else if (_unsealStartTicks[i] >= 0)
                    {
                        var elapsed = now - _unsealStartTicks[i];
                        if (elapsed < UnsealFadeDurationTicks)
                        {
                            val = 1f - Mathf.Clamp01(elapsed / (float)UnsealFadeDurationTicks);
                        }
                        else
                        {
                            _unsealStartTicks[i] = -1;
                            val = 0f;
                        }
                    }
                    else
                    {
                        val = 0f;
                    }

                    state[i] = val;
                }
            }

            RuinDistortionRenderer.Draw(_origin, state, distortionStrength);
        }

        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private void DrawHexagonPuzzleLinks()
        {
            if (!IsStageUnsealed(3)) { return; }
            if (_resealStartTick >= 0 && Find.TickManager.TicksGame - _resealStartTick > 75) { return; }

            RefreshPuzzleFlowerList();
            if (_puzzleFlowers.Count == 6)
            {
                Building_RuinPuzzleFairyFlower[] byNode = new Building_RuinPuzzleFairyFlower[6];
                for (int i = 0; i < 6; i++)
                {
                    var f = _puzzleFlowers[i];
                    if (f != null && f.NodeIndex >= 0 && f.NodeIndex < 6)
                    {
                        byNode[f.NodeIndex] = f;
                    }
                }

                for (int i = 0; i < 6; i++)
                {
                    var a = byNode[i];
                    var b = byNode[(i + 1) % 6];
                    if (a == null || b == null || !a.Spawned || !b.Spawned) { continue; }

                    bool edgeAligned = _hexagonSolved;
                    if (!edgeAligned
                        && _potIndexByCell.TryGetValue(a.Position, out var potA)
                        && _potIndexByCell.TryGetValue(b.Position, out var potB))
                    {
                        int diff = Mathf.Abs(potA - potB);
                        edgeAligned = diff == 1 || diff == 5;
                    }

                    var color = _hexagonSolved ? SolvedLinkColor : (edgeAligned ? AlignedLinkColor : TangledLinkColor);
                    float width = _hexagonSolved ? 0.65f : (edgeAligned ? 0.58f : 0.46f);
                    SoulLinkRenderer.Draw(a.LinkAnchor, b.LinkAnchor, width, color);
                    if (edgeAligned)
                    {
                        PetalLinkRenderer.Draw(a.LinkAnchor, b.LinkAnchor, 0.7f, color, i * 97 + 13);
                    }
                }
            }

            if (HasValidTether)
            {
                var from = _selectedFlower.LinkAnchor;
                var to = _tetheredPawn.DrawPos + PawnTetherOffset;
                SoulLinkRenderer.Draw(from, to, 0.45f, TetherLinkColor);
                PetalLinkRenderer.Draw(from, to, 0.75f, TetherLinkColor, _tetheredPawn.thingIDNumber);
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            CancelGallerySecurityAims();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _active, "active", false);
            Scribe_Values.Look(ref _origin, "origin", IntVec3.Invalid);
            Scribe_Values.Look(ref _layoutSeed, "layoutSeed", 0);
            Scribe_Values.Look(ref _unsealedMask, "unsealedMask", 0);
            Scribe_Values.Look(ref _trialCleared, "trialCleared", false);
            Scribe_Values.Look(ref _hexagonSolved, "hexagonSolved", false);
            Scribe_Values.Look(ref _doomRespawnTick, "doomRespawnTick", -1);
            Scribe_Values.Look(ref _trapCooldownUntilTick, "trapCooldownUntilTick", -1);
            Scribe_Values.Look(ref _resealStartTick, "resealStartTick", -1);
            Scribe_Collections.Look(ref _safePath, "safePath", LookMode.Value);
            Scribe_Collections.Look(ref _guideStartTicks, "guideStartTicks", LookMode.Value);
            Scribe_Collections.Look(ref _puzzleFlowers, "puzzleFlowers", LookMode.Reference);
            Scribe_References.Look(ref _selectedFlower, "selectedFlower");
            Scribe_References.Look(ref _tetheredPawn, "tetheredPawn");
            Scribe_Values.Look(ref _tetherExpireTick, "tetherExpireTick", -1);
            Scribe_Values.Look(ref _galleryAlarmActive, "galleryAlarmActive", false);
            Scribe_Values.Look(ref _nextGalleryBlastTick, "nextGalleryBlastTick", -1);
            Scribe_Collections.Look(ref _galleryAims, "galleryAims", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_safePath == null) { _safePath = new List<IntVec3>(); }
                if (_guideStartTicks == null) { _guideStartTicks = new List<int>(); }
                if (_puzzleFlowers == null) { _puzzleFlowers = new List<Building_RuinPuzzleFairyFlower>(); }
                if (_galleryAims == null) { _galleryAims = new List<RuinGalleryBlastAim>(); }
                _puzzleFlowers.RemoveAll(f => f == null);
                _galleryAims.RemoveAll(a => a == null || !a.cell.IsValid);
                _guideLastStepIndices.Clear();
                RebuildCaches();
            }
        }

        #endregion
    }
}
