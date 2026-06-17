using Verse;

namespace VVRace
{
    public enum FairyToilStatus : byte
    {
        Running,
        Complete,
    }

    public abstract class FairyToil : IExposable
    {
        [Unsaved]
        protected FairyJob job;

        private bool started;

        protected ViviFairy Fairy => job?.Fairy;

        internal void Attach(FairyJob job)
        {
            this.job = job;
        }

        internal void Start()
        {
            if (started) { return; }
            started = true;
            OnStarted();
        }

        internal FairyToilStatus Tick(int delta)
        {
            Start();
            return TickAction(delta);
        }

        protected virtual void OnStarted() { }

        protected abstract FairyToilStatus TickAction(int delta);

        public virtual void Cancel() { }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref started, "started");
        }
    }
}
