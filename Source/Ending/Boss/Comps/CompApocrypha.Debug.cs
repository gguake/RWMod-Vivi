using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public partial class CompApocrypha
    {
        public void DebugReduceBond(float fractionOfInitial)
        {
            if (_phase != ApocryphaPhase.Bound) { return; }

            var amount = (long)System.Math.Round(Props.initialBondCount * (double)fractionOfInitial);
            _bondCount = System.Math.Max(0L, _bondCount - System.Math.Max(1L, amount));
            if (_bondCount <= 0 && parent.Spawned)
            {
                StartTransformation();
            }
        }

        public void DebugForcePhase2()
        {
            if (!parent.Spawned) { return; }

            if (_phase == ApocryphaPhase.Bound)
            {
                _bondCount = 0;
                StartTransformation();
            }
            else if (_phase == ApocryphaPhase.Transforming)
            {
                _transformStartTick = GenTicks.TicksGame - Props.transformTicks;
            }
        }

        public void DebugDamageShield(float fractionOfMax)
        {
            if (_phase != ApocryphaPhase.Bound || ShieldBroken) { return; }

            _manaShield = Mathf.Max(0f, _manaShield - MaxManaShield * fractionOfMax);
            if (_manaShield <= 0f)
            {
                _pendingShieldBreak = true;
            }
        }

        public void DebugDamageVitality(float fractionOfMax)
        {
            if (_phase != ApocryphaPhase.Unbound || _pendingDeath) { return; }

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

            yield return EndingVfxUtility.DevAction("DEV: Bond -25%", () => DebugReduceBond(0.25f));
            yield return EndingVfxUtility.DevAction("DEV: Force phase 2", DebugForcePhase2);
            yield return EndingVfxUtility.DevAction("DEV: Vitality -25%", () => DebugDamageVitality(0.25f));
            yield return EndingVfxUtility.DevAction("DEV: Start channel", _channel.DebugStart);
            yield return EndingVfxUtility.DevAction("DEV: Finish channel", _channel.DebugFinish);
            yield return EndingVfxUtility.DevAction("DEV: Stick a needle", _needleAttack.DebugStickNeedle);
            yield return EndingVfxUtility.DevAction("DEV: Shell ready now", () => _shell.SetReadyTick(0));
            yield return EndingVfxUtility.DevAction("DEV: Needle ready now", () => _needleAttack.SetReadyTick(0));
            yield return EndingVfxUtility.DevAction("DEV: Mana shield -25%", () => DebugDamageShield(0.25f));
            yield return EndingVfxUtility.DevAction("DEV: Doom seed now", () => _seeds.SetReadyTick(0));
            yield return EndingVfxUtility.DevAction("DEV: Fairy seed now", () => ApocryphaGimmicks.TryLaunchFairySeed(Pawn));
            yield return EndingVfxUtility.DevAction("DEV: Summon assault hornets", () => HornetManifestation.SummonAround(Pawn, InterferenceHornetCount(_releaseCount), HornetSummonRole.Assault));
            yield return EndingVfxUtility.DevAction("DEV: Breath ready now", () => _breath.SetReadyTick(0));
            yield return EndingVfxUtility.DevAction("DEV: Approach altar now", DebugApproachAltarNow);
            yield return EndingVfxUtility.DevAction("DEV: Summon escort hornets", () => HornetManifestation.SummonAround(Pawn, Props.volleyEscortCount, HornetSummonRole.Escort));
        }

        private void DebugApproachAltarNow()
        {
            _nextApproachTick = 0;
            _nextStealTargetSkipCheckTick = 0;
            InterruptCurrentJob();
        }
    }
}
