using System;
using TlUi;
using UnityEngine;

[DefaultExecutionOrder(TlUiOrder.Panel + 1)]
public sealed class DocumentAllocationEnd : MonoBehaviour
{
    public DocumentAllocationStart WindowStart;
    public bool Measuring;
    public long UiCodeAllocatedBytes;
    public readonly long[] BytesPerMeasuredFrame = new long[512];
    public int MeasuredFrames;

    void Update()
    {
        var allocatedThisFrame = GC.GetAllocatedBytesForCurrentThread() - WindowStart.AllocatedBeforeUiCode;
        if (!Measuring)
            return;
        UiCodeAllocatedBytes += allocatedThisFrame;
        if (MeasuredFrames < BytesPerMeasuredFrame.Length)
            BytesPerMeasuredFrame[MeasuredFrames] = allocatedThisFrame;
        MeasuredFrames++;
    }
}
