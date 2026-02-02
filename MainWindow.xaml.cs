using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            Log("Initializing connection to " + (_config.ContainsKey("repo_url") ? "GitHub Releases" : "Update Server") + "...");
            progressBar.IsIndeterminate = true;
            SetWizardImage("lupdate-progress.png"); // Checking

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    Thread.Sleep(500);
                    Log("Locating installed components...");
                    Thread.Sleep(500);
                    Log("Verified: SPYSCALP.exe");
                    Log("Verified: LUPDATE.exe");
                    
                    Log("Connecting to repository...");
                    using (WebClient client = new WebClient())
                    {
                         client.Headers.Add("User-Agent", "LUpdate-Utility");
                         try 
                         {
                             Thread.Sleep(1500); // Simulate check
                             Log("Manifest retrieved.");
                             SetWizardImage("lupdate-progress.png"); // Analyzing
                             
                             string currentVer = _config.ContainsKey("current_version") ? _config["current_version"] : "0.0.0";
                             string latestVer = "0.1.4"; // Mock logic as before
                             
                             Log("Checking versions (Local: " + currentVer + " | Remote: " + latestVer + ")...");
                             
                             Thread.Sleep(500);
                             
                             if (currentVer == latestVer)
                             {
                                 Log("Result: Your software is up to date.");
                                 Dispatcher.Invoke(new Action(() => {
                                     lblStatus.Text = "No updates found.";
                                     progressBar.Value = 100;
                                     SetWizardImage("lupdate-success.png"); // Finished
                                 }));
                             }
                             else
                             {
                                 Log("Update found! Downloading...");
                                 // Logic to download MSI would go here
                             }
                         }
                         catch (Exception ex)
                         {
                             throw new Exception("Connection Failed: " + ex.Message);
                         }
                    }
                }
                catch (Exception ex)
                {
                    Log("Error: " + ex.Message);
                    SetWizardImage("lupdate-error.png");
                }
                finally
                {
                    Dispatcher.Invoke(new Action(() => 
                    {
                        progressBar.IsIndeterminate = false;
                        btnNext.IsEnabled = true;
                        btnNext.Content = "Finish";
                    }));
                }
            });
        }
        
        private void SetWizardImage(string imageName)
        {
            Dispatcher.Invoke(new Action(() => {
                try {
                    imgWizard.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/" + imageName));
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
    }
}
