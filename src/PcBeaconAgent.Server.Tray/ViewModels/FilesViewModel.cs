using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Core.Models;
using PcBeaconAgent.Server.Core.Services;
using PcBeaconAgent.Server.Tray.Models;
using PcBeaconAgent.Server.Tray.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Files tab. Owns the incoming-text-transfer
    /// history list and the auto-copy-to-clipboard setting. Subscribes
    /// to <see cref="TransferController.TransferReceived"/> and
    /// marshals each event to the UI thread for ObservableCollection
    /// mutation, clipboard access (STA-required), and toast display.
    /// </summary>
    /// <remarks>
    /// <b>Threading.</b> <see cref="TransferController.TransferReceived"/>
    /// fires on the HTTP request thread (thread-pool). All UI-side
    /// mutations (history add, clipboard set, toast show) must run on
    /// the WPF Dispatcher. The handler uses
    /// <see cref="Dispatcher.BeginInvoke(Delegate, DispatcherPriority, object[])"/>
    /// (asynchronous) to avoid blocking the HTTP thread.
    /// </remarks>
    public partial class FilesViewModel : ObservableObject, IDisposable
    {
        private readonly TransferController mTransferController;
        private readonly INotificationService mNotifications;
        private readonly Dispatcher mDispatcher;
        private bool mDisposed;

        /// <summary>
        /// Incoming transfer history, newest first. Bound to the
        /// ItemsControl in <see cref="Views.FilesView"/>. Mutated only
        /// on the UI thread.
        /// </summary>
        public ObservableCollection<TransferRecord> History { get; } = [];

        [ObservableProperty]
        public partial bool AutoCopyToClipboard { get; set; } = true;

        public bool HasHistory => History.Count > 0;

        public FilesViewModel(TransferController transferController, INotificationService notifications)
        {
            mTransferController = transferController;
            mNotifications = notifications;
            // Capture the Dispatcher at construction time. FilesViewModel
            // is a DI singleton resolved on the UI thread, so
            // Application.Current.Dispatcher is the WPF Dispatcher.
            mDispatcher = Application.Current.Dispatcher;

            mTransferController.TransferReceived += OnTransferReceived;
            History.CollectionChanged += OnHistoryChanged;
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

            string preview = BuildPreview(record.Text);

            if (AutoCopyToClipboard)
            {
                try
                {
                    // Clipboard.SetText requires STA (the UI thread is
                    // STA). We are on the Dispatcher thread here, so
                    // this is safe. Wrap in try/catch — clipboard can
                    // be locked by other processes (rare, but the OS
                    // should not crash our toast flow over a clipboard
                    // race).
                    Clipboard.SetText(record.Text);
                    mNotifications.ShowTransient(
                        "Transfer received",
                        $"Copied to clipboard: {preview}",
                        NotificationSeverity.Info);
                }
                catch
                {
                    // Clipboard locked — still record in history, just
                    // change the toast message so the user knows to
                    // copy manually.
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
                ? text.Substring(0, newlineIdx).TrimEnd('\r')
                : text;

            const int MaxPreview = 50;
            if (firstLine.Length <= MaxPreview)
                return $"\"{firstLine}\"";

            return $"\"{firstLine.Substring(0, MaxPreview)}…\"";
        }

        private void OnHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasHistory));
        }

        [RelayCommand]
        public void CopyToClipboard(TransferRecord? record)
        {
            if (record == null) return;

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
        public void ClearHistory()
        {
            History.Clear();
        }

        public void Dispose()
        {
            if (!mDisposed)
            {
                mTransferController.TransferReceived -= OnTransferReceived;
                History.CollectionChanged -= OnHistoryChanged;
                mDisposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
