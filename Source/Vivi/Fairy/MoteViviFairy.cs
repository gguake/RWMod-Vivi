using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public enum ViviFairyState
    {
        Wait,
        MovingToParent,
        Wander,
    }

    public class MoteViviFairy : Mote
    {
        private const float ZSwayIntervalTick = 30;
        private const float ZSwayIntervalOffset = 0.2f;

        private Pawn _parent;

        private Vector3 _curPosition;
        private Vector3? _destOffset;

        protected override bool EndOfLife => false;

        public override Vector3 DrawPos
        {
            get
            {
                var hash = GetHashCode();
                if (hash == int.MinValue) { hash++; }

                var y = (float)Mathf.Abs(hash) / int.MaxValue * Altitudes.AltInc;

                return _curPosition + new Vector3(0f, y, 0f);
            }
        }

        public void Initialize(Pawn parent)
        {
            _parent = parent;

            _curPosition = parent.Position.ToVector3Shifted() + new Vector3(Rand.Range(-0.6f, 0.6f), 0f, Rand.Range(0.1f, 0.4f));
        }

        protected override void Tick()
        {
            var ticksGame = GenTicks.TicksGame;
            base.Tick();

            var moved = false;
            var vectorToParent = _parent.TrueCenter() - _curPosition;
            var distanceToParentSqr = vectorToParent.sqrMagnitude;
            if (distanceToParentSqr > 1)
            {
                if (_destOffset == null)
                {
                    _destOffset = new Vector3(Rand.Range(-0.6f, 0.6f), 0f, Rand.Range(0.1f, 0.4f));
                }

                var speed = 0.1f;
                var normalized = (vectorToParent + _destOffset.Value).normalized;
                if (distanceToParentSqr > 4)
                {
                    speed = _parent.GetStatValue(StatDefOf.MoveSpeed) / 60f * 1.1f;
                }

                _curPosition += normalized * speed;
                moved = true;
            }
            else
            {
                if (_destOffset.HasValue)
                {
                    _destOffset = null;
                }
            }

            // Z-Swaying
            if (moved)
            {
                var swayTick = ZSwayIntervalTick / 2;
                _curPosition += new Vector3(
                    0f,
                    0f,
                    ticksGame % (swayTick * 2) >= swayTick ?
                    (ZSwayIntervalOffset / swayTick) :
                    -(ZSwayIntervalOffset / swayTick));
            }
            else
            {
                var swayTick = ZSwayIntervalTick;
                _curPosition += new Vector3(
                    0f,
                    0f,
                    ticksGame % (swayTick * 2) >= swayTick ?
                    (ZSwayIntervalOffset / swayTick) :
                    -(ZSwayIntervalOffset / swayTick));
            }
        }
    }
}
