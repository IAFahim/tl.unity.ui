using System.Collections.Generic;

namespace Tl.Ui
{
    public readonly struct UiSelectorStep
    {
        public UiSelectorStep(string typeName, string name, string[] classes)
        {
            TypeName = typeName;
            Name = name;
            Classes = classes;
        }

        public readonly string TypeName;
        public readonly string Name;
        public readonly string[] Classes;

        public bool Matches(string typeName, string name, IUiClassList classes)
        {
            if (TypeName.Length > 0 && TypeName != typeName)
                return false;
            if (Name.Length > 0 && Name != name)
                return false;
            foreach (var className in Classes)
                if (!classes.HasClass(className))
                    return false;
            return true;
        }
    }

    public interface IUiClassList
    {
        bool HasClass(string className);
    }

    public static class UiSelector
    {
        static UiSelectorStep ParseStep(string text)
        {
            var typeName = "";
            var name = "";
            var classes = new List<string>();
            var index = 0;
            while (index < text.Length)
            {
                var marker = text[index];
                var start = marker == '#' || marker == '.' ? index + 1 : index;
                var end = start;
                while (end < text.Length && text[end] != '#' && text[end] != '.')
                    end++;
                var part = text.Substring(start, end - start);
                if (marker == '#')
                    name = part;
                else if (marker == '.')
                    classes.Add(part);
                else
                    typeName = part;
                index = end;
            }
            return new UiSelectorStep(typeName, name, classes.ToArray());
        }

        public static UiSelectorStep[] Parse(string selector)
        {
            var steps = new List<UiSelectorStep>();
            foreach (var word in (selector ?? "").Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
                steps.Add(ParseStep(word));
            return steps.ToArray();
        }
    }
}
