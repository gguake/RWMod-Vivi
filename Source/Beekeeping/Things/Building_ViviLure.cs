using RimWorld;
using System;
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

    public class Building_ViviLure : Building_Storage
    {
        private const int InspectCacheInterval = 250;
        private const int SmokeIntervalRare = 3;

        private ViviLurePhase _phase;
        private Faction _faction;
        private int _settleTick = -1;

        private ViviNestSiteReport _cachedReport;
        private int _cachedTick = -99999;
        private int _cacheVersion;
        private readonly List<ViviLureConditionRow> _conditionRows = new List<ViviLureConditionRow>();
        private int _conditionRowsVersion = -1;
        private int _rareTickCount;

        public ViviLurePhase Phase => _phase;
        public bool InProgress => _phase != ViviLurePhase.Idle;

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

            var conditions = BeekeepingUtility.ArrivalConditions;
            for (int i = 0; i < conditions.Length; ++i)
            {
                var condition = conditions[i];
                AddConditionRow(ViviLureConditionSection.Visit, condition.met(map), condition.rowLabel(), tooltip: condition.tipKey != null ? (string)condition.tipKey.Translate() : null);
            }
            AddConditionRow(ViviLureConditionSection.Visit, HasBait, LocalizeString_ITab.VV_ITab_ViviLure_Bait.Translate(), tooltip: LocalizeString_ITab.VV_ITab_ViviLure_BaitTip.Translate());
            AddConditionRow(ViviLureConditionSection.Visit, !BeekeepingUtility.AnyNestOrAttraction(map, this), LocalizeString_ITab.VV_ITab_ViviLure_NoOtherLure.Translate());
            AddConditionRow(ViviLureConditionSection.Visit, BeekeepingUtility.TryFindEntryCellReaching(map, Position, out _), LocalizeString_ITab.VV_ITab_ViviLure_Reachable.Translate());

            AddConditionRow(ViviLureConditionSection.Site, report.HasEnoughFlowers, LocalizeString_ITab.VV_ITab_ViviLure_Flowers.Translate(ViviNestSiteEvaluator.MinFlowerCount),
                ScoreValue(report.flowerScore), LocalizeString_ITab.VV_ITab_ViviLure_FlowersTip.Translate());

            var roomTip = LocalizeString_ITab.VV_ITab_ViviLure_RoomTip.Translate();
            if (report.HasRoom)
            {
                AddConditionRow(ViviLureConditionSection.Site, true, LocalizeString_ITab.VV_ITab_ViviLure_Room.Translate(), ScoreValue(report.roomScore), roomTip);
                AddConditionRow(ViviLureConditionSection.Site, BeekeepingUtility.TryFindEntryCellReaching(map, report.bestRoomCell, out _), LocalizeString_ITab.VV_ITab_ViviLure_RoomReachable.Translate());
            }
            else
            {
                AddConditionRow(ViviLureConditionSection.Site, false, LocalizeString_ITab.VV_ITab_ViviLure_Room.Translate(), LocalizeString_ITab.VV_ITab_ViviLure_None.Translate(), roomTip);
            }

            AddConditionRow(ViviLureConditionSection.Site, report.HasEnoughFertileSoil, LocalizeString_ITab.VV_ITab_ViviLure_FertileSoil.Translate(ViviNestSiteEvaluator.MinFertileCells),
                tooltip: LocalizeString_ITab.VV_ITab_ViviLure_FertileSoilTip.Translate());

            AddConditionRow(ViviLureConditionSection.Site, report.HasLongEnoughGrowingPeriod, LocalizeString_ITab.VV_ITab_ViviLure_GrowingPeriod.Translate(ViviNestSiteEvaluator.MinGrowingPeriodDays),
                tooltip: LocalizeString_ITab.VV_ITab_ViviLure_GrowingPeriodTip.Translate());

            AddRow(ViviLureConditionSection.Site, ViviLureConditionState.Info, LocalizeString_ITab.VV_ITab_ViviLure_Mana.Translate(), ScoreValue(report.manaScore),
                LocalizeString_ITab.VV_ITab_ViviLure_ManaTip.Translate());

            if (report.Acceptable)
            {
                AddRow(ViviLureConditionSection.Site, ViviLureConditionState.Info, LocalizeString_ITab.VV_ITab_ViviLure_Score.Translate(),
                    LocalizeString_ITab.VV_ITab_ViviLure_ScoreValue.Translate(report.TotalScore.ToStringPercent(), report.SettlerCount),
                    LocalizeString_ITab.VV_ITab_ViviLure_ScoreTip.Translate());
            }
        }

        private static string ScoreValue(float score) => LocalizeString_ITab.VV_ITab_ViviLure_PartScore.Translate(score.ToStringPercent());

        private void AddConditionRow(ViviLureConditionSection section, bool met, string label, string value = null, string tooltip = null)
        {
            AddRow(section, met ? ViviLureConditionState.Met : ViviLureConditionState.Unmet, label, value, tooltip);
        }

        private void AddRow(ViviLureConditionSection section, ViviLureConditionState state, string label, string value = null, string tooltip = null)
        {
            _conditionRows.Add(new ViviLureConditionRow
            {
                section = section,
                state = state,
                label = label,
                value = value,
                tooltip = tooltip,
            });
        }

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
            _settleTick = -1;
        }

        public void Notify_ThiefVisitStarted()
        {
            _phase = ViviLurePhase.ThiefVisiting;
            _faction = null;
            _settleTick = -1;
        }

        public void Notify_SettlersScheduled(int fireTick)
        {
            _phase = ViviLurePhase.AwaitingSettlers;
            _settleTick = fireTick;
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
            if (InProgress)
            {
                ResetAttraction(releaseFaction: true);
            }

            base.DeSpawn(mode);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!Spawned) { return; }

            if (_phase == ViviLurePhase.AwaitingSettlers && Find.TickManager.TicksGame >= _settleTick)
            {
                FireSettlers();
            }

            if (++_rareTickCount < SmokeIntervalRare) { return; }

            _rareTickCount = 0;
            if (HasBait)
            {
                FleckMaker.ThrowSmoke(DrawPos + new UnityEngine.Vector3(0f, 0f, 0.3f), Map, 0.5f);
            }
        }

        private void FireSettlers()
        {
            var parms = new IncidentParms
            {
                target = Map,
                faction = _faction,
                forced = true,
            };

            bool fired;
            try
            {
                fired = Find.Storyteller.TryFire(new FiringIncident(VVIncidentDefOf.VV_ViviNestSettlers, null, parms));
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] failed to fire vivi nest settlers for {this}: {ex}");
                if (_phase == ViviLurePhase.AwaitingSettlers)
                {
                    ResetAttraction(releaseFaction: true);
                }
                return;
            }

            if (!fired && _phase == ViviLurePhase.AwaitingSettlers)
            {
                string reason = LocalizeString_Inspector.VV_Inspector_LureConditionIncidentBlocked.Translate();
                ResetAttraction(releaseFaction: true);
                Messages.Message(LocalizeString_Message.VV_Message_ViviNestSettlersCanceled.Translate(reason), this, MessageTypeDefOf.NeutralEvent);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _phase, "phase", ViviLurePhase.Idle);
            // "faction"은 건물 소속 세력의 저장 키다.
            Scribe_References.Look(ref _faction, "lureFaction");
            Scribe_Values.Look(ref _settleTick, "settleTick", -1);
        }

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
                    action = FireSettlers,
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
