using DisplayProfiles.Core.Input;

namespace DisplayProfiles.App;

/// <summary>Implemented by controls that want to interpret controller input themselves (for example while a monitor is being moved).</summary>
internal interface IGamepadHandler
{
    /// <summary>Returns true if the action was used. <paramref name="focused"/> is the element that currently has focus.</summary>
    bool HandleGamepad(GamepadAction action, Microsoft.UI.Xaml.FrameworkElement? focused);
}

/// <summary>Implemented by pages to give the controller a Y action and to describe the buttons that are useful on that page.</summary>
internal interface IControllerPage
{
    IReadOnlyList<(string Button, string Text)> Hints { get; }

    /// <summary>Y was pressed; returns true if the page handled it.</summary>
    bool OnPrimaryAction();

    /// <summary>X was pressed; returns true if the page handled it.</summary>
    bool OnSecondaryAction();
}
