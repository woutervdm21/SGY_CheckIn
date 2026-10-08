using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SGY.CheckIn.Services;

/// <summary>
/// The address phones reach the app on, for the check-out QR code on Kids labels: the one
/// the printing computer has the app open on. Unless that's this computer itself
/// ("localhost", when the app runs on the computer with the printer): a phone can't open
/// that, so it becomes this computer's address on the network, on the same port.
/// </summary>
/// <remarks>
/// Setting <c>PublicAddress</c> (e.g. <c>http://192.168.0.10:8080</c>) overrides all this,
/// for a network where the guess is wrong.
/// </remarks>
public static class AppAddress
{
    public static string For(HttpRequest request, IConfiguration config)
    {
        if (config["PublicAddress"] is { Length: > 0 } configured)
        {
            return configured.TrimEnd('/');
        }

        var host = request.Host;
        if (IsThisComputer(host.Host) && NetworkAddress() is { } lan)
        {
            host = host.Port is { } port ? new HostString(lan, port) : new HostString(lan);
        }
        return $"{request.Scheme}://{host}";
    }

    private static bool IsThisComputer(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));

    // The IPv4 address of the network connection with a router (Wi-Fi or cable), not a
    // virtual one with none, such as Hyper-V's or Docker's.
    private static string? NetworkAddress()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(n => n.GetIPProperties())
                .Where(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
                .SelectMany(p => p.UnicastAddresses)
                .Select(a => a.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                ?.ToString();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }
}
