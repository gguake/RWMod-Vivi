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
    public class Verb_ShootWithMana : Verb_ShootWithMode, IManaWeaponTiming
    {
        private ManaWeaponCastState manaCastState = new ManaWeaponCastState();
        public ManaWeaponCastState ManaCastState => manaCastState;
        public override float WarmupTime => base.WarmupTime * manaCastState.Multiplier;

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg,
            bool surpriseAttack = false, bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            return manaCastState.TryStartCast(this, () => base.TryStartCastOn(castTarg, destTarg,
                surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref manaCastState, "manaCastState");
            if (manaCastState == null) { manaCastState = new ManaWeaponCastState(); }
        }

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null)
                {
                    _manaComp = ManaWeaponCastState.FindManaComp(this);
                }

                return _manaComp;
            }
        }
        [Unsaved]
        private CompMana _manaComp;

        public bool HasSufficientMana => ManaWeaponCastState.HasSufficientMana(this, ManaComp);

        [Unsaved]
        private Dictionary<ThingDef, float> _tmpProjectileSelector = new Dictionary<ThingDef, float>();
        [Unsaved]
        private ThingDef _cachedSingleProjectile;
        [Unsaved]
        private bool _hasCachedSingleProjectile;
        [Unsaved]
        private ArcanePlant_Turret _cachedTurret;
        [Unsaved]
        private int _cachedReplacersVersion = -1;
        public override ThingDef Projectile
        {
            get
            {
                if (caster is ArcanePlant_Turret turret)
                {
                    var version = turret.BulletReplacersVersion;
                    if (!ReferenceEquals(_cachedTurret, turret) || _cachedReplacersVersion != version)
                    {
                        _cachedTurret = turret;
                        _cachedReplacersVersion = version;
                        _tmpProjectileSelector.Clear();
                        _cachedSingleProjectile = null;
                        _hasCachedSingleProjectile = false;
                    }

                    if (!turret.HasBulletReplacers)
                    {
                        if (!_hasCachedSingleProjectile)
                        {
                            _cachedSingleProjectile = base.Projectile;
                            _hasCachedSingleProjectile = true;
                        }
                        return _cachedSingleProjectile;
                    }

                    if (_hasCachedSingleProjectile)
                    {
                        return _cachedSingleProjectile;
                    }

                    if (_tmpProjectileSelector.Count == 0)
                    {
                        double totalReplacerChance = 0.0;
                        foreach (var replacer in turret.BulletReplacerValues)
                        {
                            totalReplacerChance += replacer.chance;
                        }

                        var baseProjectileProb = 1f - (float)totalReplacerChance;
                        if (baseProjectileProb > 0f)
                        {
                            _tmpProjectileSelector.Add(base.Projectile, baseProjectileProb);
                        }

                        foreach (var replacer in turret.BulletReplacerValues)
                        {
                            if (!_tmpProjectileSelector.TryGetValue(replacer.bulletDef, out var value))
                            {
                                value = 0f;
                            }

                            _tmpProjectileSelector[replacer.bulletDef] = value + replacer.chance;
                        }

                        if (_tmpProjectileSelector.Count == 1)
                        {
                            foreach (var kv in _tmpProjectileSelector)
                            {
                                _cachedSingleProjectile = kv.Key;
                                _hasCachedSingleProjectile = true;
                                break;
                            }
                            return _cachedSingleProjectile;
                        }
                    }

                    if (_tmpProjectileSelector.Count == 1)
                    {
                        foreach (var kv in _tmpProjectileSelector)
                        {
                            _cachedSingleProjectile = kv.Key;
                            _hasCachedSingleProjectile = true;
                            return kv.Key;
                        }
                    }
                    else if (_tmpProjectileSelector.Count > 1)
                    {
                        return _tmpProjectileSelector.RandomElementByWeight(kv => kv.Value).Key;
                    }
                }
                else if (_cachedTurret != null)
                {
                    _cachedTurret = null;
                    _cachedReplacersVersion = -1;
                    _tmpProjectileSelector.Clear();
                    _cachedSingleProjectile = null;
                    _hasCachedSingleProjectile = false;
                }

                return base.Projectile;
            }
        }

        public override string ReportLabel => base.ReportLabel;

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
            {
                return false;
            }

            if (!HasSufficientMana && !ManaWeaponCastState.AppliesTo(this) && showMessages)
            {
                var manaCost = (int)(EquipmentSource?.GetStatValue(VVStatDefOf.VV_RangedWeapon_ManaCost) ?? 0);
                Messages.Message(LocalizeString_Message.VV_Message_ManaNotEnough.Translate(manaCost.Named("MANACOST")), new LookTargets(caster), MessageTypeDefOf.RejectInput, historical: false);
            }

            return true;
        }

        public override bool Available()
        {
            if (!base.Available()) { return false; }

            return ManaComp != null && (ManaWeaponCastState.AppliesTo(this) || HasSufficientMana);
        }

        protected override bool TryCastShot()
        {
            var compMana = ManaComp;
            if (compMana == null) { return false; }

            var manaPerShoot = ManaWeaponCastState.ManaCostPerShot(this);
            if (!ManaWeaponCastState.CanPayShot(this, compMana, manaPerShoot)) { return false; }

            if (base.TryCastShot())
            {
                compMana.Stored -= manaPerShoot;

                _tmpProjectileSelector.Clear();
                _cachedSingleProjectile = null;
                _hasCachedSingleProjectile = false;
                return true;
            }

            return false;
        }
    }
}
