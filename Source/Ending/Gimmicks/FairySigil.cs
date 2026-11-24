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
        private static FairySigil _everflower;

        public readonly string name;

        private readonly List<SigilStroke> _strokes;
        private readonly float[] _strokeStarts;
        private readonly Dictionary<int, FairySigilLanes> _lanes = new Dictionary<int, FairySigilLanes>();

        public IReadOnlyList<SigilStroke> Strokes => _strokes;
        public float TotalLength { get; }

        private FairySigil(string name, List<SigilStroke> emblem)
        {
            this.name = name;

            var all = Frame();
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

        public static FairySigil Daffodil => _daffodil ??= new FairySigil("Daffodil", DaffodilEmblem());

        public static FairySigil Everflower => _everflower ??= new FairySigil("Everflower", EverflowerEmblem());

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

        private static List<SigilStroke> EverflowerEmblem()
        {
            const int petals = 6;
            var strokes = new List<SigilStroke>();
            for (int k = 0; k < petals; k++)
            {
                var angle = HalfPi + k * Tau / petals;
                strokes.Add(new SigilStroke(Leaf(Polar(0.15f, angle), Polar(0.7f, angle), 0.26f), true, SigilStrokeKind.Emblem));
            }
            for (int k = 0; k < petals; k++)
            {
                var angle = HalfPi + (k + 0.5f) * Tau / petals;
                strokes.Add(Emblem(false, Polar(0.2f, angle), Polar(0.42f, angle)));
                strokes.Add(new SigilStroke(Circle(Polar(0.47f, angle), 0.05f, 12), true, SigilStrokeKind.Emblem));
            }
            strokes.Add(new SigilStroke(Circle(Vector2.zero, 0.11f, 24), true, SigilStrokeKind.Emblem));
            return strokes;
        }

        private static SigilStroke Emblem(bool closed, params Vector2[] points)
        {
            return new SigilStroke(points, closed, SigilStrokeKind.Emblem);
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

        private static List<Vector2> Leaf(Vector2 stemBase, Vector2 tip, float bulge)
        {
            const int segments = 14;
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
    }
}
