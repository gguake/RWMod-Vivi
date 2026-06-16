using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ViviFairyController : CompProperties
    {
        public ThingDef fairyDef;
        public int fairyLifespanTicks = 30000;

        // 자유 대기 요정이 로열 비비 주위를 도는 가로로 납작한 타원 궤도 설정. (XML에서 조정 가능)
        public float orbitRadiusX = 0.6f;          // 궤도 가로 반경(넓게)
        public float orbitRadiusZ = 0.3f;          // 궤도 세로 반경(납작하게)
        public float orbitDepth = 0.06f;           // 앞/뒤 그리기 고도 가감(도넛 깊이감)
        public float orbitAngularSpeed = 0.008f;   // 궤도 회전 속도(rad/틱)

        public CompProperties_ViviFairyController()
        {
            compClass = typeof(CompViviFairyController);
        }
    }

    // 로열 비비에 부착되어 실체화된 요정 비비들과 능력 세션을 관리한다.
    // 활성 요정 목록의 단일 권위(source of truth)이며 상태 Gizmo를 출력한다.
    public class CompViviFairyController : ThingComp
    {
        public CompProperties_ViviFairyController Props => (CompProperties_ViviFairyController)props;

        private List<ViviFairy> _activeFairies = new List<ViviFairy>();
        private List<FairySession> _sessions = new List<FairySession>();
        private int _nextSessionId = 1;
        [Unsaved]
        private int _lastFairyDrivenTick = -1;

        public Pawn Royal => (Pawn)parent;
        public IReadOnlyList<ViviFairy> ActiveFairies => _activeFairies;

        public int MaterializedCount => _activeFairies.Count;

        public int FairyPoolCount
        {
            get
            {
                var holder = parent.GetComp<CompViviHolder>();
                return holder != null ? holder.FairyficatedPawnCount : 0;
            }
        }

        // 실체화 가능 횟수 = 요정화된 비비 수 - 현재 실체화된 수.
        public int RemainingMaterializeCharges => FairyPoolCount - MaterializedCount;

        // 새 명령에 사용 가능한 요정 수(대기 + 미배정).
        public int AvailableCount => _activeFairies.Count(f => f != null && !f.Destroyed && f.IsAvailable);

        public bool CanMaterialize => RemainingMaterializeCharges > 0;

        public ViviFairy MaterializeFairyAt(IntVec3 cell)
        {
            var pawn = (Pawn)parent;
            if (pawn.Map == null) { return null; }

            var fairy = (ViviFairy)ThingMaker.MakeThing(Props.fairyDef);
            fairy.Initialize(pawn, Props.fairyLifespanTicks);
            GenSpawn.Spawn(fairy, cell, pawn.Map);

            RegisterFairy(fairy);
            fairy.BeginMaterialize();
            return fairy;
        }

        public void RegisterFairy(ViviFairy fairy)
        {
            if (fairy != null && !_activeFairies.Contains(fairy))
            {
                _activeFairies.Add(fairy);
            }
        }

        public void NotifyFairyRestored(ViviFairy fairy)
        {
            RegisterFairy(fairy);
        }

        public void Notify_FairyGone(ViviFairy fairy)
        {
            _activeFairies.Remove(fairy);
        }

        // === 세션 API ===

        public int NextSessionId() => _nextSessionId++;
        public void AddSession(FairySession session) => _sessions.Add(session);
        public FairySession GetSession(int id) => _sessions.FirstOrDefault(s => s.id == id);
        public T GetActiveSession<T>() where T : FairySession => _sessions.OfType<T>().FirstOrDefault(s => !s.Ended);

        public void EndSession(int id)
        {
            GetSession(id)?.End();
        }

        // 사용 가능한 요정 n기를 한 번에 예약하고 role을 지정(이중 캐스트 레이스 방지).
        public bool TryReserveIdleFairies(int n, FairyRole role, out List<ViviFairy> reserved)
        {
            reserved = _activeFairies
                .Where(f => f != null && !f.Destroyed && f.IsAvailable)
                .OrderBy(f => f.thingIDNumber)
                .Take(n)
                .ToList();

            if (reserved.Count < n)
            {
                reserved = null;
                return false;
            }

            foreach (var f in reserved)
            {
                f.SetRole(role);
            }
            return true;
        }

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);

            PruneActiveFairies();
            AssignIdleOrbitSlots();
        }

        public void Notify_FairyTick(ViviFairy fairy, int delta)
        {
            if (fairy == null || fairy.Destroyed || !_activeFairies.Contains(fairy)) { return; }

            int tick = GenTicks.TicksGame;
            if (_lastFairyDrivenTick == tick) { return; }
            _lastFairyDrivenTick = tick;

            PruneActiveFairies();
            TickSessions(delta);
            AssignIdleOrbitSlots();
        }

        private void PruneActiveFairies()
        {
            for (int i = _activeFairies.Count - 1; i >= 0; i--)
            {
                var f = _activeFairies[i];
                if (f == null || f.Destroyed || !f.Spawned)
                {
                    _activeFairies.RemoveAt(i);
                }
            }
        }

        private void TickSessions(int delta)
        {
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                var s = _sessions[i];
                s.Tick(delta);
                if (s.Ended)
                {
                    _sessions.RemoveAt(i);
                }
            }
        }

        // 자유 대기(미배정) 요정에게 궤도 슬롯/개수만 배정한다. (실제 회전 이동은 각 요정이 매 틱 수행)
        // 컨트롤러는 카메라 거리에 따라 저빈도로 틱될 수 있어, 슬롯 배정만 맡고 부드러운 위치 갱신은 요정에게 위임한다.
        private void AssignIdleOrbitSlots()
        {
            var pawn = (Pawn)parent;
            if (!pawn.Spawned) { return; }

            var idle = _activeFairies
                .Where(f => f != null && !f.Destroyed && f.IsAvailable)
                .OrderBy(f => f.thingIDNumber)
                .ToList();
            int n = idle.Count;

            for (int i = 0; i < n; i++)
            {
                idle[i].SetOrbitSlot(i, n);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (MaterializedCount > 0)
            {
                yield return new Gizmo_ViviFairyStatus(this);
            }
        }

        // owner 사망/소멸/맵이탈 시 즉시 정리. (동화 헤디프 미적용)
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            EndAllSessions();
            DestroyAllFairiesImmediate();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (mode != DestroyMode.WillReplace)
            {
                EndAllSessions();
                DestroyAllFairiesImmediate();
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            EndAllSessions();
            DestroyAllFairiesImmediate();
        }

        // 영원꽃 연결 상실 시 호출. 부드럽게 소멸(이펙트 재생) 시키되 동화 헤디프는 남기지 않는다.
        public void DematerializeAll()
        {
            EndAllSessions();
            foreach (var f in _activeFairies.ToList())
            {
                if (f != null && !f.Destroyed)
                {
                    f.BeginDematerialize(false);
                }
            }
        }

        private void EndAllSessions()
        {
            foreach (var s in _sessions.ToList())
            {
                s.End();
            }
            _sessions.Clear();
        }

        private void DestroyAllFairiesImmediate()
        {
            foreach (var f in _activeFairies.ToList())
            {
                if (f != null && !f.Destroyed)
                {
                    f.Destroy();
                }
            }
            _activeFairies.Clear();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref _activeFairies, "activeFairies", LookMode.Reference);
            Scribe_Collections.Look(ref _sessions, "sessions", LookMode.Deep);
            Scribe_Values.Look(ref _nextSessionId, "nextSessionId", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_activeFairies == null) { _activeFairies = new List<ViviFairy>(); }
                _activeFairies.RemoveAll(f => f == null || f.Destroyed);

                if (_sessions == null) { _sessions = new List<FairySession>(); }
                _sessions.RemoveAll(s => s == null || s.Ended);
            }
        }
    }
}
