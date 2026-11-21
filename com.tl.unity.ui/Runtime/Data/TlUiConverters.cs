using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace TlUi
{
    public static class TlUiConverters
    {
        public const string Integer = "tl-integer";
        public const string Percent = "tl-percent";
        public const string Clock = "tl-clock";
        public const string Visible = "tl-visible";
        public const string Hidden = "tl-hidden";
        public const int CachedIntegers = 10000;

        static string IntegerText(string[] cache, int value)
        {
            if (value < 0 || value >= cache.Length)
                return value.ToString(CultureInfo.InvariantCulture);
            return cache[value] ??= value.ToString(CultureInfo.InvariantCulture);
        }

        static string PercentText(string[] cache, float fraction)
        {
            var percent = Mathf.RoundToInt(fraction * 100f);
            if (percent < 0 || percent >= cache.Length)
                return percent.ToString(CultureInfo.InvariantCulture) + "%";
            return cache[percent] ??= percent.ToString(CultureInfo.InvariantCulture) + "%";
        }

        static string ClockText(string[] cache, float seconds)
        {
            var wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            if (wholeSeconds >= cache.Length)
                return (wholeSeconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (wholeSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
            return cache[wholeSeconds] ??= (wholeSeconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (wholeSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        static void RegisterOnce(ConverterGroup group)
        {
            if (!ConverterGroups.TryGetConverterGroup(group.id, out _))
                ConverterGroups.RegisterConverterGroup(group);
        }

        public static void Register()
        {
            var integerCache = new string[CachedIntegers];
            var integer = new ConverterGroup(Integer);
            integer.AddConverter((ref int value) => IntegerText(integerCache, value));
            integer.AddConverter((ref float value) => IntegerText(integerCache, Mathf.RoundToInt(value)));
            RegisterOnce(integer);

            var percentCache = new string[1001];
            var percent = new ConverterGroup(Percent);
            percent.AddConverter((ref float value) => PercentText(percentCache, value));
            RegisterOnce(percent);

            var clockCache = new string[6000];
            var clock = new ConverterGroup(Clock);
            clock.AddConverter((ref float value) => ClockText(clockCache, value));
            clock.AddConverter((ref int value) => ClockText(clockCache, value));
            RegisterOnce(clock);

            var visible = new ConverterGroup(Visible);
            visible.AddConverter((ref bool value) => new StyleEnum<DisplayStyle>(value ? DisplayStyle.Flex : DisplayStyle.None));
            RegisterOnce(visible);

            var hidden = new ConverterGroup(Hidden);
            hidden.AddConverter((ref bool value) => new StyleEnum<DisplayStyle>(value ? DisplayStyle.None : DisplayStyle.Flex));
            RegisterOnce(hidden);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterWhenThePlayerStarts() => Register();
    }
}
