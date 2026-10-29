using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 아포크리파를 경호하는 거대 말벌 로드(무소속). 보스를 공격하지 않고 보스 반경 안의 적과, 보스·호위를 공격한 적만 노린다.
    // 1페이즈 보스가 적을 감지하면(SetFocus) 그동안은 경호 반경과 관계없이 그 적을 먼저 공격한다.
    // 보스가 사라지면(처치·찬탈·맵 이탈) 즉시 일반 말벌 습격 토일로 넘어간다.
    public class LordJob_ApocryphaEscort : LordJob_ApocryphaHornetGuard
    {
        private Thing _focusTarget;
        private int _focusUntilTick = -1;

        protected override float DefaultGuardRadius => 10f;
        protected override float DefaultRetaliationRadius => 25f;

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

        public LordJob_ApocryphaEscort()
        {
        }

        public LordJob_ApocryphaEscort(Pawn boss, float guardRadius, float retaliationRadius)
            : base(boss, guardRadius, retaliationRadius)
        {
        }

        protected override LordToil MakeGuardToil()
        {
            return new LordToil_ApocryphaEscort();
        }

        // 호위 말벌은 보스 곁을 지킨다.
        public override IntVec3 GetGuardPoint(Pawn pawn)
        {
            return _boss != null && _boss.Spawned ? _boss.Position : pawn.Position;
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

        protected override bool IgnoresGuardRadius(Thing target)
        {
            return target == FocusTarget;
        }

        // 공격당하면 반격하는 대상: 보스와 같은 무리의 호위 말벌.
        protected override bool IsProtected(Thing victim)
        {
            return victim is Pawn pawn && (pawn == _boss || (lord != null && pawn.GetLord() == lord));
        }

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving && _focusTarget != null && _focusTarget.Destroyed)
            {
                _focusTarget = null;
            }

            Scribe_References.Look(ref _focusTarget, "focusTarget");
            Scribe_Values.Look(ref _focusUntilTick, "focusUntilTick", -1);
        }
    }
}
