using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements;

namespace TlUi
{
    static class TlBoundNumber
    {
        public static bool TryRead<TValue>(ref TValue value, out float number)
        {
            if (typeof(TValue) == typeof(float))
            {
                number = UnsafeUtility.As<TValue, float>(ref value);
                return true;
            }
            if (typeof(TValue) == typeof(int))
            {
                number = UnsafeUtility.As<TValue, int>(ref value);
                return true;
            }
            if (typeof(TValue) == typeof(bool))
            {
                number = UnsafeUtility.As<TValue, bool>(ref value) ? 1f : 0f;
                return true;
            }
            if (typeof(TValue) == typeof(double))
            {
                number = (float)UnsafeUtility.As<TValue, double>(ref value);
                return true;
            }
            if (typeof(TValue) == typeof(long))
            {
                number = UnsafeUtility.As<TValue, long>(ref value);
                return true;
            }
            number = 0f;
            return false;
        }

        public static BindingResult NotANumber<TValue>() => new BindingResult(BindingStatus.Failure, typeof(TValue).Name + " is not a bool, int or float.");
    }

    [UxmlObject]
    public partial class TlClassBinding : DataBinding
    {
        [UxmlAttribute("uss-class")]
        public string ClassName;

        [UxmlAttribute]
        public bool Invert;

        public TlClassBinding()
        {
            bindingMode = BindingMode.ToTarget;
        }

        protected override BindingResult UpdateUI<TValue>(in BindingContext context, ref TValue value)
        {
            if (!TlBoundNumber.TryRead(ref value, out var number))
                return TlBoundNumber.NotANumber<TValue>();
            if (string.IsNullOrEmpty(ClassName))
                return new BindingResult(BindingStatus.Failure, "Tl Class Binding has no class.");
            context.targetElement.EnableInClassList(ClassName, (number != 0f) != Invert);
            return default;
        }
    }

    [UxmlObject]
    public partial class TlTriggerBinding : DataBinding
    {
        [UxmlAttribute]
        public int Timeline;

        [UxmlAttribute]
        public UiTimelineVerb WhenOn = UiTimelineVerb.Play;

        [UxmlAttribute]
        public UiTimelineVerb WhenOff = UiTimelineVerb.Stop;

        bool _known;
        bool _on;

        public TlTriggerBinding()
        {
            bindingMode = BindingMode.ToTarget;
        }

        protected override BindingResult UpdateUI<TValue>(in BindingContext context, ref TValue value)
        {
            if (!TlBoundNumber.TryRead(ref value, out var number))
                return TlBoundNumber.NotANumber<TValue>();
            var on = number != 0f;
            if (_known && on == _on)
                return default;
            if (!_known && !on)
            {
                _known = true;
                _on = false;
                return default;
            }
            var panel = TlUiPanel.Owning(context.targetElement);
            if (panel == null)
                return new BindingResult(BindingStatus.Pending);
            _known = true;
            _on = on;
            panel.RequestTimeline(on ? WhenOn : WhenOff, Timeline);
            return default;
        }
    }

    [UxmlObject]
    public partial class TlSmoothBinding : DataBinding
    {
        [UxmlAttribute]
        public float Seconds = 0.35f;

        VisualElement _target;
        IVisualElementScheduledItem _step;
        float _from;
        float _to;
        float _shown;
        long _startedAt = -1;

        public float Shown => _shown;

        public TlSmoothBinding()
        {
            bindingMode = BindingMode.ToTarget;
        }

        void Show()
        {
            if (_target is INotifyValueChanged<float> valued)
            {
                valued.SetValueWithoutNotify(_shown);
                return;
            }
            if (_target is TextElement textElement)
            {
                var text = TlUiConverters.IntegerText(Mathf.RoundToInt(_shown));
                if (!ReferenceEquals(textElement.text, text))
                    textElement.text = text;
            }
        }

        void Step(TimerState timer)
        {
            if (_startedAt < 0)
                _startedAt = timer.now;
            var progress = Mathf.Clamp01((timer.now - _startedAt) * 0.001f / Seconds);
            var eased = 1f - (1f - progress) * (1f - progress);
            _shown = Mathf.LerpUnclamped(_from, _to, eased);
            Show();
            if (progress >= 1f)
                _step.Pause();
        }

        protected override BindingResult UpdateUI<TValue>(in BindingContext context, ref TValue value)
        {
            if (!TlBoundNumber.TryRead(ref value, out var number))
                return TlBoundNumber.NotANumber<TValue>();
            var element = context.targetElement;
            if (element != _target)
            {
                _step?.Pause();
                _step = null;
                _target = element;
                _from = _to = _shown = number;
                Show();
                return default;
            }
            if (number == _to)
                return default;
            _from = _shown;
            _to = number;
            _startedAt = -1;
            if (Seconds <= 0f)
            {
                _shown = number;
                Show();
                return default;
            }
            if (_step == null)
                _step = element.schedule.Execute(Step).Every(0);
            else
                _step.Resume();
            return default;
        }
    }
}
