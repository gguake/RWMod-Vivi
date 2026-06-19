using Verse;

namespace VVRace
{
    public class BattleLogEntry_FairyImpact : BattleLogEntry_RangedImpact
    {
        private Pawn commander;
        private Thing target;
        private ThingDef fairyDef;

        public BattleLogEntry_FairyImpact() { }

        public BattleLogEntry_FairyImpact(Pawn commander, Thing target, ThingDef fairyDef)
            : base(commander, target, target, fairyDef, null, null)
        {
            this.commander = commander;
            this.target = target;
            this.fairyDef = fairyDef;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref commander, "commander");
            Scribe_References.Look(ref target, "target");
            Scribe_Defs.Look(ref fairyDef, "fairyDef");
        }

        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
        {
            if (commander == null || target == null)
            {
                return base.ToGameStringFromPOV_Worker(pov, forceLog);
            }

            var fairyLabel = fairyDef != null ? fairyDef.label : "fairy vivi";
            return LocalizeString_Etc.VV_BattleLog_FairyImpact.Translate(
                commander.Named("COMMANDER"),
                fairyLabel.Named("FAIRY"),
                target.Named("TARGET")).Resolve();
        }
    }
}
