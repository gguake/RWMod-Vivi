using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    public enum ViviLurePhase
    {
        Idle,
        ScoutVisiting,
        ThiefVisiting,
        AwaitingSettlers,
    }

    // 유인 조건 탭의 조건 줄. 방문 조건은 스토리텔러 방문 인시던트가, 둥지 자리 조건은 정찰 판정과 정착대 도착이 본다.
    public enum ViviLureConditionSection
    {
        Visit,
        Site,
    }

    public enum ViviLureConditionState
    {
        Met,
        Unmet,
        Info,
    }

    public class ViviLureConditionRow
    {
        public ViviLureConditionSection section;
        public ViviLureConditionState state;
        public string label;
        public string value;
        public string tooltip;
    }

    // 꿀 음식을 올려 두는 향로 선반. 유인 진행 상태만 저장하고, 발동은 스토리텔러 인시던트가 맡는다.
    public class Building_ViviLure : Building_Storage
    {
        private const int InspectCacheInterval = 250;
        private const int SmokeIntervalRare = 3;

        private ViviLurePhase _phase;
        private Faction _faction;
        private int _phaseStartTick;
        private int _settleTick = -1;

        private ViviNestSiteReport _cachedReport;
        private int _cachedTick = -99999;
        private int _cacheVersion;
        private readonly List<ViviLureConditionRow> _conditionRows = new List<ViviLureConditionRow>();
        private int _conditionRowsVersion = -1;
        private int _rareTickCount;

        public ViviLurePhase Phase => _phase;
        public bool InProgress => _phase != ViviLurePhase.Idle;
        public int SettleTick => _settleTick;

        // 정찰부터 정착대 도착까지 같은 임시 세력이 이어져야 한다.
        public Faction ReservedFaction => _phase == ViviLurePhase.ScoutVisiting || _phase == ViviLurePhase.AwaitingSettlers ? _faction : null;

        public static bool IsBait(Thing thing) => thing != null && (Gene_HoneyDependency.IsFoodContainingHoney(thing) || thing.def == VVThingDefOf.RawBerries);

        public IEnumerable<Thing> BaitThings
        {
            get
            {
                if (slotGroup == null) { yield break; }

                foreach (var thing in slotGroup.HeldThings)
                {
                    if (IsBait(thing))
                    {
                        yield return thing;
                    }
                }
            }
        }

        public bool HasBait
        {
            get
            {
                foreach (var _ in BaitThings)
                {
                    return true;
                }
                return false;
            }
        }

        public int BaitCount
        {
            get
            {
                var count = 0;
                foreach (var thing in BaitThings)
                {
                    count += thing.stackCount;
                }
                return count;
            }
        }

        public ViviNestSiteReport SiteReport
        {
            get
            {
                RefreshCacheIfNeeded();
                return _cachedReport;
            }
        }

        public ViviNestSiteReport EvaluateSiteNow()
        {
            _cachedTick = -99999;
            RefreshCacheIfNeeded();
            return _cachedReport;
        }

        private void RefreshCacheIfNeeded()
        {
            if (!Spawned) { return; }

            var ticks = Find.TickManager.TicksGame;
            if (_cachedReport != null && ticks - _cachedTick < InspectCacheInterval) { return; }

            _cachedTick = ticks;
            _cachedReport = ViviNestSiteEvaluator.Evaluate(Map, Position);
            _cacheVersion++;
        }

        // 유인 조건 탭이 그리는 조건 줄. 판정과 같은 조건 함수(BeekeepingUtility)와 같은 환경 보고서(ViviNestSiteReport)로 만들고, 보고서 캐시(250틱)를 새로 잴 때 다시 만든다.
        public List<ViviLureConditionRow> ConditionRows
        {
            get
            {
                RefreshCacheIfNeeded();
                if (_conditionRowsVersion != _cacheVersion)
                {
                    _conditionRowsVersion = _cacheVersion;
                    BuildConditionRows();
                }
                return _conditionRows;
            }
        }

        public int UnmetConditionCount
        {
            get
            {
                var count = 0;
                var rows = ConditionRows;
                for (int i = 0; i < rows.Count; ++i)
                {
                    if (rows[i].state == ViviLureConditionState.Unmet) { count++; }
                }
                return count;
            }
        }

        private void BuildConditionRows()
        {
            _conditionRows.Clear();
            var map = Map;
            var report = _cachedReport;
            if (map == null || report == null) { return; }

            // 방문 조건: 방문 인시던트(IncidentWorker_ViviLureVisit.CanFireNowSub)와 같다. 정착대가 도착할 때도 미끼·길을 뺀 공통 조건을 다시 본다.
            const ViviLureConditionSection visit = ViviLureConditionSection.Visit;
            AddConditionRow(visit, BeekeepingUtility.IsBeekeepingMap(map), LocalizeString_ITab.VV_ITab_ViviLure_Map.Translate());
            AddConditionRow(visit, !BeekeepingUtility.AnyRoyalVivi(map), LocalizeString_ITab.VV_ITab_ViviLure_NoRoyal.Translate());

            var viviCount = BeekeepingUtility.CountPlayerVivis(map);
            AddConditionRow(visit, viviCount <= BeekeepingUtility.MaxPlayerViviCount,
                LocalizeString_ITab.VV_ITab_ViviLure_ViviCount.Translate(BeekeepingUtility.MaxPlayerViviCount),
                LocalizeString_ITab.VV_ITab_ViviLure_ViviCountValue.Translate(viviCount));

            AddConditionRow(visit, !BeekeepingUtility.AnyHostileThreat(map), LocalizeString_ITab.VV_ITab_ViviLure_NoHostile.Translate());

            var safe = BeekeepingUtility.WanderingViviSafeTemperature;
            AddConditionRow(visit, BeekeepingUtility.TemperatureSuitable(map),
                LocalizeString_ITab.VV_ITab_ViviLure_Temperature.Translate(),
                LocalizeString_ITab.VV_ITab_ViviLure_TemperatureValue.Translate(
                    map.mapTemperature.OutdoorTemp.ToStringTemperature("F0"), map.mapTemperature.SeasonalTemp.ToStringTemperature("F0")),
                LocalizeString_ITab.VV_ITab_ViviLure_TemperatureTip.Translate(safe.min.ToStringTemperature("F0"), safe.max.ToStringTemperature("F0")));

            AddConditionRow(visit, BeekeepingUtility.FlowerSeasonNow(map), LocalizeString_ITab.VV_ITab_ViviLure_Season.Translate());
            AddConditionRow(visit, !BeekeepingUtility.OutdoorDangerous(map), LocalizeString_ITab.VV_ITab_ViviLure_Weather.Translate(),
                tooltip: LocalizeString_ITab.VV_ITab_ViviLure_WeatherTip.Translate());

            var bait = BaitCount;
            AddConditionRow(visit, bait > 0, LocalizeString_ITab.VV_ITab_ViviLure_Bait.Translate(), LocalizeString_ITab.VV_ITab_ViviLure_BaitValue.Translate(bait),
                LocalizeString_ITab.VV_ITab_ViviLure_BaitTip.Translate());

            AddConditionRow(visit, !BeekeepingUtility.AnyNestOrAttraction(map, this), LocalizeString_ITab.VV_ITab_ViviLure_NoOtherLure.Translate());
            AddConditionRow(visit, BeekeepingUtility.TryFindEntryCellReaching(map, Position, out _), LocalizeString_ITab.VV_ITab_ViviLure_Reachable.Translate());

            // 둥지 자리 조건: 정찰이 끝날 때와 정착대가 도착할 때 보는 최소 요건(ViviNestSiteReport.Acceptable)과 정착대가 방까지 올 길(IncidentWorker_ViviNestSettlers.TrySpawnSettlers)
            const ViviLureConditionSection site = ViviLureConditionSection.Site;
            AddConditionRow(site, report.HasEnoughFlowers,
                LocalizeString_ITab.VV_ITab_ViviLure_Flowers.Translate(ViviNestSiteEvaluator.MinFlowerCount),
                LocalizeString_ITab.VV_ITab_ViviLure_FlowersValue.Translate(report.flowerCount, report.flowerScore.ToStringPercent()),
                LocalizeString_ITab.VV_ITab_ViviLure_FlowersTip.Translate(ViviNestSiteEvaluator.Radius, ViviNestSiteEvaluator.FullFlowerCount));

            var roomTip = LocalizeString_ITab.VV_ITab_ViviLure_RoomTip.Translate(
                ViviNestSiteEvaluator.Radius, ViviNestSiteEvaluator.MinRoomCells, ViviNestSiteEvaluator.MaxRoomCells, ViviNestSiteEvaluator.BuildingPenalty.ToStringPercent());
            if (report.HasRoom)
            {
                AddConditionRow(site, true, LocalizeString_ITab.VV_ITab_ViviLure_Room.Translate(),
                    LocalizeString_ITab.VV_ITab_ViviLure_RoomValue.Translate(report.roomCellCount, report.roomBuildingCount, report.roomScore.ToStringPercent()), roomTip);
                AddConditionRow(site, BeekeepingUtility.TryFindEntryCellReaching(map, report.bestRoomCell, out _), LocalizeString_ITab.VV_ITab_ViviLure_RoomReachable.Translate());
            }
            else
            {
                AddConditionRow(site, false, LocalizeString_ITab.VV_ITab_ViviLure_Room.Translate(), LocalizeString_ITab.VV_ITab_ViviLure_None.Translate(), roomTip);
            }

            AddConditionRow(site, report.HasEnoughFertileSoil,
                LocalizeString_ITab.VV_ITab_ViviLure_FertileSoil.Translate(ViviNestSiteEvaluator.MinFertileCells),
                LocalizeString_ITab.VV_ITab_ViviLure_FertileSoilValue.Translate(report.fertileCellCount),
                LocalizeString_ITab.VV_ITab_ViviLure_FertileSoilTip.Translate(ViviNestSiteEvaluator.Radius, ViviNestSiteEvaluator.MinFertility.ToStringPercent()));

            AddInfoRow(site, LocalizeString_ITab.VV_ITab_ViviLure_Mana.Translate(),
                LocalizeString_ITab.VV_ITab_ViviLure_ManaValue.Translate(report.averageMana.ToString("0"), report.manaScore.ToStringPercent()),
                LocalizeString_ITab.VV_ITab_ViviLure_ManaTip.Translate(ViviNestSiteEvaluator.ManaRadius, ViviNestSiteEvaluator.FullMana));

            if (report.Acceptable)
            {
                AddInfoRow(site, LocalizeString_ITab.VV_ITab_ViviLure_Score.Translate(),
                    LocalizeString_ITab.VV_ITab_ViviLure_ScoreValue.Translate(report.TotalScore.ToStringPercent(), report.SettlerCount),
                    LocalizeString_ITab.VV_ITab_ViviLure_ScoreTip.Translate(
                        ViviNestSiteEvaluator.FlowerWeight.ToStringPercent(), ViviNestSiteEvaluator.RoomWeight.ToStringPercent(), ViviNestSiteEvaluator.ManaWeight.ToStringPercent(),
                        ViviNestSiteEvaluator.MaxSettleDelayTicks.ToStringTicksToPeriod(), ViviNestSiteEvaluator.MinSettleDelayTicks.ToStringTicksToPeriod(),
                        ViviNestSiteEvaluator.MinSettlerCount, ViviNestSiteEvaluator.MaxSettlerCount));
            }
        }

        private void AddConditionRow(ViviLureConditionSection section, bool met, string label, string value = null, string tooltip = null)
        {
            _conditionRows.Add(new ViviLureConditionRow
            {
                section = section,
                state = met ? ViviLureConditionState.Met : ViviLureConditionState.Unmet,
                label = label,
                value = value,
                tooltip = tooltip,
            });
        }

        private void AddInfoRow(ViviLureConditionSection section, string label, string value, string tooltip = null)
        {
            _conditionRows.Add(new ViviLureConditionRow
            {
                section = section,
                state = ViviLureConditionState.Info,
                label = label,
                value = value,
                tooltip = tooltip,
            });
        }

        // 진행 상태 한 줄(정보창·유인 조건 탭). 대기 중이면 null.
        public string PhaseStatus
        {
            get
            {
                switch (_phase)
                {
                    case ViviLurePhase.ScoutVisiting:
                        return LocalizeString_Inspector.VV_Inspector_LureScouting.Translate();

                    case ViviLurePhase.ThiefVisiting:
                        return LocalizeString_Inspector.VV_Inspector_LureThief.Translate();

                    case ViviLurePhase.AwaitingSettlers:
                        var remaining = _settleTick - Find.TickManager.TicksGame;
                        return LocalizeString_Inspector.VV_Inspector_LureAwaitingSettlers.Translate(remaining > 0 ? remaining.ToStringTicksToPeriod() : "-");
                }

                return null;
            }
        }

        public void Notify_ScoutVisitStarted(Faction faction)
        {
            _phase = ViviLurePhase.ScoutVisiting;
            _faction = faction;
            _phaseStartTick = Find.TickManager.TicksGame;
            _settleTick = -1;
        }

        public void Notify_ThiefVisitStarted()
        {
            _phase = ViviLurePhase.ThiefVisiting;
            _faction = null;
            _phaseStartTick = Find.TickManager.TicksGame;
            _settleTick = -1;
        }

        public void Notify_SettlersQueued(int fireTick)
        {
            _phase = ViviLurePhase.AwaitingSettlers;
            _phaseStartTick = Find.TickManager.TicksGame;
            _settleTick = fireTick;
        }

        // 정착이 시작되면 유인통은 할 일을 마친다. 세력은 이제 둥지 구성원이 붙잡고 있다.
        public void Notify_Settled()
        {
            _phase = ViviLurePhase.Idle;
            _faction = null;
            _settleTick = -1;
        }

        public void ResetAttraction(bool releaseFaction)
        {
            var faction = _faction;
            _phase = ViviLurePhase.Idle;
            _faction = null;
            _settleTick = -1;

            if (releaseFaction && faction != null)
            {
                BeekeepingUtility.ReleaseWanderingFaction(faction);
            }
        }

        // 미끼는 정착민이 먹거나 다른 창고로 옮기지 않게 금지 표시한다. 방문자는 금지 표시를 보지 않는 전용 잡을 쓴다.
        public override void Notify_ReceivedThing(Thing newItem)
        {
            base.Notify_ReceivedThing(newItem);

            if (IsBait(newItem))
            {
                newItem.SetForbidden(true, warnOnFail: false);
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // 포장했다 다시 설치해도 이전 유인이 이어지지 않게 디스폰 때 지운다.
            if (InProgress)
            {
                ResetAttraction(releaseFaction: true);
            }

            base.DeSpawn(mode);
        }

        public override void TickRare()
        {
            base.TickRare();

            if (!Spawned || ++_rareTickCount < SmokeIntervalRare) { return; }

            _rareTickCount = 0;
            if (HasBait)
            {
                FleckMaker.ThrowSmoke(DrawPos + new UnityEngine.Vector3(0f, 0f, 0.3f), Map, 0.5f);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _phase, "phase", ViviLurePhase.Idle);
            // "faction"은 건물 자신의 소속 세력 저장 키라 겹치지 않게 다른 이름을 쓴다.
            Scribe_References.Look(ref _faction, "lureFaction");
            Scribe_Values.Look(ref _phaseStartTick, "phaseStartTick");
            Scribe_Values.Look(ref _settleTick, "settleTick", -1);
        }

        // 정보창에는 진행 상태 한 줄만 둔다. 조건별 충족 여부와 점수는 유인 조건 탭(ITab_ViviLureConditions)에 있다.
        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            var phase = PhaseStatus;
            if (phase != null)
            {
                sb.AppendInNewLine(phase);
            }
            else if (!HasBait)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureNoBait.Translate());
            }
            else
            {
                var unmet = UnmetConditionCount;
                sb.AppendInNewLine(unmet > 0
                    ? LocalizeString_Inspector.VV_Inspector_LureBaitReadyUnmet.Translate(unmet)
                    : LocalizeString_Inspector.VV_Inspector_LureBaitReady.Translate());
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!DebugSettings.godMode) { yield break; }

            if (_phase == ViviLurePhase.Idle)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Lure scouts",
                    action = () => IncidentWorker_ViviLureVisit.TryStartVisit(this, ViviLureVisitor.Scouts),
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Lure bear",
                    action = () => IncidentWorker_ViviLureVisit.TryStartVisit(this, ViviLureVisitor.Bear),
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Lure hornet",
                    action = () => IncidentWorker_ViviLureVisit.TryStartVisit(this, ViviLureVisitor.Hornet),
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Settle now",
                    action = () => IncidentWorker_ViviNestSettlers.DebugSettleNow(this),
                };
            }

            if (_phase == ViviLurePhase.AwaitingSettlers)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Settlers now",
                    action = () => IncidentWorker_ViviNestSettlers.DebugFireNow(this),
                };
            }

            if (_phase != ViviLurePhase.Idle)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Reset lure",
                    action = () => ResetAttraction(releaseFaction: true),
                };
            }
        }
    }
}
