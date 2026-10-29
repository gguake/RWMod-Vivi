using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 아포크리파를 경호하는 거대 말벌 로드(무소속). 보스를 공격하지 않고 보스 반경 안의 적과, 보스·호위를 공격한 적만 노린다.
    // 1페이즈 보스가 적을 감지하면(SetFocus) 그동안은 경호 반경과 관계없이 그 적을 먼저 공격한다.
    // 보스가 사라지면(처치·찬탈·맵 이탈) 즉시 일반 말벌 습격 토일로 넘어간다.
    public class LordJob_ApocryphaEscort : LordJob
    {
        // 공격자의 마지막 공격이 이 시간 안이면 반격 대상으로 본다(원거리 연사 간격보다 넉넉하게).
        public const int RetaliationMemoryTicks = 600;

        private Pawn _boss;
        private float _guardRadius = 10f;
        private float _retaliationRadius = 25f;
        private Thing _focusTarget;
        private int _focusUntilTick = -1;

        public Pawn Boss => _boss;
        public float GuardRadius => _guardRadius;

        // 보스가 감지해 호위 말벌에게 맡긴 공격 대상(유효 시간이 지났거나 쓰러졌거나 사라졌으면 null).
        public Thing FocusTarget
        {
            get
            {
                var target = _focusTarget;
                if (target == null || !BossActive) { return null; }
                if (Find.TickManager.TicksGame > _focusUntilTick) { return null; }
                if (!target.Spawned || target.Map != _boss.Map) { return null; }
                if (target is Pawn pawn && (pawn.Dead || pawn.Downed)) { return null; }

                return target;
            }
        }

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

            var assault = new LordToil_ApocryphaReleasedAssault();
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

        // [CompApocrypha 호출] 1페이즈 보스가 적을 감지했을 때. 대상이 바뀌면 교전 중인 호위 말벌도 곧바로 새 대상으로 돌린다.
        public void SetFocus(Thing target, int untilTick)
        {
            var changed = target != FocusTarget;
            _focusTarget = target;
            _focusUntilTick = untilTick;

            if (changed && lord != null)
            {
                for (int i = lord.ownedPawns.Count - 1; i >= 0; i--)
                {
                    LordToil_ApocryphaEscort.TryEngageFocus(lord.ownedPawns[i], target);
                }
            }
        }

        public void ClearFocus()
        {
            _focusTarget = null;
            _focusUntilTick = -1;
        }

        public override bool ValidateAttackTarget(Pawn searcher, Thing target)
        {
            if (target is Pawn targetPawn && ApocryphaUtility.IsApocrypha(targetPawn))
            {
                return false;
            }

            if (BossActive && target.Map == _boss.Map && target != FocusTarget)
            {
                // 경호 반경은 바닐라 locus 판정처럼 공격 사거리만큼 넉넉하게 본다.
                var guardRadius = _guardRadius + (searcher.CurrentEffectiveVerb?.EffectiveRange ?? 0f);
                if (!target.Position.InHorDistOf(_boss.Position, guardRadius) && !RecentlyAttacked(target, IsEscortOrBoss))
                {
                    return false;
                }
            }

            return base.ValidateAttackTarget(searcher, target);
        }

        private bool IsEscortOrBoss(Thing victim)
        {
            return victim is Pawn pawn && (pawn == _boss || (lord != null && pawn.GetLord() == lord));
        }

        // 공격자(폰·포탑)가 최근 RetaliationMemoryTicks 안에 isProtected에 해당하는 대상을 공격했는지.
        public static bool RecentlyAttacked(Thing attacker, System.Predicate<Thing> isProtected)
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

            return attacked.Thing != null && isProtected(attacked.Thing);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // 파괴된 보스(월드 폰으로 넘어가 정리될 수 있음)는 참조로 저장하지 않는다.
            if (Scribe.mode == LoadSaveMode.Saving && _boss != null && _boss.Destroyed)
            {
                _boss = null;
            }

            if (Scribe.mode == LoadSaveMode.Saving && _focusTarget != null && _focusTarget.Destroyed)
            {
                _focusTarget = null;
            }

            Scribe_References.Look(ref _boss, "boss");
            Scribe_Values.Look(ref _guardRadius, "guardRadius", 10f);
            Scribe_Values.Look(ref _retaliationRadius, "retaliationRadius", 25f);
            Scribe_References.Look(ref _focusTarget, "focusTarget");
            Scribe_Values.Look(ref _focusUntilTick, "focusUntilTick", -1);
        }
    }
}
