namespace DisplayProfiles.Core.XboxMode;

/// <summary>Reports whether Windows is currently in the Xbox full screen experience ("Xbox mode").</summary>
public interface IXboxModeSource
{
    bool IsActive { get; }

    /// <summary>Raised when the state flips. May be raised on any thread.</summary>
    event Action<bool>? StateChanged;
}
