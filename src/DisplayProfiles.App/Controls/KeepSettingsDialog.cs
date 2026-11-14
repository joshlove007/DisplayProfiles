using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DisplayProfiles.App.Controls;

/// <summary>The "Keep these display settings?" prompt: reverts automatically unless the user confirms in time.</summary>
internal static class KeepSettingsDialog
{
    /// <summary>Returns true if the user chose to keep the new settings.</summary>
    public static async Task<bool> ShowAsync(XamlRoot root, int seconds)
    {
        if (ControllerUi.IsActive && OverlayHost.Current is { } overlay)
        {
            return await overlay.ShowConfirmAsync(
                "Keep these display settings?",
                string.Empty,
                "Keep changes",
                "Revert",
                defaultToSecondary: true,
                countdownSeconds: seconds,
                countdownMessage: left => $"Your previous display settings will be restored in {left} seconds.");
        }

        int remaining = seconds;
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        void UpdateText() => text.Text = $"Your previous display settings will be restored in {remaining} seconds.";
        UpdateText();

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Keep these display settings?",
            Content = text,
            PrimaryButtonText = "Keep changes",
            SecondaryButtonText = "Revert",
            DefaultButton = ContentDialogButton.Secondary,
        };

        var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += (_, _) =>
        {
            remaining--;
            if (remaining <= 0)
            {
                timer.Stop();
                dialog.Hide();
            }
            else
            {
                UpdateText();
            }
        };
        timer.Start();

        var result = await dialog.ShowAsync();
        timer.Stop();
        return result == ContentDialogResult.Primary;
    }
}
