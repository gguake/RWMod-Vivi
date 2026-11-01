using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordJob_ApocryphaEscort : LordJob_ApocryphaHornetGuard
    {
        private Thing _focusTarget;
        private int _focusUntilTick = -1;

        protected override float DefaultGuardRadius => 10f;
        protected override float DefaultRetaliationRadius => 25f;

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

        public override IntVec3 GetGuardPoint(Pawn pawn)
        {
            return _boss != null && _boss.Spawned ? _boss.Position : pawn.Position;
        }

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
