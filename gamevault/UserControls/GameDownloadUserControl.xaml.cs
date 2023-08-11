﻿using gamevault.Helper;
using gamevault.Models;
using gamevault.ViewModels;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace gamevault.UserControls
{
    /// <summary>
    /// Interaction logic for GameInstallUserControl.xaml
    /// </summary>
    public partial class GameDownloadUserControl : UserControl
    {
        private GameDownloadViewModel ViewModel { get; set; }
        private bool IsDownloadActive = false;

        private string m_DownloadPath { get; set; }
        private bool extractionCancelled = false;
        private HttpClientDownloadWithProgress client { get; set; }
        private DateTime startTime;

        private SevenZipHelper sevenZipHelper { get; set; }
        public GameDownloadUserControl(Game game, bool download)
        {
            InitializeComponent();
            ViewModel = new GameDownloadViewModel();
            this.DataContext = ViewModel;
            ViewModel.Game = game;
            ViewModel.DownloadUIVisibility = System.Windows.Visibility.Hidden;
            ViewModel.ExtractionUIVisibility = System.Windows.Visibility.Hidden;

            m_DownloadPath = $"{SettingsViewModel.Instance.RootPath}\\GameVault\\Downloads\\({ViewModel.Game.ID}){ViewModel.Game.Title}";
            m_DownloadPath = m_DownloadPath.Replace(@"\\", @"\");
            ViewModel.InstallPath = $"{SettingsViewModel.Instance.RootPath}\\GameVault\\Installations\\({ViewModel.Game.ID}){ViewModel.Game.Title}";
            sevenZipHelper = new SevenZipHelper();
            if (download)
            {
                Task.Run(async () =>
                {
                    IsDownloadActive = true;
                    ViewModel.State = "Downloading...";
                    ViewModel.DownloadUIVisibility = System.Windows.Visibility.Visible;
                    await DownloadGame();
                    await CacheHelper.CreateOfflineCacheAsync(ViewModel.Game);
                });
            }
            else
            {
                if (File.Exists($"{m_DownloadPath}\\Extract\\gamevault-metadata") && Preferences.Get(AppConfigKey.ExtractionFinished, $"{m_DownloadPath}\\Extract\\gamevault-metadata") == "1")
                {
                    ViewModel.State = "Extracted";
                    uiBtnExtract.IsEnabled = true;
                    uiBtnInstall.IsEnabled = true;
                    ((TextBlock)uiBtnExtract.Child).Text = "Re-Extract";
                }
                else
                {
                    ViewModel.State = "Downloaded";
                    uiBtnExtract.IsEnabled = true;
                }
            }
        }
        public bool IsDownloading()
        {
            return IsDownloadActive;
        }
        public bool IsGameIdDownloading(int id)
        {
            if (IsDownloadActive == true && ViewModel.Game.ID == id)
            {
                return true;
            }
            return false;
        }
        public int GetGameId()
        {
            return ViewModel.Game.ID;
        }
        public int GetBoxImageID()
        {
            return ViewModel.Game.BoxImage.ID;
        }
        public void CancelDownload()
        {
            if (client == null)
                return;

            client.Cancel();
            client.Dispose();
            IsDownloadActive = false;
            ViewModel.State = "Download Cancelled";
            ViewModel.DownloadUIVisibility = System.Windows.Visibility.Hidden;
        }
        private async Task DownloadGame()
        {

            if (!Directory.Exists(m_DownloadPath)) { Directory.CreateDirectory(m_DownloadPath); }
            client = new HttpClientDownloadWithProgress($"{SettingsViewModel.Instance.ServerUrl}/api/v1/games/{ViewModel.Game.ID}/download", $"{m_DownloadPath}\\{Path.GetFileName(ViewModel.Game.FilePath)}");
            client.ProgressChanged += DownloadProgress;
            startTime = DateTime.Now;

            try
            {
                await client.StartDownload();
            }
            catch (Exception ex)
            {
                client.Dispose();
                IsDownloadActive = false;
                ViewModel.State = $"Error: '{ex.Message}'";
                ViewModel.DownloadUIVisibility = System.Windows.Visibility.Hidden;
            }
        }
        private void DownloadProgress(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage)
        {
            App.Current.Dispatcher.Invoke((Action)delegate
            {

                ViewModel.DownloadInfo = $"{CalculateSpeed(totalBytesDownloaded, (DateTime.Now - startTime).TotalSeconds)} - {CalculateSize(totalBytesDownloaded)} of {CalculateSize((double)totalFileSize)} | Time left: {CalculateTimeLeft(totalFileSize, totalBytesDownloaded, (DateTime.Now - startTime).TotalSeconds)}";
                if (ViewModel.GameDownloadProgress != (int)progressPercentage)
                {
                    ViewModel.GameDownloadProgress = (int)progressPercentage;
                    if (ViewModel.GameDownloadProgress == 100)
                    {
                        DownloadCompleted();
                    }
                }
            });
        }

        private void DownloadCompleted()
        {
            ViewModel.DownloadUIVisibility = System.Windows.Visibility.Hidden;
            client.Dispose();
            IsDownloadActive = false;
            ViewModel.State = "Downloaded";
            uiBtnExtract.IsEnabled = true;
            if (!Directory.Exists(ViewModel.InstallPath))
            {
                Directory.CreateDirectory(ViewModel.InstallPath);
            }
            MainWindowViewModel.Instance.Installs.AddSystemFileWatcher(ViewModel.InstallPath);
        }

        private void CancelDownload_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CancelDownload();
        }
        private void CancelExtraction_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            extractionCancelled = true;
            sevenZipHelper.Cancel();
        }
        private string CalculateSpeed(double size, double tspan)
        {
            string message = string.Empty;
            if (size / tspan > 1024 * 1024) // MB
            {
                return $"{message} {Math.Round(size / (1024 * 1204) / tspan, 2)} MB/s"; //string.Format(message, size / (1024 * 1204) / tspan, "MB/s");
            }
            else if (size / tspan > 1024) // KB
            {
                return string.Format(message, size / (1024) / tspan, "KB/s");
            }
            else
            {
                return string.Format(message, size / tspan, "B/s");
            }
        }

        private string CalculateTimeLeft(long? totalFileSize, long totalBytesDownloaded, double tspan)
        {
            var averagespeed = totalBytesDownloaded / tspan;
            var timeleft = (totalFileSize / averagespeed) - (tspan);
            TimeSpan t = TimeSpan.FromSeconds(0);
            if (!double.IsInfinity(Convert.ToDouble(timeleft)))
            {
                t = TimeSpan.FromSeconds(Convert.ToInt32(timeleft));
            }
            return string.Format("{0:00}:{1:00}:{2:00}", ((int)t.TotalHours), t.Minutes, t.Seconds);
        }

        private async void DeleteFile_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MessageDialogResult result = await ((MetroWindow)App.Current.MainWindow).ShowMessageAsync($"Are you sure you want to delete '{ViewModel.Game.Title}' ?", "", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings() { AffirmativeButtonText = "Yes", NegativeButtonText = "No", AnimateHide = false });
            if (result == MessageDialogResult.Affirmative)
            {
                try
                {
                    if (Directory.Exists(m_DownloadPath))
                        Directory.Delete(m_DownloadPath, true);

                    DownloadsViewModel.Instance.DownloadedGames.Remove(this);
                }
                catch
                {
                    MainWindowViewModel.Instance.AppBarText = "Can not delete during the download, extraction, installing process";
                }
            }
        }
        private void OpenDirectory_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Directory.Exists(m_DownloadPath))
                Process.Start("explorer.exe", m_DownloadPath);
        }

        private void GameImage_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MainWindowViewModel.Instance.SetActiveControl(new GameViewUserControl(ViewModel.Game, LoginManager.Instance.IsLoggedIn()));
        }

        private void ExtractionProgress(object sender, SevenZipProgressEventArgs e)
        {
            long totalBytesDownloaded = (Convert.ToInt64(ViewModel.Game.Size) / 100) * e.PercentageDone;
            ViewModel.ExtractionInfo = $"{CalculateSpeed(totalBytesDownloaded, (DateTime.Now - startTime).TotalSeconds)} - {CalculateSize(totalBytesDownloaded)} of {CalculateSize(Convert.ToInt64(ViewModel.Game.Size))} | Time left: {CalculateTimeLeft(Convert.ToInt64(ViewModel.Game.Size), totalBytesDownloaded, (DateTime.Now - startTime).TotalSeconds)}";
            ViewModel.GameExtractionProgress = e.PercentageDone;
        }

        private async void Extract_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            uiBtnInstall.IsEnabled = false;
            ViewModel.ExtractionUIVisibility = System.Windows.Visibility.Hidden;
            ViewModel.State = "Extracting...";
            ViewModel.ExtractionUIVisibility = System.Windows.Visibility.Visible;

            sevenZipHelper.Process += ExtractionProgress;
            startTime = DateTime.Now;
            int result = await sevenZipHelper.ExtractArchive($"{m_DownloadPath}\\{Path.GetFileName(ViewModel.Game.FilePath)}", $"{m_DownloadPath}\\Extract");
            if (result == 0)
            {
                if (!File.Exists($"{m_DownloadPath}\\Extract\\gamevault-metadata"))
                {
                    File.Create($"{m_DownloadPath}\\Extract\\gamevault-metadata").Close();
                }
                Preferences.Set(AppConfigKey.ExtractionFinished, "1", $"{m_DownloadPath}\\Extract\\gamevault-metadata");
                ViewModel.State = "Extracted";
                ((TextBlock)uiBtnExtract.Child).Text = "Re-Extract";
                uiBtnInstall.IsEnabled = true;
                ViewModel.ExtractionUIVisibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                if (Directory.Exists($"{m_DownloadPath}\\Extract"))
                {
                    Directory.Delete($"{m_DownloadPath}\\Extract", true);
                }
                if (extractionCancelled)
                {
                    extractionCancelled = false;
                    ViewModel.State = "Extraction cancelled";
                }
                else
                {
                    ViewModel.State = "Something went wrong during extraction";
                }
                ViewModel.ExtractionUIVisibility = System.Windows.Visibility.Hidden;
            }
        }
        private string CalculateSize(double size)
        {
            try
            {
                size = size / 1000000;
                if (size > 1000)
                {
                    size = size / 1000;
                    size = Math.Round(size, 2);
                    return $"{size} GB";
                }
                size = Math.Round(size, 2);
                return $"{size} MB";
            }
            catch (Exception ex)
            {
                return "?";
            }
        }

        private void OpenInstallOptions_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            uiInstallOptions.Visibility = System.Windows.Visibility.Visible;
            LoadSetupExecutables();

        }

        private void InstallOptionCancel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            uiInstallOptions.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void LoadSetupExecutables()
        {
            if (Directory.Exists($"{m_DownloadPath}\\Extract"))
            {
                string[] allExecutables = Directory.GetFiles($"{m_DownloadPath}\\Extract", "*.EXE", SearchOption.AllDirectories);
                for (int count = 0; count < allExecutables.Length; count++)
                {
                    allExecutables[count] = Path.GetFileName(allExecutables[count]);
                }
                uiCbSetupExecutable.ItemsSource = allExecutables;
                if (allExecutables.Length == 1)
                {
                    uiCbSetupExecutable.SelectedIndex = 0;
                }
            }
            else
            {
                uiCbSetupExecutable.ItemsSource = null;
            }
        }

        private async void Install_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ((FrameworkElement)sender).IsEnabled = false;
            if (ViewModel.Game.Type == GameType.WINDOWS_PORTABLE)
            {
                bool error = false;
                uiProgressRingInstall.IsActive = true;
                await Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    try
                    {
                        if (Directory.Exists($"{ViewModel.InstallPath}\\Copy"))
                        {
                            Directory.Delete($"{ViewModel.InstallPath}\\Copy", true);
                        }
                        Directory.Move($"{m_DownloadPath}\\Extract", $"{ViewModel.InstallPath}\\Copy");
                    }
                    catch { error = true; }
                });
                uiBtnInstall.IsEnabled = false;
                uiProgressRingInstall.IsActive = false;
                ((FrameworkElement)sender).IsEnabled = true;
                ViewModel.State = "Downloaded";
                ((TextBlock)uiBtnExtract.Child).Text = "Extract";
                if (error)
                {
                    MainWindowViewModel.Instance.AppBarText = "Something wen't wrong during installation";
                }
                else
                {
                    MainWindowViewModel.Instance.AppBarText = $"Successfully installed '{ViewModel.Game.Title}'";
                }
            }
            else if (ViewModel.Game.Type == GameType.WINDOWS_SETUP)
            {
                string setupEexecutable = string.Empty;
                if (!Directory.Exists($"{m_DownloadPath}\\Extract"))
                    return;
                uiProgressRingInstall.IsActive = true;
                string[] allExecutables = Directory.GetFiles($"{m_DownloadPath}\\Extract", "*.EXE", SearchOption.AllDirectories);
                for (int count = 0; count < allExecutables.Length; count++)
                {
                    if (Path.GetFileName(allExecutables[count]) == uiCbSetupExecutable.SelectedValue.ToString())
                    {
                        setupEexecutable = allExecutables[count];
                        break;
                    }
                }

                if (File.Exists(setupEexecutable))
                {
                    Process setupProcess = null;
                    try
                    {
                        setupProcess = ProcessHelper.StartApp(setupEexecutable);
                    }
                    catch
                    {
                        try
                        {
                            setupProcess = ProcessHelper.StartApp(setupEexecutable, true);
                        }
                        catch
                        {
                            MainWindowViewModel.Instance.AppBarText = $"Can not execute '{setupEexecutable}'";
                        }
                    }
                    if (setupProcess != null)
                    {
                        await setupProcess.WaitForExitAsync();                      
                        ((FrameworkElement)sender).IsEnabled = true;
                    }
                }
                else
                {
                    MainWindowViewModel.Instance.AppBarText = $"Could not find executable '{setupEexecutable}'";
                }
            }
            uiInstallOptions.Visibility = System.Windows.Visibility.Collapsed;
            uiProgressRingInstall.IsActive = false;
        }

        private void CopyInstallPathToClipboard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Clipboard.SetText(ViewModel.InstallPath);
        }
    }
}