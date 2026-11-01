using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordJob_ApocryphaFlowerGuard : LordJob_ApocryphaHornetGuard
    {
        // 말벌이 한 꽃에 몰리지 않도록 경비 중인 말벌마다 이 거리(칸)를 탐색 거리에 더한다.
        private const float GuardCrowdPenalty = 6f;

        private float _bossFollowRadius = 10f;

        private readonly Dictionary<Pawn, Thing> _guardedFlowers = new Dictionary<Pawn, Thing>();

        protected override float DefaultGuardRadius => 6f;
        protected override float DefaultRetaliationRadius => 20f;

        public float BossFollowRadius => _bossFollowRadius;

        public LordJob_ApocryphaFlowerGuard()
        {
        }

        public LordJob_ApocryphaFlowerGuard(Pawn boss, float guardRadius, float retaliationRadius, float bossFollowRadius)
            : base(boss, guardRadius, retaliationRadius)
        {
            _bossFollowRadius = bossFollowRadius;
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

        public Thing GetGuardedFlower(Pawn pawn)
        {
            RestoreFromDuty(pawn);
            return _guardedFlowers.TryGetValue(pawn, out var flower) && IsGuardableFlower(flower) ? flower : null;
        }

        public bool RefreshGuardedFlower(Pawn pawn)
        {
            RestoreFromDuty(pawn);
            _guardedFlowers.TryGetValue(pawn, out var previous);
            if (previous != null && IsGuardableFlower(previous)) { return false; }

            var flower = ChooseFlower(pawn);
            if (flower == null)
            {
                _guardedFlowers.Remove(pawn);
            }
            else
            {
                _guardedFlowers[pawn] = flower;
            }

            return flower != previous;
        }

        // 불러올 때마다 경비 대상이 바뀌지 않도록 저장되지 않는 꽃 목록을 경비 듀티의 대상에서 복원한다.
        private void RestoreFromDuty(Pawn pawn)
        {
            if (_guardedFlowers.ContainsKey(pawn)) { return; }

            var duty = pawn.mindState?.duty;
            if (duty != null && duty.def == ApocryphaDutyDefOf.VV_TitanicHornetFlowerGuard && IsGuardableFlower(duty.focus.Thing))
            {
                _guardedFlowers[pawn] = duty.focus.Thing;
            }
        }

        public override IntVec3 GetGuardPoint(Pawn pawn)
        {
            var flower = GetGuardedFlower(pawn);
            if (flower != null) { return flower.Position; }

            return _boss != null && _boss.Spawned ? _boss.Position : pawn.Position;
        }

        private bool IsGuardableFlower(Thing flower)
        {
            return flower is Building_ApocryphaFlower && flower.Spawned && _boss != null && flower.Map == _boss.Map;
        }

        private Thing ChooseFlower(Pawn pawn)
        {
            if (!BossActive || pawn.Map != _boss.Map) { return null; }

            var map = _boss.Map;
            Thing best = null;
            var bestCost = float.MaxValue;
            foreach (var def in new[] { ApocryphaGimmickDefOf.VV_DoomFlower, ApocryphaGimmickDefOf.VV_FairyFlower })
            {
                var flowers = map.listerThings.ThingsOfDef(def);
                for (int i = 0; i < flowers.Count; i++)
                {
                    var flower = flowers[i];
                    if (!IsGuardableFlower(flower)) { continue; }

                    var cost = pawn.Position.DistanceTo(flower.Position) + GuardCrowdPenalty * CountGuards(flower, pawn);
                    if (cost >= bestCost) { continue; }
                    if (!pawn.CanReach(flower, PathEndMode.Touch, Danger.Deadly)) { continue; }

                    best = flower;
                    bestCost = cost;
                }
            }

            return best;
        }

        private int CountGuards(Thing flower, Pawn except)
        {
            var count = 0;
            foreach (var pair in _guardedFlowers)
            {
                if (pair.Value == flower && pair.Key != except) { count++; }
            }

            return count;
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _bossFollowRadius, "bossFollowRadius", 10f);
        }
    }
}
