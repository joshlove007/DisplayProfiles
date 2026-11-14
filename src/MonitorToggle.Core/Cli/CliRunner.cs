using MonitorToggle.Core.Integration;
using MonitorToggle.Core.Model;
using MonitorToggle.Core.Services;

namespace MonitorToggle.Core.Cli;

/// <summary>
/// Command-line surface of the app: <c>apply &lt;profile&gt;</c>, <c>toggle</c>, <c>next</c>, <c>list</c>,
/// <c>current</c> and <c>capture &lt;name&gt;</c>. Shortcuts created by the app use these commands.
/// </summary>
public static class CliRunner
{
    public const int NotHandled = -1;
    public const int Success = 0;
    public const int Failure = 1;
    public const int Usage = 2;

    public const string HelpText = """
        MonitorToggle commands:
          apply <profile>    Switch to a profile (name, unique name prefix, or id)
          toggle | next      Switch to the next profile in the list
          list               List profiles; the active one is marked with *
          current            Show the active profile
          capture <name>     Save the current display layout as a new profile
          refresh            Update the profile list shown by the Game Bar widget
          help               Show this help
        Links such as monitortoggle:apply/TV or monitortoggle:next work in place of a command.
        Start without a command to open the app. Use --background to start only the tray icon.
        """;

    /// <summary>Runs a command; returns <see cref="NotHandled"/> when the arguments are not a CLI command (start the GUI).</summary>
    public static int Run(string[] args, ProfileService service, TextWriter output, TextWriter error)
    {
        if (args.Length > 0 && WidgetBridge.TranslateUri(args[0]) is { } translated)
        {
            return Run(translated, service, output, error);
        }

        if (args.Length > 0 && args[0].StartsWith(WidgetBridge.UriScheme + ":", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"Unrecognized link '{args[0]}'.");
            return Usage;
        }

        if (args.Length == 0 || (args[0].StartsWith("--", StringComparison.Ordinal) && args[0] != "--help"))
        {
            return NotHandled;
        }

        string command = args[0].ToLowerInvariant();
        string[] rest = args[1..];

        switch (command)
        {
            case "apply":
                return Apply(rest, service, output, error);
            case "toggle":
            case "next":
                return Next(service, output, error);
            case "list":
                return List(service, output);
            case "current":
                return Current(service, output);
            case "capture":
                return Capture(rest, service, output, error);
            case "refresh":
                WidgetBridge.Publish(service);
                return Success;
            case "help":
            case "--help":
            case "-h":
            case "/?":
                output.WriteLine(HelpText);
                return Success;
            default:
                error.WriteLine($"Unknown command '{args[0]}'.");
                error.WriteLine(HelpText);
                return Usage;
        }
    }

    private static int Apply(string[] args, ProfileService service, TextWriter output, TextWriter error)
    {
        if (args.Length == 0)
        {
            error.WriteLine("Usage: apply <profile>");
            return Usage;
        }

        string name = string.Join(' ', args);
        var profile = service.Resolve(name);
        if (profile is null)
        {
            error.WriteLine($"No profile matches '{name}'.");
            return Failure;
        }

        return ApplyProfile(service, profile, output, error);
    }

    private static int Next(ProfileService service, TextWriter output, TextWriter error)
    {
        var profile = service.NextProfile();
        if (profile is null)
        {
            error.WriteLine("There are no profiles yet. Create one in the app or run: capture <name>");
            return Failure;
        }

        return ApplyProfile(service, profile, output, error);
    }

    private static int ApplyProfile(ProfileService service, Profile profile, TextWriter output, TextWriter error)
    {
        var result = service.Apply(profile);
        foreach (string warning in result.Warnings)
        {
            output.WriteLine($"Warning: {warning}");
        }

        if (!result.Success)
        {
            error.WriteLine(result.Error);
            return Failure;
        }

        output.WriteLine($"Switched to '{profile.Name}'.");
        return Success;
    }

    private static int List(ProfileService service, TextWriter output)
    {
        var active = service.FindActiveProfile();
        foreach (var profile in service.Data.Profiles)
        {
            output.WriteLine($"{(profile.Id == active?.Id ? "*" : " ")} {profile.Name}");
        }

        return Success;
    }

    private static int Current(ProfileService service, TextWriter output)
    {
        var active = service.FindActiveProfile();
        output.WriteLine(active is null ? "The current layout does not match any profile." : active.Name);
        return Success;
    }

    private static int Capture(string[] args, ProfileService service, TextWriter output, TextWriter error)
    {
        string name = string.Join(' ', args).Trim();
        if (name.Length == 0)
        {
            error.WriteLine("Usage: capture <name>");
            return Usage;
        }

        if (service.Resolve(name) is { } existing && string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"A profile named '{existing.Name}' already exists.");
            return Failure;
        }

        var profile = Profile.Create(name, service.CaptureLayout());
        service.Update(d => d with { Profiles = [.. d.Profiles, profile] });
        output.WriteLine($"Saved the current layout as '{name}'.");
        return Success;
    }
}
