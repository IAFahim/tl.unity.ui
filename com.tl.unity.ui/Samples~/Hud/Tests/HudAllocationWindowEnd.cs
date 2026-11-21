using System;
using TlUi;
using UnityEngine;

[DefaultExecutionOrder(TlUiOrder.Panel + 1)]
public sealed class HudAllocationWindowEnd : MonoBehaviour
{
    public HudAllocationWindowStart WindowStart;
    public bool Measuring;
    public long UiCodeAllocatedBytes;
    public int MeasuredFrames;

    void Update()
    {
        var allocatedThisFrame = GC.GetAllocatedBytesForCurrentThread() - WindowStart.AllocatedBeforeUiCode;
        if (!Measuring)
            return;
        UiCodeAllocatedBytes += allocatedThisFrame;
        MeasuredFrames++;
    }
}
