using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DisplayProfiles.Core.XboxMode;

/// <summary>
/// Detects the Xbox full screen experience through Microsoft's documented gamingexperience.h API
/// (IsGamingFullScreenExperienceActive plus a change notification). The API set is loaded dynamically so the
/// app still runs on Windows builds that don't have it.
/// </summary>
public sealed unsafe class FullScreenExperienceMonitor : IXboxModeSource, IDisposable
{
    private const string ApiSet = "api-ms-win-gaming-experience-l1-1-0.dll";

    private readonly nint library;
    private readonly delegate* unmanaged[Stdcall]<int> isActive;
    private readonly delegate* unmanaged[Stdcall]<delegate* unmanaged[Stdcall]<void*, void>, void*, nint*, int> register;
    private readonly delegate* unmanaged[Stdcall]<nint, void> unregister;
    private readonly object gate = new();
    private GCHandle self;
    private nint registration;
    private bool last;

    private FullScreenExperienceMonitor(
        nint library,
        delegate* unmanaged[Stdcall]<int> isActive,
        delegate* unmanaged[Stdcall]<delegate* unmanaged[Stdcall]<void*, void>, void*, nint*, int> register,
        delegate* unmanaged[Stdcall]<nint, void> unregister)
    {
        this.library = library;
        this.isActive = isActive;
        this.register = register;
        this.unregister = unregister;
    }

    public event Action<bool>? StateChanged;

    public bool IsActive => isActive() != 0;

    /// <summary>Returns null when this Windows build has no Xbox full screen experience API.</summary>
    public static FullScreenExperienceMonitor? TryCreate()
    {
        if (!NativeLibrary.TryLoad(ApiSet, out nint library))
        {
            return null;
        }

        if (NativeLibrary.TryGetExport(library, "IsGamingFullScreenExperienceActive", out nint active) &&
            NativeLibrary.TryGetExport(library, "RegisterGamingFullScreenExperienceChangeNotification", out nint reg) &&
            NativeLibrary.TryGetExport(library, "UnregisterGamingFullScreenExperienceChangeNotification", out nint unreg))
        {
            return new FullScreenExperienceMonitor(
                library,
                (delegate* unmanaged[Stdcall]<int>)active,
                (delegate* unmanaged[Stdcall]<delegate* unmanaged[Stdcall]<void*, void>, void*, nint*, int>)reg,
                (delegate* unmanaged[Stdcall]<nint, void>)unreg);
        }

        NativeLibrary.Free(library);
        return null;
    }

    /// <summary>Starts listening for changes. Returns false if Windows refused the registration.</summary>
    public bool Start()
    {
        if (registration != 0)
        {
            return true;
        }

        last = IsActive;
        self = GCHandle.Alloc(this);
        nint handle;
        int hr = register(&OnChanged, (void*)GCHandle.ToIntPtr(self), &handle);
        if (hr < 0)
        {
            self.Free();
            return false;
        }

        registration = handle;
        return true;
    }

    public void Dispose()
    {
        if (registration != 0)
        {
            unregister(registration);
            registration = 0;
        }

        if (self.IsAllocated)
        {
            self.Free();
        }

        NativeLibrary.Free(library);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnChanged(void* context)
    {
        if (GCHandle.FromIntPtr((nint)context).Target is FullScreenExperienceMonitor monitor)
        {
            monitor.Refresh();
        }
    }

    // The callback only says "something changed"; re-query and report real flips.
    private void Refresh()
    {
        bool now = IsActive;
        lock (gate)
        {
            if (now == last)
            {
                return;
            }

            last = now;
        }

        StateChanged?.Invoke(now);
    }
}
