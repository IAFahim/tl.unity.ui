using System.Collections.Generic;
using Tl.Ui;
using Tl.Unity;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace TlUi.Editor
{
    public sealed class TlUiAnimatePreset
    {
        public string Menu;
        public UiProperty Property;
        public float From;
        public float To;
        public AnimationCurve Ease;
        public bool Hold;
        public double Seconds;
    }

    public static class TlUiAnimatePresets
    {
        public static AnimationCurve FastThenSlow() => new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        public static AnimationCurve Steady() => AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public static AnimationCurve OverAndBack() => new AnimationCurve(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(0.7f, 1.1f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));

        public static AnimationCurve UpAndBackDown() => new AnimationCurve(new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.5f, 1f, 0f, 0f), new Keyframe(1f, 0f, -4f, 0f));

        public static AnimationCurve Wiggle() => new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.125f, 1f), new Keyframe(0.375f, -1f), new Keyframe(0.625f, 1f), new Keyframe(0.875f, -1f), new Keyframe(1f, 0f));

        public static IReadOnlyList<TlUiAnimatePreset> All => new[]
        {
            new TlUiAnimatePreset { Menu = "Appear/Fade in", Property = UiProperty.Opacity, From = 0f, To = 1f, Ease = FastThenSlow(), Hold = true, Seconds = 0.4 },
            new TlUiAnimatePreset { Menu = "Appear/Slide in from the left", Property = UiProperty.MoveX, From = -400f, To = 0f, Ease = FastThenSlow(), Hold = true, Seconds = 0.5 },
            new TlUiAnimatePreset { Menu = "Appear/Slide in from the right", Property = UiProperty.MoveX, From = 400f, To = 0f, Ease = FastThenSlow(), Hold = true, Seconds = 0.5 },
            new TlUiAnimatePreset { Menu = "Appear/Slide in from the top", Property = UiProperty.MoveY, From = -300f, To = 0f, Ease = FastThenSlow(), Hold = true, Seconds = 0.5 },
            new TlUiAnimatePreset { Menu = "Appear/Slide in from the bottom", Property = UiProperty.MoveY, From = 300f, To = 0f, Ease = FastThenSlow(), Hold = true, Seconds = 0.5 },
            new TlUiAnimatePreset { Menu = "Appear/Pop in", Property = UiProperty.Scale, From = 0f, To = 1f, Ease = OverAndBack(), Hold = true, Seconds = 0.35 },
            new TlUiAnimatePreset { Menu = "Appear/Type out the text", Property = UiProperty.Reveal, From = 0f, To = 1f, Ease = Steady(), Hold = true, Seconds = 1.0 },
            new TlUiAnimatePreset { Menu = "Disappear/Fade out", Property = UiProperty.Opacity, From = 1f, To = 0f, Ease = FastThenSlow(), Hold = true, Seconds = 0.4 },
            new TlUiAnimatePreset { Menu = "Disappear/Slide out to the left", Property = UiProperty.MoveX, From = 0f, To = -400f, Ease = FastThenSlow(), Hold = true, Seconds = 0.5 },
            new TlUiAnimatePreset { Menu = "Disappear/Slide out to the right", Property = UiProperty.MoveX, From = 0f, To = 400f, Ease = FastThenSlow(), Hold = true, Seconds = 0.5 },
            new TlUiAnimatePreset { Menu = "Disappear/Shrink away", Property = UiProperty.Scale, From = 1f, To = 0f, Ease = FastThenSlow(), Hold = true, Seconds = 0.3 },
            new TlUiAnimatePreset { Menu = "Attention/Pop (grow and back)", Property = UiProperty.Scale, From = 1f, To = 1.2f, Ease = UpAndBackDown(), Hold = false, Seconds = 0.25 },
            new TlUiAnimatePreset { Menu = "Attention/Shake", Property = UiProperty.MoveX, From = 0f, To = 12f, Ease = Wiggle(), Hold = false, Seconds = 0.4 },
            new TlUiAnimatePreset { Menu = "Attention/Blink", Property = UiProperty.Opacity, From = 1f, To = 0.3f, Ease = UpAndBackDown(), Hold = false, Seconds = 0.5 },
            new TlUiAnimatePreset { Menu = "Attention/Spin once", Property = UiProperty.Rotate, From = 0f, To = 360f, Ease = FastThenSlow(), Hold = false, Seconds = 0.6 },
            new TlUiAnimatePreset { Menu = "Fill/Fill a progress bar (0 to 100)", Property = UiProperty.Value, From = 0f, To = 100f, Ease = FastThenSlow(), Hold = true, Seconds = 1.0 },
            new TlUiAnimatePreset { Menu = "Fill/Fill a radial fill (0 to 1)", Property = UiProperty.Value, From = 0f, To = 1f, Ease = FastThenSlow(), Hold = true, Seconds = 1.0 },
        };

        public static bool IsTlsStartingEase(AnimationCurve ease) =>
            ease != null && ease.length == 2
            && Mathf.Approximately(ease[0].time, 0f) && Mathf.Approximately(ease[0].value, 1f)
            && Mathf.Approximately(ease[1].time, 1f) && Mathf.Approximately(ease[1].value, 0f);

        public static bool RunsBackwards(AnimationCurve ease) => ease != null && ease.length > 0 && ease.Evaluate(0f) > ease.Evaluate(1f) + 0.01f;

        public static bool IsUntouched(float from, float to, AnimationCurve ease) => from == 0f && to == 0f && IsTlsStartingEase(ease);

        static SerializedProperty Field(SerializedObject clipAsset, string dataPath, string name) => clipAsset.FindProperty(dataPath + name);

        public static void Apply(Object clipAsset, string dataPath, TlUiAnimatePreset preset, TimelineClip clip, TlUiTimelines timelinesObject)
        {
            if (clipAsset == null || preset == null)
                return;
            var nameBefore = clip != null && timelinesObject != null && clip.asset is TlClip tl ? TlUiTargets.ClipNameFor(tl.Data, TlUiTargets.TargetNameOf(clip, timelinesObject) ?? "") : null;
            var fresh = new SerializedObject(clipAsset);
            Field(fresh, dataPath, nameof(AnimateClip.Property)).enumValueIndex = (int)preset.Property;
            Field(fresh, dataPath, nameof(AnimateClip.From)).floatValue = preset.From;
            Field(fresh, dataPath, nameof(AnimateClip.To)).floatValue = preset.To;
            Field(fresh, dataPath, nameof(AnimateClip.Ease)).animationCurveValue = preset.Ease;
            Field(fresh, dataPath, nameof(AnimateClip.Hold)).boolValue = preset.Hold;
            fresh.ApplyModifiedProperties();
            if (clip == null)
                return;
            var track = clip.GetParentTrack();
            if (track != null)
            {
                Undo.RecordObject(track, "Tl UI Preset");
                clip.duration = preset.Seconds;
                EditorUtility.SetDirty(track);
            }
            if (nameBefore != null)
                TlUiTargets.RenameIfNamedByTlUi(clip, nameBefore, timelinesObject);
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }

        public static void SwapFromAndTo(Object clipAsset, string dataPath)
        {
            if (clipAsset == null)
                return;
            var fresh = new SerializedObject(clipAsset);
            var from = Field(fresh, dataPath, nameof(AnimateClip.From));
            var to = Field(fresh, dataPath, nameof(AnimateClip.To));
            var wasFrom = from.floatValue;
            from.floatValue = to.floatValue;
            to.floatValue = wasFrom;
            fresh.ApplyModifiedProperties();
        }

        public static void TurnTheEaseUphill(Object clipAsset, string dataPath)
        {
            if (clipAsset == null)
                return;
            var fresh = new SerializedObject(clipAsset);
            Field(fresh, dataPath, nameof(AnimateClip.Ease)).animationCurveValue = FastThenSlow();
            fresh.ApplyModifiedProperties();
        }

        public static void ShowMenu(Rect under, Object clipAsset, string dataPath, bool easeRunsBackwards)
        {
            var menu = new GenericMenu();
            foreach (var preset in All)
            {
                var chosen = preset;
                menu.AddItem(new GUIContent(preset.Menu), false, () => Apply(clipAsset, dataPath, chosen, TlUiTargets.TimelineClipOf(clipAsset as TlClip), TlUiScreens.TimelinesObjectFor(TimelineEditor.inspectedAsset)));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Swap From and To"), false, () => SwapFromAndTo(clipAsset, dataPath));
            if (easeRunsBackwards)
                menu.AddItem(new GUIContent("Make Ease go uphill"), false, () => TurnTheEaseUphill(clipAsset, dataPath));
            menu.DropDown(under);
        }
    }
}
