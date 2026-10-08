using SGY.CheckIn.Models;

namespace SGY.CheckIn.Services;

/// <summary>A child's name label to print, sent from one device to a print station.</summary>
public sealed record LabelJob(int YouthId, string ChildName);

public enum LabelSendOutcome
{
    /// <summary>A print station took the label and sent it to its printer.</summary>
    Sent,

    /// <summary>No print station is on for the group.</summary>
    NoStation,

    /// <summary>Print stations are on, but none managed to print it.</summary>
    Failed,
}

/// <summary>How sending a label went, and which print station printed it.</summary>
public sealed record LabelSendResult(LabelSendOutcome Outcome, string? Station = null);

/// <summary>
/// The computers with a label printer ("print stations") that are open right now, so a
/// phone or tablet can have its labels printed on one. A station is a browser on the
/// computer with the DYMO, set to print labels here, with the app open on any page
/// (PrintStationListener in the main layout signs it up). Every device talks to this one
/// server, so a phone's label goes: phone → here → the station's browser → its printer.
/// </summary>
/// <remarks>
/// A label goes to the station the phone picked if it's on, otherwise the one that came on
/// most recently; if that one doesn't answer, the next. Nothing is queued for later: a
/// label printed after the child has gone in is no use, so the phone is told straight away
/// instead and a leader writes one by hand. Stations only live in memory; each one signs
/// up again when the app reconnects or restarts.
/// </remarks>
public sealed class PrintStations(ILogger<PrintStations> logger)
{
    /// <summary>
    /// How long a station gets to load and print a label: well beyond the few seconds it
    /// takes, short enough that a phone isn't left waiting on a station that's gone.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(12);

    private sealed record Station(Guid Id, Group Group, string Name, long Order, Func<LabelJob, CancellationToken, Task<bool>> Print);

    private readonly Lock gate = new();
    private readonly List<Station> stations = [];
    private long order;

    /// <summary>Raised from any thread when a station comes on or goes off.</summary>
    public event Action? Changed;

    /// <summary>
    /// Signs up a print station for <paramref name="group"/> until the returned handle is
    /// disposed. <paramref name="print"/> prints a label on the station and says whether it did.
    /// </summary>
    public IDisposable Register(Group group, string name, Func<LabelJob, CancellationToken, Task<bool>> print)
    {
        var station = new Station(Guid.NewGuid(), group, name, Interlocked.Increment(ref order), print);
        lock (gate)
        {
            stations.Add(station);
        }
        logger.LogInformation("Print station {Name} on for {Group}", name, group);
        Changed?.Invoke();
        return new Registration(this, station.Id);
    }

    /// <summary>The names of the group's stations that are on, most recently on first.</summary>
    public IReadOnlyList<string> Online(Group group)
    {
        lock (gate)
        {
            return [.. stations.Where(s => s.Group == group).OrderByDescending(s => s.Order).Select(s => s.Name).Distinct()];
        }
    }

    /// <summary>
    /// Prints <paramref name="job"/> on one of the group's stations: <paramref name="preferred"/>
    /// if it's on, otherwise the most recently on, trying the next if one fails.
    /// </summary>
    public async Task<LabelSendResult> SendAsync(Group group, LabelJob job, string? preferred, CancellationToken ct = default)
    {
        List<Station> candidates;
        lock (gate)
        {
            candidates = [.. stations
                .Where(s => s.Group == group)
                .OrderByDescending(s => s.Name == preferred)
                .ThenByDescending(s => s.Order)];
        }
        if (candidates.Count == 0)
        {
            return new(LabelSendOutcome.NoStation);
        }

        foreach (var station in candidates)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            try
            {
                if (await station.Print(job, timeout.Token))
                {
                    return new(LabelSendOutcome.Sent, station.Name);
                }
                logger.LogWarning("Print station {Name} couldn't print the label for {YouthId}", station.Name, job.YouthId);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Print station {Name} didn't print the label for {YouthId}", station.Name, job.YouthId);
            }
        }
        return new(LabelSendOutcome.Failed);
    }

    private void Unregister(Guid id)
    {
        Station? removed;
        lock (gate)
        {
            removed = stations.Find(s => s.Id == id);
            if (removed is null)
            {
                return;
            }
            stations.Remove(removed);
        }
        logger.LogInformation("Print station {Name} off for {Group}", removed.Name, removed.Group);
        Changed?.Invoke();
    }

    private sealed class Registration(PrintStations owner, Guid id) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.Unregister(id);
            }
        }
    }
}
