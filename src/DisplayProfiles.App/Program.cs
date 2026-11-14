using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.AppLifecycle;
using DisplayProfiles.Core.Cli;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Console;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DisplayProfiles.App;

public static class Program
{
    private const string InstanceKey = "DisplayProfiles.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        int exitCode = RunCommandLine(args);
        if (exitCode != CliRunner.NotHandled)
        {
            return exitCode;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();

        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            // Another copy owns the tray icon: ask it to show its window, then quit.
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            using var done = new ManualResetEventSlim();
            Task.Run(async () =>
            {
                await mainInstance.RedirectActivationToAsync(activation);
                done.Set();
            });
            done.Wait();
            return 0;
        }

        Application.Start(_ =>
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
            var app = new App(
                startHidden: args.Contains("--background", StringComparer.OrdinalIgnoreCase),
                mainInstance,
                initialPage: ValueAfter(args, "--page"));
            GC.KeepAlive(app);
        });
        return 0;
    }

    private static string? ValueAfter(string[] args, string flag)
    {
        int index = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static unsafe int RunCommandLine(string[] args)
    {
        if (args.Length == 0)
        {
            return CliRunner.NotHandled;
        }

        // A WinExe has no console of its own. Use redirected output if there is any (pipes, files),
        // else borrow the parent's console so `DisplayProfiles list` prints to the terminal.
        bool redirected = IsStdoutRedirected();
        bool hasConsole = !redirected && PInvoke.AttachConsole(PInvoke.ATTACH_PARENT_PROCESS);
        bool canPrint = redirected || hasConsole;
        TextWriter output = redirected
            ? new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true }
            : hasConsole ? OpenConsole() : new StringWriter();
        TextWriter error = canPrint ? output : new StringWriter();

        var profiles = AppServices.CreateProfiles();
        int code = CliRunner.Run(args, profiles, output, error);
        output.Flush();

        if (!canPrint && code != CliRunner.NotHandled && code != CliRunner.Success)
        {
            // Started from a shortcut: surface failures, since there is no console to print to.
            fixed (char* text = (error.ToString() ?? string.Empty).Trim())
            fixed (char* caption = "Display Profiles")
            {
                PInvoke.MessageBox(HWND.Null, text, caption, MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONERROR);
            }
        }

        return code;
    }

    private const uint GenericWrite = 0x40000000;

    private static unsafe bool IsStdoutRedirected()
    {
        var handle = PInvoke.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
        if ((nint)handle.Value is 0 or -1)
        {
            return false;
        }

        var type = PInvoke.GetFileType(handle);
        return type == FILE_TYPE.FILE_TYPE_DISK || type == FILE_TYPE.FILE_TYPE_PIPE;
    }

    private static unsafe StreamWriter OpenConsole()
    {
        fixed (char* conout = "CONOUT$")
        {
            var handle = PInvoke.CreateFile(
                conout,
                GenericWrite,
                FILE_SHARE_MODE.FILE_SHARE_WRITE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                HANDLE.Null);
            return new StreamWriter(
                new FileStream(new SafeFileHandle(handle, ownsHandle: true), FileAccess.Write),
                new UTF8Encoding(false)) { AutoFlush = true };
        }
    }
}
