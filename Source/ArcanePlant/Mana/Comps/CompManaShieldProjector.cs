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
        public ThingDef mote;

        [NoTranslate]
        public string iconPath;

        public int shieldMaxHp;
        public float requiredManaPct;

        public Texture2D Icon
        {
            get
            {
                if (_icon == null)
                {
                    _icon = ContentFinder<Texture2D>.Get(iconPath);
                }
                return _icon;
            }
        }
        private Texture2D _icon;

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

        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null) { _manaComp = parent.GetComp<CompMana>(); }
                return _manaComp;
            }
        }
        private CompMana _manaComp;

        public override void PostExposeData()
        {
            Scribe_References.Look(ref _shield, "shield");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var duration = Props.shieldDef.GetCompProperties<CompProperties_DestroyAfterDelay>().delayTicks;

            var commandActivateShield = new Command_Action();
            commandActivateShield.icon = Props.Icon;
            commandActivateShield.defaultLabel = LocalizeString_Command.VV_Command_ActivateShield.Translate();
            commandActivateShield.defaultDesc = LocalizeString_Command.VV_Command_ActivateShieldDesc.Translate(duration.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor), Props.requiredManaPct.ToStringPercentEmptyZero());
            commandActivateShield.action = () =>
            {
                DeployShield();
            };

            if (Shield != null)
            {
                commandActivateShield.Disabled = true;
                commandActivateShield.disabledReason = LocalizeString_Command.VV_Command_ActivateShieldAlreadyActivated.Translate();
            }

            var manaComp = ManaComp;
            if (manaComp == null || manaComp.StoredPct < Props.requiredManaPct)
            {
                commandActivateShield.Disabled = true;
                commandActivateShield.disabledReason = LocalizeString_Command.VV_Command_ActivateShieldNotEnoughEnergy.Translate(Props.requiredManaPct.ToStringPercentEmptyZero());
            }

            yield return commandActivateShield;
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
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

            var manaComp = ManaComp;
            if (manaComp == null || manaComp.StoredPct < Props.requiredManaPct) { return; }

            _shield = GenSpawn.Spawn(Props.shieldDef, parent.Position, parent.Map);

            var shieldHp = Props.shieldMaxHp;
            var dayPct = GenLocalDate.DayPercent(parent.Map);
            if (dayPct < 0.2f || dayPct > 0.7f)
            {
                shieldHp *= 8;
            }

            var interceptor = _shield.TryGetComp<CompProjectileInterceptor>();
            interceptor.maxHitPointsOverride = shieldHp;
            interceptor.currentHitPoints = shieldHp;

            ManaComp.StoredPct -= Props.requiredManaPct;

            if (Props.mote != null)
            {
                MoteMaker.MakeStaticMote(parent.DrawPos, parent.Map, Props.mote);
            }
        }
    }
}
