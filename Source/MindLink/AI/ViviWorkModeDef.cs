using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ViviWorkModeDef : Def
    {
        public class ViviWorkPrioritySetting
        {
            public WorkTypeDef workTypeDef;
            public int priority;

            public void LoadDataFromXmlCustom(XmlNode xmlRoot)
            {
                DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "workTypeDef", xmlRoot.Name);
                priority = ParseHelper.FromString<int>(xmlRoot.FirstChild.Value);
            }

            public override string ToString()
            {
                if (workTypeDef == null)
                {
                    return "(null workType)";
                }
                return workTypeDef.defName + "-" + priority;
            }
        }

        [NoTranslate]
        public string iconPath;
        public Texture2D uiIcon;
        public int uiOrder;

        public List<ViviWorkPrioritySetting> workPriorities;
        public int defaultWorkPriority = 3;

        public IEnumerable<(WorkTypeDef workTypeDef, int priority)> AllWorkTypePriority
        {
            get
            {
                foreach (var workTypeDef in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    var priority = workPriorities?.FirstOrDefault(v => v.workTypeDef == workTypeDef)?.priority ?? defaultWorkPriority;
                    yield return (workTypeDef, priority);
                }

                yield break;
            }
        }

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
