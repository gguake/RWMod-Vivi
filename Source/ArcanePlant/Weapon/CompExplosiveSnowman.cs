using RimWorld;
using Verse;

namespace VVRace
{
    // 바닐라 CompExplosive는 무소속 눈사람을 가해자로 삼아 우호 세력원 사망 시 호감도를 깎는다.
    // 발사자를 가해자로 지정해야 적을 노린 사격의 아군 오발 면제도 적용된다.
    // 바닐라의 피격 조기 기폭은 맞힌 쪽을 가해자로 삼고 무기·조준 대상을 버리므로 장전된 눈사람은 심지와 피격 모두 발사자 명의로 터뜨린다.
    public class CompExplosiveSnowman : CompExplosive
    {
        private Thing _launcher;
        private ThingDef _equipmentDef;
        private ThingDef _projectileDef;
        private Thing _intendedTarget;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref _launcher, "launcher");
            Scribe_Defs.Look(ref _equipmentDef, "equipmentDef");
            Scribe_Defs.Look(ref _projectileDef, "projectileDef");
            Scribe_References.Look(ref _intendedTarget, "intendedTarget");
        }

        public void Arm(Thing launcher, ThingDef equipmentDef, ThingDef projectileDef, Thing intendedTarget)
        {
            _launcher = launcher;
            _equipmentDef = equipmentDef;
            _projectileDef = projectileDef;
            _intendedTarget = intendedTarget;

            StartWick(launcher);
        }

        public override void CompTick()
        {
            if (_launcher != null && wickStarted && wickTicksLeft <= 1)
            {
                DetonateByLauncher();
                return;
            }

            base.CompTick();
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (_launcher == null || !DetonatesFromDamage(dinfo))
            {
                base.PostPreApplyDamage(ref dinfo, out absorbed);
                return;
            }

            DetonateByLauncher();
            absorbed = parent.Destroyed;
        }

        // 바닐라 PostPreApplyDamage의 즉시 기폭 조건
        private bool DetonatesFromDamage(DamageInfo dinfo)
        {
            return CanEverExplodeFromDamage
                && dinfo.Def.ExternalViolenceFor(parent)
                && dinfo.Amount >= parent.HitPoints
                && (Props.requiredDamageTypeToExplode == null || Props.requiredDamageTypeToExplode == dinfo.Def)
                && parent.MapHeld != null;
        }

        private void DetonateByLauncher()
        {
            var map = parent.MapHeld;
            var position = parent.PositionHeld;
            var radius = ExplosiveRadius();

            destroyedThroughDetonation = true;
            parent.Kill();

            if (Props.explosionEffect != null)
            {
                var effecter = Props.explosionEffect.Spawn();
                effecter.Trigger(new TargetInfo(position, map), new TargetInfo(position, map));
                effecter.Cleanup();
            }

            GenExplosion.DoExplosion(
                center: position,
                map: map,
                radius: radius,
                damType: Props.explosiveDamageType,
                instigator: _launcher,
                damAmount: Props.damageAmountBase,
                armorPenetration: Props.armorPenetrationBase,
                explosionSound: Props.explosionSound,
                weapon: _equipmentDef,
                projectile: _projectileDef,
                intendedTarget: _intendedTarget,
                postExplosionSpawnThingDef: Props.postExplosionSpawnThingDef,
                postExplosionSpawnChance: Props.postExplosionSpawnChance,
                postExplosionSpawnThingCount: Props.postExplosionSpawnThingCount,
                postExplosionGasType: Props.postExplosionGasType,
                applyDamageToExplosionCellsNeighbors: Props.applyDamageToExplosionCellsNeighbors,
                preExplosionSpawnThingDef: Props.preExplosionSpawnThingDef,
                preExplosionSpawnChance: Props.preExplosionSpawnChance,
                preExplosionSpawnThingCount: Props.preExplosionSpawnThingCount,
                chanceToStartFire: Props.chanceToStartFire,
                damageFalloff: Props.damageFalloff,
                doVisualEffects: Props.doVisualEffects,
                propagationSpeed: Props.propagationSpeed,
                doSoundEffects: Props.doSoundEffects);
        }
    }
}
