using System.IO;
using System.Windows.Forms;
using DownpatcherSharp;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System;
using System.Linq;
using MahApps.Metro.Controls.Dialogs;
using System.Threading.Tasks;
using System.Threading;
using System.Management;
using System.ComponentModel;
using System.Text.RegularExpressions;
using MahApps.Metro.Controls;
using IWshRuntimeLibrary;
using File = System.IO.File;

namespace BL3Downpatcher
{
    public class Borderlands3 : Game
    {
        public Borderlands3(string filePath, string gameName) : base(filePath, gameName) { }

        public override string getCurrentPatch()
        {
            try
            {
                string exePath = gameDir.FullName + @"\OakGame\Binaries\Win64\Borderlands3.exe";
                FileInfo exeInfo = new FileInfo(exePath);
                switch (exeInfo.Length)
                {
                    case 625574032:
                        return "1.0.0";
                    case 650755728:
                        return "1.0.1";
                }
                return "Unknown";
            }
            catch (Exception ex)
            {
                return "Unknown";
            }
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        Borderlands3 game;
        public MainWindow()
        {
            game = new Borderlands3(Properties.Settings.Default.gamePathDirectory, "Borderlands3");
            InitializeComponent();
            GamePath.Text = Properties.Settings.Default.gamePathDirectory;
            foreach (string p in game.getPatches())
                patchBox.Items.Add(p);
            HotfixState.Content = checkHotfixState();
        }

        #region Patcher Stuff
        private void PatchGameClick(object sender, System.Windows.RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            game.setCurrentPatch(((string)patchBox.SelectedItem));
            updateVersionLabel();
            Mouse.OverrideCursor = null;
            //await showMessage("", "Patched!");
        }

        #region File Path Management

        private void BrowseDialog(object sender, MouseButtonEventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = false;
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    GamePath.Text = dialog.SelectedPath;
                }
            }

        }

        private void GamePathChanged(object sender, TextChangedEventArgs e)
        {
            Properties.Settings.Default.gamePathDirectory = GamePath.Text;
            Properties.Settings.Default.Save();
            game.setCurrentFilePath(GamePath.Text);
            updateVersionLabel();
        }

        #endregion

        #region Version Management
        private void updateVersionLabel()
        {
            VersionLabel.Content = game.getCurrentPatch();
        }
        #endregion

        #endregion

        #region Hotfix Management

        private static readonly string hostsEnabled = "127.0.0.1    discovery.services.gearboxsoftware.com";
        private readonly string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

        private string checkHotfixState()
        {
            return File.ReadAllText(hostsPath).Contains(hostsEnabled) ? "Disabled" : "Enabled";
        }

        private void EnableHotfixes(object sender, System.Windows.RoutedEventArgs e)
        {
            var linesToKeep = File.ReadAllLines(hostsPath).Where(l => l != hostsEnabled);
            File.WriteAllLines(hostsPath, linesToKeep);
            flushDNSCache();
            showMessage("Complete", "Hotfixes Enabled!");
        }

        private void DisableHotfixes(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                using (StreamWriter w = File.AppendText(hostsPath))
                {
                    w.WriteLine(hostsEnabled);
                }
                flushDNSCache();
                showMessage("Complete", "Hotfixes Disabled!");
            }
            catch (Exception ex)
            {
                showMessage("Error!", "An error occurred! Try running in administrator mode. If this doesn't work, contact FromDarkHell!");
                Helpers.writeErrorToDisk("Borderlands3", ex);
            }
        }

        private void flushDNSCache()
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/c ipconfig /flushdns";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo = startInfo;
            process.Start();

            process.WaitForExit(1000);
            process.Close();

            HotfixState.Content = checkHotfixState();
        }



        #endregion

        #region Desktop Shortcut Management
        private static string epicCmd = "";
        private async void CreateDesktopShortcut(object sender, System.Windows.RoutedEventArgs e)
        {
            var result = await showMessage("Alert", "BL3 should be ran to make the shortcut. Yes?", MessageDialogStyle.AffirmativeAndNegative);
            if (result != MessageDialogResult.Affirmative)
                return;

            Process.Start(@"com.epicgames.launcher://apps/Catnip?action=launch&silent=true");
            bool hasLaunchedProperProcess = false;
            Process correctProcess = null;
            while (!hasLaunchedProperProcess)
            {
                foreach (Process process in Process.GetProcessesByName("Borderlands3"))
                {
                    if (process.MainModule.FileName.Contains("OakGame"))
                    {
                        correctProcess = process;
                        hasLaunchedProperProcess = true;
                    }
                }
                Thread.Sleep(1000);
            }

            if (correctProcess == null)
                return;

            try
            {

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + correctProcess.Id))
                using (ManagementObjectCollection objects = searcher.Get())
                {
                    epicCmd = objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
                }
            }
            // Intentionally empty - no security access to the process.
            catch (Win32Exception ex) when ((uint)ex.ErrorCode == 0x80004005) { }
            // Intentionally empty - the process exited before getting details.
            catch (InvalidOperationException) { }
            correctProcess.Kill();

            DirectoryInfo egStore = new DirectoryInfo(game.gameDir.FullName + @"\.egstore\");
            FileInfo correctOvtFile = null;
            foreach (var d in egStore.GetDirectories().Where(d => !d.Name.Contains("Pending")).Select(d => d))
                correctOvtFile = d.GetFiles("catnip*").FirstOrDefault();

            string newOvt = string.Format("-epicovt=\"{0}\"", correctOvtFile.FullName);
            epicCmd = Regex.Replace(epicCmd, "-epicovt=\".{1,}\"", (newOvt));
            string exePath = game.gameDir.FullName + @"\OakGame\Binaries\Win64\Borderlands3.exe";
            Regex regex = new Regex("\".{1,}\" OakGame");
            epicCmd = regex.Replace(epicCmd, "OakGame", 1);

            WshShell wsh = new WshShell();
            IWshShortcut shortcut = wsh.CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Borderlands 3 - Downpatched.lnk") as IWshShortcut;
            shortcut.Arguments = epicCmd;
            shortcut.TargetPath = exePath;

            shortcut.WindowStyle = 1;
            shortcut.Description = "Borderlands 3 - Downpatched";
            shortcut.Save();
            await showMessage("", "Complete!");
        }


        #endregion


        private async Task<MessageDialogResult> showMessage(string title, string message, MessageDialogStyle style = MessageDialogStyle.Affirmative)
        {
            return await this.ShowMessageAsync(title, message, style);

        }
    }
}
