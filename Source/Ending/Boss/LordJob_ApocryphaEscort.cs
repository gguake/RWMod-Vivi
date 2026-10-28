using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 아포크리파를 경호하는 거대 말벌 로드(무소속). 보스를 공격하지 않고 보스 반경 안의 적과, 보스·호위를 공격한 적만 노린다.
    // 보스가 사라지면(처치·찬탈·맵 이탈) 즉시 일반 말벌 습격 토일로 넘어간다.
    public class LordJob_ApocryphaEscort : LordJob
    {
        // 공격자의 마지막 공격이 이 시간 안이면 반격 대상으로 본다(원거리 연사 간격보다 넉넉하게).
        private const int RetaliationMemoryTicks = 600;

        private Pawn _boss;
        private float _guardRadius = 10f;
        private float _retaliationRadius = 25f;

        public Pawn Boss => _boss;
        public float GuardRadius => _guardRadius;

        // 대상 탐색 반경(보스 기준). 경호 반경 밖은 ValidateAttackTarget에서 반격 대상만 남긴다.
        public float SearchRadius => Mathf.Max(_guardRadius, _retaliationRadius);

        public bool BossActive
        {
            get
            {
                if (_boss == null || _boss.Destroyed || _boss.Dead || !_boss.Spawned) { return false; }
                if (lord != null && _boss.Map != lord.Map) { return false; }

                var comp = _boss.TryGetComp<CompApocrypha>();
                return comp == null || comp.Phase != ApocryphaPhase.Dying;
            }
        }

        public override bool AddFleeToil => false;

        // 호위가 모두 죽어도 보스가 살아 있으면 증원이 합류할 수 있도록 로드를 유지한다.
        public override bool ShouldExistWithoutPawns => BossActive;

        public LordJob_ApocryphaEscort()
        {
        }

        public LordJob_ApocryphaEscort(Pawn boss, float guardRadius, float retaliationRadius)
        {
            _boss = boss;
            _guardRadius = guardRadius;
            _retaliationRadius = retaliationRadius;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();

            var escort = new LordToil_ApocryphaEscort();
            graph.AddToil(escort);

            var assault = new LordToil_HornetAssault();
            graph.AddToil(assault);

            var bossGone = new Transition(escort, assault);
            bossGone.AddTrigger(new Trigger_Custom(signal => signal.type == TriggerSignalType.Tick && !BossActive));
            graph.AddTransition(bossGone);

            return graph;
        }

        public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason)
        {
            if (reason == PawnLostCondition.InMentalState && p.MentalStateDef == VVMentalStateDefOf.VV_HornetBerserk)
            {
                return false;
            }

            return true;
        }

        public override bool ValidateAttackTarget(Pawn searcher, Thing target)
        {
            if (target is Pawn targetPawn && ApocryphaUtility.IsApocrypha(targetPawn))
            {
                return false;
            }

            if (BossActive && target.Map == _boss.Map)
            {
                // 경호 반경은 바닐라 locus 판정처럼 공격 사거리만큼 넉넉하게 본다.
                var guardRadius = _guardRadius + (searcher.CurrentEffectiveVerb?.EffectiveRange ?? 0f);
                if (!target.Position.InHorDistOf(_boss.Position, guardRadius) && !RecentlyAttackedEscort(target))
                {
                    return false;
                }
            }

            return base.ValidateAttackTarget(searcher, target);
        }

        private bool RecentlyAttackedEscort(Thing attacker)
        {
            LocalTargetInfo attacked;
            int attackTick;
            if (attacker is Pawn attackerPawn && attackerPawn.mindState != null)
            {
                attacked = attackerPawn.mindState.lastAttackedTarget;
                attackTick = attackerPawn.mindState.lastAttackTargetTick;
            }
            else if (attacker is Building_Turret turret)
            {
                attacked = turret.LastAttackedTarget;
                attackTick = turret.LastAttackTargetTick;
            }
            else
            {
                return false;
            }

            if (Find.TickManager.TicksGame - attackTick > RetaliationMemoryTicks) { return false; }

            return attacked.Thing is Pawn victim && (victim == _boss || (lord != null && victim.GetLord() == lord));
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // 파괴된 보스(월드 폰으로 넘어가 정리될 수 있음)는 참조로 저장하지 않는다.
            if (Scribe.mode == LoadSaveMode.Saving && _boss != null && _boss.Destroyed)
            {
                _boss = null;
            }

            Scribe_References.Look(ref _boss, "boss");
            Scribe_Values.Look(ref _guardRadius, "guardRadius", 10f);
            Scribe_Values.Look(ref _retaliationRadius, "retaliationRadius", 25f);
        }
    }
}
