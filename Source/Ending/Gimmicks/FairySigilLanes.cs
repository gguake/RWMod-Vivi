using System.Collections.Generic;
using UnityEngine;

namespace VVRace
{
    public readonly struct SigilLanePiece
    {
        public readonly int lane;
        public readonly SigilStroke stroke;
        public readonly float from;
        public readonly float to;
        public readonly float keyFrom;
        public readonly float keyTo;

        public SigilLanePiece(int lane, SigilStroke stroke, float from, float to, float keyFrom, float keyTo)
        {
            this.lane = lane;
            this.stroke = stroke;
            this.from = from;
            this.to = to;
            this.keyFrom = keyFrom;
            this.keyTo = keyTo;
        }

        public float Length => Mathf.Abs(to - from);

        public float KeyAt(float distance)
        {
            var length = Length;
            return length <= 0f ? keyFrom : Mathf.LerpUnclamped(keyFrom, keyTo, Mathf.Abs(distance - from) / length);
        }
    }

    public sealed class FairySigilLanes
    {
        private const float PenUpCost = 0.45f;
        private const float SplitSnapFraction = 0.4f;
        private const float SplitMinRemainder = 0.15f;

        private struct LaneItem
        {
            public bool penUp;
            public Vector2 from;
            public Vector2 to;
            public SigilStroke stroke;
            public float strokeFrom;
            public float strokeTo;
            public float start;
            public float cost;
        }

        private struct Piece
        {
            public SigilStroke stroke;
            public float from;
            public float to;

            public Piece(SigilStroke stroke, float from, float to)
            {
                this.stroke = stroke;
                this.from = from;
                this.to = to;
            }

            public Vector2 StartPos => stroke.PositionAt(from);
            public Vector2 EndPos => stroke.PositionAt(to);
            public bool IsWholeLoop => stroke.closed && Mathf.Abs(to - from) >= stroke.Length - 1e-4f;
        }

        public readonly FairySigil sigil;

        private readonly List<LaneItem>[] _items;
        private readonly Vector2[] _positions;
        private readonly float[] _lengths;
        private readonly List<SigilLanePiece> _pieces = new List<SigilLanePiece>();

        public int Count => _items.Length;
        public IReadOnlyList<SigilLanePiece> Pieces => _pieces;
        public float AverageLength { get; }

        public FairySigilLanes(FairySigil sigil, int count)
        {
            this.sigil = sigil;
            _items = new List<LaneItem>[count];
            _positions = new Vector2[count];
            _lengths = new float[count];
            for (int i = 0; i < count; i++)
            {
                _items[i] = new List<LaneItem>();
            }

            var group = new List<SigilStroke>();
            for (int kind = (int)SigilStrokeKind.Ring; kind <= (int)SigilStrokeKind.Rune; kind++)
            {
                group.Clear();
                foreach (var stroke in sigil.Strokes)
                {
                    if ((int)stroke.kind == kind) { group.Add(stroke); }
                }
                if (group.Count == 0) { continue; }

                var ring = kind == (int)SigilStrokeKind.Ring || kind == (int)SigilStrokeKind.InnerRing;
                var chunks = ring ? SplitRings(group, count, kind == (int)SigilStrokeKind.InnerRing) : Partition(group, count);
                AssignChunks(chunks, ring);
            }

            float sum = 0f;
            for (int lane = 0; lane < count; lane++)
            {
                float t = 0f;
                var items = _items[lane];
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    item.start = t;
                    items[i] = item;
                    t += item.cost;
                }
                _lengths[lane] = t;
                sum += t;

                foreach (var item in items)
                {
                    if (item.penUp) { continue; }
                    _pieces.Add(new SigilLanePiece(lane, item.stroke, item.strokeFrom, item.strokeTo,
                        t > 0f ? item.start / t : 0f, t > 0f ? (item.start + item.cost) / t : 1f));
                }
            }
            AverageLength = sum / count;
        }

        public static float CostFactor(SigilStrokeKind kind)
        {
            switch (kind)
            {
                case SigilStrokeKind.Ring:
                case SigilStrokeKind.InnerRing:
                    return 0.5f;
                case SigilStrokeKind.Rune:
                    return 0.7f;
                default:
                    return 1f;
            }
        }

        public void GetHead(int lane, float progress, out Vector2 position, out Vector2 tangent, out bool drawing)
        {
            var items = _items[lane];
            if (items.Count == 0)
            {
                position = Vector2.zero;
                tangent = Vector2.up;
                drawing = false;
                return;
            }

            var time = Mathf.Clamp01(progress) * _lengths[lane];
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (time > item.start + item.cost && i < items.Count - 1) { continue; }

                var f = item.cost <= 0f ? 1f : Mathf.Clamp01((time - item.start) / item.cost);
                if (item.penUp)
                {
                    position = Vector2.LerpUnclamped(item.from, item.to, f);
                    tangent = (item.to - item.from).normalized;
                    drawing = false;
                    return;
                }

                item.stroke.Sample(Mathf.LerpUnclamped(item.strokeFrom, item.strokeTo, f), out position, out tangent);
                if (item.strokeTo < item.strokeFrom) { tangent = -tangent; }
                drawing = true;
                return;
            }

