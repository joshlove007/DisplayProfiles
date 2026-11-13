namespace MonitorToggle.Core.Display;

public readonly record struct DisplayMode(int Width, int Height, int RefreshHz)
{
    public override string ToString() => $"{Width} × {Height}, {RefreshHz} Hz";
}
