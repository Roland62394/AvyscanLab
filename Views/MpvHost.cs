using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace CleanScan.Views;

public sealed class MpvHost : NativeControlHost
{
    public event Action<nint>? HandleReady;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        HandleReady?.Invoke(handle.Handle);
        return handle;
    }
}
