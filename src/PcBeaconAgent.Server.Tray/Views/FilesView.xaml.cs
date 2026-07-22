using System.Windows.Controls;

namespace PcBeaconAgent.Server.Tray.Views
{
    /// <summary>
    /// Code-behind for <see cref="FilesView"/>. Displays the incoming
    /// text transfer history and the auto-copy settings. All logic is
    /// in <see cref="ViewModels.FilesViewModel"/> — this file is just
    /// InitializeComponent.
    /// </summary>
    public partial class FilesView : UserControl
    {
        public FilesView()
        {
            InitializeComponent();
        }
    }
}
