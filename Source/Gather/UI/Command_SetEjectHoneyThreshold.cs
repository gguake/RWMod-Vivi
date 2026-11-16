using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 바닐라 Command_SetTargetFuelLevel과 같은 방식: 여러 정제기를 함께 선택하면 한 번에 설정
    public class Command_SetEjectHoneyThreshold : Command
    {
        public Building_AutoHoneyExtractor extractor;

        private List<Building_AutoHoneyExtractor> _extractors;

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);

            if (_extractors == null)
            {
                _extractors = new List<Building_AutoHoneyExtractor>();
            }

            if (!_extractors.Contains(extractor))
            {
                _extractors.Add(extractor);
            }

            var dialog = new Dialog_Slider(
                x => LocalizeString_Command.VV_Command_SetEjectHoneyThresholdSlider.Translate(x),
                Building_AutoHoneyExtractor.MinEjectThreshold,
                Building_AutoHoneyExtractor.MaxEjectThreshold,
                value =>
                {
                    for (int i = 0; i < _extractors.Count; ++i)
                    {
                        _extractors[i].EjectThreshold = value;
                    }
                },
                extractor.EjectThreshold);

            Find.WindowStack.Add(dialog);
        }

        public override bool InheritInteractionsFrom(Gizmo other)
        {
            if (_extractors == null)
            {
                _extractors = new List<Building_AutoHoneyExtractor>();
            }

            _extractors.Add(((Command_SetEjectHoneyThreshold)other).extractor);
            return false;
        }
    }
}
