using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 요정 능력 세션의 공통 기반. 컨트롤러가 소유하고 매 틱 Tick(delta)을 호출한다.
    // 요정 목록은 Scribe_References로 안전하게 복원되며, 종료 시 요정 role을 None으로 되돌린다.
    public abstract class FairySession : IExposable
    {
        public int id;
        protected Pawn owner;
        protected List<ViviFairy> fairies = new List<ViviFairy>();
        protected bool _ended;

        public bool Ended => _ended;
        public abstract FairyRole Role { get; }

        protected FairySession() { }

        protected FairySession(int id, Pawn owner, List<ViviFairy> fairies)
        {
            this.id = id;
            this.owner = owner;
            this.fairies = fairies;
        }

        protected Map Map => owner != null ? owner.Map : null;

        public virtual bool IsValid()
        {
            if (_ended) { return false; }
            if (owner == null || !owner.Spawned || owner.Dead) { return false; }

            fairies.RemoveAll(f => f == null || f.Destroyed || !f.Spawned);
            return fairies.Count > 0;
        }

        public void Tick(int delta)
        {
            if (!IsValid())
            {
                End();
                return;
            }
            TickActive(delta);
        }

        protected abstract void TickActive(int delta);

        public virtual void End()
        {
            if (_ended) { return; }
            _ended = true;

            foreach (var f in fairies)
            {
                if (f != null && !f.Destroyed)
                {
                    f.SetRole(FairyRole.None);
                }
            }

            OnEnded();
        }

        protected virtual void OnEnded() { }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref owner, "owner");
            Scribe_Collections.Look(ref fairies, "fairies", LookMode.Reference);
            Scribe_Values.Look(ref _ended, "ended");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (fairies == null) { fairies = new List<ViviFairy>(); }
                fairies.RemoveAll(f => f == null);
            }
        }
    }

    // 요정 - 경호: 요정 1기가 아군을 따라다니며 주변 5x5 적대 대상을 공격.
    public class GuardSession : FairySession
    {
        private const float GuardScanRadius = 2.9f;   // 5x5

        private Pawn ally;

        public override FairyRole Role => FairyRole.Guard;

        public GuardSession() { }
        public GuardSession(int id, Pawn owner, List<ViviFairy> fairies, Pawn ally) : base(id, owner, fairies)
        {
            this.ally = ally;
        }

        public override bool IsValid()
        {
            if (ally == null || !ally.Spawned || ally.Dead) { return false; }
            return base.IsValid();
        }

        protected override void TickActive(int delta)
        {
            var fairy = fairies[0];
            if (fairy == null || fairy.Destroyed) { End(); return; }

            // 수명 만료 → 보호 취소 + 요정 즉시 소멸 (다른 능력과 다른 특례)
            if (fairy.LifespanExpired)
            {
                fairy.BeginDematerialize(true);
                End();
                return;
            }

            Vector3 restPos = fairy.OrbitPositionAround(ally, 0, 1);

            if (fairy.State == FairyState.Idle)
            {
                var target = ViviFairyTargeting.FindHostileNear(
                    owner as IAttackTargetSearcher, ally.Position, GuardScanRadius, ally.Position, excludeDowned: true);

                if (target != null)
                {
                    fairy.StartDashAttack(target, restPos, ViviFairy.DefaultDashSpeed);
                }
                else
                {
                    fairy.IdleOrbitAround(ally, 0, 1);
                }
            }
        }

        protected override void OnEnded()
        {
            if (ally != null && !ally.Dead && ally.health != null)
            {
                var hediff = ally.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyGuarded);
                if (hediff != null) { ally.health.RemoveHediff(hediff); }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ally, "ally");
        }
    }

    // 요정 - 집중: 요정 3기가 적 주변으로 포위 이동 후 집중 공격. 최대 2000틱.
    public class ConcentrationSession : FairySession
    {
        private const int MaxDurationTicks = 2000;
        private const int SpreadWaitTicks = 35;
        private const float ConcRadius = 1.6f;

        private Thing target;
        private int phase;       // 0=포위 이동, 1=공격
        private int phaseTimer;
        private int elapsed;

        public override FairyRole Role => FairyRole.Concentration;

        public ConcentrationSession() { }
        public ConcentrationSession(int id, Pawn owner, List<ViviFairy> fairies, Thing target) : base(id, owner, fairies)
        {
            this.target = target;
        }

        protected override void TickActive(int delta)
        {
            elapsed += delta;

            if (target == null || !target.Spawned || target.Map != Map || (target is Pawn tp && tp.Dead))
            {
                End();
                return;
            }
            if (elapsed >= MaxDurationTicks)
            {
                End();
                return;
            }

            Vector3 center = target.TrueCenter().Yto0();

            if (phase == 0)
            {
                var map = Map;
                for (int i = 0; i < fairies.Count; i++)
                {
                    var f = fairies[i];
                    if (f == null || f.Destroyed) { continue; }

                    var cell = SlotPos(center, i).ToIntVec3();
                    if (map == null || !cell.InBounds(map)) { cell = target.Position; }
                    f.TeleportTo(cell);
                }

                phase = 1;
                phaseTimer = SpreadWaitTicks;
                return;
            }

            phaseTimer -= delta;
            if (phaseTimer > 0) { return; }

            for (int i = 0; i < fairies.Count; i++)
            {
                var f = fairies[i];
                if (f == null || f.Destroyed) { continue; }

                if (f.State == FairyState.Idle)
                {
                    f.StartDashAttack(target, SlotPos(center, i), ViviFairy.DefaultDashSpeed);
                }
            }
        }

        private static Vector3 SlotPos(Vector3 center, int i)
        {
            float ang = (90f + 120f * i) * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ConcRadius;
        }

        protected override void OnEnded()
        {
            if (target is Pawn p && !p.Dead && p.health != null)
            {
                var hediff = p.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyConcentrated);
                if (hediff != null) { p.health.RemoveHediff(hediff); }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref phaseTimer, "phaseTimer");
            Scribe_Values.Look(ref elapsed, "elapsed");
        }
    }

    // 요정 - 전개: 요정 6기가 로열 비비 주위를 6각 궤도로 맴돌며 시야 내 적을 추적 공격. 최대 10000틱, 토글.
    public class ExpansionSession : FairySession
    {
        private const int MaxDurationTicks = 10000;
        private const float HexRadius = 2.2f;
        private const float OrbitSpeed = 0.01f;     // rad/tick
        private const float ExpansionRadius = 14f;

        private int elapsed;
        private float orbitPhase;

        public override FairyRole Role => FairyRole.Expansion;

        public ExpansionSession() { }
        public ExpansionSession(int id, Pawn owner, List<ViviFairy> fairies) : base(id, owner, fairies) { }

        protected override void TickActive(int delta)
        {
            elapsed += delta;
            if (elapsed >= MaxDurationTicks)
            {
                End();
                return;
            }

            orbitPhase += OrbitSpeed * delta;
            Vector3 center = owner.DrawPos.Yto0();
            var searcher = owner as IAttackTargetSearcher;

            for (int i = 0; i < fairies.Count; i++)
            {
                var f = fairies[i];
                if (f == null || f.Destroyed) { continue; }

                float ang = (Mathf.PI / 3f) * i + orbitPhase;
                Vector3 slot = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * HexRadius;

                if (f.State == FairyState.Idle)
                {
                    var target = ViviFairyTargeting.FindHostileNear(
                        searcher, owner.Position, ExpansionRadius, owner.Position, excludeDowned: false);

                    if (target != null)
                    {
                        f.StartDashAttack(target, slot, ViviFairy.DefaultDashSpeed);
                    }
                    else
                    {
                        f.IdleFollowStep(slot, delta);
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref elapsed, "elapsed");
            Scribe_Values.Look(ref orbitPhase, "orbitPhase");
        }
    }
}
