using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaShellAttack : IExposable
    {
        private const int TargetScanInterval = 30;
        private const int StunRecoverTicks = 120;
        private const float AimSpreadRadius = 2.5f;

        private static readonly Color AimBeamColor = new Color(1f, 0.86f, 0.45f, 0.85f);

        private CompApocrypha _comp;

        private List<ApocryphaShellAim> _aims = new List<ApocryphaShellAim>();
        private int _readyTick;
        private Effecter _aimSustainer;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public bool IsAiming => _aims.Count > 0;
        public IReadOnlyList<ApocryphaShellAim> Aims => _aims;

        public ApocryphaShellAttack(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _readyTick, "readyTick", 0);
            Scribe_Collections.Look(ref _aims, "aims", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _aims.RemoveAll(v => v == null || !v.cell.IsValid);
            }
        }

        public void SetReadyTick(int tick)
        {
            _readyTick = tick;
        }

        public void Tick(int now, bool stunned)
        {
            if (stunned)
            {
                if (IsAiming)
                {
                    Cancel();
                    _readyTick = now + StunRecoverTicks;
                }
                return;
            }

            for (int i = _aims.Count - 1; i >= 0; i--)
            {
                var aim = _aims[i];
                if (now < aim.fireTick) { continue; }

                _aims.RemoveAt(i);
                aim.CleanupVisuals();

                if (!InMinRange(aim.cell))
                {
                    ApocryphaGimmicks.TryFireFairyShell(Pawn, aim.cell);
                }
            }

            if (_aims.Count < Props.maxShellAims && now >= _readyTick && !_comp.Channel.IsChanneling && !_comp.Channel.PendingSteal)
            {
                if (_comp.Targeting.TryFindTargetCell(Props.shellRange, IsNearAimedCell, InMinRange, out var cell))
                {
                    _aims.Add(new ApocryphaShellAim(cell, now + Props.shellAimTicks));
                    _readyTick = now + Props.shellCastIntervalTicks.RandomInRange;
                    Pawn.rotationTracker.FaceCell(cell);
                }
                else
                {
                    _readyTick = now + TargetScanInterval;
                }
            }

            TickAimVisuals();
        }

        public void Cancel()
        {
            CleanupVisuals();
            _aims.Clear();
        }

        public void CleanupVisuals()
        {
            for (int i = 0; i < _aims.Count; i++)
            {
                _aims[i].CleanupVisuals();
            }

            _aimSustainer?.Cleanup();
            _aimSustainer = null;
        }

        public void Draw()
        {
            for (int i = 0; i < _aims.Count; i++)
            {
                SoulLinkRenderer.Draw(_comp.BodyCenter, _aims[i].cell.ToVector3Shifted(), 0.35f, AimBeamColor);
            }
        }

        // 모트·이펙터는 저장되지 않으므로 불러온 후 첫 틱에 다시 생성한다.
        private void TickAimVisuals()
        {
            if (_aims.Count == 0)
            {
                _aimSustainer?.Cleanup();
                _aimSustainer = null;
                return;
            }

            var map = Pawn.Map;
            var bossTarget = new TargetInfo(Pawn);
            var bodyCenter = _comp.BodyCenter;
            for (int i = 0; i < _aims.Count; i++)
            {
                _aims[i].TickVisuals(Pawn, map, bodyCenter, Props.shellAimTicks);
            }

            if (_aimSustainer == null)
            {
                _aimSustainer = VVEffecterDefOf.VV_Effecter_ApocryphaAimSustain.Spawn();
                _aimSustainer.Trigger(bossTarget, bossTarget);
            }

            _aimSustainer.EffectTick(bossTarget, bossTarget);
        }

        private bool InMinRange(IntVec3 cell)
        {
            return (cell - Pawn.Position).LengthHorizontalSquared < Props.shellMinRange * Props.shellMinRange;
        }

        private bool IsNearAimedCell(IntVec3 cell)
        {
            for (int i = 0; i < _aims.Count; i++)
            {
                if (_aims[i].cell.InHorDistOf(cell, AimSpreadRadius)) { return true; }
            }

            return false;
        }
    }
}
