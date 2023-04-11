using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ViviWorkModeDef : Def
    {
        [NoTranslate]
        public string iconPath;
        public Texture2D uiIcon;
        public int uiOrder;

        public override void PostLoad()
        {
            if (!string.IsNullOrEmpty(iconPath))
            {
                LongEventHandler.ExecuteWhenFinished(delegate
                {
                    uiIcon = ContentFinder<Texture2D>.Get(iconPath);
                });
            }
        }

    }
}
