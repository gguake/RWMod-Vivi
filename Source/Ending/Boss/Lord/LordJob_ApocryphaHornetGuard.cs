using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public abstract class LordJob_ApocryphaHornetGuard : LordJob
    {
        public const int RetaliationMemoryTicks = 600;

        protected Pawn _boss;
        protected float _guardRadius;
        protected float _retaliationRadius;

        public Pawn Boss => _boss;
        public float GuardRadius => _guardRadius;

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

        public override bool ShouldExistWithoutPawns => BossActive;

        // 로드별 저장 기본 반경. 기본값은 세이브에 기록되지 않으므로 변경하면 기존 세이브의 반경도 달라진다.
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

        protected abstract LordToil MakeGuardToil();

        public abstract IntVec3 GetGuardPoint(Pawn pawn);

        protected abstract bool IsProtected(Thing victim);

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
                var guardRadius = _guardRadius + (searcher.CurrentEffectiveVerb?.EffectiveRange ?? 0f);
                if (!target.Position.InHorDistOf(GetGuardPoint(searcher), guardRadius) && !RecentlyAttacked(target, IsProtected))
                {
                    return false;
                }
            }

            return base.ValidateAttackTarget(searcher, target);
        }

        public static bool RecentlyAttacked(Thing attacker, System.Predicate<Thing> isProtected)
        {
            if (!(attacker is IAttackTargetSearcher searcher)) { return false; }

            if (attacker is Pawn attackerPawn && attackerPawn.mindState == null) { return false; }

            if (Find.TickManager.TicksGame - searcher.LastAttackTargetTick > RetaliationMemoryTicks) { return false; }

            var attacked = searcher.LastAttackedTarget.Thing;
            return attacked != null && isProtected(attacked);
        }

        public bool AnyPotentialTargetNear(Pawn searcher)
        {
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
                    if (pawn.Dead || pawn.Downed || pawn.kindDef == searcher.kindDef || ApocryphaUtility.IsApocrypha(pawn)) { continue; }

                    return true;
                }

                if (thing is Building && !(thing is Building_ApocryphaFlower)) { return true; }
            }

            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();

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
