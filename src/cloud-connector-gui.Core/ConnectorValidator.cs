namespace CloudConnectorGui.Core;

public static class ConnectorValidator
{
    public static IReadOnlyList<string> Validate(LaunchOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Address))
        {
            errors.Add("Address is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            errors.Add("Token is required.");
        }

        if (options.Endpoints.Count == 0)
        {
            errors.Add("At least one endpoint is required.");
        }

        var localPorts = new HashSet<int>();
        foreach (var endpoint in options.Endpoints)
        {
            ValidatePort(endpoint.LocalPort, "Local port", errors, out var localPort);
            ValidatePort(endpoint.RemotePort, "Remote port", errors, out _);

            if (localPort.HasValue && !localPorts.Add(localPort.Value))
            {
                errors.Add($"Local port {localPort.Value} is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(endpoint.RemoteHost))
            {
                errors.Add("Remote host is required.");
            }
        }

        return errors;
    }

    private static void ValidatePort(string value, string label, List<string> errors, out int? port)
    {
        port = null;
        if (!int.TryParse(value.Trim(), out var parsed) || parsed < 1 || parsed > 65535)
        {
            errors.Add($"{label} must be a number from 1 to 65535.");
            return;
        }

        port = parsed;
    }
}
