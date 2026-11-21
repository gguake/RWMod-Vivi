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
            public bool contact;
        }

        public struct Field
        {
            public Pawn a;
            public Pawn b;
            public Pawn c;
            public bool contact;
        }

        private const int RefreshInterval = 15;
        private const float LinkRange = 12.9f;
        private const int MaxGroupSize = 3;
        private const float LineDrainPerHour = 0.12f;
        private const float FieldDrainPerHour = 0.3f;
        private const float BeamWidth = 0.45f;
        private const float PetalWidth = 0.75f;
        private const float IdleAlpha = 0.7f;
        private const float MinFieldArea = 0.5f;

        private static readonly Color LinkColor = new Color(1f, 0.95f, 0.72f, 1f);

        private readonly CompApocrypha _comp;
        private readonly List<Pawn> _pawns = new List<Pawn>();
        private readonly List<(int a, int b, float distance)> _pairs = new List<(int, int, float)>();
        private readonly List<Link> _links = new List<Link>();
        private readonly List<Field> _fields = new List<Field>();
        private int[] _root = new int[0];
        private int[] _size = new int[0];
        private float _drainPerHour;

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
                _comp.DrainBond(_comp.Bond.Max * _drainPerHour * RefreshInterval / GenDate.TicksPerHour);
            }
        }

        public void Clear()
        {
            _pawns.Clear();
            _links.Clear();
            _fields.Clear();
            _drainPerHour = 0f;
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
            if (count < 2) { return; }

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
            }
            for (int i = 0; i < count; i++)
            {
                _root[i] = i;
                _size[i] = 1;
            }

            foreach (var (a, b, _) in _pairs)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra == rb || _size[ra] + _size[rb] > MaxGroupSize) { continue; }

                _root[rb] = ra;
                _size[ra] += _size[rb];
            }

            foreach (var (a, b, _) in _pairs)
            {
                if (Find(a) == Find(b))
                {
                    _links.Add(new Link { a = _pawns[a], b = _pawns[b] });
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (Find(i) != i || _size[i] != MaxGroupSize) { continue; }

                var members = new List<Pawn>(MaxGroupSize);
                for (int j = 0; j < count; j++)
                {
                    if (Find(j) == i) { members.Add(_pawns[j]); }
                }

                if (CountLinksWithin(members) == MaxGroupSize && AttunementField.TriangleArea(members[0].DrawPos, members[1].DrawPos, members[2].DrawPos) >= MinFieldArea)
                {
                    _fields.Add(new Field { a = members[0], b = members[1], c = members[2] });
                }
            }
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

        private int CountLinksWithin(List<Pawn> members)
        {
            var n = 0;
            for (int i = 0; i < _links.Count; i++)
            {
                if (members.Contains(_links[i].a) && members.Contains(_links[i].b)) { n++; }
            }
            return n;
        }

        private void UpdateContact()
        {
            var body = _comp.BodyCenter;
            var radius = _comp.Props.asphodelBodyRadius;

            for (int i = 0; i < _fields.Count; i++)
            {
                var field = _fields[i];
                field.contact = AttunementField.PointInTriangle(body, field.a.DrawPos, field.b.DrawPos, field.c.DrawPos);
                _fields[i] = field;
                if (field.contact)
                {
                    _drainPerHour += FieldDrainPerHour;
                }
            }

            for (int i = 0; i < _links.Count; i++)
            {
                var link = _links[i];
                link.contact = AttunementField.CircleIntersectsSegment(body, radius, link.a.DrawPos, link.b.DrawPos);
                _links[i] = link;
                if (link.contact && !InContactField(link))
                {
                    _drainPerHour += LineDrainPerHour;
                }
            }
        }

        private bool InContactField(Link link)
        {
            for (int i = 0; i < _fields.Count; i++)
            {
                var field = _fields[i];
                if (field.contact && (field.a == link.a || field.b == link.a || field.c == link.a))
                {
                    return true;
                }
            }
            return false;
        }

        public void Draw()
        {
            for (int i = 0; i < _fields.Count; i++)
            {
                var field = _fields[i];
                if (!field.a.Spawned || !field.b.Spawned || !field.c.Spawned) { continue; }

                AttunementField.Draw(field.a.DrawPos, field.b.DrawPos, field.c.DrawPos, field.contact ? 1f : IdleAlpha);
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
