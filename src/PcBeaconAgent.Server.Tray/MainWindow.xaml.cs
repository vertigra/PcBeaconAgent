using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Server.Core.Interfaces;
using System.Windows;

namespace PcBeaconAgent.Server.Tray;

public partial class MainWindow : Window
{
    private readonly IServiceProvider mServices;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        mServices = services;
        RefreshPin();
    }

    public void RefreshPin()
    {
        var pairing = mServices.GetService<IPairingService>();
        if (pairing != null && pairing.IsPairingActive)
        {
            // The PIN itself is not exposed via the interface — it is only
            // logged. For the tray we need to show it, so we regenerate
            // and rely on the log event. A future refactor could add a
            // GetCurrentPin() method to IPairingService.
            // For now, show a placeholder.
            PinText.Text = "Check log or\nregenerate";
        }
        else
        {
            PinText.Text = "No active\nPIN";
        }
    }

    private void RegenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var pairing = mServices.GetService<IPairingService>();
        pairing?.RegeneratePin();
        RefreshPin();
    }
}
