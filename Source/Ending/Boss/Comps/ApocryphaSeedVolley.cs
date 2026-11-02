using UnityEngine;
using Verse;

namespace VVRace
{
    public class ApocryphaSeedVolley : IExposable
    {
        private const int RetryTicks = 600;

        private CompApocrypha _comp;

        private int _nextTick = -1;
        private int _volleysLaunched;

        private CompProperties_Apocrypha Props => _comp.Props;

        public int VolleysLaunched => _volleysLaunched;
        public int DoomSeedCount => Mathf.Min(Props.doomSeedVolleyMax, Props.doomSeedVolleyStart + _volleysLaunched / Mathf.Max(1, Props.doomSeedVolleyGrowthEvery));
        public bool FairySeedDue => Props.fairySeedEveryVolleys > 0 && (_volleysLaunched + 1) % Props.fairySeedEveryVolleys == 0;

        public ApocryphaSeedVolley(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref _nextTick, "nextTick", -1);
            Scribe_Values.Look(ref _volleysLaunched, "volleysLaunched", 0);
        }

        public void SetReadyTick(int tick)
        {
            _nextTick = tick;
        }

        public void Tick(int now)
        {
            if (_nextTick < 0 || now < _nextTick) { return; }

            _nextTick = now + (Launch(FairySeedDue) ? Props.doomSeedIntervalTicks.RandomInRange : RetryTicks);
        }

        public void Notify_ShieldBroken(int now)
        {
            Launch(true);
            _nextTick = now + Props.doomSeedIntervalTicks.RandomInRange;
        }

        private bool Launch(bool withFairy)
        {
            ApocryphaGimmicks.TryLaunchSeedVolley(_comp.Pawn, DoomSeedCount, withFairy, out var doomLaunched, out var fairyLaunched);
            if (doomLaunched + fairyLaunched <= 0) { return false; }

            _volleysLaunched++;
            return true;
        }
    }
}
