using RimWorld;
using Verse;

namespace VVRace
{
    // 발사된 눈사람의 2차 폭발을 쏜 폰 명의로 일으킨다.
    // 바닐라 CompExplosive는 무소속인 눈사람 자신을 가해자로 삼아, 우호 세력원이 죽으면 호감도가 깎이고(MemberNeutrallyDied)
    // 플레이어가 적을 노리고 쐈을 때의 교차사격 면제도 받지 못한다.
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
            // 심지가 다 타는 틱에 바닐라 Detonate 대신 터뜨린다. 그 전에 맞아서 터지면 바닐라대로 때린 쪽 명의
            if (_launcher != null && wickStarted && wickTicksLeft <= 1)
            {
                DetonateByLauncher();
                return;
            }

            base.CompTick();
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
