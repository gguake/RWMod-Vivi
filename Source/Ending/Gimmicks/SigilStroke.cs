using System.Collections.Generic;
using UnityEngine;

namespace VVRace
{
    public enum SigilStrokeKind
    {
        Ring,
        Emblem,
        InnerRing,
        Rune,
    }

    public readonly struct SigilSpanPoint
    {
        public readonly Vector2 position;
        public readonly float distance;
        public readonly Vector2 offset;

        public SigilSpanPoint(Vector2 position, float distance, Vector2 offset)
        {
            this.position = position;
            this.distance = distance;
            this.offset = offset;
        }
    }

    public sealed class SigilStroke
    {
        private const float MinMiterCos = 0.4f;
        private const float Epsilon = 1e-5f;

        public readonly SigilStrokeKind kind;
        public readonly bool closed;
        public readonly float weight;

        private readonly Vector2[] _path;
        private readonly float[] _cumulative;
        private readonly Vector2[] _offsets;

        public float Length => _cumulative[_cumulative.Length - 1];
        internal Vector2[] Path => _path;
        internal float[] Cumulative => _cumulative;

        public SigilStroke(IList<Vector2> points, bool closed, SigilStrokeKind kind, float weight = 1f)
        {
            this.kind = kind;
            this.closed = closed;
            this.weight = weight;

            var count = points.Count + (closed ? 1 : 0);
            _path = new Vector2[count];
            for (int i = 0; i < points.Count; i++)
            {
                _path[i] = points[i];
            }
            if (closed)
            {
                _path[count - 1] = points[0];
            }

            _cumulative = new float[count];
            for (int i = 1; i < count; i++)
            {
                _cumulative[i] = _cumulative[i - 1] + Vector2.Distance(_path[i - 1], _path[i]);
            }

            _offsets = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 tangentIn;
                Vector2 tangentOut;
                if (closed)
                {
                    var prev = i > 0 ? _path[i - 1] : _path[count - 2];
                    var next = i < count - 1 ? _path[i + 1] : _path[1];
                    tangentIn = (_path[i] - prev).normalized;
                    tangentOut = (next - _path[i]).normalized;
                }
                else
                {
                    tangentIn = i > 0 ? (_path[i] - _path[i - 1]).normalized : (_path[1] - _path[0]).normalized;
                    tangentOut = i < count - 1 ? (_path[i + 1] - _path[i]).normalized : tangentIn;
                }

                var tangent = tangentIn + tangentOut;
                tangent = tangent.sqrMagnitude < 1e-12f ? tangentOut : tangent.normalized;
                var normal = new Vector2(-tangent.y, tangent.x);
                var cos = Mathf.Max(Vector2.Dot(normal, new Vector2(-tangentOut.y, tangentOut.x)), MinMiterCos);
                _offsets[i] = normal / cos;
            }
        }

        public void Sample(float distance, out Vector2 position, out Vector2 tangent)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            var i = SegmentAt(distance);
            var a = _path[i - 1];
            var b = _path[i];
            var segment = _cumulative[i] - _cumulative[i - 1];
            var f = segment <= 1e-9f ? 0f : (distance - _cumulative[i - 1]) / segment;
            position = Vector2.LerpUnclamped(a, b, f);
            tangent = (b - a).normalized;
        }

        public Vector2 PositionAt(float distance)
        {
            Sample(distance, out var position, out _);
            return position;
        }

        public void AppendSpan(float from, float to, List<SigilSpanPoint> into)
        {
            var lo = Mathf.Min(from, to);
            var hi = Mathf.Max(from, to);
            var start = into.Count;

            into.Add(new SigilSpanPoint(PositionAt(lo), lo, OffsetAt(lo)));
            for (int i = 1; i < _path.Length - 1; i++)
            {
                if (_cumulative[i] > lo + Epsilon && _cumulative[i] < hi - Epsilon)
                {
                    into.Add(new SigilSpanPoint(_path[i], _cumulative[i], _offsets[i]));
                }
            }
            into.Add(new SigilSpanPoint(PositionAt(hi), hi, OffsetAt(hi)));

            if (to < from)
            {
                into.Reverse(start, into.Count - start);
            }
        }

        public bool IsOpenEnd(float distance)
        {
            return !closed && (distance <= Epsilon || distance >= Length - Epsilon);
        }

        private Vector2 OffsetAt(float distance)
        {
            var i = SegmentAt(distance);
            if (Mathf.Abs(_cumulative[i] - distance) < Epsilon) { return _offsets[i]; }
            if (Mathf.Abs(_cumulative[i - 1] - distance) < Epsilon) { return _offsets[i - 1]; }

            var tangent = (_path[i] - _path[i - 1]).normalized;
            return new Vector2(-tangent.y, tangent.x);
        }

        private int SegmentAt(float distance)
        {
            int lo = 1;
            int hi = _cumulative.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_cumulative[mid] < distance) { lo = mid + 1; }
                else { hi = mid; }
            }
            return lo;
        }
    }
}