            position = Vector2.zero;
            tangent = Vector2.up;
            drawing = false;
        }

        private static List<List<Piece>> SplitRings(List<SigilStroke> group, int count, bool reversed)
        {
            var chunks = new List<List<Piece>>(count);
            for (int i = 0; i < count; i++)
            {
                var chunk = new List<Piece>();
                foreach (var stroke in group)
                {
                    var a = stroke.Length * i / count;
                    var b = stroke.Length * (i + 1) / count;
                    chunk.Add(reversed ? new Piece(stroke, b, a) : new Piece(stroke, a, b));
                }
                chunks.Add(chunk);
            }
            return chunks;
        }

        private static List<List<Piece>> Partition(List<SigilStroke> group, int count)
        {
            var starts = new float[group.Count];
            float total = 0f;
            for (int i = 0; i < group.Count; i++)
            {
                starts[i] = total;
                total += group[i].Length;
            }

            var target = total / count;
            var bounds = new float[count + 1];
            for (int k = 1; k < count; k++)
            {
                var b = k * target;
                int j = 0;
                while (j < group.Count - 1 && starts[j] + group[j].Length <= b) { j++; }

                var length = group[j].Length;
                var offset = b - starts[j];
                if (length < SplitSnapFraction * target || offset < SplitMinRemainder * target || length - offset < SplitMinRemainder * target)
                {
                    b = offset < length * 0.5f ? starts[j] : starts[j] + length;
                }
                bounds[k] = Mathf.Max(b, bounds[k - 1]);
            }
            bounds[count] = total;

            var chunks = new List<List<Piece>>(count);
            for (int k = 0; k < count; k++)
            {
                var chunk = new List<Piece>();
                for (int j = 0; j < group.Count; j++)
                {
                    var a = Mathf.Max(bounds[k], starts[j]) - starts[j];
                    var b = Mathf.Min(bounds[k + 1], starts[j] + group[j].Length) - starts[j];
                    if (b - a > 1e-5f) { chunk.Add(new Piece(group[j], a, b)); }
                }
                chunks.Add(chunk);
            }
            return chunks;
        }

        private void AssignChunks(List<List<Piece>> chunks, bool keepOrder)
        {
            var free = new List<int>(_items.Length);
            for (int i = 0; i < _items.Length; i++) { free.Add(i); }

            var remaining = new List<Piece>();
            foreach (var chunk in chunks)
            {
                if (chunk.Count == 0) { continue; }

                var start = chunk[0].StartPos;
                int lane = free[0];
                foreach (var candidate in free)
                {
                    if (Vector2.Distance(_positions[candidate], start) < Vector2.Distance(_positions[lane], start)) { lane = candidate; }
                }
                free.Remove(lane);

                if (keepOrder)
                {
                    foreach (var piece in chunk) { AddPiece(lane, piece); }
                    continue;
                }

                remaining.Clear();
                remaining.AddRange(chunk);
                while (remaining.Count > 0)
                {
                    var position = _positions[lane];
                    int best = -1;
                    bool reverse = false;
                    float bestDistance = float.MaxValue;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var d0 = Vector2.Distance(position, remaining[i].StartPos);
                        if (d0 < bestDistance - 1e-6f)
                        {
                            bestDistance = d0;
                            best = i;
                            reverse = false;
                        }
                        if (remaining[i].IsWholeLoop) { continue; }

                        var d1 = Vector2.Distance(position, remaining[i].EndPos);
                        if (d1 < bestDistance - 1e-6f)
                        {
                            bestDistance = d1;
                            best = i;
                            reverse = true;
                        }
                    }

                    var chosen = remaining[best];
                    remaining.RemoveAt(best);
                    AddPiece(lane, reverse ? new Piece(chosen.stroke, chosen.to, chosen.from) : chosen);
                }
            }
        }

        private void AddPiece(int lane, Piece piece)
        {
            var start = piece.StartPos;
            var distance = Vector2.Distance(_positions[lane], start);
            if (distance > 1e-4f)
            {
                _items[lane].Add(new LaneItem { penUp = true, from = _positions[lane], to = start, cost = distance * PenUpCost });
            }
            _items[lane].Add(new LaneItem
            {
                stroke = piece.stroke,
                strokeFrom = piece.from,
                strokeTo = piece.to,
                cost = Mathf.Abs(piece.to - piece.from) * CostFactor(piece.stroke.kind),
            });
            _positions[lane] = piece.EndPos;
        }
    }
}
