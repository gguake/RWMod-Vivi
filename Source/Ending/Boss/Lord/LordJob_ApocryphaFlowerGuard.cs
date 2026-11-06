using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 멸망꽃에서 빚어진 말벌은 자기를 낳은 꽃만 지킨다. 꽃이 사라지면 LordToil_ApocryphaFlowerGuard가 정착지 습격으로 돌린다.
    public class LordJob_ApocryphaFlowerGuard : LordJob_ApocryphaHornetGuard
    {
        private readonly Dictionary<Pawn, Thing> _guardedFlowers = new Dictionary<Pawn, Thing>();

        protected override float DefaultGuardRadius => 6f;
        protected override float DefaultRetaliationRadius => 20f;

        public LordJob_ApocryphaFlowerGuard()
        {
        }

        public LordJob_ApocryphaFlowerGuard(Pawn boss, float guardRadius, float retaliationRadius)
            : base(boss, guardRadius, retaliationRadius)
        {
        }

        protected override LordToil MakeGuardToil()
        {
            return new LordToil_ApocryphaFlowerGuard();
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            _guardedFlowers.Remove(p);
        }

        // 로드에 넣기 전에 정해야 합류 때 듀티가 그 꽃으로 잡힌다.
        public void AssignFlower(Pawn pawn, Thing flower)
        {
            _guardedFlowers[pawn] = flower;
        }

        public Thing GetGuardedFlower(Pawn pawn)
        {
            // 저장되지 않는 꽃 목록은 불러온 뒤 경비 듀티의 대상에서 복원한다.
            if (!_guardedFlowers.TryGetValue(pawn, out var flower))
            {
                var duty = pawn.mindState?.duty;
                if (duty != null && duty.def == VVDutyDefOf.VV_TitanicHornetFlowerGuard && duty.focus.Thing != null)
                {
                    flower = duty.focus.Thing;
                    _guardedFlowers[pawn] = flower;
                }
            }

            return flower is Building_ApocryphaFlower && flower.Spawned && _boss != null && flower.Map == _boss.Map
                ? flower : null;
        }

        public override IntVec3 GetGuardPoint(Pawn pawn)
        {
            var flower = GetGuardedFlower(pawn);
            return flower != null ? flower.Position : pawn.Position;
        }

        public override bool ValidateAttackTarget(Pawn searcher, Thing target)
        {
            if (target is Building_ApocryphaFlower) { return false; }

            return base.ValidateAttackTarget(searcher, target);
        }

        protected override bool IsProtected(Thing victim)
        {
            if (victim is Building_ApocryphaFlower) { return true; }

            return victim is Pawn pawn && (pawn == _boss || (lord != null && pawn.GetLord() == lord));
        }
    }
}
