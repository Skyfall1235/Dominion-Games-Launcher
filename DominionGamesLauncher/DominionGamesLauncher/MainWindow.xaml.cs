using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Security.Policy;
using System.Windows.Threading;

namespace DominionGamesLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private string rootPath;
        private string versionFile;
        private string gameZip;
        private string gameExe;
        const string gameName = "EnterNameHere"; // must be the name of your build zip as well

        private LauncherStatus _status;
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status) 
                {
                    case LauncherStatus.Ready:
                        PlayButton.Content = "Play"; // could use an image here?
                        break;
                    case LauncherStatus.Failed:
                        PlayButton.Content = "Update failed - Retry";
                        break;
                    case LauncherStatus.DownloadingGame:
                        PlayButton.Content = "Downloading Game";
                        break;
                    case LauncherStatus.DownloadingUpdate:
                        PlayButton.Content = "Downloading Update";
                        break;
                    default: 
                        break;
                }

            }
        }


        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();
            rootPath = Directory.GetCurrentDirectory();
            versionFile = Path.Combine(rootPath, "version.txt");
            gameZip = Path.Combine(rootPath, gameName+".zip");
            gameExe = Path.Combine(rootPath, "Build", gameName + ".exe");

            // Initialize the timer
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(15); // Update every second
            timer.Tick += Timer_Tick;

            // Start the timer
            timer.Start();
        }

        //is called once upon the first render of the window
        private void window_ContentRendered(object sender, EventArgs e)
        {

            CheckForUpdates();
        }

        #region Click Methods
        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.Ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = Path.Combine(rootPath, "Build");
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.Failed)
            {
                CheckForUpdates();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SupportButton_Click(object sender, RoutedEventArgs e)
        {

        }
        #endregion


        #region Updating game files
        private void CheckForUpdates()
        {
            if (File.Exists(versionFile))
            {
                Version localVersion = new Version(File.ReadAllText(versionFile));
                GameVersionText.Text = localVersion.ToString();

                try
                {
                    HttpClient webClient = new();
                    Version onlineVersion;
                    //check the version file for what version the game is
                    string versionText = GetOnlineVersionText("version file link");
                    //if it returned null, we know it failed
                    if (versionText != null)
                    {
                        onlineVersion = new Version(versionText);
                    }
                    else return;

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        _ = InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.Ready;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.Failed;
                    MessageBox.Show($"Error checking for game updates: {ex}");
                }
            }
            else
            {
                _ = InstallGameFiles(false, Version.zero);
            }
        }

        private async Task InstallGameFiles(bool _isUpdate, Version _onlineVersion)
        {
            try
            {
                using var httpClient = new HttpClient();

                if (_isUpdate)
                {
                    Status = LauncherStatus.DownloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.DownloadingGame;
                    _onlineVersion = new Version(await httpClient.GetStringAsync("version file link"));
                }

                using var response = await httpClient.GetAsync(new Uri("game zip link"));
                response.EnsureSuccessStatusCode();

                using var fileStream = new FileStream(gameZip, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fileStream);

                await ExtractGameZipAsync(gameZip, rootPath);
                File.Delete(gameZip);

                File.WriteAllText(versionFile, _onlineVersion.ToString());

                GameVersionText.Text = _onlineVersion.ToString();
                Status = LauncherStatus.Ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }


        public async Task<string> GetContents(string url)
        {
            using var webClient = new HttpClient();
            var response = await webClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }

        private async Task ExtractGameZipAsync(string zipPath, string targetPath)
        {
            using (var zipArchive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in zipArchive.Entries)
                {
                    string entryPath = Path.Combine(targetPath, entry.FullName);
                    if (IsDirectoryEntry(entry))
                    {
                        Directory.CreateDirectory(entryPath);
                    }
                    else
                    {
                        using (var entryStream = entry.Open())
                        {
                            using (var fileStream = new FileStream(entryPath, FileMode.Create, FileAccess.Write))
                            {
                                await entryStream.CopyToAsync(fileStream);
                            }
                        }
                    }
                }
            }
        }

        bool IsDirectoryEntry(ZipArchiveEntry entry)
        {
            return entry.FullName.EndsWith("/") && string.IsNullOrEmpty(entry.Name);
        }

        public string GetOnlineVersionText(string url)
        {
            string onlineVersionText = "";

            // Create an HttpClient instance
            using (var httpClient = new HttpClient())
            {
                try
                {
                    // Send the GET request and ensure success
                    var response = httpClient.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();

                    // Read the response content
                    onlineVersionText = response.Content.ReadAsStringAsync().Result;
                }
                catch (Exception ex)
                {
                    // Log the exception
                    Status = LauncherStatus.Failed;
                    MessageBox.Show($"Error updating files: {ex}");
                }
            }

            return onlineVersionText;
        }

        #endregion


        #region Timer methods
        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update the UTC time display
            DisplayUTCTime();
        }

        void DisplayUTCTime()
        {
            DateTime utcNow = DateTime.UtcNow;
            string timeStamp = utcNow.ToString("HH:mm") + " UTC"; ;
            Console.WriteLine(timeStamp);
            TimeStamp.Text = timeStamp;
        }
        #endregion

        
    }



    public enum LauncherStatus
    {
        Ready,
        Failed,
        DownloadingGame,
        DownloadingUpdate
    }

    /// <summary>
    /// Represents a version with major, minor, and subMinor components.
    /// </summary>
    struct Version
    {
        // Represents a zero-initialized Version.
        internal static Version zero = new Version(0, 0, 0);

        // Components of the version.
        short major;
        short minor;
        short subMinor;

        /// <summary>
        /// Initializes a Version with specified components.
        /// </summary>
        internal Version(short _major, short _minor, short _subMinor)
        {
            major = _major;
            minor = _minor;
            subMinor = _subMinor;
        }

        /// <summary>
        /// Parses a version string in the format "major.minor.subMinor". If the format is invalid, initializes the version to zero.
        /// </summary>
        internal Version(string _version)
        {
            string[] _versionStrings = _version.Split('.');
            if (_versionStrings.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }
            major = short.Parse(_versionStrings[0]);
            minor = short.Parse(_versionStrings[1]);
            subMinor = short.Parse(_versionStrings[2]);
        }

        /// <summary>
        /// Checks if the current version is different from another version.
        /// </summary>
        /// <returns> True if any component differs, otherwise returns false.</returns>
        internal bool IsDifferentThan(Version _otherVersion)
        {
            if (major != _otherVersion.major || minor != _otherVersion.minor || subMinor != _otherVersion.subMinor)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Overrides the ToString() method to represent the version as a string.
        /// </summary>
        public override string ToString()
        {
            return $"{major}.{minor}.{subMinor}";
        }
    }


}
