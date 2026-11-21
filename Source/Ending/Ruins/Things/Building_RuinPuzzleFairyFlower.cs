using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Building_RuinPuzzleFairyFlower : Building
    {
        public const int SwapGlideTicks = 22;
        private static readonly Vector3 LinkAnchorOffset = new Vector3(0f, 0f, -0.06f);

        private int _nodeIndex;
        private Vector3 _glideFrom;
        private int _glideStartTick = -1;

        public int NodeIndex
        {
            get => _nodeIndex;
            set => _nodeIndex = value;
        }

        public Vector3 LinkAnchor => DrawPos + LinkAnchorOffset;

        public override Vector3 DrawPos
        {
            get
            {
                var target = base.DrawPos;
                if (_glideStartTick < 0) { return target; }

                var elapsed = Find.TickManager.TicksGame - _glideStartTick;
                if (elapsed < 0 || elapsed >= SwapGlideTicks)
                {
                    _glideStartTick = -1;
                    return target;
                }

                var t = ApocryphaUtility.EaseInOut(elapsed / (float)SwapGlideTicks);
                return Vector3.Lerp(_glideFrom, target, t);
            }
        }

        public void BeginSwapGlide(Vector3 previousDrawPos)
        {
            _glideFrom = previousDrawPos;
            _glideStartTick = Find.TickManager.TicksGame;
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            var ruinsComp = Spawned ? Map?.GetComponent<MapComponent_ViviAncientRuins>() : null;
            if (ruinsComp != null && ruinsComp.IsActiveRuinsMap)
            {
                if (sb.Length > 0) { sb.AppendLine(); }

                if (!ruinsComp.IsHexagonSolved)
                {
                    var tethered = ruinsComp.GetTetheredPawnFor(this);
                    if (tethered != null)
                    {
                        sb.AppendLine(LocalizeString_Inspector.VV_Inspector_RuinPuzzleFlowerTethered.Translate(tethered.Named("PAWN")));
                    }
                    sb.Append(LocalizeString_Inspector.VV_Inspector_RuinPuzzleFlowerHint.Translate());
                }
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode && Spawned)
            {
                var ruinsComp = Map?.GetComponent<MapComponent_ViviAncientRuins>();
                if (ruinsComp != null && ruinsComp.IsActiveRuinsMap && !ruinsComp.IsHexagonSolved)
                {
                    yield return EndingVfxUtility.DevAction("DEV: Solve hexagon puzzle", () => ruinsComp.DebugSolveHexagon());
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _nodeIndex, "nodeIndex", 0);
        }
    }
}
