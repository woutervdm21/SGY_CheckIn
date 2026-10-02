namespace SGY.CheckIn.Services;

public enum ToastKind
{
    Success,
    Error,
}

public sealed record Toast(int Id, ToastKind Kind, string Message);

/// <summary>
/// Pop-up confirmations along the bottom of the screen ("Saved", or why something failed),
/// rendered by ToastHost in the main layout. Scoped, so each device's circuit has its own
/// toasts, and they survive in-app navigation: a page can show one and then navigate away.
/// </summary>
public sealed class ToastService
{
    private static readonly TimeSpan SuccessDuration = TimeSpan.FromSeconds(4);

    // Errors stay up longer: the reason needs reading, not just noticing.
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(8);

    private readonly Lock gate = new();
    private readonly List<Toast> toasts = [];
    private int nextId;

    /// <summary>Raised from any thread whenever a toast appears or goes.</summary>
    public event Action? Changed;

    public IReadOnlyList<Toast> Current
    {
        get
        {
            lock (gate)
            {
                return [.. toasts];
            }
        }
    }

    public void ShowSuccess(string message) => Show(ToastKind.Success, message, SuccessDuration);

    public void ShowError(string message) => Show(ToastKind.Error, message, ErrorDuration);

    public void Dismiss(int id)
    {
        lock (gate)
        {
            if (toasts.RemoveAll(t => t.Id == id) == 0)
            {
                return;
            }
        }
        Changed?.Invoke();
    }

    private void Show(ToastKind kind, string message, TimeSpan duration)
    {
        Toast toast;
        lock (gate)
        {
            toast = new Toast(++nextId, kind, message);
            toasts.Add(toast);

            // Keep the pile short if several happen in a row.
            if (toasts.Count > 3)
            {
                toasts.RemoveAt(0);
            }
        }
        Changed?.Invoke();

        _ = DismissLaterAsync(toast.Id, duration);
    }

    private async Task DismissLaterAsync(int id, TimeSpan delay)
    {
        await Task.Delay(delay);
        Dismiss(id);
    }
}
