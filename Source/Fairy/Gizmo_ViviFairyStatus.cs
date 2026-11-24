using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Gizmo_ViviFairyStatus : Gizmo
    {
        private const float CellSize = 13f;
        private const float CellGap = 4f;
        private const float Padding = 8f;
        private const float LabelHeight = 20f;
        private const int MaxCellsPerRow = 4;

        private static readonly Color IdleColor = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color ActionColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color EmptyColor = new Color(0.25f, 0.25f, 0.28f, 1f);

        private readonly CompViviHolder _comp;
        private LoadedLanguage _cachedLang;
        private string _cachedHeader;
        private int _cachedTooltipMat = -1;
        private int _cachedTooltipTotal = -1;
        private TipSignal _cachedTooltip;
        private bool _cachedTooltipValid;

        public Gizmo_ViviFairyStatus(CompViviHolder comp)
        {
            _comp = comp;
            Order = -90f;
        }

        public override bool Visible => _comp != null && _comp.parent is Pawn pawn && pawn.IsColonistPlayerControlled && _comp.MaterializedCount > 0 && Find.Selector.NumSelected == 1;

        public override float GetWidth(float maxWidth)
        {
            int slots = Mathf.Max(1, Mathf.Max(_comp.FairyficatedPawnCount, _comp.MaterializedCount));
            int columns = Mathf.Min(slots, MaxCellsPerRow);
            float w = Padding * 2f + columns * CellSize + (columns - 1) * CellGap;
            return Mathf.Clamp(w, 75f, maxWidth);
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            int totalBound = _comp.FairyficatedPawnCount;
            int matCount = _comp.MaterializedCount;
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(rect);

            var inner = rect.ContractedBy(Padding);

            var activeLang = LanguageDatabase.activeLanguage;
            if (_cachedHeader == null || _cachedLang != activeLang)
            {
                _cachedLang = activeLang;
                _cachedHeader = LocalizeString_Gizmo.VV_Gizmo_FairyStatusHeader.Translate().Resolve();
                _cachedTooltipValid = false;
            }

            using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft))
            {
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, LabelHeight), _cachedHeader);
            }

            var activeFairies = _comp.ActiveFairies;
            int actionCount = 0;
            int aliveCount = 0;
            for (int i = 0; i < activeFairies.Count; i++)
            {
                var f = activeFairies[i];
                if (f != null && !f.Destroyed)
                {
                    aliveCount++;
                    if (f.InAction)
                    {
                        actionCount++;
                    }
                }
            }

            float startX = inner.x;
            float startY = inner.y + LabelHeight + 4f;
            float cellStride = CellSize + CellGap;
            int slots = Mathf.Max(totalBound, matCount);
            for (int i = 0; i < slots; i++)
            {
                int column = i % MaxCellsPerRow;
                int row = i / MaxCellsPerRow;
                var cell = new Rect(startX + column * cellStride, startY + row * cellStride, CellSize, CellSize);
                Color color;
                if (i < actionCount)
                {
                    color = ActionColor;
                }
                else if (i < aliveCount)
                {
                    color = IdleColor;
                }
                else
                {
                    color = EmptyColor;
                }

                Widgets.DrawBoxSolid(cell, color);
            }

            if (!_cachedTooltipValid || _cachedTooltipMat != matCount || _cachedTooltipTotal != totalBound)
            {
                _cachedTooltipMat = matCount;
                _cachedTooltipTotal = totalBound;
                _cachedTooltip = LocalizeString_Gizmo.VV_Gizmo_FairyStatusTooltip.Translate(matCount, totalBound);
                _cachedTooltipValid = true;
            }
            TooltipHandler.TipRegion(rect, _cachedTooltip);

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
