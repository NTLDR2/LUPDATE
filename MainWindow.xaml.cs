using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveUpdate.Engine;
using LiveUpdate.Engine.SignatureVerifiers;
using LiveUpdate.UI;

namespace LUpdate
{
    public partial class MainWindow : Window
    {
        private string _configFile = "LUPDATE.conf";
        private Dictionary<string, string> _config = new Dictionary<string, string>();
        private bool _isExpertMode = false;
        
        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            
            Log("LUPDATE Startup initialized.");
            Log("Mode: Standard (Wizard).");
            
            // Auto-check if CLI arg present
            if (App.CheckNowMode)
            {
                SwitchToExpert(); // CLI usually implies expert/log view
                PerformUpdateCheck();
            }
        }

        private void Log(string message)
        {
            // Thread-safe update
            Dispatcher.Invoke(new Action(() => {
                string entry = string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), message);
                txtLog.Text += entry + "\n";
                scrollLog.ScrollToBottom();
                
                // Also update status line in easy mode
                lblStatus.Text = message;
            }));
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configFile))
                {
                    string[] lines = File.ReadAllLines(_configFile);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || !line.Contains("=")) continue;
                        string[] parts = line.Split(new char[]{'='}, 2);
                        _config[parts[0].Trim()] = parts[1].Trim().Trim('"');
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Config Error: " + ex.Message);
            }
        }

        private void OnModeToggle(object sender, RoutedEventArgs e)
        {
            if (_isExpertMode) SwitchToWizard(); else SwitchToExpert();
        }

        private void SwitchToExpert()
        {
            _isExpertMode = true;
            pnlWizard.Visibility = Visibility.Collapsed;
            pnlExpert.Visibility = Visibility.Visible;
            linkMode.Text = "Switch to Wizard Mode";
        }

        private void SwitchToWizard()
        {
            _isExpertMode = false;
            pnlWizard.Visibility = Visibility.Visible;
            pnlExpert.Visibility = Visibility.Collapsed;
            linkMode.Text = "Switch to Expert Mode";
        }

        private void btnCheck_Click(object sender, RoutedEventArgs e)
        {
            if (btnNext.Content.ToString() == "Finish")
            {
                Application.Current.Shutdown();
                return;
            }
            PerformUpdateCheck();
        }
        
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void PerformUpdateCheck()
        {
            btnNext.IsEnabled = false;
            string repoUrl = _config.ContainsKey("repo_url") ? _config["repo_url"] : "";
            Log("Initialize LiveUpdate engine with AppCast: " + repoUrl);
            progressBar.IsIndeterminate = true;
            SetWizardImage("Graphics/lupdate-progress.png"); 

            // Initialize LiveUpdate engine
            // SecurityMode.Unsafe is used here as we are not providing public keys in this simple integration.
            // In a real scenario, use Ed25519Checker with a public key.
            var sparkle = new SparkleUpdater(repoUrl, new Ed25519Checker(LiveUpdate.Engine.Enums.SecurityMode.Unsafe))
            {
                UIFactory = new UIFactory(),
                //RelaunchAfterUpdate = true,
                //RestartApplication = true
            };
            
            // Hook into events if we want to log to our main window
            sparkle.UpdateDetected += (sender, args) => 
            {
                Log("Update detected: " + args.LatestVersion);
                Dispatcher.Invoke(new Action(() => {
                   progressBar.IsIndeterminate = false;
                   progressBar.Value = 100;
                   SetWizardImage("Graphics/lupdate-success.png"); 
                }));
            };
            
            sparkle.LoopFinished += (sender, args) =>
            {
                 Log("Update check finished.");
                 Dispatcher.Invoke(new Action(() => {
                    progressBar.IsIndeterminate = false;
                    btnNext.IsEnabled = true;
                    btnNext.Content = "Finish";
                 }));
            };
            
            sparkle.CloseApplication += () => 
            {
                Log("Application closing for update...");
                Dispatcher.Invoke(new Action(() => {
                    Application.Current.Shutdown();
                }));
            };

            Log("Checking for updates...");
            
            // Run asynchronously
            ThreadPool.QueueUserWorkItem(state => {
                 // CheckForUpdatesAtUserRequest shows UI if update found, or 'up to date' message.
                 // It manages its own UI.
                 sparkle.CheckForUpdatesAtUserRequest();
            });
        }
        
        private void SetWizardImage(string imageName)
        {
            Dispatcher.Invoke(new Action(() => {
                try {
                    imgWizard.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/" + imageName.Replace("/", "\\"))); // Handle path separators for pack URIs
                } catch {}
            }));
        }

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            AboutWindow about = new AboutWindow();
            about.Owner = this;
            about.ShowDialog();
        }

        private void OnOptionsClick(object sender, RoutedEventArgs e)
        {
            OptionsWindow opt = new OptionsWindow();
            opt.Owner = this;
            opt.ShowDialog();
        }

        private void OnSaveLog(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Filter = "Log Files (*.log)|*.log|Text Files (*.txt)|*.txt";
            sfd.FileName = "LUpdate_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".log";
            
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, txtLog.Text);
                    MessageBox.Show("Log saved successfully.", "LiveUpdate", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving log: " + ex.Message, "LiveUpdate", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        // =====================================================================
        // Test UI Menu — Launch forked NetSparkle UI windows via UIFactory
        // =====================================================================

        private UIFactory GetTestFactory()
        {
            return new UIFactory();
        }

        private void OnTestUpdateAvailable(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Update Available Window");
            var factory = GetTestFactory();
            var updates = new List<AppCastItem>
            {
                new AppCastItem()
                {
                    Title = "LiveUpdate v2.0.0",
                    Version = "2.0.0",
                    DownloadLink = "https://example.com/lupdate-2.0.0.msi",
                    Description = "<h2>LiveUpdate v2.0.0</h2><ul><li>New feature: Check-only mode</li><li>Improved download progress</li><li>Bug fixes throughout</li></ul>",
                    PublicationDate = DateTime.Now.AddDays(-1)
                }
            };
            var window = factory.CreateUpdateAvailableWindow(updates, null, "1.0.0", "LiveUpdate");
            (window as Window)?.ShowDialog();
        }

        private void OnTestDownloadProgress(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Download Progress Window");
            var factory = GetTestFactory();
            var window = factory.CreateProgressWindow("Downloading LiveUpdate v2.0.0...", "Install and Relaunch");
            (window as Window)?.ShowDialog();
        }

        private void OnTestCheckingForUpdates(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Checking for Updates Window");
            var factory = GetTestFactory();
            var window = factory.ShowCheckingForUpdates();
            (window as Window)?.ShowDialog();
        }

        private void OnTestToast(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Toast Notification");
            var factory = GetTestFactory();
            factory.ShowToast(() =>
            {
                Dispatcher.Invoke(() => Log("Test UI: Toast was clicked!"));
            });
        }

        private void OnTestVersionUpToDate(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Version Is Up To Date");
            var factory = GetTestFactory();
            factory.ShowVersionIsUpToDate();
        }

        private void OnTestVersionSkipped(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Version Skipped By User");
            var factory = GetTestFactory();
            factory.ShowVersionIsSkippedByUserRequest();
        }

        private void OnTestUnknownInstaller(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Unknown Installer Format");
            var factory = GetTestFactory();
            factory.ShowUnknownInstallerFormatMessage("lupdate-setup.tar.gz");
        }

        private void OnTestCannotDownloadAppcast(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Cannot Download Appcast");
            var factory = GetTestFactory();
            factory.ShowCannotDownloadAppcast("https://example.com/appcast.xml");
        }

        private void OnTestDownloadError(object sender, RoutedEventArgs e)
        {
            Log("Test UI: Download Error");
            var factory = GetTestFactory();
            factory.ShowDownloadErrorMessage("Connection timed out after 30 seconds", "https://example.com/lupdate-2.0.0.msi");
        }
    }
}
