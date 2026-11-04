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
        private string _cachedConditionReason;
        private bool _cachedConditionOk;
        private int _cachedTick = -99999;
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
            _cachedConditionOk = BeekeepingUtility.CheckArrivalConditions(Map, out _cachedConditionReason);
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

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            switch (_phase)
            {
                case ViviLurePhase.ScoutVisiting:
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureScouting.Translate());
                    return sb.ToString();

                case ViviLurePhase.ThiefVisiting:
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureThief.Translate());
                    return sb.ToString();

                case ViviLurePhase.AwaitingSettlers:
                    var remaining = _settleTick - Find.TickManager.TicksGame;
                    sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_LureAwaitingSettlers.Translate(
                        remaining > 0 ? remaining.ToStringTicksToPeriod() : "-"));
                    return sb.ToString();
            }

            sb.AppendInNewLine(HasBait
                ? LocalizeString_Inspector.VV_Inspector_LureBaitReady.Translate()
                : LocalizeString_Inspector.VV_Inspector_LureNoBait.Translate());

            RefreshCacheIfNeeded();
            if (!_cachedConditionOk && !_cachedConditionReason.NullOrEmpty())
            {
                sb.AppendInNewLine(_cachedConditionReason);
            }

            if (_cachedReport != null)
            {
                sb.AppendInNewLine(_cachedReport.ToInspectString());
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
