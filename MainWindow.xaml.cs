using System;
using System.Windows;

using System.IO;
using System.Diagnostics;

using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using NLog;

namespace dataSync
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    ///
    public partial class MainWindow : Window
    {
        private static string thefile = Properties.Settings.Default.thefile;
        private static string dbfile = Properties.Settings.Default.dbfile;
        private static string path = Properties.Settings.Default.path;

        private static string user = Properties.Settings.Default.user;
        private static string email = Properties.Settings.Default.email;

        private static string rootPath {
            get
            {
                string[] path = Properties.Settings.Default.path.Split(';');

                string validPath = "";
                foreach (string p in path)
                {
                    if (Directory.Exists(p))
                    {
                        validPath = p;
                    }
                }
                return validPath;
            }
        }

        private static string repoPath = System.IO.Path.Combine(rootPath, Properties.Settings.Default.repoDir);
        private static string thefilePath = System.IO.Path.Combine(repoPath, Properties.Settings.Default.thefile);
        private static string appPath = System.IO.Path.Combine(rootPath, Properties.Settings.Default.app);

        public static Identity id = new Identity(user, email);
        private static StreamWriter thefileWriter = File.AppendText(thefilePath);
        private static Repository repo = new Repository(repoPath);
        private static CredentialsHandler credential = new CredentialsHandler(
        (url, usernameFromUrl, types) =>
            new UsernamePasswordCredentials()
            {
                Username = Properties.Settings.Default.gitUsername,
                Password = Properties.Settings.Default.gitPasswd
            });
        private static LibGit2Sharp.Signature signature = new LibGit2Sharp.Signature(id, DateTimeOffset.Now);
        private static NLog.Logger logger = NLog.LogManager.GetLogger("fileLogger");

        private bool pull()
        {
            LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions();
            options.FetchOptions = new FetchOptions();
            options.FetchOptions.CredentialsProvider = credential;
            syncStatus.Text = "数据更新中。。。";
            logger.Debug("Updating...");

            try
            {
                logger.Debug("Pulling from remote");
                var result = Commands.Pull(repo, signature, options);
                logger.Debug(result.Status);
            }
            catch (Exception e)
            {
                syncStatus.Text = "数据更新失败，请点击手动同步重试";
                logger.Error("Pull failed: " + e.Message);
                return false;
            }
            syncStatus.Text = "数据更新完成";
            logger.Debug("Pull success");
            return true;
        }

        private bool push()
        {
            LibGit2Sharp.PushOptions options = new LibGit2Sharp.PushOptions();
            options.CredentialsProvider = credential;
            logger.Debug("Downloading...");
            try
            {
                logger.Debug("Pushing to remote");
                repo.Network.Push(repo.Head, options);
            }
            catch (Exception e)
            {
                syncStatus.Text = "数据上传失败，请点击手动同步重试";
                logger.Error("Push failed: " + e.ToString());
                return false;
            }
            syncStatus.Text = "数据上传完成，可关闭此窗口";
            logger.Debug("Push success");
            return true;
        }

        private bool commitChange(string status)
        {
            thefileWriter.Write("a");
            thefileWriter.Flush();
            repo.Index.Add(thefile);
            repo.Index.Add(dbfile);
            repo.Index.Write();
            repo.Commit(status, signature, signature);
            return push();
        }

        private bool dataAvailablity()
        {
            logger.Debug("Checking data availablity");
            string lastAuthor = repo.Head.Tip.Author.Name;
            string lastCommit = repo.Head.Tip.Message;
            if (lastAuthor != user && lastCommit.Contains("lock"))
            {
                dataStatus.Text = lastAuthor + "正在使用数据文件";
                logger.Error(lastAuthor + " is using the file");
                return false;
            }

            logger.Debug("data available");
            dataStatus.Text = "数据文件可以使用";
            return true;
        }

        private void launch()
        {
            ProcessStartInfo brSystem = new ProcessStartInfo();
            brSystem.FileName = appPath;
            brSystem.WorkingDirectory = rootPath;

            logger.Debug("Launch the software");
            using (Process proc = Process.Start(brSystem))
            {
                proc.WaitForExit();
            }
            logger.Debug("The software is endded");
        }

        public MainWindow()
        {
            InitializeComponent();
            logger.Debug("");
            logger.Debug("=======================");
            logger.Debug("=======================");
            logger.Debug("=======================");
            logger.Debug("=======================");
            logger.Debug("Program started");
            if (!(pull())) { return; }
            if (!(dataAvailablity())) { return; }
            launchButton.IsEnabled = true;
        }

        private void SyncData(object sender, RoutedEventArgs e)
        {
            logger.Debug("Sync button pressed");
            pull();
            dataAvailablity();
        }

        private void LaunchApp(object sender, RoutedEventArgs e)
        {
            logger.Debug("Launch button pressed");
            if (!pull()) { return; }
            launchButton.IsEnabled = false;
            syncButton.IsEnabled = false;
            if (!(commitChange("lock"))) { return; }
            launch();
            commitChange("release");
            launchButton.IsEnabled = true;
            syncButton.IsEnabled = true;
        }
    }
}
