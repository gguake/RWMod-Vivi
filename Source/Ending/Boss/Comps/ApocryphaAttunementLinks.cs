using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaAttunementLinks
    {
        public struct Link
        {
            public Pawn a;
            public Pawn b;
            public int field;
            public bool contact;
        }

        public class Field
        {
            public readonly List<Pawn> members = new List<Pawn>(AttunementField.MaxVertices);
            public bool contact;

            public bool Hexagon => members.Count == AttunementField.MaxVertices;
        }

        private const int RefreshInterval = 15;
        private const float LinkRange = 12.9f;
        private const int MinFieldSize = 3;
        private const float BeamWidth = 0.45f;
        private const float PetalWidth = 0.75f;
        private const float IdleAlpha = 0.7f;
        private const float MinFieldArea = 0.5f;
        private const float WhitenTicks = 45f;
        private const float MinCornerAngle = 15f;

        private static readonly Color LinkColor = new Color(1f, 0.95f, 0.72f, 1f);

        private static readonly List<int> _tmpGroup = new List<int>(AttunementField.MaxVertices);
        private static readonly List<int> _tmpRing = new List<int>(AttunementField.MaxVertices);
        private static readonly List<int> _tmpBestRing = new List<int>(AttunementField.MaxVertices);
        private static readonly List<Vector3> _tmpPoints = new List<Vector3>(AttunementField.MaxVertices);

        private readonly CompApocrypha _comp;
        private readonly List<Pawn> _pawns = new List<Pawn>();
        private readonly List<(int a, int b, float distance)> _pairs = new List<(int, int, float)>();
        private readonly List<Link> _links = new List<Link>();
        private readonly List<Field> _fields = new List<Field>();
        private readonly List<Field> _fieldPool = new List<Field>();
        private int[] _root = new int[0];
        private int[] _size = new int[0];
        private int[] _fieldOf = new int[0];
        private float _drainPerHour;
        private float _damageBondLossFactor;
        private int _hexagonSinceTick = -1;

        public float DamageBondLossFactor => _damageBondLossFactor;

        public ApocryphaAttunementLinks(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void Tick()
        {
            if (!_comp.Pawn.IsHashIntervalTick(RefreshInterval)) { return; }

            Rebuild();
            UpdateContact();

            if (_drainPerHour > 0f)
            {
                _comp.DrainBond(_comp.Props.fairyBondInitial * _drainPerHour * RefreshInterval / GenDate.TicksPerHour);
            }
        }

        public void Clear()
        {
            _pawns.Clear();
            _links.Clear();
            for (int i = 0; i < _fields.Count; i++)
            {
                _fields[i].members.Clear();
                _fields[i].contact = false;
                _fieldPool.Add(_fields[i]);
            }
            _fields.Clear();
            _drainPerHour = 0f;
            _damageBondLossFactor = 0f;
        }

        private void Rebuild()
        {
            Clear();

            var spawned = _comp.Pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                var pawn = spawned[i];
                if (pawn.Dead || pawn.Downed || pawn.stances.stunner.Stunned) { continue; }
                if (!pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_FairyAttunement)) { continue; }

                _pawns.Add(pawn);
            }

            var count = _pawns.Count;
            if (count >= 2)
            {
                BuildGroups(count);
                for (int i = 0; i < count; i++)
                {
                    if (Find(i) == i && _size[i] >= MinFieldSize)
                    {
                        while (TryAddField(i, count)) { }
                    }
                }

                foreach (var (a, b, _) in _pairs)
                {
                    if (Find(a) != Find(b)) { continue; }

                    var field = _fieldOf[a] == _fieldOf[b] ? _fieldOf[a] : -1;
                    if (field < 0 || IsRingEdge(_fields[field], _pawns[a], _pawns[b]))
                    {
                        _links.Add(new Link { a = _pawns[a], b = _pawns[b], field = field });
                    }
                }
            }

            if (!_fields.Any(v => v.Hexagon))
            {
                _hexagonSinceTick = -1;
            }
            else if (_hexagonSinceTick < 0)
            {
                _hexagonSinceTick = GenTicks.TicksGame;
            }
        }

        private void BuildGroups(int count)
        {
            _pairs.Clear();
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    var distance = (_pawns[i].DrawPos - _pawns[j].DrawPos).MagnitudeHorizontal();
                    if (distance <= LinkRange)
                    {
                        _pairs.Add((i, j, distance));
                    }
                }
            }
            _pairs.SortBy(v => v.distance);

            if (_root.Length < count)
            {
                _root = new int[count];
                _size = new int[count];
                _fieldOf = new int[count];
            }
            for (int i = 0; i < count; i++)
            {
                _root[i] = i;
                _size[i] = 1;
                _fieldOf[i] = -1;
            }

            foreach (var (a, b, _) in _pairs)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra == rb || _size[ra] + _size[rb] > AttunementField.MaxVertices) { continue; }

                _root[rb] = ra;
                _size[ra] += _size[rb];
            }
        }

        private bool TryAddField(int root, int count)
        {
            _tmpGroup.Clear();
            for (int i = 0; i < count; i++)
            {
                if (_fieldOf[i] < 0 && Find(i) == root) { _tmpGroup.Add(i); }
            }

            _tmpBestRing.Clear();
            var bestArea = 0f;
            for (int mask = (1 << _tmpGroup.Count) - 1; mask > 0; mask--)
            {
                _tmpRing.Clear();
                for (int i = 0; i < _tmpGroup.Count; i++)
                {
                    if ((mask & (1 << i)) != 0) { _tmpRing.Add(_tmpGroup[i]); }
                }

                if (_tmpRing.Count < MinFieldSize || _tmpRing.Count < _tmpBestRing.Count) { continue; }

                var area = SortRing(_tmpRing);
                if (area < MinFieldArea || (_tmpRing.Count == _tmpBestRing.Count && area <= bestArea)) { continue; }

                bestArea = area;
                _tmpBestRing.Clear();
                _tmpBestRing.AddRange(_tmpRing);
            }

            if (_tmpBestRing.Count == 0) { return false; }

            var field = _fieldPool.Count > 0 ? _fieldPool.Pop() : new Field();
            for (int i = 0; i < _tmpBestRing.Count; i++)
            {
                field.members.Add(_pawns[_tmpBestRing[i]]);
                _fieldOf[_tmpBestRing[i]] = _fields.Count;
            }
            _fields.Add(field);
            return true;
        }

        private float SortRing(List<int> ring)
        {
            var center = Vector3.zero;
            for (int i = 0; i < ring.Count; i++)
            {
                center += _pawns[ring[i]].DrawPos;
            }
            center /= ring.Count;

            ring.SortBy(v => (_pawns[v].DrawPos - center).AngleFlat(), v => _pawns[v].thingIDNumber);

            _tmpPoints.Clear();
            for (int i = 0; i < ring.Count; i++)
            {
                var prev = _pawns[ring[(i + ring.Count - 1) % ring.Count]].DrawPos;
                var point = _pawns[ring[i]].DrawPos;
                var next = _pawns[ring[(i + 1) % ring.Count]].DrawPos;
                var length = (point - next).MagnitudeHorizontal();
                if (length > LinkRange || length < 0.01f) { return 0f; }
                if (Mathf.Abs(Mathf.DeltaAngle((point - prev).AngleFlat(), (next - point).AngleFlat())) < MinCornerAngle) { return 0f; }

                _tmpPoints.Add(point);
            }

            return AttunementField.PolygonArea(_tmpPoints);
        }

        private static bool IsRingEdge(Field field, Pawn a, Pawn b)
        {
            var members = field.members;
            var gap = Mathf.Abs(members.IndexOf(a) - members.IndexOf(b));
            return gap == 1 || gap == members.Count - 1;
        }

        private int Find(int i)
        {
            while (_root[i] != i)
            {
                _root[i] = _root[_root[i]];
                i = _root[i];
            }
            return i;
        }

        private static List<Vector3> PointsOf(Field field)
        {
            _tmpPoints.Clear();
            for (int i = 0; i < field.members.Count; i++)
            {
                _tmpPoints.Add(field.members[i].DrawPos);
            }
            return _tmpPoints;
        }

        private void UpdateContact()
        {
            var props = _comp.Props;
            var body = _comp.BodyCenter;
            var radius = props.asphodelBodyRadius;

            for (int i = 0; i < _fields.Count; i++)
            {
                var field = _fields[i];
                field.contact = AttunementField.PointInPolygon(body, PointsOf(field));
                if (field.contact)
                {
                    var factor = field.Hexagon ? props.contactHexagonFactor : 1f;
                    _drainPerHour += props.contactFieldDrainPerHour * factor;
                    _damageBondLossFactor += props.contactFieldDamageBondLoss * factor;
                }
            }

            for (int i = 0; i < _links.Count; i++)
            {
                var link = _links[i];
                link.contact = AttunementField.CircleIntersectsSegment(body, radius, link.a.DrawPos, link.b.DrawPos);
                _links[i] = link;
                if (link.contact && !InContactField(link))
                {
                    _drainPerHour += props.contactLineDrainPerHour;
                    _damageBondLossFactor += props.contactLineDamageBondLoss;
                }
            }
        }

        private bool InContactField(Link link)
        {
            for (int i = 0; i < _fields.Count; i++)
            {
                var field = _fields[i];
                if (field.contact && (field.members.Contains(link.a) || field.members.Contains(link.b)))
                {
                    return true;
                }
            }
            return false;
        }

        public void Draw()
        {
            var whiten = _hexagonSinceTick < 0 ? 0f : Mathf.Clamp01((GenTicks.TicksGame - _hexagonSinceTick) / WhitenTicks);
            for (int i = 0; i < _fields.Count; i++)
            {
                var field = _fields[i];
                if (field.members.Any(v => !v.Spawned)) { continue; }

                AttunementField.Draw(PointsOf(field), field.contact ? 1f : IdleAlpha, field.Hexagon ? whiten : 0f);
            }

            for (int i = 0; i < _links.Count; i++)
            {
                var link = _links[i];
                if (!link.a.Spawned || !link.b.Spawned) { continue; }

                var color = LinkColor;
                color.a = link.contact ? 1f : IdleAlpha;
                SoulLinkRenderer.Draw(link.a.DrawPos, link.b.DrawPos, BeamWidth, color);
                PetalLinkRenderer.Draw(link.a.DrawPos, link.b.DrawPos, PetalWidth, color, link.a.thingIDNumber ^ link.b.thingIDNumber);
            }
        }
    }
}
