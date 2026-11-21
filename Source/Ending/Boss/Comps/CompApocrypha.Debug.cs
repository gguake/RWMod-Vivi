using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public partial class CompApocrypha
    {
        public void DebugForcePhase2()
        {
            if (!parent.Spawned) { return; }

            if (_phase == ApocryphaPhase.Queen)
            {
                StartRebirth();
            }
            else if (_phase == ApocryphaPhase.Asphodel)
            {
                StartDying();
            }
        }

        public void DebugDamageShield(float fractionOfMax)
        {
            if (_phase != ApocryphaPhase.Asphodel || ShieldBroken) { return; }

            _manaShield = Mathf.Max(0f, _manaShield - MaxManaShield * fractionOfMax);
            if (_manaShield <= 0f)
            {
                _pendingShieldBreak = true;
            }
        }

        public void DebugDamageVitality(float fractionOfMax)
        {
            if (_phase != ApocryphaPhase.Queen || _pendingDeath) { return; }

            _vitality = Mathf.Max(0f, _vitality - MaxVitality * fractionOfMax);
            if (_vitality <= 0f)
            {
                _pendingDeath = true;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!DebugSettings.godMode) { yield break; }

            yield return EndingVfxUtility.DevAction("DEV: Force phase 2", DebugForcePhase2);
            yield return EndingVfxUtility.DevAction("DEV: Start channel", _channel.DebugStart);
            yield return EndingVfxUtility.DevAction("DEV: Finish channel", _channel.DebugFinish);
            yield return EndingVfxUtility.DevAction("DEV: Approach altar now", DebugApproachAltarNow);
            yield return EndingVfxUtility.DevAction("DEV: Summon escort hornets", () => HornetManifestation.SummonAround(Pawn, ScaledHornetCount(Props.escortSummonCount), HornetSummonRole.Escort));

            if (_phase == ApocryphaPhase.Queen)
            {
                yield return EndingVfxUtility.DevAction("DEV: Vitality -25%", () => DebugDamageVitality(0.25f));
                yield return EndingVfxUtility.DevAction("DEV: Stick a needle", _needleAttack.DebugStickNeedle);
                yield return EndingVfxUtility.DevAction("DEV: Shake off needles now", _needleAttack.DebugShakeOffNow);
                yield return EndingVfxUtility.DevAction("DEV: Doom wave now", () => _doomWave.SetReadyTick(0));
                yield return EndingVfxUtility.DevAction("DEV: Breath ready now", () => _breath.SetReadyTick(0));
                yield return EndingVfxUtility.DevAction("DEV: Recall escorts now", () => _escortSummon.StartCast(GenTicks.TicksGame));
            }
            else if (_phase == ApocryphaPhase.Asphodel)
            {
                yield return EndingVfxUtility.DevAction("DEV: Needle ready now", () => _needleAttack.SetReadyTick(0));
                yield return EndingVfxUtility.DevAction("DEV: Fairy barrier -25%", () => DebugDamageShield(0.25f));
                yield return EndingVfxUtility.DevAction("DEV: Restore barrier now", () => _shieldRestoreTick = GenTicks.TicksGame);
                yield return EndingVfxUtility.DevAction("DEV: Fairy bond -10%", () => DrainBond(_bond.Max * 0.1f));
            }
        }

        private void DebugApproachAltarNow()
        {
            if (_channel.TryGetStealTarget(out var target, out _, out _))
            {
                var currentDistance = Pawn.Position.DistanceTo(target.Position);
                _approachRadius = _approachRadius < 0f ? currentDistance : Mathf.Min(_approachRadius, currentDistance);
            }

            _nextApproachTick = 0;
            _nextStealTargetSkipCheckTick = 0;
            InterruptCurrentJob();
        }
    }
}
