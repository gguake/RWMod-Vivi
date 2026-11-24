using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{

    public class Hediff_FairyConcentrated : HediffWithComps
    {
        private static readonly List<Hediff_FairyConcentrated> _activeMarkers = new List<Hediff_FairyConcentrated>();
        private static Game _cachedGame;

        private static void EnsureCachedGame()
        {
            if (_cachedGame != Current.Game)
            {
                _activeMarkers.Clear();
                _cachedGame = Current.Game;
            }
        }

        internal static List<Hediff_FairyConcentrated> ActiveMarkers
        {
            get
            {
                EnsureCachedGame();
                return _activeMarkers;
            }
        }

        private void RegisterMarker()
        {
            EnsureCachedGame();
            if (!_activeMarkers.Contains(this))
            {
                _activeMarkers.Add(this);
            }
        }

        private void UnregisterMarker()
        {
            EnsureCachedGame();
            _activeMarkers.Remove(this);
        }

        internal static void Notify_HediffSetDirty(Pawn pawn)
        {
            if (pawn == null || _activeMarkers.Count == 0) { return; }
            EnsureCachedGame();

            var hediffs = pawn.health?.hediffSet?.hediffs;
            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                var marker = _activeMarkers[i];
                if (marker.pawn != pawn) { continue; }
                if (hediffs == null || !hediffs.Contains(marker))
                {
                    _activeMarkers.RemoveAt(i);
                }
            }
        }

        public Pawn ownerVivi;
        [Unsaved]
        private Effecter _targetMarkerEffecter;

        public override bool ShouldRemove
        {
            get
            {
                if (base.ShouldRemove) { return true; }
                if (ownerVivi == null || ownerVivi.Dead || !ownerVivi.Spawned) { return true; }
                if (pawn == null || pawn.DeadOrDowned || !pawn.Spawned) { return true; }
                if (pawn.Map != ownerVivi.Map) { return true; }
                if (!pawn.HostileTo(ownerVivi)) { return true; }
                return false;
            }
        }

        public static Hediff_FairyConcentrated GetOwnedBy(Pawn pawn, Pawn owner)
        {
            if (pawn == null || pawn.health == null || owner == null) { return null; }

            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_FairyConcentrated marker &&
                    marker.def == VVHediffDefOf.VV_FairyConcentrated &&
                    marker.IsOwnedBy(owner))
                {
                    return marker;
                }
            }

            return null;
        }

        public static Hediff_FairyConcentrated GetTargetOwnedBy(Pawn owner)
        {
            if (owner == null || owner.Map == null) { return null; }

            var pawns = owner.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var marker = GetOwnedBy(pawns[i], owner);
                if (marker != null) { return marker; }
            }

            return null;
        }

        public static void RemoveOwnedBy(Pawn owner)
        {
            if (owner == null || Find.Maps == null) { return; }

            var markers = new List<Hediff_FairyConcentrated>();
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn == null || pawn.health == null) { continue; }

                    var hediffs = pawn.health.hediffSet.hediffs;
                    for (int i = 0; i < hediffs.Count; i++)
                    {
                        if (hediffs[i] is Hediff_FairyConcentrated marker &&
                            marker.def == VVHediffDefOf.VV_FairyConcentrated &&
                            marker.ownerVivi == owner)
                        {
                            markers.Add(marker);
                        }
                    }
                }
            }

            foreach (var marker in markers)
            {
                marker.pawn?.health?.RemoveHediff(marker);
            }
        }

        public bool IsOwnedBy(Pawn owner)
        {
            return owner != null && ownerVivi == owner && !ShouldRemove;
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            RegisterMarker();
        }

        public override void Tick()
        {
            base.Tick();
            MaintainTargetMarkerEffect();
        }

        public override void PostRemoved()
        {
            UnregisterMarker();
            EndTargetMarkerEffect();
            base.PostRemoved();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            UnregisterMarker();
            EndTargetMarkerEffect();

            // 죽은 폰은 헤디프가 틱되지 않아 ShouldRemove로 정리되지 않으므로 여기서 지운다.
            // HediffSet.Notify_PawnDied는 목록을 역방향으로 순회하므로 즉시 제거해도 다른 헤디프의 알림이 누락되지 않는다.
            pawn.health.RemoveHediff(this);
        }

        private void MaintainTargetMarkerEffect()
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || ShouldRemove)
            {
                EndTargetMarkerEffect();
                return;
            }

            if (_targetMarkerEffecter == null)
            {
                _targetMarkerEffecter = VVEffecterDefOf.VV_Effecter_FairyTargetMarker?.SpawnAttached(pawn, pawn.Map);
            }

            _targetMarkerEffecter?.EffectTick(new TargetInfo(pawn), TargetInfo.Invalid);
        }

        private void EndTargetMarkerEffect()
        {
            if (_targetMarkerEffecter == null) { return; }

            _targetMarkerEffecter.ForceEnd();
            _targetMarkerEffecter = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerVivi, "ownerVivi");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RegisterMarker();
            }
        }
    }
}
