using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
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
        public const string FixedText = "tl-fixed-text";
        public const string Not = "tl-not";
        public const string Invisible = "tl-invisible";
        public const int CachedIntegers = 10000;

        static readonly string[] s_Integers = new string[CachedIntegers];

        public static string IntegerText(int value)
        {
            if (value < 0 || value >= s_Integers.Length)
                return value.ToString(CultureInfo.InvariantCulture);
            return s_Integers[value] ??= value.ToString(CultureInfo.InvariantCulture);
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

        static string TextOf(Dictionary<FixedString32Bytes, string> cache, FixedString32Bytes value)
        {
            if (!cache.TryGetValue(value, out var text))
                cache.Add(value, text = value.ToString());
            return text;
        }

        static string TextOf(Dictionary<FixedString64Bytes, string> cache, FixedString64Bytes value)
        {
            if (!cache.TryGetValue(value, out var text))
                cache.Add(value, text = value.ToString());
            return text;
        }

        static void RegisterOnce(ConverterGroup group)
        {
            if (!ConverterGroups.TryGetConverterGroup(group.id, out _))
                ConverterGroups.RegisterConverterGroup(group);
        }

        public static void Register()
        {
            if (!ConverterGroups.TryGetConverterGroup(Integer, out _))
            {
                for (var value = 0; value < 1000; value++)
                    s_Integers[value] ??= value.ToString(CultureInfo.InvariantCulture);
                var integer = new ConverterGroup(Integer);
                integer.AddConverter((ref int value) => IntegerText(value));
                integer.AddConverter((ref float value) => IntegerText(Mathf.RoundToInt(value)));
                RegisterOnce(integer);
            }

            if (!ConverterGroups.TryGetConverterGroup(Percent, out _))
            {
                var percentCache = new string[1001];
                for (var p = 0; p <= 100; p++)
                    percentCache[p] = p.ToString(CultureInfo.InvariantCulture) + "%";
                var percent = new ConverterGroup(Percent);
                percent.AddConverter((ref float value) => PercentText(percentCache, value));
                RegisterOnce(percent);
            }

            if (!ConverterGroups.TryGetConverterGroup(Clock, out _))
            {
                var clockCache = new string[6000];
                for (var s = 0; s < 600; s++)
                    clockCache[s] = (s / 60).ToString(CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
                var clock = new ConverterGroup(Clock);
                clock.AddConverter((ref float value) => ClockText(clockCache, value));
                clock.AddConverter((ref int value) => ClockText(clockCache, value));
                RegisterOnce(clock);
            }

            var visible = new ConverterGroup(Visible);
            visible.AddConverter((ref bool value) => new StyleEnum<DisplayStyle>(value ? DisplayStyle.Flex : DisplayStyle.None));
            RegisterOnce(visible);

            var shortTexts = new Dictionary<FixedString32Bytes, string>();
            var longTexts = new Dictionary<FixedString64Bytes, string>();
            var fixedText = new ConverterGroup(FixedText);
            fixedText.AddConverter((ref FixedString32Bytes value) => TextOf(shortTexts, value));
            fixedText.AddConverter((ref FixedString64Bytes value) => TextOf(longTexts, value));
            RegisterOnce(fixedText);

            var hidden = new ConverterGroup(Hidden);
            hidden.AddConverter((ref bool value) => new StyleEnum<DisplayStyle>(value ? DisplayStyle.None : DisplayStyle.Flex));
            RegisterOnce(hidden);

            var not = new ConverterGroup(Not);
            not.AddConverter((ref bool value) => !value);
            RegisterOnce(not);

            var invisible = new ConverterGroup(Invisible);
            invisible.AddConverter((ref bool value) => new StyleEnum<Visibility>(value ? Visibility.Hidden : Visibility.Visible));
            RegisterOnce(invisible);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterWhenThePlayerStarts() => Register();
    }
}
