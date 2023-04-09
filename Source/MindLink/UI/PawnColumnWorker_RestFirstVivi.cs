using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PawnColumnWorker_RestFirstVivi : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (!pawn.IsColonistPlayerControlled || !pawn.Spawned || !pawn.TryGetViviGene(out var vivi))
            {
                return;
            }

            var disabled = pawn.WorkTypeIsDisabled(VVWorkTypeDefOf.Patient) || pawn.WorkTypeIsDisabled(VVWorkTypeDefOf.PatientBedRest);

            rect.xMin += (rect.width - 24f) / 2f;
            rect.yMin += (rect.height - 24f) / 2f;

            if (vivi.ViviControlSettings != null)
            {
                var restFirst = vivi.ViviControlSettings.DoRestFirst;
                Widgets.Checkbox(rect.position, ref restFirst, 24f, paintable: def.paintable, disabled: disabled);

                if (restFirst != vivi.ViviControlSettings.DoRestFirst)
                {
                    vivi.ViviControlSettings.DoRestFirst = restFirst;
                }
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), 28);
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
        }

        public override int GetMinCellHeight(Pawn pawn)
        {
            return Mathf.Max(base.GetMinCellHeight(pawn), 24);
        }
    }
}
