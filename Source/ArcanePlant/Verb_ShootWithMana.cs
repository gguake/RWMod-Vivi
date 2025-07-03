using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class Verb_ShootWithMana : Verb_Shoot
    {
        private CompMana _manaComp;
        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null)
                { 
                    _manaComp = caster.TryGetComp<CompMana>();
                }

                if (_manaComp == null)
                {
                    _manaComp = EquipmentSource?.TryGetComp<CompMana>();
                }

                return _manaComp;
            }
        }

        public bool HasSufficientMana
        {
            get
            {
                if (Bursting)
                {
                    return ManaComp.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost);
                }
                else
                {
                    return ManaComp.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) * BurstShotCount;
                }
            }
        }

        private Dictionary<ThingDef, float> _tmpProjectileSelector;
        public override ThingDef Projectile
        {
            get
            {
                if (caster is ArcanePlant_Turret turret)
                {
                    if (_tmpProjectileSelector == null)
                    {
                        _tmpProjectileSelector = new Dictionary<ThingDef, float>();

                        var bulletReplacers = turret.BulletReplacers;
                        if (bulletReplacers.Any())
                        {
                            var baseProjectileProb = 1f - bulletReplacers.Sum(v => v.chance);
                            if (baseProjectileProb > 0f) { _tmpProjectileSelector.Add(base.Projectile, baseProjectileProb); }

                            foreach (var replacer in turret.BulletReplacers)
                            {
                                if (!_tmpProjectileSelector.TryGetValue(replacer.bulletDef, out var value))
                                {
                                    value = 0f;
                                }

                                _tmpProjectileSelector[replacer.bulletDef] = value + replacer.chance;
                            }
                        }
                    }

                    if (_tmpProjectileSelector.Count == 0)
                    {
                        return base.Projectile;
                    }
                    else if (_tmpProjectileSelector.Count == 1)
                    {
                        return _tmpProjectileSelector.FirstOrDefault().Key;
                    }
                    else
                    {
                        return _tmpProjectileSelector.RandomElementByWeight(kv => kv.Value).Key;
                    }
                }

                return base.Projectile;
            }
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
            {
                return false;
            }

            if (!HasSufficientMana)
            {
                var manaCost = (int)(EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) ?? 0);
                Messages.Message($"ManaNotEnough".Translate(manaCost.Named("MANACOST")), new LookTargets(caster), MessageTypeDefOf.RejectInput, historical: false);
            }

            return true;
        }

        public override bool Available()
        {
            if (!base.Available()) { return false; }

            return HasSufficientMana;
        }

        public override string ReportLabel => base.ReportLabel;

        protected override bool TryCastShot()
        {
            var compMana = ManaComp;
            if (compMana == null) { return false; }

            var manaPerShoot = EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) ?? 0;
            if (compMana.Stored < manaPerShoot) { return false; }

            var shot =  base.TryCastShot();
            if (shot)
            {
                compMana.Stored -= manaPerShoot;
            }

            return shot;
        }
    }
}
