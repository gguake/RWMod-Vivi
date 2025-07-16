using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class EverflowerRitualDef : Def
    {
        public EverflowerRitualWorker Worker
        {
            get
            {
                if (_workerInt == null)
                {
                    _workerInt = (EverflowerRitualWorker)Activator.CreateInstance(workerClass, new object[] { this });
                }
                return _workerInt;
            }
        }
        public Type workerClass;
        [Unsaved]
        private EverflowerRitualWorker _workerInt;

        public int uiOrder;

        [NoTranslate]
        public string uiIconPath;
        public Texture2D uiIcon = BaseContent.BadTex;

        public int attuneLevel;
        public bool allowUnlinkedPawn;
        public float requiredPsychicSensitivity = 1.5f;

        public JobDef job;
        public int jobWorkAmount;

        public int globalCooldown;

        public override void PostLoad()
        {
            if (!string.IsNullOrEmpty(uiIconPath))
            {
                LongEventHandler.ExecuteWhenFinished(delegate
                {
                    uiIcon = ContentFinder<Texture2D>.Get(uiIconPath);
                });
            }
        }
    }
}
