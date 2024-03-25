using System;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class LineRendererManager
    {
        public static GameObject LineRendererObject
        {
            get
            {
                if (_lineRendererObject == null)
                {
                    _lineRendererObject = new GameObject("VVRace_LineRendererObject", new Type[] { typeof(LineRenderer) });
                    _lineRendererObject.AddComponent<LineRenderer>();

                    _lineRenderer = _lineRendererObject.GetComponent<LineRenderer>();
                    _lineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    _lineRenderer.forceRenderingOff = true;
                    _lineRenderer.receiveShadows = false;
                    _lineRenderer.generateLightingData = false;
                }

                return _lineRendererObject;
            }
        }
        private static GameObject _lineRendererObject;

        public static LineRenderer LineRenderer
        {
            get
            {
                if (LineRendererObject == null) { return null; }

                return _lineRenderer;
            }
        }
        private static LineRenderer _lineRenderer;
    }
}
