namespace CloudConnectorGui.Core;

public sealed record Endpoint(string LocalPort, string RemoteHost, string RemotePort)
{
    public string ToRemoteArgument()
    {
        return $"R:{LocalPort.Trim()}:{RemoteHost.Trim()}:{RemotePort.Trim()}";
    }
}
