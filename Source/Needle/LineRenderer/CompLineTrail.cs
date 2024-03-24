using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompProperties_LineTrail : CompProperties
    {
        public int maxTrailPoint = 10;
        public int refreshInterval = 30;

        public string texPath;
        public ShaderTypeDef shader;
        public Color color;
        public SimpleCurve widthCurve;
        public LineTextureMode lineTextureMode = LineTextureMode.Stretch;

        [Unsaved]
        public Material material;

        [Unsaved]
        public AnimationCurve aniCurve;

        public CompProperties_LineTrail()
        {
            compClass = typeof(CompLineTrail);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);

            LongEventHandler.ExecuteWhenFinished(delegate
            {
                if (texPath != null)
                {
                    material = MaterialPool.MatFrom(texPath, shader.Shader, color);
                }

                if (widthCurve != null)
                {
                    aniCurve = widthCurve.ToAnimationCurve();
                }
            });
        }
    }

    public class CompLineTrail : ThingComp
    {
        private Vector3[] points;
        private Mesh mesh;
        private bool trailStart;

        public CompProperties_LineTrail Props => (CompProperties_LineTrail)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            points = new Vector3[Props.maxTrailPoint];
        }

        public override void CompTick()
        {
            if (!parent.Spawned)
            {
                trailStart = false;
                return;
            }

            var position = parent.DrawPos;
            if (!trailStart)
            {
                for (int i = 0; i < points.Length; ++i)
                {
                    points[i] = position;
                }

                trailStart = true;
            }

            if (parent.IsHashIntervalTick(Props.refreshInterval))
            {
                for (int i = points.Length - 1; i >= 1; i--)
                {
                    points[i] = points[i - 1];
                }
                points[0] = position;

                var lineRenderer = LineRendererManager.LineRenderer;
                lineRenderer.positionCount = points.Length;
                lineRenderer.material = Props.material;
                lineRenderer.textureMode = Props.lineTextureMode;
                lineRenderer.widthCurve = Props.aniCurve;

                for (int i = 0; i < points.Length; ++i)
                {
                    lineRenderer.SetPosition(i, points[points.Length - 1 - i]);
                }

                if (mesh == null)
                {
                    mesh = new Mesh();
                }
                lineRenderer.BakeMesh(mesh, Find.Camera);
            }
        }

        public override void PostDraw()
        {
            if (mesh != null)
            {
                Graphics.DrawMesh(mesh, new Vector3(), Quaternion.identity, Props.material, 0);
            }
        }
    }
}
