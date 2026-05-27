using HarmonyLib;
using RimWorld;
using RPEF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Verb_ShootWithMana : Verb_ShootWithMode
    {
        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null)
                {
                    _manaComp = EquipmentSource?.TryGetComp<CompMana>();
                }

                if (_manaComp == null)
                {
                    _manaComp = caster.TryGetComp<CompMana>();
                }

                return _manaComp;
            }
        }
        [Unsaved]
        private CompMana _manaComp;

        public bool HasSufficientMana
        {
            get
            {
                if (Bursting)
                {
                    return ManaComp.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) / BurstShotCount;
                }
                else
                {
                    return ManaComp.Stored >= EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost);
                }
            }
        }

        [Unsaved]
        private Dictionary<ThingDef, float> _tmpProjectileSelector = new Dictionary<ThingDef, float>();
        public override ThingDef Projectile
        {
            get
            {
                if (caster is ArcanePlant_Turret turret)
                {
                    if (_tmpProjectileSelector.Count == 0)
                    {
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
                        else
                        {
                            _tmpProjectileSelector.Add(base.Projectile, 1f);
                        }
                    }
                    
                    if (_tmpProjectileSelector.Count == 1)
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

        public override int ProjectileCount => (int)EquipmentSource.GetStatValue(VVStatDefOf.VV_BulletPelletCount);

        public override string ReportLabel => base.ReportLabel;

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

        protected override bool TryCastShot()
        {
            var compMana = ManaComp;
            if (compMana == null) { return false; }

            var manaPerShoot = EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) / BurstShotCount ?? 0;
            if (compMana.Stored < manaPerShoot) { return false; }

            if (base.TryCastShot())
            {
                compMana.Stored -= manaPerShoot;

                _tmpProjectileSelector.Clear();
                return true;
            }

            return false;
        }
    }
}
