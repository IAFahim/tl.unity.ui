using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace TlUi
{
    [UxmlElement]
    public partial class TlRadialFill : VisualElement, INotifyValueChanged<float>
    {
        public const string UssClassName = "tl-radial-fill";
        public static readonly BindingId ValueProperty = nameof(value);

        static readonly CustomStyleProperty<Color> s_FillColor = new CustomStyleProperty<Color>("--tl-fill-color");
        static readonly CustomStyleProperty<Color> s_TrackColor = new CustomStyleProperty<Color>("--tl-track-color");
        static readonly CustomStyleProperty<float> s_Thickness = new CustomStyleProperty<float>("--tl-thickness");

        float m_Value;
        Color m_FillColor = Color.white;
        Color m_TrackColor = new Color(1f, 1f, 1f, 0.2f);
        float m_Thickness = 8f;

        [UxmlAttribute]
        [CreateProperty]
        public float value
        {
            get => m_Value;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (clamped == m_Value)
                    return;
                var previous = m_Value;
                SetValueWithoutNotify(clamped);
                using (var changed = ChangeEvent<float>.GetPooled(previous, clamped))
                {
                    changed.target = this;
                    SendEvent(changed);
                }
                NotifyPropertyChanged(ValueProperty);
            }
        }

        public TlRadialFill()
        {
            AddToClassList(UssClassName);
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(ReadTheCustomStyle);
        }

        public void SetValueWithoutNotify(float newValue)
        {
            m_Value = Mathf.Clamp01(newValue);
            MarkDirtyRepaint();
        }

        void ReadTheCustomStyle(CustomStyleResolvedEvent resolved)
        {
            var style = resolved.customStyle;
            if (style.TryGetValue(s_FillColor, out var fill))
                m_FillColor = fill;
            if (style.TryGetValue(s_TrackColor, out var track))
                m_TrackColor = track;
            if (style.TryGetValue(s_Thickness, out var thickness))
                m_Thickness = thickness;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var area = contentRect;
            var radius = Mathf.Min(area.width, area.height) * 0.5f - m_Thickness * 0.5f;
            if (radius <= 0f)
                return;
            var painter = context.painter2D;
            painter.lineWidth = m_Thickness;
            painter.lineCap = LineCap.Butt;
            painter.strokeColor = m_TrackColor;
            painter.BeginPath();
            painter.Arc(area.center, radius, 0f, 360f);
            painter.Stroke();
            if (m_Value <= 0f)
                return;
            painter.strokeColor = m_FillColor;
            painter.BeginPath();
            painter.Arc(area.center, radius, -90f, -90f + 360f * m_Value);
            painter.Stroke();
        }
    }
}
