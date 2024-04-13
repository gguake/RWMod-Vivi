using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_ManaShieldProjector : CompProperties
    {
        public ThingDef shieldDef;

        public int requiredMinimumManaForActivate;
        public float manaEfficiency;

        public ThingDef mote;

        public CompProperties_ManaShieldProjector()
        {
            compClass = typeof(CompManaShieldProjector);
        }
    }

    [StaticConstructorOnStartup]
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

        private static readonly Texture2D ShieldActivateTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_PurifimintShield");
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var duration = Props.shieldDef.GetCompProperties<CompProperties_DestroyAfterDelay>().delayTicks;

            var commandActivateShield = new Command_Action();
            commandActivateShield.icon = ShieldActivateTex;
            commandActivateShield.defaultLabel = LocalizeString_Command.VV_Command_ActivateShield.Translate();
            commandActivateShield.defaultDesc = LocalizeString_Command.VV_Command_ActivateShieldDesc.Translate(duration.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor));
            commandActivateShield.action = () =>
            {
                DeployShield();
            };

            if (Shield != null)
            {
                commandActivateShield.Disabled = true;
                commandActivateShield.disabledReason = LocalizeString_Command.VV_Command_ActivateShieldAlreadyActivated.Translate();
            }

            var plant = parent as ArcanePlant;
            if (plant == null || plant.Mana < Props.requiredMinimumManaForActivate)
            {
                commandActivateShield.Disabled = true;
                commandActivateShield.disabledReason = LocalizeString_Command.VV_Command_ActivateShieldNotEnoughEnergy.Translate(Props.requiredMinimumManaForActivate);
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
                var destroyer = _shield.TryGetComp<CompDestroyAfterDelay>();

                var sb = new StringBuilder();
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_ViviBroadShieldHitPoint.Translate(interceptor.currentHitPoints, interceptor.HitPointsMax));
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_ViviBroadShieldDuration.Translate(destroyer.TicksLeft.ToStringTicksToPeriod(allowSeconds: true)));
                return sb.ToString();
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

            if (Props.mote != null)
            {
                MoteMaker.MakeStaticMote(parent.DrawPos, parent.Map, Props.mote);
            }
        }
    }
}
