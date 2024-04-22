using Verse;

namespace VVRace
{
    public class ArcanePlant_Dreamum : ArcanePlant
    {
        public const int TranquilizeDays = 2;
        public const int HediffInterval = 250;

        public override void Tick()
        {
            base.Tick();

            if (!Spawned || Destroyed)
            {
                return;
            }

            if (this.IsHashIntervalTick(HediffInterval))
            {
                var cells = GenRadial.RadialPatternInRadius(2.9f);
                foreach (var v in cells)
                {
                    var cell = Position + v;
                    if (!cell.InBounds(Map)) { continue; }

                    var pawn = cell.GetFirstPawn(Map);
                    if (pawn != null && !pawn.IsVivi() && pawn.health?.hediffSet != null && pawn.def.race.IsFlesh)
                    {
                        var hediff = pawn.health.GetOrAddHediff(VVHediffDefOf.VV_DreamumTranquilize);
                        if (hediff != null)
                        {
                            hediff.Severity += 1f / (TranquilizeDays * HediffInterval) / 60000f;
                        }
                    }
                }
            }
        }
    }
}
