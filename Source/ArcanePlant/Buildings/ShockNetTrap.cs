using RimWorld;
using System.Text;
using Verse;

namespace VVRace
{
    public class ShockNetTrap : Building_Trap
    {
        public CompPowerTrader CompPowerTrader
        {
            get
            {
                if (_compPowerTrader == null)
                {
                    _compPowerTrader = GetComp<CompPowerTrader>();
                }
                return _compPowerTrader;
            }
        }
        private CompPowerTrader _compPowerTrader;

        private int _cooldownTicks;
        private bool _powered;

        public override int? OverrideGraphicIndex => _cooldownTicks > 0 || !_powered ? 0 : 1;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _cooldownTicks, "cooldownTicks");
            Scribe_Values.Look(ref _powered, "powered");
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            _powered = !CompPowerTrader.Off;
        }

        protected override float SpringChance(Pawn p)
        {
            if (_cooldownTicks > 0) { return 0f; }
            if (CompPowerTrader.Off) { return 0f; }

            return base.SpringChance(p);
        }

        protected override void SpringSub(Pawn p)
        {
            _cooldownTicks = Rand.Range(7500, 10000);

            GenExplosion.DoExplosion(Position, Map, 0.9f, DamageDefOf.EMP, this);
            if (!p.RaceProps.IsMechanoid)
            {
                _ = p.TakeDamage(new DamageInfo(DamageDefOf.Stun, 30f));
            }

            DirtyMapMesh(Map);
        }

        protected override void Tick()
        {
            base.Tick();

            if (Spawned && _cooldownTicks > 0)
            {
                if (!CompPowerTrader.Off)
                {
                    _cooldownTicks--;
                    if (_cooldownTicks == 0)
                    {
                        DirtyMapMesh(Map);
                    }
                }
            }
        }

        protected override void ReceiveCompSignal(string signal)
        {
            if (signal == CompPowerPlant.PowerTurnedOnSignal)
            {
                _powered = true;
            }
            if (signal == CompPowerPlant.PowerTurnedOffSignal)
            {
                _powered = false;
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());

            if (_cooldownTicks > 0)
            {
                sb.AppendInNewLine(LocalizeString_Inspector.VV_Inspector_ShockNetTrapCooldown.Translate(_cooldownTicks.ToStringSecondsFromTicks()));
            }

            return sb.ToString();
        }
    }
}
