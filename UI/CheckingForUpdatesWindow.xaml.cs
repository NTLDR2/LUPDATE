using LiveUpdate.Engine;
using LiveUpdate.Engine.Interfaces;
using System;
using System.Windows;

namespace LiveUpdate.UI
{
    /// <summary>
    /// Interaction logic for CheckingForUpdatesWindow.xaml.
    /// 
    /// Window that shows while NetSparkle is checking for updates.
    /// </summary>
    public partial class CheckingForUpdatesWindow : Window, ICheckingForUpdates
    {
        /// <inheritdoc/>
        public event EventHandler? UpdatesUIClosing;

        /// <summary>
        /// Create the window that tells the user that SparkleUpdater is checking
        /// for updates
        /// </summary>
        public CheckingForUpdatesWindow()
        {
            InitializeComponent();
            Closing += CheckingForUpdatesWindow_Closing;
        }

        private void CheckingForUpdatesWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Closing -= CheckingForUpdatesWindow_Closing;
            UpdatesUIClosing?.Invoke(sender, new EventArgs());
        }

        void ICheckingForUpdates.Close()
        {
            Close();
        }

        void ICheckingForUpdates.Show()
        {
            Show();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
