using System;
using RimWorld;
using Verse;

namespace VVRace
{
    public interface IManaWeaponTiming
    {
        ManaWeaponCastState ManaCastState { get; }
    }

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

        public static CompMana FindManaComp(Verb verb)
        {
            return verb.EquipmentSource?.TryGetComp<CompMana>() ?? verb.caster?.TryGetComp<CompMana>();
        }

        public static float ManaCostPerShot(Verb verb)
        {
            return verb.EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) / verb.BurstShotCount ?? 0f;
        }

        public static bool HasSufficientMana(Verb verb, CompMana compMana)
        {
            if (compMana == null) { return false; }

            var cost = verb.EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost);
            if (verb.Bursting)
            {
                cost /= verb.BurstShotCount;
            }

            return compMana.Stored >= cost;
        }

        public static bool CanPayShot(Verb verb, CompMana compMana, float manaPerShot)
        {
            return compMana.Stored >= manaPerShot || AppliesTo(verb);
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
