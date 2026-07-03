using System.Globalization;

namespace CloudConnectorGui.Core;

public static class ConnectorArguments
{
    public static IReadOnlyList<string> Build(LaunchOptions options)
    {
        var errors = ConnectorValidator.Validate(options);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors));
        }

        var args = new List<string>
        {
            "--header",
            $"token: {options.Token.Trim()}"
        };

        if (!string.IsNullOrWhiteSpace(options.Proxy))
        {
            args.Add("--proxy");
            args.Add(options.Proxy.Trim());
        }

        if (options.Verbose)
        {
            args.Add("-v");
        }

        args.Add(options.Address.Trim());
        args.AddRange(options.Endpoints.Select(endpoint => endpoint.ToRemoteArgument()));
        return args;
    }

    public static string ToDisplayCommand(string executable, LaunchOptions options)
    {
        return string.Join(" ", new[] { Quote(executable) }.Concat(Build(options).Select(Quote)));
    }

    private static string Quote(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"', StringComparison.Ordinal))
        {
            return value;
        }

        return string.Create(CultureInfo.InvariantCulture, $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"");
    }
}
