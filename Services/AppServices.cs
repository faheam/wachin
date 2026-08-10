using Wachin.Models;

namespace Wachin.Services;

public static class AppServices
{
    public static AppState State { get; private set; } = null!;
    public static bool IsAdmin { get; private set; }
    public static string Version => "1.0.0";

    public static event Action<Toast>? ToastRequested;

    public static void Init()
    {
        State = AppState.Load();
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            IsAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            IsAdmin = false;
        }
    }

    public static void ShowToast(string title, string message, ToastKind kind = ToastKind.Info,
        string? actionText = null, Action? action = null)
    {
        ToastRequested?.Invoke(new Toast
        {
            Title = title, Message = message, Kind = kind,
            ActionText = actionText, Action = action
        });
    }

    public static void ShowSuccess(string title, string message)
        => ShowToast(title, message, ToastKind.Success);

    public static void ShowError(string title, string message)
        => ShowToast(title, message, ToastKind.Error);

    public static void ShowWarning(string title, string message)
        => ShowToast(title, message, ToastKind.Warning);
}


