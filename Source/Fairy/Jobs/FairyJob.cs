using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public enum FairyJobKind : byte
    {
        Materialize,
        Dematerialize,
        Idle,
        Guard,
        Concentration,
        Expansion,
    }

    public enum FairyJobInterruptReason : byte
    {
        None,
        OwnerUnavailable,
        TargetUnavailable,
        FairyUnavailable,
        LifespanExpired,
        DurationExpired,
        AbilityToggledOff,
        DematerializeAll,
        ExternalCancel,
    }

    public abstract class FairyJob : IExposable
    {
        private const int MaxToilTransitionsPerTick = 8;

        public int id;

        [Unsaved]
        protected ViviFairy fairy;

        protected Pawn owner;
        protected List<FairyToil> toils = new List<FairyToil>();
        private int toilIndex;
        protected bool ended;

        public ViviFairy Fairy => fairy;
        public Pawn Owner => owner;
        public bool Ended => ended;
        public abstract FairyJobKind Kind { get; }
        public virtual FairyRole Role => FairyRole.None;

        protected FairyJob() { }

        protected FairyJob(int id, Pawn owner)
        {
            this.id = id;
            this.owner = owner;
        }

        internal virtual void NotifyAssigned(ViviFairy fairy)
        {
            this.fairy = fairy;
            if (owner == null)
            {
                owner = fairy?.Owner;
            }
            EnsureToils();
        }

        internal void StartCurrentToil()
        {
            CurrentToil?.Start();
        }

        internal void NotifyReplaced()
        {
            ended = true;
            CancelToils();
        }

        public void Tick(int delta)
        {
            if (ended) { return; }

            if (TryGetInterruptReason(out var reason))
            {
                Interrupt(reason);
                return;
            }

            TickActiveBeforeToil(delta);
            if (ended) { return; }

            EnsureToils();

            int transitions = 0;
            int tickDelta = delta;
            while (!ended && transitions++ < MaxToilTransitionsPerTick)
            {
                var toil = CurrentToil;
                if (toil == null)
                {
                    OnToilSequenceFinished();
                    return;
                }

                var status = toil.Tick(tickDelta);
                if (status == FairyToilStatus.Running)
                {
                    break;
                }

                toilIndex++;
                tickDelta = 0;
                if (toilIndex >= toils.Count)
                {
                    OnToilSequenceFinished();
                    break;
                }
                CurrentToil?.Start();
            }

            if (!ended)
            {
                TickActiveAfterToil(delta);
            }
        }

        protected virtual void MakeToils() { }

        protected virtual void TickActiveBeforeToil(int delta) { }

        protected virtual void TickActiveAfterToil(int delta) { }

        protected virtual void OnToilSequenceFinished()
        {
            End();
        }

        protected virtual bool TryGetInterruptReason(out FairyJobInterruptReason reason)
        {
            reason = FairyJobInterruptReason.None;

            if (ended) { return false; }
            if (fairy == null || fairy.Destroyed || !fairy.Spawned)
            {
                reason = FairyJobInterruptReason.FairyUnavailable;
                return true;
            }
            if (owner == null || !owner.Spawned || owner.Dead || owner.Map != fairy.Map)
            {
                reason = FairyJobInterruptReason.OwnerUnavailable;
                return true;
            }

            return false;
        }

        protected T CurrentToilAs<T>() where T : FairyToil
        {
            return CurrentToil as T;
        }

        protected void ResetToils(params FairyToil[] newToils)
        {
            CancelToils();
            toils = newToils != null ? newToils.Where(t => t != null).ToList() : new List<FairyToil>();
            toilIndex = 0;
            AttachToils();
            StartCurrentToil();
        }

        public virtual void End()
        {
            Finish(interrupted: false, FairyJobInterruptReason.None);
        }

        public virtual void Interrupt(FairyJobInterruptReason reason)
        {
            Finish(interrupted: true, reason);
        }

        private void Finish(bool interrupted, FairyJobInterruptReason reason)
        {
            if (ended) { return; }

            ended = true;
            CancelToils();

            if (interrupted)
            {
                OnInterrupted(reason);
            }
            OnEnded();
        }

        protected virtual void OnInterrupted(FairyJobInterruptReason reason) { }

        protected virtual void OnEnded() { }

        private FairyToil CurrentToil
        {
            get
            {
                if (toils == null || toilIndex < 0 || toilIndex >= toils.Count)
                {
                    return null;
                }
                return toils[toilIndex];
            }
        }

        private void EnsureToils()
        {
            if (toils == null)
            {
                toils = new List<FairyToil>();
            }
            if (toils.Count == 0)
            {
                MakeToils();
            }
            AttachToils();
        }

        private void AttachToils()
        {
            if (toils == null) { return; }
            foreach (var toil in toils)
            {
                toil?.Attach(this);
            }
        }

        private void CancelToils()
        {
            if (toils == null) { return; }
            foreach (var toil in toils)
            {
                toil?.Cancel();
            }
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref owner, "owner");
            Scribe_Collections.Look(ref toils, "toils", LookMode.Deep);
            Scribe_Values.Look(ref toilIndex, "toilIndex");
            Scribe_Values.Look(ref ended, "ended");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (toils == null) { toils = new List<FairyToil>(); }
                toils.RemoveAll(t => t == null);
            }
        }
    }
}
