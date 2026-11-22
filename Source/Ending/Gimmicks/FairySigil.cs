using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public sealed class FairySigil
    {
        public const float WorldRadius = 5.7f;

        private const float HalfPi = Mathf.PI * 0.5f;
        private const float Tau = Mathf.PI * 2f;

        private static Dictionary<SkillDef, FairySigil> _cache;
        private static FairySigil _everflower;

        public readonly SkillDef skill;
        public readonly string name;

        private readonly List<SigilStroke> _strokes;
        private readonly float[] _strokeStarts;
        private readonly Dictionary<int, FairySigilLanes> _lanes = new Dictionary<int, FairySigilLanes>();

        public IReadOnlyList<SigilStroke> Strokes => _strokes;
        public float TotalLength { get; }

        private FairySigil(SkillDef skill, List<SigilStroke> emblem) : this(skill.defName, emblem)
        {
            this.skill = skill;
        }

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

        public static IEnumerable<SkillDef> SupportedSkills
        {
            get
            {
                EnsureCache();
                return _cache.Keys;
            }
        }

        public static FairySigil Everflower => _everflower ??= new FairySigil("Everflower", EverflowerEmblem());

        public static FairySigil For(SkillDef skill)
        {
            if (skill == null) { return null; }
            EnsureCache();
            return _cache.TryGetValue(skill, out var sigil) ? sigil : null;
        }

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

        private static void EnsureCache()
        {
            if (_cache != null) { return; }
            _cache = new Dictionary<SkillDef, FairySigil>
            {
                { SkillDefOf.Melee, new FairySigil(SkillDefOf.Melee, Sword()) },
                { SkillDefOf.Construction, new FairySigil(SkillDefOf.Construction, Honeycomb()) },
                { SkillDefOf.Crafting, new FairySigil(SkillDefOf.Crafting, Gear()) },
                { SkillDefOf.Plants, new FairySigil(SkillDefOf.Plants, Sprout()) },
                { SkillDefOf.Social, new FairySigil(SkillDefOf.Social, SpeechBubble()) },
                { SkillDefOf.Medicine, new FairySigil(SkillDefOf.Medicine, MedicalCross()) },
                { SkillDefOf.Intellectual, new FairySigil(SkillDefOf.Intellectual, Hexagram()) },
            };
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

        private static List<SigilStroke> Sword()
        {
            return new List<SigilStroke>
            {
                Emblem(true, new Vector2(0f, 0.72f), new Vector2(0.1f, 0.56f), new Vector2(0.1f, -0.12f), new Vector2(-0.1f, -0.12f), new Vector2(-0.1f, 0.56f)),
                Emblem(false, new Vector2(-0.32f, -0.04f), new Vector2(-0.25f, -0.14f), new Vector2(0.25f, -0.14f), new Vector2(0.32f, -0.04f)),
                Emblem(false, new Vector2(0f, -0.14f), new Vector2(0f, -0.46f)),
                new SigilStroke(Regular(new Vector2(0f, -0.55f), 0.09f, 4, HalfPi), true, SigilStrokeKind.Emblem),
            };
        }

        private static List<SigilStroke> Honeycomb()
        {
            const float size = 0.33f;
            const float step = Mathf.PI / 3f;
            var outline = new List<Vector2>();
            var strokes = new List<SigilStroke> { null };
            for (int k = 0; k < 3; k++)
            {
                var angle = HalfPi + k * Tau / 3f;
                var center = Polar(size, angle);
                var origin = Mathf.RoundToInt((angle + Mathf.PI - step * 0.5f) / step);
                for (int j = 1; j <= 4; j++)
                {
                    outline.Add(center + Polar(size, step * 0.5f + (origin + j) * step));
                }
                strokes.Add(Emblem(false, Vector2.zero, center + Polar(size, step * 0.5f + (origin + 1) * step)));
            }
            strokes[0] = new SigilStroke(outline, true, SigilStrokeKind.Emblem);
            return strokes;
        }

        private static List<SigilStroke> Gear()
        {
            const int teeth = 8;
            const float outer = 0.64f;
            const float root = 0.5f;
            const float pitch = Tau / teeth;
            var points = new List<Vector2>();
            for (int k = 0; k < teeth; k++)
            {
                var angle = HalfPi + k * pitch;
                points.Add(Polar(root, angle - pitch * 0.3f));
                points.Add(Polar(outer, angle - pitch * 0.17f));
                points.Add(Polar(outer, angle + pitch * 0.17f));
                points.Add(Polar(root, angle + pitch * 0.3f));
                var valley = Arc(Vector2.zero, root, angle + pitch * 0.3f, angle + pitch * 0.7f, 3);
                for (int i = 1; i < valley.Count - 1; i++)
                {
                    points.Add(valley[i]);
                }
            }
            return new List<SigilStroke>
            {
                new SigilStroke(points, true, SigilStrokeKind.Emblem),
                new SigilStroke(Circle(Vector2.zero, 0.22f, 36), true, SigilStrokeKind.Emblem),
            };
        }

        private static List<SigilStroke> Sprout()
        {
            var stemBase = new Vector2(0f, 0.06f);
            return new List<SigilStroke>
            {
                new SigilStroke(Bezier(new Vector2(0f, -0.56f), new Vector2(0.07f, -0.36f), new Vector2(-0.07f, -0.14f), stemBase, 16), false, SigilStrokeKind.Emblem),
                new SigilStroke(Leaf(stemBase, new Vector2(-0.54f, 0.4f), 0.3f), true, SigilStrokeKind.Emblem),
                new SigilStroke(Leaf(stemBase, new Vector2(0.48f, 0.56f), 0.3f), true, SigilStrokeKind.Emblem),
                new SigilStroke(Arc(new Vector2(0f, -1.25f), 0.7f, HalfPi + 0.44f, HalfPi - 0.44f, 14), false, SigilStrokeKind.Emblem),
            };
        }

        private static List<SigilStroke> SpeechBubble()
        {
            const float left = -0.56f;
            const float right = 0.56f;
            const float bottom = -0.22f;
            const float top = 0.44f;
            const float corner = 0.16f;
            var points = new List<Vector2>();
            points.AddRange(Arc(new Vector2(right - corner, top - corner), corner, 0f, HalfPi, 6));
            points.AddRange(Arc(new Vector2(left + corner, top - corner), corner, HalfPi, Mathf.PI, 6));
            points.AddRange(Arc(new Vector2(left + corner, bottom + corner), corner, Mathf.PI, Mathf.PI * 1.5f, 6));
            points.Add(new Vector2(-0.3f, bottom));
            points.Add(new Vector2(-0.38f, -0.54f));
            points.Add(new Vector2(-0.06f, bottom));
            points.AddRange(Arc(new Vector2(right - corner, bottom + corner), corner, Mathf.PI * 1.5f, Tau, 6));

            var strokes = new List<SigilStroke> { new SigilStroke(points, true, SigilStrokeKind.Emblem) };
            for (int i = -1; i <= 1; i++)
            {
                strokes.Add(new SigilStroke(Circle(new Vector2(i * 0.26f, 0.11f), 0.065f, 16), true, SigilStrokeKind.Emblem));
            }
            return strokes;
        }

        private static List<SigilStroke> MedicalCross()
        {
            const float arm = 0.6f;
            const float half = 0.19f;
            return new List<SigilStroke>
            {
                Emblem(true,
                    new Vector2(-half, arm), new Vector2(half, arm), new Vector2(half, half), new Vector2(arm, half),
                    new Vector2(arm, -half), new Vector2(half, -half), new Vector2(half, -arm), new Vector2(-half, -arm),
                    new Vector2(-half, -half), new Vector2(-arm, -half), new Vector2(-arm, half), new Vector2(-half, half)),
            };
        }

        private static List<SigilStroke> Hexagram()
        {
            return new List<SigilStroke>
            {
                new SigilStroke(Regular(Vector2.zero, 0.68f, 3, HalfPi), true, SigilStrokeKind.Emblem),
                new SigilStroke(Regular(Vector2.zero, 0.68f, 3, -HalfPi), true, SigilStrokeKind.Emblem),
                new SigilStroke(Circle(Vector2.zero, 0.13f, 30), true, SigilStrokeKind.Emblem),
            };
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
