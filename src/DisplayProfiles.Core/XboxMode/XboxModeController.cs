using DisplayProfiles.Core.Services;

namespace DisplayProfiles.Core.XboxMode;

/// <summary>Applies the configured "Xbox mode" profile when the PC enters Xbox mode (and the exit profile when it leaves).</summary>
public sealed class XboxModeController(IXboxModeSource source, ProfileService profiles)
{
    private readonly object gate = new();
    private bool? lastHandled;

    /// <summary>Time to let the shell settle before touching the displays.</summary>
    public TimeSpan SettleDelay { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Raised after a profile was applied (or failed) because of an Xbox mode change.</summary>
    public event Action<string, ApplyResult>? Applied;

    public void Start()
    {
        source.StateChanged += state => _ = HandleAsync(state);

        // The app may start after the PC is already in Xbox mode (e.g. when it is the shell at sign-in).
        if (source.IsActive)
        {
            _ = HandleAsync(true);
        }
    }

    internal async Task HandleAsync(bool state)
    {
        if (SettleDelay > TimeSpan.Zero)
        {
            await Task.Delay(SettleDelay).ConfigureAwait(false);
        }

        // Ignore flaps: only act if the state is still what we were told, and we haven't handled it already.
        if (source.IsActive != state)
        {
            return;
        }

        lock (gate)
        {
            if (lastHandled == state)
            {
                return;
            }

            lastHandled = state;
        }

        profiles.Reload();
        var settings = profiles.Data.Settings;
        var targetId = state ? settings.XboxEnterProfileId : settings.XboxExitProfileId;
        var target = targetId is { } id ? profiles.Data.Profiles.FirstOrDefault(p => p.Id == id) : null;
        if (target is null)
        {
            return;
        }

        if (profiles.FindActiveProfile()?.Id == target.Id)
        {
            return;
        }

        var result = await Task.Run(() => profiles.Apply(target)).ConfigureAwait(false);
        Applied?.Invoke(target.Name, result);
    }
}
