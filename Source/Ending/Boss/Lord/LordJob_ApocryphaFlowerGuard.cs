using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
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

        public void AssignFlower(Pawn pawn, Thing flower)
        {
            _guardedFlowers[pawn] = flower;
        }

        public Thing GetGuardedFlower(Pawn pawn)
        {
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
