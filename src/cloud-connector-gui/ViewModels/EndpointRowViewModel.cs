using CloudConnectorGui.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CloudConnectorGui.ViewModels;

public sealed partial class EndpointRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string localPort = string.Empty;

    [ObservableProperty]
    private string remoteHost = string.Empty;

    [ObservableProperty]
    private string remotePort = string.Empty;

    public EndpointRowViewModel()
    {
    }

    public EndpointRowViewModel(Endpoint endpoint)
    {
        localPort = endpoint.LocalPort;
        remoteHost = endpoint.RemoteHost;
        remotePort = endpoint.RemotePort;
    }

    public Endpoint ToEndpoint()
    {
        return new Endpoint(LocalPort, RemoteHost, RemotePort);
    }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(LocalPort)
        && string.IsNullOrWhiteSpace(RemoteHost)
        && string.IsNullOrWhiteSpace(RemotePort);
}
