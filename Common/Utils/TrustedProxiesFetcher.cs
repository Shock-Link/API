using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace OpenShock.Common.Utils;

public static class TrustedProxiesFetcher
{
    private static readonly HttpClient Client = new();

    public static readonly string[] PrivateNetworks =
    [
        // Loopback
        "127.0.0.0/8",
        "::1/128",
        "::ffff:127.0.0.0/8",
        
        // Private IPv4
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",

        // Private IPv6
        "fc00::/7",
        "fe80::/10",
    ];

    private static readonly IPNetwork[] PrivateNetworksParsed =
        PrivateNetworks.Select(x => IPNetwork.Parse(x)).ToArray();

    // Fetched 00:00:00 30/09/2024 UTC
    private static readonly IPNetwork[] PrefetchedCloudflareProxies = 
    {
        // IPv4
        IPNetwork.Parse("173.245.48.0/20"),
        IPNetwork.Parse("103.21.244.0/22"),
        IPNetwork.Parse("103.22.200.0/22"),
        IPNetwork.Parse("103.31.4.0/22"),
        IPNetwork.Parse("141.101.64.0/18"),
        IPNetwork.Parse("108.162.192.0/18"),
        IPNetwork.Parse("190.93.240.0/20"),
        IPNetwork.Parse("188.114.96.0/20"),
        IPNetwork.Parse("197.234.240.0/22"),
        IPNetwork.Parse("198.41.128.0/17"),
        IPNetwork.Parse("162.158.0.0/15"),
        IPNetwork.Parse("104.16.0.0/13"),
        IPNetwork.Parse("104.24.0.0/14"),
        IPNetwork.Parse("172.64.0.0/13"),
        IPNetwork.Parse("131.0.72.0/22"),

        // IPv6
        IPNetwork.Parse("2400:cb00::/32"),
        IPNetwork.Parse("2606:4700::/32"),
        IPNetwork.Parse("2803:f800::/32"),
        IPNetwork.Parse("2405:b500::/32"),
        IPNetwork.Parse("2405:8100::/32"),
        IPNetwork.Parse("2a06:98c0::/29"),
        IPNetwork.Parse("2c0f:f248::/32"),
    };

    private static readonly char[] NewLineSeperators = [' ', '\r', '\n', '\t'];
    
    private static async Task<List<IPNetwork>> FetchCloudflareIPs(Uri uri, CancellationToken ct)
    {
        using var response = await Client.GetAsync(uri, ct);
        var stringResponse = await response.Content.ReadAsStringAsync(ct);
        
        return ParseNetworks(stringResponse.AsSpan());
    }

    private static List<IPNetwork> ParseNetworks(ReadOnlySpan<char> response)
    {
        var ranges = response.Split(NewLineSeperators);

        var networks = new List<IPNetwork>();

        foreach (var range in ranges)
        {
            networks.Add(IPNetwork.Parse(response[range]));
        }

        return networks;
    }
    
    private static async Task<IPNetwork[]> FetchCloudflareIPs()
    {
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5)); // Don't want to make application startup slow
            var ct = cts.Token;
            
            var v4Task = FetchCloudflareIPs(new Uri("https://www.cloudflare.com/ips-v4"), ct);
            var v6Task = FetchCloudflareIPs(new Uri("https://www.cloudflare.com/ips-v6"), ct);

            await Task.WhenAll(v4Task, v6Task);

            return [.. v4Task.Result, .. v6Task.Result];
        }
        catch (Exception)
        {
            return PrefetchedCloudflareProxies;
        }
    }

    public static async Task<IPNetwork[]> GetTrustedNetworksAsync(bool fetch = true)
    {
        var cfProxies = fetch ? await FetchCloudflareIPs() : PrefetchedCloudflareProxies;

        return [.. PrivateNetworksParsed, .. cfProxies];
    }

    public static IPNetwork[] GetTrustedNetworks(bool fetch = true)
    {
        return GetTrustedNetworksAsync(fetch).Result;
    }
}