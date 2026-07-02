using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Server.Core.Interfaces;
using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PcBeaconAgent.Server.Tray;

/// <summary>
/// Manages the NotifyIcon (tray icon) and its context menu.
/// Uses WinForms NotifyIcon inside a WPF application — this is the
/// standard approach because WPF does not have a built-in tray icon.
/// </summary>
public class NotifyIconManager : IDisposable
{
    private readonly Forms.NotifyIcon mNotifyIcon;
    private readonly IServiceProvider mServices;
    private bool mDisposed;

    public NotifyIconManager(IServiceProvider services)
    {
        mServices = services;
        mNotifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "PcBeaconAgent"
        };

        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("Show PIN", null, OnShowPin);
        contextMenu.Items.Add("Regenerate PIN", null, OnRegeneratePin);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, OnExit);

        mNotifyIcon.ContextMenuStrip = contextMenu;
        mNotifyIcon.DoubleClick += OnShowPin;
    }

    public void Show()
    {
        mNotifyIcon.Visible = true;
    }

    private void OnShowPin(object? sender, EventArgs e)
    {
        var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        if (mainWindow == null)
        {
            mainWindow = new MainWindow(mServices);
            Application.Current.MainWindow = mainWindow;
        }

        mainWindow.RefreshPin();
        mainWindow.Show();
        mainWindow.Activate();
    }

    private void OnRegeneratePin(object? sender, EventArgs e)
    {
        var pairing = mServices.GetService<IPairingService>();
        pairing?.RegeneratePin();

        // Update the tooltip with the new PIN status.
        mNotifyIcon.Text = pairing?.IsPairingActive == true
            ? "PcBeaconAgent — PIN active"
            : "PcBeaconAgent — No active PIN";

        OnShowPin(sender, e);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        Application.Current.Shutdown(0);
    }

    public void Dispose()
    {
        if (!mDisposed)
        {
            mNotifyIcon.Visible = false;
            mNotifyIcon.Dispose();
            mDisposed = true;
        }
    }
}
