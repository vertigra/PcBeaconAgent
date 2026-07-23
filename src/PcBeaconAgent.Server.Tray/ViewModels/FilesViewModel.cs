using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Models;
using PcBeaconAgent.Server.Core.Services;
using PcBeaconAgent.Server.Tray.Models;
using PcBeaconAgent.Server.Tray.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Files tab. Owns the transfer history list
    /// (both incoming and outgoing), the auto-copy-to-clipboard
    /// setting, and the "Send to phone" UI (device picker + text/file
    /// send commands). Subscribes to
    /// <see cref="TransferController.TransferReceived"/> and
    /// <see cref="IConnectionTracker.CountChanged"/> to keep the UI
    /// live.
    /// </summary>
    /// <remarks>
    /// <b>Threading.</b> <see cref="TransferController.TransferReceived"/>
    /// fires on the HTTP request thread (thread-pool). All UI-side
    /// mutations must run on the WPF Dispatcher. The handler uses
    /// <see cref="Dispatcher.BeginInvoke(Delegate, DispatcherPriority, object[])"/>
    /// (asynchronous) to avoid blocking the HTTP thread.
    /// </remarks>
    public partial class FilesViewModel : ObservableObject, IDisposable
    {
        private readonly ILogger<FilesViewModel> mLogger;
        private readonly TransferController mTransferController;
        private readonly INotificationService mNotifications;
        private readonly IConnectionTracker mConnectionTracker;
        private readonly WebApiOptions mApiOptions;
        private readonly Dispatcher mDispatcher;
        private bool mDisposed;

        /// <summary>
        /// Transfer history, newest first. Contains both incoming
        /// (from Android) and outgoing (to Android) records. Bound to
        /// the ItemsControl in <see cref="Views.FilesView"/>.
        /// </summary>
        public ObservableCollection<TransferRecord> History { get; } = [];

        /// <summary>
        /// Connected Android clients. Refreshed from
        /// <see cref="IConnectionTracker"/> on every
        /// <see cref="IConnectionTracker.CountChanged"/> event. Bound
        /// to the device picker ComboBox in the "Send to phone"
        /// section.
        /// </summary>
        public ObservableCollection<ConnectedDeviceInfo> ConnectedDevices { get; } = [];

        [ObservableProperty]
        public partial bool AutoCopyToClipboard { get; set; } = true;

        public bool HasHistory => History.Count > 0;

        public bool HasConnectedDevices => ConnectedDevices.Count > 0;

        /// <summary>
        /// Currently selected device in the device picker. Null when
        /// no device is selected or no devices are connected.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSendToPhone))]
        public partial ConnectedDeviceInfo? SelectedDevice { get; set; }

        /// <summary>
        /// Text to send to the selected phone. Bound to a TextBox in
        /// the "Send to phone" section.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSendToPhone))]
        public partial string OutgoingText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsSending { get; set; }

        [ObservableProperty]
        public partial string SendStatus { get; set; } = string.Empty;

        public bool CanSendToPhone => SelectedDevice != null && !IsSending && !string.IsNullOrWhiteSpace(OutgoingText);

        public FilesViewModel(
            TransferController transferController,
            INotificationService notifications,
            IConnectionTracker connectionTracker,
            WebApiOptions apiOptions,
            ILogger<FilesViewModel> logger)
        {
            mTransferController = transferController;
            mNotifications = notifications;
            mConnectionTracker = connectionTracker;
            mApiOptions = apiOptions;
            mLogger = logger;
            mDispatcher = Application.Current.Dispatcher;

            mTransferController.TransferReceived += OnTransferReceived;
            mConnectionTracker.CountChanged += OnCountChanged;
            History.CollectionChanged += OnHistoryChanged;

            // Populate the device list immediately — clients may already
            // be connected when the window opens.
            RefreshConnectedDevices();
        }

        private void OnTransferReceived(TransferRecord record)
        {
            // Marshal to UI thread. BeginInvoke (async) — the HTTP
            // thread should not block waiting for the clipboard or the
            // toast window. Same pattern as TrayViewModel uses for
            // pairing state changes.
            mDispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action<TransferRecord>(HandleTransferReceived),
                record);
        }

        private void HandleTransferReceived(TransferRecord record)
        {
            History.Insert(0, record);

            // Branch on kind — text gets auto-copy + text preview,
            // file gets a "saved to disk" toast with the file name.
            if (record.Kind == TransferKind.File)
            {
                HandleFileReceived(record);
            }
            else
            {
                HandleTextReceived(record);
            }
        }

        private void HandleTextReceived(TransferRecord record)
        {
            string preview = BuildPreview(record.Text);

            if (AutoCopyToClipboard)
            {
                try
                {
                    Clipboard.SetText(record.Text);
                    mNotifications.ShowTransient(
                        "Transfer received",
                        $"Copied to clipboard: {preview}",
                        NotificationSeverity.Info);
                }
                catch
                {
                    mNotifications.ShowTransient(
                        "Transfer received",
                        $"{preview}  (clipboard busy — copy from Files tab)",
                        NotificationSeverity.Warning);
                }
            }
            else
            {
                mNotifications.ShowTransient(
                    "Transfer received",
                    $"{preview}  (open Files tab to copy)",
                    NotificationSeverity.Info);
            }
        }

        private void HandleFileReceived(TransferRecord record)
        {
            // Files are already saved to disk by TransferController —
            // no clipboard interaction. Show a toast with the file name
            // so the user knows what arrived. The toast message is
            // intentionally short; full path is in the Files tab.
            mNotifications.ShowTransient(
                "File received",
                $"{record.FileName}  ({FormatFileSize(record.SizeBytes)})",
                NotificationSeverity.Info);
        }

        /// <summary>
        /// Formats a byte count as a human-readable file size string
        /// (e.g. "1.2 KB", "3.4 MB"). Used in the file-received toast
        /// and in the Files tab rows.
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            // Binary prefixes (KB = 1024, not 1000) — matches Windows
            // Explorer's display convention.
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            // Show one decimal place for KB and above, none for bytes.
            return unitIndex == 0
                ? $"{(int)size} {units[unitIndex]}"
                : $"{size:F1} {units[unitIndex]}";
        }

        /// <summary>
        /// Builds a short preview of the transfer text for the toast.
        /// Takes the first line and truncates to 50 characters with an
        /// ellipsis. Quoted so the user can see the boundaries.
        /// </summary>
        private static string BuildPreview(string text)
        {
            // First line only — multi-line payloads would push the
            // toast height past the taskbar gap and look cramped.
            int newlineIdx = text.IndexOf('\n');
            string firstLine = newlineIdx >= 0
                ? text[..newlineIdx].TrimEnd('\r')
                : text;

            const int MaxPreview = 50;
            if (firstLine.Length <= MaxPreview)
                return $"\"{firstLine}\"";

            return $"\"{firstLine[..MaxPreview]}…\"";
        }

        private void OnHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasHistory));
        }

        // ── Connected devices ────────────────────────────────────────

        private void OnCountChanged(int newCount)
        {
            // CountChanged is marshaled to the UI thread by
            // ConnectionTracker (captured the Dispatcher sync context
            // at construction). We are already on the UI thread.
            RefreshConnectedDevices();
        }

        /// <summary>
        /// Rebuilds the <see cref="ConnectedDevices"/> collection from
        /// the connection tracker's current snapshot. Must run on the
        /// UI thread because ObservableCollection mutation is not
        /// thread-safe.
        /// </summary>
        private void RefreshConnectedDevices()
        {
            ConnectedDevices.Clear();
            foreach (var kvp in mConnectionTracker.ConnectedClients)
            {
                ConnectedDevices.Add(new ConnectedDeviceInfo(
                    kvp.Key,
                    kvp.Value.MachineName,
                    kvp.Value.RemoteIp));
            }
            OnPropertyChanged(nameof(HasConnectedDevices));

            // If the selected device disconnected, clear the selection
            // so CanSendToPhone re-evaluates.
            if (SelectedDevice != null && !ConnectedDevices.Contains(SelectedDevice))
            {
                SelectedDevice = null;
            }

            // Auto-select the first device if none is selected and
            // devices are available — saves the user a click.
            if (SelectedDevice == null && ConnectedDevices.Count > 0)
            {
                SelectedDevice = ConnectedDevices[0];
            }
        }

        // ── Send to phone commands ───────────────────────────────────

        [RelayCommand]
        public async Task SendTextToPhoneAsync()
        {
            if (!CanSendToPhone || SelectedDevice == null) return;

            IsSending = true;
            SendStatus = "Sending…";

            try
            {
                var (accepted, message) = await mTransferController.SendTextToClientAsync(
                    OutgoingText, SelectedDevice.ConnectionId, Environment.MachineName);

                if (accepted)
                {
                    SendStatus = "Sent.";
                    OutgoingText = string.Empty;
                    await System.Threading.Tasks.Task.Delay(800);
                    SendStatus = string.Empty;
                }
                else
                {
                    SendStatus = message;
                }
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to send text to phone {Conn}", SelectedDevice.ConnectionId);
                SendStatus = "Could not send. Check the connection.";
            }
            finally
            {
                IsSending = false;
            }
        }

        [RelayCommand]
        public async Task SendFileToPhoneAsync()
        {
            if (SelectedDevice == null || IsSending) return;

            var dialog = new OpenFileDialog
            {
                Title = "Select a file to send",
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            IsSending = true;
            SendStatus = $"Sending {Path.GetFileName(dialog.FileName)}…";

            try
            {
                string downloadBaseUrl = $"http://{GetLocalLanIp()}:{mApiOptions.ApiPort}";

                using var stream = File.OpenRead(dialog.FileName);
                var (accepted, message) = await mTransferController.SendFileToClientAsync(
                    stream, Path.GetFileName(dialog.FileName),
                    SelectedDevice.ConnectionId, Environment.MachineName,
                    downloadBaseUrl);

                if (accepted)
                {
                    SendStatus = $"Sent: {Path.GetFileName(dialog.FileName)}";
                    await System.Threading.Tasks.Task.Delay(1500);
                    SendStatus = string.Empty;
                }
                else
                {
                    SendStatus = message;
                }
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to send file to phone {Conn}", SelectedDevice.ConnectionId);
                SendStatus = "Could not send. Check the connection.";
            }
            finally
            {
                IsSending = false;
            }
        }

        /// <summary>
        /// Resolves the PC's LAN IPv4 address for constructing the
        /// download base URL. Uses the first active Ethernet/Wi-Fi
        /// interface's first IPv4 unicast address. Falls back to
        /// "localhost" if no suitable interface is found (e.g. the
        /// server is running in a container without LAN access — in
        /// that case file download would not work anyway).
        /// </summary>
        private static string GetLocalLanIp()
        {
            try
            {
                var ni = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                        (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));

                if (ni == null) return "localhost";

                var ip = ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                return ip?.Address.ToString() ?? "localhost";
            }
            catch
            {
                return "localhost";
            }
        }

        [RelayCommand]
        public void CopyToClipboard(TransferRecord? record)
        {
            if (record == null) return;

            // Only text records have copyable content.
            if (record.Kind != TransferKind.Text)
            {
                mNotifications.ShowTransient(
                    "Not text",
                    "Only text transfers can be copied to clipboard.",
                    NotificationSeverity.Info);
                return;
            }

            try
            {
                Clipboard.SetText(record.Text);
                mNotifications.ShowTransient(
                    "Copied",
                    "Transfer text copied to clipboard.",
                    NotificationSeverity.Info);
            }
            catch
            {
                mNotifications.ShowTransient(
                    "Clipboard busy",
                    "Could not copy — another app holds the clipboard. Try again.",
                    NotificationSeverity.Warning);
            }
        }

        [RelayCommand]
        public void OpenFolder(TransferRecord? record)
        {
            if (record == null) return;
            if (record.Kind != TransferKind.File) return;
            if (string.IsNullOrEmpty(record.SavedFilePath)) return;

            try
            {
                // Use explorer.exe with /select, to open the parent
                // folder and highlight the file — same UX as "Open file
                // location" in Windows Explorer.
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{record.SavedFilePath}\"");
            }
            catch (Exception ex)
            {
                mLogger.LogWarning(ex, "Failed to open folder for {Path}", record.SavedFilePath);
                mNotifications.ShowTransient(
                    "Cannot open",
                    "Could not open the folder. The file may have been moved or deleted.",
                    NotificationSeverity.Warning);
            }
        }

        [RelayCommand]
        public void ClearHistory()
        {
            History.Clear();
        }

        public void Dispose()
        {
            if (!mDisposed)
            {
                mTransferController.TransferReceived -= OnTransferReceived;
                mConnectionTracker.CountChanged -= OnCountChanged;
                History.CollectionChanged -= OnHistoryChanged;
                mDisposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
