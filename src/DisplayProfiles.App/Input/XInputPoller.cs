using DisplayProfiles.Core.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.XboxController;

namespace DisplayProfiles.App.Input;

/// <summary>Reads the first connected Xbox-compatible controller through XInput.</summary>
internal sealed class XInputPoller
{
    private const int Slots = 4;
    private const int RecheckEveryNthRead = 120;

    private readonly bool[] connected = new bool[Slots];
    private int reads;

    public bool TryRead(out PadState state)
    {
        // Asking a slot with no controller is slow, so only look for newly connected controllers now and then.
        bool recheck = reads++ % RecheckEveryNthRead == 0;
        for (uint slot = 0; slot < Slots; slot++)
        {
            if (!connected[slot] && !recheck)
            {
                continue;
            }

            connected[slot] = PInvoke.XInputGetState(slot, out XINPUT_STATE reading) == 0;
            if (connected[slot])
            {
                var pad = reading.Gamepad;
                state = new PadState((PadButtons)(ushort)pad.wButtons, pad.sThumbLX, pad.sThumbLY, pad.sThumbRX, pad.sThumbRY);
                return true;
            }
        }

        state = default;
        return false;
    }
}
