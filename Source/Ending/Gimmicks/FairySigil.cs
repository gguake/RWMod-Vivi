using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public sealed class FairySigil
    {
        public const float WorldRadius = 5.7f;

        private const float HalfPi = Mathf.PI * 0.5f;
        private const float Tau = Mathf.PI * 2f;

        private static FairySigil _daffodil;
        private static FairySigil _asphodel;

        public readonly string name;

        private readonly List<SigilStroke> _strokes;
        private readonly float[] _strokeStarts;
        private readonly Dictionary<int, FairySigilLanes> _lanes = new Dictionary<int, FairySigilLanes>();

        public IReadOnlyList<SigilStroke> Strokes => _strokes;
        public float TotalLength { get; }

        private FairySigil(string name, List<SigilStroke> frame, List<SigilStroke> emblem)
        {
            this.name = name;

            var all = new List<SigilStroke>(frame.Count + emblem.Count);
            all.AddRange(frame);
            all.AddRange(emblem);
            _strokes = new List<SigilStroke>(all.Count);
            for (int kind = (int)SigilStrokeKind.Ring; kind <= (int)SigilStrokeKind.Rune; kind++)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if ((int)all[i].kind == kind) { _strokes.Add(all[i]); }
                }
            }

            _strokeStarts = new float[_strokes.Count];
            float total = 0f;
            for (int i = 0; i < _strokes.Count; i++)
            {
                _strokeStarts[i] = total;
                total += _strokes[i].Length;
            }
            TotalLength = total;
        }

        public static FairySigil Daffodil => _daffodil ??= new FairySigil("Daffodil", Frame(), DaffodilEmblem());

        public static FairySigil Asphodel => _asphodel ??= new FairySigil("Asphodel", AsphodelFrame(), AsphodelEmblem());

        public void Sample(float distance, out Vector2 position, out Vector2 tangent)
        {
            distance = Mathf.Clamp(distance, 0f, TotalLength);
            int i = _strokes.Count - 1;
            while (i > 0 && _strokeStarts[i] > distance) { i--; }
            _strokes[i].Sample(distance - _strokeStarts[i], out position, out tangent);
        }

        public FairySigilLanes GetLanes(int count)
        {
            count = Mathf.Clamp(count, 1, 16);
            if (!_lanes.TryGetValue(count, out var lanes))
            {
                lanes = new FairySigilLanes(this, count);
                _lanes[count] = lanes;
            }
            return lanes;
        }

        public static Vector3 ToWorld(Vector3 center, Vector2 local, float radius = WorldRadius)
        {
            return new Vector3(center.x + local.x * radius, center.y, center.z + local.y * radius);
        }

        private static List<SigilStroke> Frame()
        {
            var strokes = new List<SigilStroke>
            {
                new SigilStroke(Circle(Vector2.zero, 1f, 120), true, SigilStrokeKind.Ring, 0.85f),
                new SigilStroke(Circle(Vector2.zero, 0.88f, 108), true, SigilStrokeKind.InnerRing, 0.7f),
            };
            for (int k = 0; k < 12; k++)
            {
                var angle = HalfPi + Mathf.PI / 12f + k * Mathf.PI / 6f;
                if (k % 3 == 1)
                {
                    strokes.Add(new SigilStroke(Circle(Polar(0.94f, angle), 0.05f, 14), true, SigilStrokeKind.Rune, 0.75f));
                }
                else
                {
                    strokes.Add(new SigilStroke(new[] { Polar(0.905f, angle), Polar(0.975f, angle) }, false, SigilStrokeKind.Rune, 0.6f));
                }
            }
            return strokes;
        }

        private static List<SigilStroke> DaffodilEmblem()
        {
            const int petals = 6;
            const float step = Tau / petals;
            var strokes = new List<SigilStroke>();

            // 외곽 장식: 꽃잎 끝을 잇는 여섯 갈래 아치(단일 닫힌 고리로 이어 끝점 캡 중첩을 방지)와 보조 꽃받침 잎맥
            var archPoints = new List<Vector2>(petals * 14);
            for (int k = 0; k < petals; k++)
            {
                var a0 = HalfPi + k * step;
                var a1 = HalfPi + (k + 1) * step;
                var mid = a0 + step * 0.5f;

                var p0 = Polar(0.82f, a0);
                var p1 = Polar(0.82f, a1);
                var c0 = Polar(0.68f, a0 + step * 0.25f);
                var c1 = Polar(0.68f, a1 - step * 0.25f);
                var seg = Bezier(p0, c0, c1, p1, 14);
                for (int i = 0; i < seg.Count - 1; i++)
                {
                    archPoints.Add(seg[i]);
                }

                // 꽃잎 사이의 보조 화피와 작은 마름모 별장식
                strokes.Add(new SigilStroke(Leaf(Polar(0.32f, mid), Polar(0.66f, mid), 0.16f), true, SigilStrokeKind.Emblem, 0.7f));
                strokes.Add(new SigilStroke(Regular(Polar(0.75f, mid), 0.055f, 4, mid), true, SigilStrokeKind.Rune, 0.72f));
            }
            strokes.Add(new SigilStroke(archPoints, true, SigilStrokeKind.InnerRing, 0.62f));

            // 수선화의 여섯 장 뾰족한 주 꽃잎(외곽·내부 이중 윤곽·중앙 잎맥)
            for (int k = 0; k < petals; k++)
            {
                var angle = HalfPi + k * step;
                strokes.Add(new SigilStroke(Leaf(Polar(0.27f, angle), Polar(0.82f, angle), 0.31f), true, SigilStrokeKind.Emblem, 0.95f));
                strokes.Add(new SigilStroke(Leaf(Polar(0.32f, angle), Polar(0.69f, angle), 0.17f), true, SigilStrokeKind.Emblem, 0.65f));
                strokes.Add(new SigilStroke(new[] { Polar(0.29f, angle), Polar(0.75f, angle) }, false, SigilStrokeKind.Emblem, 0.58f));

                // 꽃잎 끝 바깥 작은 다이아몬드 점
                strokes.Add(new SigilStroke(Circle(Polar(0.85f, angle), 0.028f, 10), true, SigilStrokeKind.Rune, 0.75f));
            }

            // 수선화 특유의 물결치는 부화관(나팔관) 외곽과 내부 물결 고리
            strokes.Add(new SigilStroke(RuffledCircle(Vector2.zero, 0.265f, 0.042f, 12, 96, HalfPi), true, SigilStrokeKind.Emblem, 0.95f));
            strokes.Add(new SigilStroke(RuffledCircle(Vector2.zero, 0.185f, 0.018f, 6, 60, HalfPi + step * 0.5f), true, SigilStrokeKind.Emblem, 0.72f));

            // 부화관 주름선과 6개의 수술·중심 암술
            for (int k = 0; k < petals; k++)
            {
                var angle = HalfPi + k * step;
                var halfAngle = angle + step * 0.5f;
                strokes.Add(new SigilStroke(new[] { Polar(0.19f, angle), Polar(0.30f, angle) }, false, SigilStrokeKind.Emblem, 0.6f));
                strokes.Add(new SigilStroke(new[] { Polar(0.075f, halfAngle), Polar(0.155f, halfAngle) }, false, SigilStrokeKind.Emblem, 0.65f));
                strokes.Add(new SigilStroke(Circle(Polar(0.17f, halfAngle), 0.022f, 8), true, SigilStrokeKind.Rune, 0.75f));
            }

            strokes.Add(new SigilStroke(Circle(Vector2.zero, 0.072f, 24), true, SigilStrokeKind.Emblem, 0.88f));
            return strokes;
        }

        private static List<SigilStroke> AsphodelFrame()
        {
            const int petals = 6;
            const float step = Tau / petals;
            const float bandStep = Tau / 12f;

            var strokes = new List<SigilStroke>
            {
                new SigilStroke(Circle(Vector2.zero, 1f, 144), true, SigilStrokeKind.Ring, 0.92f),
                new SigilStroke(Circle(Vector2.zero, 0.968f, 144), true, SigilStrokeKind.Ring, 0.56f),
                new SigilStroke(Circle(Vector2.zero, 0.862f, 128), true, SigilStrokeKind.InnerRing, 0.82f),
                new SigilStroke(Circle(Vector2.zero, 0.832f, 120), true, SigilStrokeKind.InnerRing, 0.52f),
            };

            for (int k = 0; k < 12; k++)
            {
                var a0 = HalfPi + k * bandStep;
                var a1 = HalfPi + (k + 1) * bandStep;
                var s0 = a0 + bandStep * 0.17f;
                var s1 = a1 - bandStep * 0.17f;
                var mid = (a0 + a1) * 0.5f;
                strokes.Add(new SigilStroke(BandLeaf(0.915f, 0.035f, s0, s1, 10), true, SigilStrokeKind.Rune, 0.6f));
                strokes.Add(new SigilStroke(new[] { Polar(0.892f, mid), Polar(0.938f, mid) }, false, SigilStrokeKind.Rune, 0.54f));
            }

            for (int k = 0; k < petals; k++)
            {
                var angle = HalfPi + k * step;
                var mid = angle + step * 0.5f;

                strokes.Add(new SigilStroke(Regular(Polar(0.915f, angle), 0.039f, 4, angle), true, SigilStrokeKind.Rune, 0.88f));
                strokes.Add(new SigilStroke(Circle(Polar(0.915f, angle), 0.014f, 10), true, SigilStrokeKind.Rune, 0.78f));

                strokes.Add(new SigilStroke(Circle(Polar(0.915f, mid), 0.037f, 16), true, SigilStrokeKind.Rune, 0.84f));
                strokes.Add(new SigilStroke(Circle(Polar(0.915f, mid), 0.016f, 10), true, SigilStrokeKind.Rune, 0.74f));
            }

            return strokes;
        }

        private static List<SigilStroke> AsphodelEmblem()
        {
            const int petals = 6;
            const float step = Tau / petals;
            var strokes = new List<SigilStroke>();

            var outerArch = new List<Vector2>(petals * 18);
            var subArch = new List<Vector2>(petals * 12);
            for (int k = 0; k < petals; k++)
            {
                var a0 = HalfPi + k * step;
                var a1 = HalfPi + (k + 1) * step;
                var mid = a0 + step * 0.5f;

                var segA = Bezier(Polar(0.818f, a0), Polar(0.755f, a0 + step * 0.17f), Polar(0.765f, mid - step * 0.12f), Polar(0.718f, mid), 9);
                for (int i = 0; i < segA.Count - 1; i++) { outerArch.Add(segA[i]); }
                var segB = Bezier(Polar(0.718f, mid), Polar(0.765f, mid + step * 0.12f), Polar(0.755f, a1 - step * 0.17f), Polar(0.818f, a1), 9);
                for (int i = 0; i < segB.Count - 1; i++) { outerArch.Add(segB[i]); }

                var segSub = Bezier(Polar(0.818f, a0), Polar(0.675f, a0 + step * 0.24f), Polar(0.675f, a1 - step * 0.24f), Polar(0.818f, a1), 12);
                for (int i = 0; i < segSub.Count - 1; i++) { subArch.Add(segSub[i]); }
            }
            strokes.Add(new SigilStroke(outerArch, true, SigilStrokeKind.InnerRing, 0.68f));
            strokes.Add(new SigilStroke(subArch, true, SigilStrokeKind.InnerRing, 0.46f));

            // 아스포델의 6장 뾰족한 별 모양 주 꽃잎(외곽·내부 윤곽·선명한 중앙 용골맥·대칭 측맥)
            for (int k = 0; k < petals; k++)
            {
                var angle = HalfPi + k * step;
                strokes.Add(new SigilStroke(AsphodelPetal(Polar(0.185f, angle), Polar(0.818f, angle), 0.265f, 16), true, SigilStrokeKind.Emblem, 1f));
                strokes.Add(new SigilStroke(AsphodelPetal(Polar(0.245f, angle), Polar(0.690f, angle), 0.145f, 14), true, SigilStrokeKind.Emblem, 0.68f));
                strokes.Add(new SigilStroke(new[] { Polar(0.195f, angle), Polar(0.790f, angle) }, false, SigilStrokeKind.Emblem, 0.88f));

                for (int s = -1; s <= 1; s += 2)
                {
                    var vein = Bezier(
                        Polar(0.29f, angle),
                        Polar(0.41f, angle + s * 0.085f),
                        Polar(0.52f, angle + s * 0.115f),
                        Polar(0.60f, angle + s * 0.072f),
                        8);
                    strokes.Add(new SigilStroke(vein, false, SigilStrokeKind.Emblem, 0.52f));
                }
            }

            // 꽃잎 사이로 뻗어나오는 아스포델의 긴 수술·곡선 부수술·길쭉한 꽃밥과 기저 꽃받침
            for (int k = 0; k < petals; k++)
            {
                var mid = HalfPi + (k + 0.5f) * step;

                strokes.Add(new SigilStroke(AsphodelPetal(Polar(0.195f, mid), Polar(0.435f, mid), 0.23f, 12), true, SigilStrokeKind.Emblem, 0.74f));
                strokes.Add(new SigilStroke(new[] { Polar(0.21f, mid), Polar(0.385f, mid) }, false, SigilStrokeKind.Emblem, 0.50f));
                strokes.Add(new SigilStroke(new[] { Polar(0.435f, mid), Polar(0.575f, mid) }, false, SigilStrokeKind.Emblem, 0.76f));

                for (int s = -1; s <= 1; s += 2)
                {
                    var curl = Bezier(
                        Polar(0.39f, mid + s * 0.04f),
                        Polar(0.485f, mid + s * 0.155f),
                        Polar(0.565f, mid + s * 0.165f),
                        Polar(0.595f, mid + s * 0.095f),
                        10);
                    strokes.Add(new SigilStroke(curl, false, SigilStrokeKind.Emblem, 0.58f));
                    strokes.Add(new SigilStroke(Circle(Polar(0.612f, mid + s * 0.095f), 0.016f, 10), true, SigilStrokeKind.Rune, 0.74f));
                }

                strokes.Add(new SigilStroke(Leaf(Polar(0.57f, mid), Polar(0.692f, mid), 0.36f, 10), true, SigilStrokeKind.Emblem, 0.92f));
                strokes.Add(new SigilStroke(Regular(Polar(0.772f, mid), 0.036f, 4, mid), true, SigilStrokeKind.Rune, 0.82f));
            }

            // 중심 화심: 원형 화반·6엽 물결 고리·6각 별심
            strokes.Add(new SigilStroke(Circle(Vector2.zero, 0.185f, 48), true, SigilStrokeKind.Emblem, 0.88f));
            strokes.Add(new SigilStroke(RuffledCircle(Vector2.zero, 0.155f, 0.014f, 6, 48, HalfPi), true, SigilStrokeKind.Emblem, 0.66f));
            var starPoints = new List<Vector2>(petals * 2);
            for (int k = 0; k < petals; k++)
            {
                var a = HalfPi + k * step;
                var m = a + step * 0.5f;
                starPoints.Add(Polar(0.115f, a));
                starPoints.Add(Polar(0.058f, m));
            }
            strokes.Add(new SigilStroke(starPoints, true, SigilStrokeKind.Emblem, 0.82f));
            return strokes;
        }

        private static Vector2 Polar(float radius, float angle)
        {
            return new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
        }

        private static List<Vector2> Arc(Vector2 center, float radius, float from, float to, int segments)
        {
            var points = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                points.Add(center + Polar(radius, from + (to - from) * i / segments));
            }
            return points;
        }

        private static List<Vector2> Circle(Vector2 center, float radius, int segments)
        {
            var points = Arc(center, radius, HalfPi, HalfPi + Tau, segments);
            points.RemoveAt(points.Count - 1);
            return points;
        }

        private static List<Vector2> RuffledCircle(Vector2 center, float baseRadius, float amplitude, int lobes, int segments, float startAngle)
        {
            var points = new List<Vector2>(segments);
            for (int i = 0; i < segments; i++)
            {
                var t = (float)i / segments;
                var angle = startAngle + Tau * t;
                var radius = baseRadius + amplitude * Mathf.Cos(lobes * Tau * t);
                points.Add(center + Polar(radius, angle));
            }
            return points;
        }

        private static List<Vector2> Regular(Vector2 center, float radius, int sides, float startAngle)
        {
            var points = new List<Vector2>(sides);
            for (int i = 0; i < sides; i++)
            {
                points.Add(center + Polar(radius, startAngle + Tau * i / sides));
            }
            return points;
        }

        private static List<Vector2> Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int segments)
        {
            var points = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                points.Add(RimWorld.Bezier.EvaluateCubic(p0, p1, p2, p3, (float)i / segments));
            }
            return points;
        }

        private static List<Vector2> Leaf(Vector2 stemBase, Vector2 tip, float bulge, int segments = 14)
        {
            var direction = tip - stemBase;
            var normal = new Vector2(-direction.y, direction.x) * bulge;
            var near = Vector2.LerpUnclamped(stemBase, tip, 0.25f);
            var far = Vector2.LerpUnclamped(stemBase, tip, 0.7f);
            var points = Bezier(stemBase, near + normal, far + normal, tip, segments);
            var back = Bezier(tip, far - normal, near - normal, stemBase, segments);
            for (int i = 1; i < back.Count - 1; i++)
            {
                points.Add(back[i]);
            }
            return points;
        }

        private static List<Vector2> AsphodelPetal(Vector2 stemBase, Vector2 tip, float bulge, int segments = 16)
        {
            var direction = tip - stemBase;
            var normal = new Vector2(-direction.y, direction.x) * bulge;
            var near = Vector2.LerpUnclamped(stemBase, tip, 0.26f);
            var far = Vector2.LerpUnclamped(stemBase, tip, 0.62f);
            var points = Bezier(stemBase, near + normal, far + normal * 0.88f, tip, segments);
            var back = Bezier(tip, far - normal * 0.88f, near - normal, stemBase, segments);
            for (int i = 1; i < back.Count - 1; i++)
            {
                points.Add(back[i]);
            }
            return points;
        }

        private static List<Vector2> BandLeaf(float centerRadius, float halfThickness, float a0, float a1, int segments)
        {
            var points = new List<Vector2>(segments * 2);
            for (int i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var angle = a0 + (a1 - a0) * t;
                var radius = centerRadius + halfThickness * Mathf.Sin(t * Mathf.PI);
                points.Add(Polar(radius, angle));
            }
            for (int i = 1; i < segments; i++)
            {
                var t = 1f - (float)i / segments;
                var angle = a0 + (a1 - a0) * t;
                var radius = centerRadius - halfThickness * Mathf.Sin(t * Mathf.PI);
                points.Add(Polar(radius, angle));
            }
            return points;
        }
    }
}
