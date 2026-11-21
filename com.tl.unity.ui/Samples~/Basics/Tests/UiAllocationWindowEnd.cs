using System;
using TlUi;
using UnityEngine;

[DefaultExecutionOrder(TlUiOrder.Panel + 1)]
public sealed class UiAllocationWindowEnd : MonoBehaviour
{
    public UiAllocationWindowStart WindowStart;
    public bool Measuring;
    public long UiCodeAllocatedBytes;
    public long LargestUiCodeFrame;
    public int MeasuredFrames;
    public long LastFrameBytes;

    void Update()
    {
        var allocatedThisFrame = GC.GetAllocatedBytesForCurrentThread() - WindowStart.AllocatedBeforeUiCode;
        LastFrameBytes = allocatedThisFrame;
        if (!Measuring)
            return;
        UiCodeAllocatedBytes += allocatedThisFrame;
        LargestUiCodeFrame = Math.Max(LargestUiCodeFrame, allocatedThisFrame);
        MeasuredFrames++;
    }
}
