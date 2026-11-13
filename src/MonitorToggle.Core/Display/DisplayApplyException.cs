namespace MonitorToggle.Core.Display;

public sealed class DisplayApplyException(string message, Exception? inner = null) : Exception(message, inner);
