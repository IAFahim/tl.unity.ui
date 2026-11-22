using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;

namespace TlUi
{
    public static class UiToolkitTickProbe
    {
        struct BeforeUiToolkitUpdate
        {
        }

        struct AfterUiToolkitUpdate
        {
        }

        const string UiToolkitUpdate = "UIElementsUpdatePanels";

        static long s_StartedAt;
        static bool s_Installed;

        public static bool Measuring;
        public static long AllocatedBytes;
        public static int Ticks;
        public static long LargestTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetThePreviousPlay()
        {
            s_Installed = false;
            Measuring = false;
            Reset();
        }

        public static void Reset()
        {
            AllocatedBytes = 0;
            Ticks = 0;
            LargestTick = 0;
        }

        public static bool Install()
        {
            if (s_Installed)
                return true;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (!Wrap(ref loop))
                return false;
            PlayerLoop.SetPlayerLoop(loop);
            s_Installed = true;
            return true;
        }

        public static void Uninstall()
        {
            if (!s_Installed)
                return;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Unwrap(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
            s_Installed = false;
            Measuring = false;
        }

        static bool Wrap(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null)
                return false;
            var systems = new List<PlayerLoopSystem>(system.subSystemList);
            for (var index = 0; index < systems.Count; index++)
            {
                if (systems[index].type != null && systems[index].type.Name == UiToolkitUpdate)
                {
                    systems.Insert(index + 1, new PlayerLoopSystem { type = typeof(AfterUiToolkitUpdate), updateDelegate = Stop });
                    systems.Insert(index, new PlayerLoopSystem { type = typeof(BeforeUiToolkitUpdate), updateDelegate = Start });
                    system.subSystemList = systems.ToArray();
                    return true;
                }
                var child = systems[index];
                if (Wrap(ref child))
                {
                    systems[index] = child;
                    system.subSystemList = systems.ToArray();
                    return true;
                }
            }
            return false;
        }

        static void Unwrap(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null)
                return;
            var systems = new List<PlayerLoopSystem>(system.subSystemList);
            systems.RemoveAll(child => child.type == typeof(BeforeUiToolkitUpdate) || child.type == typeof(AfterUiToolkitUpdate));
            for (var index = 0; index < systems.Count; index++)
            {
                var child = systems[index];
                Unwrap(ref child);
                systems[index] = child;
            }
            system.subSystemList = systems.ToArray();
        }

        static void Start()
        {
            if (Measuring)
                s_StartedAt = GC.GetAllocatedBytesForCurrentThread();
        }

        static void Stop()
        {
            if (!Measuring)
                return;
            var tick = GC.GetAllocatedBytesForCurrentThread() - s_StartedAt;
            AllocatedBytes += tick;
            if (tick > LargestTick)
                LargestTick = tick;
            Ticks++;
        }
    }
}
