using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompProperties_BroadshieldProjector : CompProperties
    {
        public ThingDef shieldDef;
        public int minEnergyForActivate;
        public float energyEfficiency;

        public CompProperties_BroadshieldProjector()
        {
            compClass = typeof(CompBroadshieldProjector);
        }
    }

    public class CompBroadshieldProjector : ThingComp
    {
        public CompProperties_BroadshieldProjector Props => (CompProperties_BroadshieldProjector)props;

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
            if (plant == null || plant.Energy < Props.minEnergyForActivate)
            {
                commandActivateShield.Disabled = true;
                commandActivateShield.disabledReason = LocalizeTexts.CommandActivateShieldNotEnoughenrgy.Translate(Props.minEnergyForActivate);
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
                return LocalizeTexts.InspectorViviBroadShield.Translate(interceptor.currentHitPoints, interceptor.HitPointsMax);
            }

            return base.CompInspectStringExtra();
        }

        private void DeployShield()
        {
            if (Shield != null) { return; }

            var plant = parent as ArcanePlant;
            if (plant == null || plant.Energy < Props.minEnergyForActivate) { return; }

            var hitPoints = (int)(Props.energyEfficiency * plant.Energy);
            if (hitPoints <= 0) { return; }

            _shield = GenSpawn.Spawn(Props.shieldDef, parent.Position, parent.Map);
            
            var interceptor = _shield.TryGetComp<CompProjectileInterceptor>();
            interceptor.maxHitPointsOverride = hitPoints;
            interceptor.currentHitPoints = hitPoints;

            plant.AddEnergy(-plant.Energy);
        }
    }
}
