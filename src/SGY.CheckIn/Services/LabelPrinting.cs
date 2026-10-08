using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>What this device does with a Kids name label.</summary>
public enum LabelMode
{
    /// <summary>Prints it here: the computer with the DYMO, which is also a print station.</summary>
    Here,

    /// <summary>Sends it to a print station: a phone or tablet.</summary>
    Station,

    /// <summary>No labels from this device.</summary>
    Off,
}

/// <summary>
/// One device's label printing: its setting (kept in the browser by labels.js, as each
/// device has its own), and printing a child's label here or on a print station to match.
/// Scoped, so each browser tab's circuit has its own.
/// </summary>
public sealed class LabelPrinting(IJSRuntime js, PrintStations stations, ToastService toasts, ILogger<LabelPrinting> logger) : IDisposable
{
    /// <summary>A print station's name until it's given one.</summary>
    public const string DefaultStationName = "Label printer";

    // What labels.js keeps for this device.
    private sealed record Settings(string Mode, string Name, string Target);

    // One label at a time through this browser's printer, whether from here or another device.
    private readonly SemaphoreSlim printing = new(1, 1);
    private Task? loading;
    private DotNetObjectReference<LabelPrinting>? watcher;

    /// <summary>False until the setting has been read from the browser (after the first render).</summary>
    public bool Loaded { get; private set; }

    public LabelMode Mode { get; private set; } = LabelMode.Station;

    /// <summary>This device's name as a print station, shown to the phones sending to it.</summary>
    public string StationName { get; private set; } = DefaultStationName;

    /// <summary>The print station a phone sends to when more than one is on; empty for any.</summary>
    public string Target { get; private set; } = "";

    /// <summary>Whether this browser is connected to the server (a station that isn't can't print).</summary>
    public bool Connected { get; private set; } = true;

    /// <summary>Raised when the setting is read or changed, or the connection drops or returns.</summary>
    public event Action? Changed;

    /// <summary>Reads the setting from the browser, once. Only after the first render.</summary>
    public Task LoadAsync() => loading ??= LoadCoreAsync();

    private async Task LoadCoreAsync()
    {
        try
        {
            await ReadAsync();
            watcher = DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("sgLabels.watch", watcher);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            // Check-in still works; the device keeps the default until the page reloads.
            logger.LogWarning(ex, "Couldn't read this device's label setting");
        }
        Loaded = true;
        Changed?.Invoke();
    }

    /// <summary>Called by labels.js when another tab of this browser changes the setting.</summary>
    [JSInvokable]
    public async Task ReloadAsync()
    {
        try
        {
            await ReadAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Couldn't read this device's label setting");
            return;
        }
        Changed?.Invoke();
    }

    private async Task ReadAsync()
    {
        var settings = await js.InvokeAsync<Settings>("sgLabels.settings");
        Mode = ParseMode(settings.Mode);
        StationName = string.IsNullOrWhiteSpace(settings.Name) ? DefaultStationName : settings.Name.Trim();
        Target = settings.Target ?? "";
    }

    public Task SetModeAsync(LabelMode mode) => SaveAsync("mode", mode.ToString().ToLowerInvariant(), () => Mode = mode);

    public Task SetStationNameAsync(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? DefaultStationName : name.Trim();
        return SaveAsync("name", name, () => StationName = name);
    }

    public Task SetTargetAsync(string target) => SaveAsync("target", target, () => Target = target);

    internal void SetConnected(bool connected)
    {
        if (Connected != connected)
        {
            Connected = connected;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Prints a child's name label the way this device is set up to: here, on a print
    /// station, or not at all. Says what happened in a toast, unless it printed here.
    /// </summary>
    public async Task PrintAsync(Youth child, Group group)
    {
        if (!group.PrintsLabels())
        {
            return;
        }
        await LoadAsync();

        switch (Mode)
        {
            case LabelMode.Here:
                if (!await PrintHereAsync(child.Id, group))
                {
                    toasts.ShowError($"Couldn't print {child.Name}'s label. Check the printer, then find them and press Print label.");
                }
                break;

            case LabelMode.Station:
                var result = await stations.SendAsync(group, new LabelJob(child.Id, child.FullName), Target);
                switch (result.Outcome)
                {
                    case LabelSendOutcome.Sent:
                        toasts.ShowSuccess($"{child.Name}'s label sent to {result.Station}");
                        break;
                    case LabelSendOutcome.NoStation:
                        toasts.ShowError($"No label printer is on, so {child.Name} has no label. Write one by hand, or press Print label once it's on.");
                        break;
                    default:
                        toasts.ShowError($"The label printer didn't print {child.Name}'s label. Check it, then press Print label.");
                        break;
                }
                break;
        }
    }

    /// <summary>
    /// Prints a label on this browser's printer, one at a time, and says whether it went to
    /// the printer. Used for this device's own labels and, on a print station, for the ones
    /// sent from other devices.
    /// </summary>
    public async Task<bool> PrintHereAsync(int youthId, Group group, CancellationToken ct = default)
    {
        await printing.WaitAsync(ct);
        try
        {
            return await js.InvokeAsync<bool>("sgLabels.print", ct, $"/labels/{youthId}?group={group.ToKey()}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Printing the label for {YouthId} failed", youthId);
            return false;
        }
        finally
        {
            printing.Release();
        }
    }

    public void Dispose() => watcher?.Dispose();

    private async Task SaveAsync(string key, string value, Action apply)
    {
        apply();
        Changed?.Invoke();
        try
        {
            await js.InvokeVoidAsync("sgLabels.save", key, value);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Couldn't save this device's label setting");
        }
    }

    // Anything unknown sends: only the computer with the DYMO should print itself.
    private static LabelMode ParseMode(string? mode) => mode switch
    {
        "here" => LabelMode.Here,
        "off" => LabelMode.Off,
        _ => LabelMode.Station,
    };
}

/// <summary>
/// Tells this circuit's <see cref="LabelPrinting"/> when the browser's connection drops and
/// returns, so a print station that's lost its connection stops taking labels at once,
/// rather than phones waiting on it until it times out.
/// </summary>
public sealed class LabelPrintingCircuitHandler(LabelPrinting labels) : CircuitHandler
{
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        labels.SetConnected(false);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        labels.SetConnected(true);
        return Task.CompletedTask;
    }
}
