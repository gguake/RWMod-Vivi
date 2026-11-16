using RimWorld;
using RimWorld.Planet;
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
        private bool _coreExtracted;

        public bool MissiveReceived => _missiveReceived;
        public bool HasCoordinates => _tile.Valid;
        public PlanetTile Tile => _tile;
        public Site Site => _site != null && !_site.Destroyed ? _site : null;
        public int Investigated => _investigated;
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

        private static bool TryFindSiteTile(out PlanetTile tile)
        {
            var near = Find.AnyPlayerHomeMap?.Tile ?? PlanetTile.Invalid;
            return TileFinder.TryFindNewSiteTile(out tile, near, MinSiteDistance, MaxSiteDistance, allowCaravans: false, selectLandmarkChance: 0f);
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

            OnZoneUnlocked(flower?.MapHeld, index);
        }

        // 구역 봉쇄·해금 구조는 유적 맵 작업에서 채운다.
        private void OnZoneUnlocked(Map map, int index)
        {
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
            var cell = pawn != null && pawn.Spawned ? pawn.Position : altar.PositionHeld;
            if (!GenPlace.TryPlaceThing(core, cell, map, ThingPlaceMode.Near)) { return null; }

            _coreExtracted = true;

            Messages.Message(
                ViviEndingUtility.TranslateWithResonator(LocalizeString_Message.VV_Message_RuinCoreExtracted, pawn),
                core,
                MessageTypeDefOf.PositiveEvent);
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
