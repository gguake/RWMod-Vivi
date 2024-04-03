using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_ManaShieldProjector : CompProperties
    {
        public ThingDef shieldDef;

        public int requiredMinimumManaForActivate;
        public float manaEfficiency;

        public CompProperties_ManaShieldProjector()
        {
            compClass = typeof(CompManaShieldProjector);
        }
    }

    public class CompManaShieldProjector : ThingComp
    {
        public CompProperties_ManaShieldProjector Props => (CompProperties_ManaShieldProjector)props;

        public Thing Shield
        {
            get
            {
                if (_shield == null) { return null; }
                if (!_shield.Spawned)
                {
                    _shield = null;
                    return null;
                }

                return _shield;
            }
        }
        private Thing _shield;

        public override void PostExposeData()
        {
            Scribe_References.Look(ref _shield, "shield");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var commandActivateShield = new Command_Action();
            commandActivateShield.defaultLabel = LocalizeTexts.CommandActivateShield.Translate();
            commandActivateShield.defaultDesc = LocalizeTexts.CommandActivateShieldDesc.Translate();
            commandActivateShield.action = () =>
            {
                DeployShield();
            };

            if (Shield != null)
            {
                commandActivateShield.Disabled = true;
                commandActivateShield.disabledReason = LocalizeTexts.CommandActivateShieldAlreadyActivated.Translate();
            }

            var plant = parent as ArcanePlant;
            if (plant == null || plant.Mana < Props.requiredMinimumManaForActivate)
            {
                commandActivateShield.Disabled = true;
                commandActivateShield.disabledReason = LocalizeTexts.CommandActivateShieldNotEnoughEnergy.Translate(Props.requiredMinimumManaForActivate);
            }

            yield return commandActivateShield;
        }

        public override void PostDeSpawn(Map map)
        {
            if (_shield != null)
            {
                _shield.Destroy();
                _shield = null;
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (_shield != null)
            {
                _shield.Destroy();
                _shield = null;
            }
        }

        public override string CompInspectStringExtra()
        {
            if (Shield != null)
            {
                var interceptor = _shield.TryGetComp<CompProjectileInterceptor>();
                return LocalizeString_Inspector.VV_Inspector_ViviBroadShield.Translate(interceptor.currentHitPoints, interceptor.HitPointsMax);
            }

            return base.CompInspectStringExtra();
        }

        private void DeployShield()
        {
            if (Shield != null) { return; }

            var plant = parent as ArcanePlant;
            if (plant == null || plant.Mana < Props.requiredMinimumManaForActivate) { return; }

            var hitPoints = (int)(Props.manaEfficiency * plant.Mana);
            if (hitPoints <= 0) { return; }

            _shield = GenSpawn.Spawn(Props.shieldDef, parent.Position, parent.Map);
            
            var interceptor = _shield.TryGetComp<CompProjectileInterceptor>();
            interceptor.maxHitPointsOverride = hitPoints;
            interceptor.currentHitPoints = hitPoints;

            plant.AddMana(-plant.Mana);
        }
    }
}
