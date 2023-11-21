using UnityEngine;
using Verse;

namespace VVRace
{
    public class ShootusTop
    {
        [TweakValue("ShootusTop_PositionOffsetX", -1f, 1f)]
        public static float PositionOffsetX = 0.2f;
        [TweakValue("ShootusTop_PositionOffsetZ", -1f, 1f)]
        public static float PositionOffsetZ = 0.45f;

        private const float IdleTurnDegreesPerTick = 0.3f;
        private const int IdleTurnDuration = 140;
        private const int IdleTurnIntervalMin = 150;
        private const int IdleTurnIntervalMax = 350;

        private Shootus parent;

        private float curRotationInt = Rand.Range(0f, 360f);
        private int ticksUntilIdleTurn;
        private int idleTurnTicksLeft;
        private bool idleTurnClockwise = Rand.Bool;

        public float CurRotation
        {
            get
            {
                return curRotationInt;
            }
            set
            {
                curRotationInt = value;
                if (curRotationInt > 360f)
                {
                    curRotationInt -= 360f;
                }
                if (curRotationInt < 0f)
                {
                    curRotationInt += 360f;
                }
            }
        }

        public void SetRotationFromOrientation()
        {
            CurRotation = parent.Rotation.AsAngle;
        }

        public ShootusTop(Shootus parent)
        {
            this.parent = parent;
        }

        public void ForceFaceTarget(LocalTargetInfo targ)
        {
            if (parent.Gun == null) { return; }

            if (targ.IsValid)
            {
                CurRotation = (targ.Cell.ToVector3Shifted() - parent.DrawPos).AngleFlat();
            }
        }

        public void TurretTopTick()
        {
            if (parent.Gun == null) { return; }

            var currentTarget = parent.CurrentTarget;
            if (currentTarget.IsValid)
            {
                CurRotation = (currentTarget.Cell.ToVector3Shifted() - parent.DrawPos).AngleFlat();
                ticksUntilIdleTurn = Rand.RangeInclusive(IdleTurnIntervalMin, IdleTurnIntervalMax);
            }

            else if (ticksUntilIdleTurn > 0)
            {
                ticksUntilIdleTurn--;
                if (ticksUntilIdleTurn == 0)
                {
                    if (Rand.Value < 0.5f)
                    {
                        idleTurnClockwise = true;
                    }
                    else
                    {
                        idleTurnClockwise = false;
                    }

                    idleTurnTicksLeft = IdleTurnDuration;
                }
            }
            else
            {
                if (idleTurnClockwise)
                {
                    CurRotation += IdleTurnDegreesPerTick;
                }
                else
                {
                    CurRotation -= IdleTurnDegreesPerTick;
                }

                idleTurnTicksLeft--;
                if (idleTurnTicksLeft <= 0)
                {
                    ticksUntilIdleTurn = Rand.RangeInclusive(IdleTurnIntervalMin, IdleTurnIntervalMax);
                }
            }
        }

        public void DrawTurret(Vector3 recoilDrawOffset, float recoilAngleOffset)
        {
            if (parent.Gun == null) { return; }

            var v = new Vector3(parent.def.building.turretTopOffset.x, 0f, parent.def.building.turretTopOffset.y).RotatedBy(CurRotation);
            float turretTopDrawSize = parent.def.building.turretTopDrawSize;

            v = v.RotatedBy(recoilAngleOffset);
            v += recoilDrawOffset;
            v += new Vector3(PositionOffsetX, 0f, PositionOffsetZ);

            float rot = parent.CurrentEffectiveVerb?.AimAngleOverride ?? CurRotation;
            var matrix = default(Matrix4x4);

            matrix.SetTRS(parent.DrawPos + Altitudes.AltIncVect * 5f + v, (-90f + rot).ToQuat(), new Vector3(turretTopDrawSize, 1f, turretTopDrawSize));
            Graphics.DrawMesh(MeshPool.plane10, matrix, parent.Gun.Graphic.MatSingleFor(parent.Gun), 0);
        }
    }
}
