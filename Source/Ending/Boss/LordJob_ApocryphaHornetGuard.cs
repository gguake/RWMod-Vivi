using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 아포크리파 곁을 지키는 거대 말벌 로드(호위·꽃 경비)의 공통 부분(무소속).
    // 보스를 공격하지 않고, 지키는 곳(보스 또는 꽃) 근처의 적과 지키는 대상을 최근 공격한 적만 노린다.
    // 보스가 사라지면(처치·찬탈·맵 이탈) 즉시 일반 말벌 습격 토일로 넘어간다.
    public abstract class LordJob_ApocryphaHornetGuard : LordJob
    {
        // 공격자의 마지막 공격이 이 시간 안이면 반격 대상으로 본다(원거리 연사 간격보다 넉넉하게).
        public const int RetaliationMemoryTicks = 600;

        protected Pawn _boss;
        protected float _guardRadius;
        protected float _retaliationRadius;

        public Pawn Boss => _boss;
        public float GuardRadius => _guardRadius;

        // 대상 탐색 반경(지키는 곳 기준). 경호·경비 반경 밖은 ValidateAttackTarget에서 반격 대상만 남긴다.
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

        // 말벌이 모두 죽어도 보스가 살아 있으면 증원이 합류할 수 있도록 로드를 유지한다.
        public override bool ShouldExistWithoutPawns => BossActive;

        // 반경의 저장 기본값(로드마다 다르다). 기본값과 같은 값은 저장되지 않으므로 바꾸면 불러온 반경이 달라진다.
        protected abstract float DefaultGuardRadius { get; }
        protected abstract float DefaultRetaliationRadius { get; }

        protected LordJob_ApocryphaHornetGuard()
        {
        }

        protected LordJob_ApocryphaHornetGuard(Pawn boss, float guardRadius, float retaliationRadius)
        {
            _boss = boss;
            _guardRadius = guardRadius;
            _retaliationRadius = retaliationRadius;
        }

        // 보스가 있는 동안 말벌에게 듀티를 주는 토일.
        protected abstract LordToil MakeGuardToil();

        // 대상 탐색·경호 반경의 기준점(보스 또는 지키는 꽃).
        public abstract IntVec3 GetGuardPoint(Pawn pawn);

        // 공격당하면 반격하는 대상.
        protected abstract bool IsProtected(Thing victim);

        // 경호 반경과 관계없이 노리는 대상인지(보스가 감지해 맡긴 적 등).
        protected virtual bool IgnoresGuardRadius(Thing target)
        {
            return false;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();

            var guard = MakeGuardToil();
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

            // 쓰러진 말벌도 보스가 살아 있는 동안은 무리에 남겨 둔다. 다시 일어나면 듀티를 이어받는다(로드 밖에서 제한 없이 날뛰지 않게).
            // 무리에 남아 있어야 먹이 공급과 찬탈·엔딩 정리(보스와 함께 소실)에도 포함된다.
            if (reason == PawnLostCondition.Incapped && BossActive)
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

            if (BossActive && target.Map == _boss.Map && !IgnoresGuardRadius(target))
            {
                // 경호 반경은 바닐라 locus 판정처럼 공격 사거리만큼 넉넉하게 본다.
                var guardRadius = _guardRadius + (searcher.CurrentEffectiveVerb?.EffectiveRange ?? 0f);
                if (!target.Position.InHorDistOf(GetGuardPoint(searcher), guardRadius) && !RecentlyAttacked(target, IsProtected))
                {
                    return false;
                }
            }

            return base.ValidateAttackTarget(searcher, target);
        }

        // 공격자(폰·포탑·비비 포탑 식물 등 공격 대상을 고르는 것)가 최근 RetaliationMemoryTicks 안에 isProtected에 해당하는 대상을 공격했는지.
        public static bool RecentlyAttacked(Thing attacker, System.Predicate<Thing> isProtected)
        {
            if (!(attacker is IAttackTargetSearcher searcher)) { return false; }

            // 폰의 공격 기록은 mindState에 있다.
            if (attacker is Pawn attackerPawn && attackerPawn.mindState == null) { return false; }

            if (Find.TickManager.TicksGame - searcher.LastAttackTargetTick > RetaliationMemoryTicks) { return false; }

            var attacked = searcher.LastAttackedTarget.Thing;
            return attacked != null && isProtected(attacked);
        }

        // 말벌의 대상 탐색 범위(지키는 곳에서 탐색 반경 + 공격 사거리) 안에 노릴 만한 폰·건물이 하나라도 있는지 가볍게 훑는다.
        // JobGiver_HornetBerserk의 대상 탐색(영역 BFS)보다 넓게 거르므로, 여기서 없으면 그쪽도 찾지 못한다.
        public bool AnyPotentialTargetNear(Pawn searcher)
        {
            // 탐색 범위가 제한되지 않는 경우(보스가 없거나 다른 맵)는 거르지 않는다.
            if (!BossActive || searcher.Map != _boss.Map) { return true; }

            var center = GetGuardPoint(searcher);
            var radius = SearchRadius + (searcher.CurrentEffectiveVerb?.EffectiveRange ?? 0f);
            var radiusSquared = radius * radius;
            var targets = searcher.Map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
            for (int i = 0; i < targets.Count; i++)
            {
                var thing = targets[i];
                if ((thing.Position - center).LengthHorizontalSquared > radiusSquared) { continue; }

                if (thing is Pawn pawn)
                {
                    // 같은 말벌·보스·쓰러진 폰은 노리지 않는다(MentalState_HornetBerserk·JobGiver_HornetBerserk와 같은 기준).
                    if (pawn.Dead || pawn.Downed || pawn.kindDef == searcher.kindDef || ApocryphaUtility.IsApocrypha(pawn)) { continue; }

                    return true;
                }

                // 보스가 피운 꽃은 말벌의 적이 아니다.
                if (thing is Building && !(thing is Building_ApocryphaFlower)) { return true; }
            }

            return false;
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
            Scribe_Values.Look(ref _guardRadius, "guardRadius", DefaultGuardRadius);
            Scribe_Values.Look(ref _retaliationRadius, "retaliationRadius", DefaultRetaliationRadius);
        }
    }
}
