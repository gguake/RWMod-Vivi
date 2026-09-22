using System;
using RimWorld;
using Verse;

namespace VVRace
{
    public interface IManaWeaponTiming
    {
        ManaWeaponCastState ManaCastState { get; }
    }

    // 사격 시점의 마나 현황을 기록하기 위한 스냅샷
    public class ManaWeaponCastState : IExposable
    {
        private bool insufficientMana;

        public float Multiplier => insufficientMana ? 3f : 1f;

        public static bool AppliesTo(Verb verb)
        {
            return verb.CasterIsPawn &&
                verb.EquipmentSource?.TryGetComp<CompEquippableManaWeapon>() != null;
        }

        public static bool IsInsufficient(Thing weapon)
        {
            var mana = weapon?.TryGetComp<CompMana>();
            return mana != null && mana.Stored < weapon.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost);
        }

        public bool TryStartCast(Verb verb, Func<bool> startCast)
        {
            if (verb.Bursting) { return false; }

            var previous = insufficientMana;
            insufficientMana = AppliesTo(verb) && IsInsufficient(verb.EquipmentSource);
            if (startCast()) { return true; }
            insufficientMana = previous;
            return false;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref insufficientMana, "insufficientMana");
        }
    }
}
