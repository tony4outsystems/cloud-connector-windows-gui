using System.Globalization;
using System.Text;
using CloudConnectorGui.Core;

namespace CloudConnectorGui.App;

public sealed class GuiConfigurationStore
{
    private const string FileName = "cloud-connector-gui.toml";
    private readonly string filePath;

    public GuiConfigurationStore()
        : this(Path.Combine(AppContext.BaseDirectory, FileName))
    {
    }

    public GuiConfigurationStore(string filePath)
    {
        this.filePath = filePath;
    }

    public string FilePath => filePath;

    public GuiConfiguration Load()
    {
        if (!File.Exists(filePath))
        {
            return new GuiConfiguration();
        }

        var address = string.Empty;
        var token = string.Empty;
        var proxy = string.Empty;
        var verbose = false;
        var selfUpdateCheckInterval = SelfUpdateIntervals.Daily;
        DateOnly? lastSelfUpdateCheck = null;
        var endpoints = new List<Endpoint>();
        EndpointDraft? currentEndpoint = null;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Equals("[[endpoints]]", StringComparison.OrdinalIgnoreCase))
            {
                AddEndpointIfPresent(endpoints, currentEndpoint);
                currentEndpoint = new EndpointDraft();
                continue;
            }

            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (currentEndpoint is not null)
            {
                ApplyEndpointValue(currentEndpoint, key, value);
                continue;
            }

            switch (key)
            {
                case "address":
                    address = ParseString(value);
                    break;
                case "token":
                    token = ParseString(value);
                    break;
                case "proxy":
                    proxy = ParseString(value);
                    break;
                case "verbose":
                    verbose = bool.TryParse(value, out var parsed) && parsed;
                    break;
                case "self_update_check_interval":
                    selfUpdateCheckInterval = SelfUpdateIntervals.Normalize(ParseString(value));
                    break;
                case "last_self_update_check":
                    lastSelfUpdateCheck = ParseDate(value);
                    break;
            }
        }

        AddEndpointIfPresent(endpoints, currentEndpoint);

        return new GuiConfiguration
        {
            Address = address,
            Token = token,
            Proxy = proxy,
            Verbose = verbose,
            SelfUpdateCheckInterval = selfUpdateCheckInterval,
            LastSelfUpdateCheck = lastSelfUpdateCheck,
            Endpoints = endpoints
        };
    }

    public void Save(GuiConfiguration configuration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);
        File.WriteAllText(filePath, ToToml(configuration), Encoding.UTF8);
    }

    private static string ToToml(GuiConfiguration configuration)
    {
        var builder = new StringBuilder();
        builder.Append("address = \"").Append(Escape(configuration.Address)).AppendLine("\"");
        builder.Append("token = \"").Append(Escape(configuration.Token)).AppendLine("\"");
        builder.Append("proxy = \"").Append(Escape(configuration.Proxy)).AppendLine("\"");
        builder.Append("verbose = ").AppendLine(configuration.Verbose.ToString().ToLowerInvariant());
        builder.Append("self_update_check_interval = \"")
            .Append(Escape(SelfUpdateIntervals.Normalize(configuration.SelfUpdateCheckInterval)))
            .AppendLine("\"");
        if (configuration.LastSelfUpdateCheck is not null)
        {
            builder.Append("last_self_update_check = ")
                .AppendLine(configuration.LastSelfUpdateCheck.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        foreach (var endpoint in configuration.Endpoints)
        {
            builder.AppendLine();
            builder.AppendLine("[[endpoints]]");
            builder.Append("local_port = \"").Append(Escape(endpoint.LocalPort)).AppendLine("\"");
            builder.Append("remote_host = \"").Append(Escape(endpoint.RemoteHost)).AppendLine("\"");
            builder.Append("remote_port = \"").Append(Escape(endpoint.RemotePort)).AppendLine("\"");
        }

        return builder.ToString();
    }

    private static void ApplyEndpointValue(EndpointDraft endpoint, string key, string value)
    {
        switch (key)
        {
            case "local_port":
                endpoint.LocalPort = ParseString(value);
                break;
            case "remote_host":
                endpoint.RemoteHost = ParseString(value);
                break;
            case "remote_port":
                endpoint.RemotePort = ParseString(value);
                break;
        }
    }

    private static void AddEndpointIfPresent(List<Endpoint> endpoints, EndpointDraft? endpoint)
    {
        if (endpoint is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(endpoint.LocalPort)
            && string.IsNullOrWhiteSpace(endpoint.RemoteHost)
            && string.IsNullOrWhiteSpace(endpoint.RemotePort))
        {
            return;
        }

        endpoints.Add(new Endpoint(endpoint.LocalPort, endpoint.RemoteHost, endpoint.RemotePort));
    }

    private static string ParseString(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '"' || trimmed[^1] != '"')
        {
            return trimmed;
        }

        var builder = new StringBuilder();
        for (var i = 1; i < trimmed.Length - 1; i++)
        {
            var current = trimmed[i];
            if (current != '\\' || i + 1 >= trimmed.Length - 1)
            {
                builder.Append(current);
                continue;
            }

            i++;
            builder.Append(trimmed[i] switch
            {
                'b' => '\b',
                't' => '\t',
                'n' => '\n',
                'f' => '\f',
                'r' => '\r',
                '"' => '"',
                '\\' => '\\',
                var escaped => escaped
            });
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\b", "\\b", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\f", "\\f", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static DateOnly? ParseDate(string value)
    {
        var trimmed = value.Trim().Trim('"');
        return DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static string StripComment(string line)
    {
        var inString = false;
        var escaped = false;
        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (current == '#' && !inString)
            {
                return line[..i];
            }
        }

        return line;
    }

    private sealed class EndpointDraft
    {
        public string LocalPort { get; set; } = string.Empty;

        public string RemoteHost { get; set; } = string.Empty;

        public string RemotePort { get; set; } = string.Empty;
    }
}
