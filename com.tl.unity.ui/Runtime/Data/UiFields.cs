using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;

namespace TlUi
{
    public static class UiFields<T> where T : unmanaged
    {
        static readonly int[] s_Offsets;
        static readonly int[] s_Sizes;

        static UiFields()
        {
            var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            s_Offsets = new int[fields.Length];
            s_Sizes = new int[fields.Length];
            for (var field = 0; field < fields.Length; field++)
            {
                s_Offsets[field] = UnsafeUtility.GetFieldOffset(fields[field]);
                s_Sizes[field] = UnsafeUtility.SizeOf(fields[field].FieldType);
            }
        }

        public static int Count => s_Offsets.Length;

        public static unsafe T TakeChangedFields(T into, T changed, T before)
        {
            var target = (byte*)&into;
            var after = (byte*)&changed;
            var previous = (byte*)&before;
            for (var field = 0; field < s_Offsets.Length; field++)
            {
                var offset = s_Offsets[field];
                if (UnsafeUtility.MemCmp(after + offset, previous + offset, s_Sizes[field]) != 0)
                    UnsafeUtility.MemCpy(target + offset, after + offset, s_Sizes[field]);
            }
            return into;
        }
    }
}
