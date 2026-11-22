using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ApocryphaBlastBreach : IExposable
    {
        private const int DeadlineTicks = 5000;
        private const float StandRadius = 2f;
        private const float ScreenShake = 0.8f;

        private readonly CompApocrypha _comp;

        private Thing _target;
        private float _maxDistance;
        private IntVec3 _standCell = IntVec3.Invalid;
        private IntVec3 _blockedCell = IntVec3.Invalid;
        private int _deadlineTick = -1;
        private FairyBlastAim _aim;
        private IntVec3 _pathBlockedCell = IntVec3.Invalid;
        private bool _aimAtPathBlock;

        private Effecter _castEffecter;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public bool InProgress => _blockedCell.IsValid;
        public IntVec3 BlockedCell => _blockedCell;
        public FairyBlastAim Aim => _aim;
        public bool ClearingPathBlock => _pathBlockedCell.IsValid || _aimAtPathBlock;
        public int PathBlockTicksLeft => _aimAtPathBlock ? _aim.detonateTick - GenTicks.TicksGame : FairyBlastAim.AimTicks;

        public ApocryphaBlastBreach(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref _target, "target");
            Scribe_Values.Look(ref _maxDistance, "maxDistance", 0f);
            Scribe_Values.Look(ref _standCell, "standCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _blockedCell, "blockedCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _deadlineTick, "deadlineTick", -1);
            Scribe_Deep.Look(ref _aim, "aim");
            Scribe_Values.Look(ref _pathBlockedCell, "pathBlockedCell", IntVec3.Invalid);
            Scribe_Values.Look(ref _aimAtPathBlock, "aimAtPathBlock", false);
        }

        public void NotePathBlocked(IntVec3 cell)
        {
            if (!ClearingPathBlock)
            {
                _pathBlockedCell = cell;
            }
        }

        public bool TryStart(Thing target, float maxDistance, out IntVec3 standCell)
        {
            standCell = IntVec3.Invalid;
            if (ClearingPathBlock) { return false; }

            Cancel();
            if (_comp.Phase != ApocryphaPhase.Asphodel || target == null || !target.Spawned || target.Map != Pawn.Map) { return false; }
            if (Pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)) { return false; }
            if (!TryFindBlockedCell(target, maxDistance, out standCell, out var blocked)) { return false; }

            _target = target;
            _maxDistance = maxDistance;
            _standCell = standCell;
            _blockedCell = blocked;
            _deadlineTick = GenTicks.TicksGame + DeadlineTicks;
            return true;
        }

        public bool TryContinue(out IntVec3 standCell)
        {
            standCell = _standCell;
            if (!InProgress) { return false; }

            if (_comp.Phase != ApocryphaPhase.Asphodel || GenTicks.TicksGame > _deadlineTick || _target == null || !_target.Spawned || _target.Map != Pawn.Map)
            {
                CancelBreach();
                return false;
            }

            return true;
        }

        public void Tick(int now, bool stunned)
        {
            var map = Pawn.Map;
            if (_aim != null && now >= _aim.detonateTick)
            {
                var aim = _aim;
                _aim = null;
                _aimAtPathBlock = false;
                aim.Detonate(map, Props.blastBreachRadius, ApocryphaGimmicks.GetBlastIgnoredThings(map, Pawn), Pawn, ScreenShake);
                DamageBlockers(map, aim.cell);
                if (InProgress)
                {
                    Rescan();
                }
            }

            if (_aim == null)
            {
                EndingVfxUtility.CleanupEffecter(ref _castEffecter);
            }
            else
            {
                _aim.TickVisuals(map);

                var source = new TargetInfo(Pawn.Position, map);
                EndingVfxUtility.MaintainEffecter(ref _castEffecter, VVEffecterDefOf.VV_Effecter_ApocryphaAimSustain, source, source);
            }

            if (_aim != null || stunned) { return; }

            if (_pathBlockedCell.IsValid)
            {
                _aim = new FairyBlastAim(_pathBlockedCell, now + FairyBlastAim.AimTicks);
                _aimAtPathBlock = true;
                _pathBlockedCell = IntVec3.Invalid;
            }
            else if (InProgress && Pawn.Position.InHorDistOf(_standCell, StandRadius))
            {
                _aim = new FairyBlastAim(_blockedCell, now + FairyBlastAim.AimTicks);
            }
        }

        public void Cancel()
        {
            _pathBlockedCell = IntVec3.Invalid;
            _aimAtPathBlock = false;
            CancelBreach();
        }

        private void CancelBreach()
        {
            if (!_aimAtPathBlock)
            {
                _aim?.CleanupVisuals();
                _aim = null;
                EndingVfxUtility.CleanupEffecter(ref _castEffecter);
            }

            _target = null;
            _standCell = IntVec3.Invalid;
            _blockedCell = IntVec3.Invalid;
            _deadlineTick = -1;
        }

        private void Rescan()
        {
            if (_target == null || !_target.Spawned || Pawn.CanReach(_target, PathEndMode.Touch, Danger.Deadly)
                || !TryFindBlockedCell(_target, _maxDistance, out var standCell, out var blocked))
            {
                CancelBreach();
                return;
            }

            _standCell = standCell;
            _blockedCell = blocked;
        }

        private void DamageBlockers(Map map, IntVec3 center)
        {
            foreach (var cell in GenRadial.RadialCellsAround(center, Props.blastBreachRadius, useCenter: true))
            {
                if (!cell.InBounds(map)) { continue; }

                var edifice = cell.GetEdifice(map);
                if (edifice == null || !ApocryphaUtility.BlocksPath(edifice) || !JobGiver_ApocryphaMain.CanSmash(edifice, _target)) { continue; }

                edifice.TakeDamage(new DamageInfo(VVDamageDefOf.VV_FairyBlast, Props.blastBreachBuildingDamage, 1f, -1f, Pawn));
            }
        }

        private bool TryFindBlockedCell(Thing target, float maxDistance, out IntVec3 standCell, out IntVec3 blocked)
        {
            var map = Pawn.Map;
            var origin = Pawn.Position;
            var targetRect = target.OccupiedRect();
            blocked = IntVec3.Invalid;
            standCell = origin;
            foreach (var c in GenSight.PointsOnLineOfSight(origin, target.Position))
            {
                if (c == origin) { continue; }
                if (targetRect.Contains(c)) { break; }
                if (maxDistance > 0f && !origin.InHorDistOf(c, maxDistance)) { break; }
                if (!ApocryphaUtility.IsBreachableCell(map, c)) { return false; }

                var edifice = c.GetEdifice(map);
                if (edifice != null && ApocryphaUtility.BlocksPath(edifice))
                {
                    blocked = c;
                    break;
                }

                if (!c.Standable(map)) { continue; }

                if (!Pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                {
                    blocked = c;
                    break;
                }

                standCell = c;
            }

            return blocked.IsValid;
        }
    }
}
