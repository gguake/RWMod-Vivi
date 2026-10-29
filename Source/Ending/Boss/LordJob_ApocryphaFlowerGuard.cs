using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 보스전 동안 맵 외곽에서 증원되는 거대 말벌 로드(무소속). 정착지를 습격하는 대신 각 말벌이 가까운 멸망꽃·요정꽃 곁을 지키고,
    // 꽃이 하나도 없으면 아포크리파 곁으로 간다. 지키는 곳(꽃 또는 보스) 근처의 적과, 꽃·보스·같은 무리를 공격한 적만 노린다.
    // 보스가 사라지면(처치·찬탈·맵 이탈) 즉시 일반 말벌 습격 토일로 넘어간다.
    public class LordJob_ApocryphaFlowerGuard : LordJob
    {
        // 이미 지키는 말벌이 있는 꽃은 이 거리(칸)만큼 멀리 있는 것으로 쳐서, 같은 쪽에서 들어온 말벌이 한 송이에만 몰리지 않게 한다.
        private const float GuardCrowdPenalty = 6f;

        private Pawn _boss;
        private float _guardRadius = 6f;
        private float _retaliationRadius = 20f;
        private float _bossFollowRadius = 10f;

        // 말벌별로 지키는 꽃. 저장하지 않는다(불러오면 처음 판단할 때 다시 고른다).
        private readonly Dictionary<Pawn, Thing> _guardedFlowers = new Dictionary<Pawn, Thing>();

        public Pawn Boss => _boss;
        public float GuardRadius => _guardRadius;
        public float BossFollowRadius => _bossFollowRadius;

        // 대상 탐색 반경(지키는 곳 기준). 경비 반경 밖은 ValidateAttackTarget에서 반격 대상만 남긴다.
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

        // 경비 말벌이 모두 죽어도 보스가 살아 있으면 다음 증원이 합류할 수 있도록 로드를 유지한다.
        public override bool ShouldExistWithoutPawns => BossActive;

        public LordJob_ApocryphaFlowerGuard()
        {
        }

        public LordJob_ApocryphaFlowerGuard(Pawn boss, float guardRadius, float retaliationRadius, float bossFollowRadius)
        {
            _boss = boss;
            _guardRadius = guardRadius;
            _retaliationRadius = retaliationRadius;
            _bossFollowRadius = bossFollowRadius;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();

            var guard = new LordToil_ApocryphaFlowerGuard();
            graph.AddToil(guard);

            var assault = new LordToil_ApocryphaReleasedAssault();
            graph.AddToil(assault);

            var bossGone = new Transition(guard, assault);
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

            // 쓰러진 말벌도 보스가 살아 있는 동안은 무리에 남겨 둔다. 다시 일어나면 경비 듀티를 이어받는다(로드 밖에서 제한 없이 날뛰지 않게).
            if (reason == PawnLostCondition.Incapped && BossActive)
            {
                return false;
            }

            return true;
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            _guardedFlowers.Remove(p);
        }

        // 말벌이 지금 지키는 꽃(꽃이 하나도 없으면 null → 보스 곁).
        public Thing GetGuardedFlower(Pawn pawn)
        {
            RestoreFromDuty(pawn);
            return _guardedFlowers.TryGetValue(pawn, out var flower) && IsGuardableFlower(flower) ? flower : null;
        }

        // 지키던 꽃이 사라졌거나(파괴·만개·시듦) 보스 곁에 있다가 새 꽃이 피었으면 가까운 꽃을 다시 고른다. 바뀌었으면 true.
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

        // 지키는 꽃 목록은 저장하지 않으므로, 불러온 뒤에는 저장된 경비 듀티의 초점에서 되살린다(불러올 때마다 지키는 꽃이 바뀌지 않게).
        private void RestoreFromDuty(Pawn pawn)
        {
            if (_guardedFlowers.ContainsKey(pawn)) { return; }

            var duty = pawn.mindState?.duty;
            if (duty != null && duty.def == ApocryphaDutyDefOf.VV_TitanicHornetFlowerGuard && IsGuardableFlower(duty.focus.Thing))
            {
                _guardedFlowers[pawn] = duty.focus.Thing;
            }
        }

        // 대상 탐색 기준점: 지키는 꽃, 꽃이 없으면 보스.
        public IntVec3 GetGuardPoint(Pawn pawn)
        {
            var flower = GetGuardedFlower(pawn);
            if (flower != null) { return flower.Position; }

            return _boss != null && _boss.Spawned ? _boss.Position : pawn.Position;
        }

        private bool IsGuardableFlower(Thing flower)
        {
            return flower is Building_ApocryphaFlower && flower.Spawned && _boss != null && flower.Map == _boss.Map;
        }

        // 가까운 멸망꽃·요정꽃. 이미 다른 말벌이 지키는 꽃은 그 수만큼 멀리 있는 것으로 친다.
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
            if (target is Pawn targetPawn && ApocryphaUtility.IsApocrypha(targetPawn)) { return false; }
            if (target is Building_ApocryphaFlower) { return false; }

            if (BossActive && target.Map == _boss.Map)
            {
                // 경비 반경은 바닐라 locus 판정처럼 공격 사거리만큼 넉넉하게 본다.
                var guardRadius = _guardRadius + (searcher.CurrentEffectiveVerb?.EffectiveRange ?? 0f);
                if (!target.Position.InHorDistOf(GetGuardPoint(searcher), guardRadius) && !LordJob_ApocryphaEscort.RecentlyAttacked(target, IsProtected))
                {
                    return false;
                }
            }

            return base.ValidateAttackTarget(searcher, target);
        }

        // 공격당하면 반격하는 대상: 보스, 같은 무리의 말벌, 보스가 피운 꽃.
        private bool IsProtected(Thing victim)
        {
            if (victim is Building_ApocryphaFlower) { return true; }

            return victim is Pawn pawn && (pawn == _boss || (lord != null && pawn.GetLord() == lord));
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
            Scribe_Values.Look(ref _guardRadius, "guardRadius", 6f);
            Scribe_Values.Look(ref _retaliationRadius, "retaliationRadius", 20f);
            Scribe_Values.Look(ref _bossFollowRadius, "bossFollowRadius", 10f);
        }
    }
}
