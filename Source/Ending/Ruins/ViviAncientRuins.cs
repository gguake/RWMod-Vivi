using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 유적은 하나뿐이고 맵을 떠나면 맵이 지워지므로 진행 상태는 Site가 아니라 여기에 둔다.
    public class ViviAncientRuins : IExposable
    {
        public const int EchoCount = 4;
        public const int RequiredAttunementLevel = 4;

        private const int MinSiteDistance = 6;
        private const int MaxSiteDistance = 14;
        private const int ExpandedMaxSiteDistance = 24;
        private const int FallbackMinSiteDistance = 4;
        private const int FallbackMaxSiteDistance = 28;
        private static readonly IntRange MissiveDelayTicks = new IntRange(60000, 120000);

        private static readonly string[] EchoLabelKeys =
        {
            LocalizeString_Letter.VV_Letter_RuinEcho1Label,
            LocalizeString_Letter.VV_Letter_RuinEcho2Label,
            LocalizeString_Letter.VV_Letter_RuinEcho3Label,
            LocalizeString_Letter.VV_Letter_RuinEcho4Label,
        };

        private static readonly string[] EchoTextKeys =
        {
            LocalizeString_Letter.VV_Letter_RuinEcho1,
            LocalizeString_Letter.VV_Letter_RuinEcho2,
            LocalizeString_Letter.VV_Letter_RuinEcho3,
            LocalizeString_Letter.VV_Letter_RuinEcho4,
        };

        private readonly ViviEndingQuest _ending;

        private int _missiveDueTick = -1;
        private bool _missiveReceived;
        private PlanetTile _tile = PlanetTile.Invalid;
        private Site _site;
        private int _investigated;
        private bool _hexagonSolved;
        private bool _coreExtracted;

        public bool MissiveReceived => _missiveReceived;
        public bool HasCoordinates => _tile.Valid;
        public PlanetTile Tile => _tile;
        public Site Site => _site != null && !_site.Destroyed ? _site : null;
        public int Investigated => _investigated;
        public bool HexagonSolved => _hexagonSolved;
        public bool CoreExtracted => _coreExtracted;

        public ViviAncientRuins(ViviEndingQuest ending)
        {
            _ending = ending;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _missiveDueTick, "missiveDueTick", -1);
            Scribe_Values.Look(ref _missiveReceived, "missiveReceived", false);
            Scribe_Values.Look(ref _tile, "tile", PlanetTile.Invalid);
            Scribe_References.Look(ref _site, "site");
            Scribe_Values.Look(ref _investigated, "investigated", 0);
            Scribe_Values.Look(ref _hexagonSolved, "hexagonSolved", false);
            Scribe_Values.Look(ref _coreExtracted, "coreExtracted", false);
        }

        public static bool IsQualified(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.IsRoyalVivi()) { return false; }

            var everflower = pawn.GetCompVivi()?.LinkedEverflower;
            return everflower != null && !everflower.Destroyed && everflower.AttunementLevel >= RequiredAttunementLevel;
        }

        #region 밀서·좌표
        public void Notify_TalesCompleted()
        {
            if (_missiveReceived || _missiveDueTick >= 0) { return; }

            _missiveDueTick = Find.TickManager.TicksGame + MissiveDelayTicks.RandomInRange;
        }

        public void TickMissive(int now)
        {
            if (_missiveReceived || _missiveDueTick < 0 || now < _missiveDueTick) { return; }

            var federation = ViviEndingUtility.FindFederation(requireNonHostile: true);
            if (federation == null) { return; }

            SendMissive(federation);
        }

        public void SendMissive(Faction federation)
        {
            if (_missiveReceived) { return; }

            _missiveReceived = true;

            var text = LocalizeString_Letter.VV_Letter_EndingFederationMissive.Translate().Resolve()
                + "\n\n" + LocalizeString_Letter.VV_Letter_EndingFederationMissiveConditions.Translate().Resolve();

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_EndingFederationMissiveLabel.Translate(),
                text,
                LetterDefOf.PositiveEvent,
                LookTargets.Invalid,
                federation,
                _ending.Quest);
        }

        public AcceptanceReport CanRequestCoordinates()
        {
            if (!_missiveReceived || HasCoordinates) { return false; }

            var unmet = ViviEndingRequirements.GetUnmetReason();
            if (unmet != null) { return unmet; }

            return true;
        }

        public bool RevealRuins()
        {
            if (HasCoordinates) { return Site != null; }
            if (!TryFindSiteTile(out var tile)) { return false; }

            _tile = tile;
            if (!SpawnSite()) { return false; }

            Messages.Message(LocalizeString_Message.VV_Message_EndingRuinsRevealed.Translate(), _site, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public void TickSite()
        {
            if (!HasCoordinates || _coreExtracted || Site != null) { return; }

            if (SpawnSite())
            {
                Log.Warning("[ViViRace] the ancient ruins site disappeared before its core was extracted; restored it at the same tile.");
            }
        }

        public static bool TryFindSiteTile(out PlanetTile tile)
        {
            var surface = Find.WorldGrid.Surface;
            var anchor = FindSurfaceAnchorTile(surface);
            bool requireReachable = anchor.Valid && !Find.World.Impassable(anchor);
            var allowedLandmarks = VVSitePartDefOf.VV_AncientRuins?.GetModExtension<AncientRuinsSiteExtension>()?.allowedLandmarks;

            // 1순위: 비비 고대 유적 분위기에 가장 어울리는 온화·비산악·비해안·비강 지표 타일 (허용된 안전 랜드마크 외 랜드마크 제외)
            if (TryFindSiteTileWith(out tile, anchor, surface, MinSiteDistance, MaxSiteDistance, allowedLandmarks,
                t => IsValidBaseSurfaceTile(t, anchor, requireReachable) && IsIdealRuinsTile(t, allowedLandmarks)))
            {
                return true;
            }

            // 2순위: 같은 거리 내에서 사막·툰드라·늪지·구릉·강·해안을 허용하되 극한/용암/흉터/산악/비허용 랜드마크 제외
            if (TryFindSiteTileWith(out tile, anchor, surface, MinSiteDistance, MaxSiteDistance, allowedLandmarks,
                t => IsValidBaseSurfaceTile(t, anchor, requireReachable) && IsAcceptableRuinsTile(t, allowedLandmarks)))
            {
                return true;
            }

            // 3순위: 극지·산맥·용암지대 한가운데 정착한 경우를 위해 탐색 반경을 넓혀 적합한 지형을 우선 탐색
            if (TryFindSiteTileWith(out tile, anchor, surface, MinSiteDistance, ExpandedMaxSiteDistance, allowedLandmarks,
                t => IsValidBaseSurfaceTile(t, anchor, requireReachable) && IsAcceptableRuinsTile(t, allowedLandmarks)))
            {
                return true;
            }

            // 4순위(소프트락 방지): 이동 가능한 모든 지표 타일 허용 (도보 도달 가능 우선 -> 고립 섬/궤도 기지 대비 최후 폴백)
            if (requireReachable && TryFindSiteTileWith(out tile, anchor, surface, MinSiteDistance, ExpandedMaxSiteDistance, allowedLandmarks,
                t => IsValidBaseSurfaceTile(t, anchor, requireReachable: true)))
            {
                return true;
            }

            return TryFindSiteTileWith(out tile, anchor, surface, FallbackMinSiteDistance, FallbackMaxSiteDistance, allowedLandmarks,
                t => IsValidBaseSurfaceTile(t, anchor, requireReachable: false));
        }

        private static bool TryFindSiteTileWith(out PlanetTile tile, PlanetTile near, PlanetLayer surface, int minDist, int maxDist, List<LandmarkDef> allowedLandmarks, Predicate<PlanetTile> validator)
        {
            return TileFinder.TryFindNewSiteTile(
                out tile,
                near,
                minDist,
                maxDist,
                allowCaravans: false,
                allowedLandmarks: allowedLandmarks,
                selectLandmarkChance: 0f,
                canSelectComboLandmarks: false,
                tileFinderMode: TileFinderMode.Near,
                exitOnFirstTileFound: false,
                canBeSpace: false,
                layer: surface,
                validator: validator);
        }

        private static PlanetTile FindSurfaceAnchorTile(PlanetLayer surface)
        {
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var m = maps[i];
                if (m != null && m.IsPlayerHome && m.Tile.Valid && m.Tile.Layer != null && m.Tile.Layer.IsRootSurface)
                {
                    return m.Tile;
                }
            }

            var anyHome = Find.AnyPlayerHomeMap;
            if (anyHome != null && anyHome.Tile.Valid && surface != null)
            {
                var projected = surface.GetClosestTile_NewTemp(anyHome.Tile, validSettlement: true);
                if (projected.Valid)
                {
                    return projected;
                }
            }

            if (TileFinder.TryFindRandomPlayerTile(out var playerTile, allowCaravans: true, validator: null, canBeSpace: false, layer: surface))
            {
                return playerTile;
            }

            return PlanetTile.Invalid;
        }

        private static bool IsValidBaseSurfaceTile(PlanetTile pt, PlanetTile anchor, bool requireReachable)
        {
            if (!pt.Valid || pt.Layer == null || !pt.Layer.IsRootSurface) { return false; }

            var t = pt.Tile;
            if (t == null || !t.OnSurface || t.WaterCovered || Find.World.Impassable(pt)) { return false; }

            var biome = t.PrimaryBiome;
            if (biome == null || !biome.implemented || !biome.canBuildBase
                || biome.impassable || biome.isWaterBiome || biome.inVacuum || biome.isBackgroundBiome)
            {
                return false;
            }

            if (Find.WorldObjects.AnyWorldObjectAt(pt)) { return false; }

            if (requireReachable && anchor.Valid && !Find.WorldReachability.CanReach(anchor, pt))
            {
                return false;
            }

            return true;
        }

        private static bool IsAllowedLandmark(Tile tile, List<LandmarkDef> allowedLandmarks)
        {
            var landmark = tile?.Landmark;
            if (landmark == null) { return true; }
            if (landmark.isComboLandmark) { return false; }
            return allowedLandmarks != null && allowedLandmarks.Contains(landmark.def);
        }

        private static bool IsHarshOrUnfittingBiome(BiomeDef biome)
        {
            if (biome == null) { return true; }
            if (biome.isExtremeBiome) { return true; }
            if (biome == BiomeDefOf.Scarlands) { return true; }
            if (string.Equals(biome.defName, "LavaField", StringComparison.OrdinalIgnoreCase)) { return true; }
            return false;
        }

        private static bool IsIdealRuinsTile(PlanetTile pt, List<LandmarkDef> allowedLandmarks)
        {
            var t = pt.Tile;
            if (t == null || !IsAllowedLandmark(t, allowedLandmarks)) { return false; }

            var biome = t.PrimaryBiome;
            if (IsHarshOrUnfittingBiome(biome)) { return false; }
            if (biome.plantDensity < 0.2f || t.swampiness > 0.25f) { return false; }

            if (t.hilliness != Hilliness.Flat && t.hilliness != Hilliness.SmallHills) { return false; }
            if (t.IsCoastal) { return false; }

            if (t is SurfaceTile surfaceTile && !surfaceTile.Rivers.NullOrEmpty()) { return false; }

            return !HasDisruptiveNaturalMutator(t, allowJunkOrPollution: false);
        }

        private static bool IsAcceptableRuinsTile(PlanetTile pt, List<LandmarkDef> allowedLandmarks)
        {
            var t = pt.Tile;
            if (t == null || !IsAllowedLandmark(t, allowedLandmarks)) { return false; }
            if (IsHarshOrUnfittingBiome(t.PrimaryBiome)) { return false; }
            if (t.hilliness == Hilliness.Mountainous || t.hilliness == Hilliness.Impassable) { return false; }

            return !HasDisruptiveNaturalMutator(t, allowJunkOrPollution: true);
        }

        private static bool HasDisruptiveNaturalMutator(Tile tile, bool allowJunkOrPollution)
        {
            var mutators = tile?.Mutators;
            if (mutators == null || mutators.Count == 0) { return false; }

            for (int i = 0; i < mutators.Count; i++)
            {
                var m = mutators[i];
                if (m == null) { continue; }
                if (m.IsCave || m.structureGenParms != null) { return true; }
                if (!allowJunkOrPollution && (m.junkDensityFactor > 1.01f || m.pollutionRange.min > 0f)) { return true; }
            }

            return false;
        }

        private bool SpawnSite()
        {
            var partDef = VVSitePartDefOf.VV_AncientRuins;
            var siteDef = VVWorldObjectDefOf.VV_AncientRuins;
            if (partDef == null || siteDef == null || !_tile.Valid) { return false; }

            var site = SiteMaker.MakeSite(partDef, _tile, null, ifHostileThenMustRemainHostile: false, worldObjectDef: siteDef);
            Find.WorldObjects.Add(site);
            _site = site;
            return true;
        }
        #endregion

        #region 유적 조사
        public AcceptanceReport CanInvestigate(int index, Pawn pawn)
        {
            if (index <= _investigated) { return false; }
            if (index != _investigated + 1)
            {
                return LocalizeString_Etc.VV_FailReason_RuinPreviousFlower.Translate();
            }

            if (!IsQualified(pawn))
            {
                return LocalizeString_Etc.VV_FailReason_RuinNotQualified.Translate(RequiredAttunementLevel);
            }

            return true;
        }

        public void Investigate(int index, Pawn pawn, Thing flower)
        {
            if (index != _investigated + 1 || index < 1 || index > EchoCount) { return; }

            _investigated = index;

            Find.LetterStack.ReceiveLetter(
                EchoLabelKeys[index - 1].Translate(),
                ViviEndingUtility.TranslateWithResonator(EchoTextKeys[index - 1], pawn),
                LetterDefOf.NeutralEvent,
                pawn != null ? new LookTargets(pawn) : LookTargets.Invalid,
                null,
                _ending.Quest);

            var targetMap = flower?.MapHeld ?? Site?.Map ?? pawn?.MapHeld;
            OnZoneUnlocked(targetMap, index);
        }

        public void Notify_HexagonSolved()
        {
            _hexagonSolved = true;
        }

        private void OnZoneUnlocked(Map map, int index)
        {
            if (index >= EchoCount)
            {
                _hexagonSolved = true;
            }

            map?.GetComponent<MapComponent_ViviAncientRuins>()?.Notify_EchoInvestigated(index);
        }

        public AcceptanceReport CanExtractCore(Pawn pawn)
        {
            if (_coreExtracted)
            {
                return LocalizeString_Etc.VV_FailReason_RuinCoreExtracted.Translate();
            }

            if (_investigated < EchoCount)
            {
                return LocalizeString_Etc.VV_FailReason_RuinCoreLocked.Translate();
            }

            if (!IsQualified(pawn))
            {
                return LocalizeString_Etc.VV_FailReason_RuinNotQualified.Translate(RequiredAttunementLevel);
            }

            return true;
        }

        public Thing ExtractCore(Pawn pawn, Thing altar)
        {
            if (_coreExtracted || VVThingDefOf.VV_AncientAmberCore == null) { return null; }

            var map = altar?.MapHeld ?? pawn?.MapHeld;
            if (map == null) { return null; }

            var core = ThingMaker.MakeThing(VVThingDefOf.VV_AncientAmberCore);
            bool addedToInventory = pawn != null
                && pawn.inventory?.innerContainer != null
                && pawn.inventory.innerContainer.TryAdd(core);

            if (!addedToInventory)
            {
                var cell = pawn != null && pawn.Spawned ? pawn.Position : altar.PositionHeld;
                if (!GenPlace.TryPlaceThing(core, cell, map, ThingPlaceMode.Near)) { return null; }
            }

            _coreExtracted = true;

            var target = addedToInventory ? new LookTargets(pawn) : new LookTargets(core);
            Messages.Message(
                ViviEndingUtility.TranslateWithResonator(LocalizeString_Message.VV_Message_RuinCoreExtracted, pawn),
                target,
                MessageTypeDefOf.PositiveEvent);

            map.GetComponent<MapComponent_ViviAncientRuins>()?.BeginResealSequence();
            return core;
        }

        public bool GrantReplacementCore(Map map)
        {
            if (map == null || !_coreExtracted || VVThingDefOf.VV_AncientAmberCore == null) { return false; }

            var core = ThingMaker.MakeThing(VVThingDefOf.VV_AncientAmberCore);
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, new[] { core }, forbid: false);
            return true;
        }
        #endregion

        #region 개발자 도구
        public void DebugInvestigateNext(Pawn pawn)
        {
            if (_investigated >= EchoCount) { return; }

            Investigate(_investigated + 1, pawn, null);
        }

        public void DebugComplete()
        {
            _missiveReceived = true;
            _investigated = EchoCount;
            _hexagonSolved = true;
            _coreExtracted = true;
        }

        public void DebugDestroySite()
        {
            var site = Site;
            if (site != null)
            {
                site.Destroy();
            }
            _site = null;
        }
        #endregion
    }
}
