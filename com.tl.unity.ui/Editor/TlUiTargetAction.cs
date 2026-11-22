using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.Timeline;

namespace TlUi.Editor
{
    [MenuEntry("Tl UI Target…", MenuPriority.CustomClipActionSection.start)]
    public sealed class TlUiTargetAction : ClipAction
    {
        public override ActionValidity Validate(IEnumerable<TimelineClip> clips) => clips.Any() && clips.All(TlUiTargets.TakesATarget) ? ActionValidity.Valid : ActionValidity.NotApplicable;

        public override bool Execute(IEnumerable<TimelineClip> clips)
        {
            var picked = clips.ToList();
            var timelinesObject = TlUiTargets.TimelinesObjectOf(TimelineEditor.inspectedDirector);
            var menu = new GenericMenu();
            if (timelinesObject == null)
            {
                menu.AddDisabledItem(new GUIContent("Select the object with Tl UI Timelines to pick targets"));
                menu.ShowAsContext();
                return true;
            }
            var current = picked.Count == 1 ? TlUiTargets.TargetNameOf(picked[0], timelinesObject) : null;
            var names = TlUiTargets.NamesToOffer(timelinesObject);
            if (names.Count == 0)
                menu.AddDisabledItem(new GUIContent("Give elements a Name in UI Builder first"));
            foreach (var name in names)
            {
                var chosen = name;
                menu.AddItem(new GUIContent(chosen), chosen == current, () => TlUiTargets.Assign(picked, timelinesObject, chosen));
            }
            menu.ShowAsContext();
            return true;
        }
    }
}
