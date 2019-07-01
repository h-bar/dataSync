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
        private static string repoUri = Properties.Settings.Default.repoUri;
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

        private static string repoPath = Path.Combine(rootPath, Properties.Settings.Default.repoDir);
        private static string thefilePath = Path.Combine(repoPath, thefile);
        private static string dbfilePath = Path.Combine(repoPath, dbfile);
        private static string appPath = Path.Combine(rootPath, Properties.Settings.Default.app);

        public static Identity id = new Identity(user, email);
        private static CredentialsHandler credential = new CredentialsHandler(
        (url, usernameFromUrl, types) =>
            new UsernamePasswordCredentials()
            {
                Username = Properties.Settings.Default.gitUsername,
                Password = Properties.Settings.Default.gitPasswd
            });
        private static Signature signature = new Signature(id, DateTimeOffset.Now);
        private static Logger logger = LogManager.GetLogger("fileLogger");

        private Repository repo;

        private void clone()
        {
            CloneOptions options = new CloneOptions();
            options.CredentialsProvider = credential;
            try
            {
                logger.Debug("Cloning repo...");
                Repository.Clone(repoUri, repoPath, options);
            }
            catch (Exception e)
            {
                logger.Debug("Cloning failed: " + e.Message);
            }
            
        }
        private bool pull()
        {
            PullOptions options = new PullOptions();
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
                //initButton.IsEnabled = true;
                launchButton.IsEnabled = false;
                return false;
            }
            syncStatus.Text = "数据更新完成";
            logger.Debug("Pull success");
            //initButton.IsEnabled = false;
            launchButton.IsEnabled = true;
            return true;
        }

        private bool push()
        {
            PushOptions options = new PushOptions();
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
            StreamWriter thefileWriter = File.AppendText(thefilePath);
            thefileWriter.Write("a");
            thefileWriter.Flush();
            thefileWriter.Close();
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
                launchButton.IsEnabled = false;
                return false;
            }

            logger.Debug("data available");
            dataStatus.Text = "数据文件可以使用";
            launchButton.IsEnabled = true;
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

            try
            {
                repo = new Repository(repoPath);
            }
            catch (Exception e)
            {
                if(Directory.Exists(repoPath))
                {
                    Directory.Delete(repoPath, true);
                }

                clone();
                repo = new Repository(repoPath);
            }
            if (!(pull())) { return; }
            if (!(dataAvailablity())) { return; }
            launchButton.IsEnabled = true;
        }

        private void InitData(object sender, RoutedEventArgs e)
        {
            logger.Debug("Init button pressed");
            string temppath = Path.Combine(rootPath, "tempdata");
            string tempdbpath = Path.Combine(temppath, dbfile);
            Directory.Move(repoPath, temppath);
            clone();

            if (File.Exists(tempdbpath))
            {
                File.Move(tempdbpath, dbfilePath);
            }
            repo = new Repository(repoPath);
            commitChange("release");
        }
        private void SyncData(object sender, RoutedEventArgs e)
        {
            logger.Debug("Sync button pressed");
            pull();
            dataAvailablity();
        }

        private void UploadData(object sender, RoutedEventArgs e)
        {
            logger.Debug("Upload button pressed");
            push();
        }

        private void LaunchApp(object sender, RoutedEventArgs e)
        {
            logger.Debug("Launch button pressed");
            if (!pull()) { return; }
            launchButton.IsEnabled = false;
            syncButton.IsEnabled = false;
            //initButton.IsEnabled = false;
            if (!(commitChange("lock"))) { return; }
            launch();
            commitChange("release");
            launchButton.IsEnabled = true;
            syncButton.IsEnabled = true;
        }
    }
}
