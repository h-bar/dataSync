using System;
using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
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
        private static Logger logger = LogManager.GetLogger("fileLogger");

        private Repository repo;

        private string prepareRepo()
        {
            try
            {
                logger.Info("Prepare repo object");
                repo = new Repository(repoPath);
            }
            catch(Exception e)
            {
                logger.Error("Prepare repo failed: " + e.Message);
                return e.Message;
            }
            return "";
        }
        private string pull()
        {
            PullOptions options = new PullOptions();
            options.FetchOptions = new FetchOptions();
            options.FetchOptions.CredentialsProvider = credential;
            try
            {
                logger.Info("Pull from remote");
                var result = Commands.Pull(repo, new Signature(id, DateTimeOffset.Now), options);
                logger.Info(result.Status);
            }
            catch (Exception e)
            {
                logger.Error("Pull failed: " + e.Message);
                return e.Message;
            }
            return "";
        }
        private string push()
        {
            PushOptions options = new PushOptions();
            options.CredentialsProvider = credential;
            options.OnPushTransferProgress = (current, total, bytes) =>
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    pbBar.Value = current / total * 70 + 30;
                }));
                return true;
            };
            try
            {
                logger.Info("Push to remote");
                repo.Network.Push(repo.Head, options);
            }
            catch (Exception e)
            {
                logger.Error("Push failed: " + e.Message);
                return e.Message;
            }
            return "";
        }

        private string commitChange(string status)
        {
            try
            {
                logger.Info("Write a to the file");
                using (StreamWriter thefileWriter = File.AppendText(thefilePath))
                {  
                    thefileWriter.Write("a");
                    thefileWriter.Flush();
                    thefileWriter.Close();
                }

                logger.Info("Add files to git index");
                repo.Index.Add(thefile);
                repo.Index.Add(dbfile);
                repo.Index.Write();

                logger.Info("Commit message");
                repo.Commit(status, new Signature(id, DateTimeOffset.Now), new Signature(id, DateTimeOffset.Now));
            }
            catch (Exception e)
            {
                logger.Error("Push failed: " + e.Message);
                return e.Message;
            }
            return "";
        }
        private string dataAvailablity()
        {
            logger.Info("Checking data availablity");
            string lastAuthor = repo.Head.Tip.Author.Name;
            string lastCommit = repo.Head.Tip.Message;
            if (lastAuthor != user && lastCommit.Contains("lock"))
            {
                string msg = lastAuthor + " 正在使用数据文件";
                logger.Error(msg);
                return msg;
            }
            return "";
        }

        private string launch()
        {
            ProcessStartInfo brSystem = new ProcessStartInfo();
            brSystem.FileName = appPath;
            brSystem.WorkingDirectory = rootPath;
            try
            {
                logger.Info("Launch the software");
                Process.Start(brSystem).WaitForExit();
                logger.Info("The software is exited");
            }
            catch (Exception e)
            {
                logger.Error("Launch software error: " + e.Message);
                return e.Message;
            }
            return "";
        }

        private void errorCheck(string errorM)
        {
            if (errorM != "")
            {
                MessageBox.Show(errorM, "错误", MessageBoxButton.OK);
                Environment.Exit(0);
            }
        }

        private void MainTasks()
        {
            Dispatcher.BeginInvoke((Action) (() =>
            {
                prompt.Text = "准备数据。。。";
                pbBar.Value = 0;
            }));

            errorCheck(prepareRepo());

            Dispatcher.BeginInvoke((Action)(() =>
            {
                prompt.Text = "下载数据。。。";
                pbBar.Value = 10;
            }));
            errorCheck(pull());

            Dispatcher.BeginInvoke((Action)(() =>
            {
                prompt.Text = "检查数据可用性。。。";
                pbBar.Value = 20;
            }));
            errorCheck(dataAvailablity());

            Dispatcher.BeginInvoke((Action)(() =>
            {
                pbBar.Value = 25;
                prompt.Text = "锁定数据。。。";
            }));
            errorCheck(commitChange("lock"));

            Dispatcher.BeginInvoke((Action)(() =>
            {
                pbBar.Value = 30;
            }));
            errorCheck(push());

            Dispatcher.BeginInvoke((Action)(() =>
            {
                pbBar.Value = 100;
                prompt.Text = "启动软件。。。";
            }));
            errorCheck(launch());

            Dispatcher.BeginInvoke((Action)(() =>
            {
                pbBar.Value = 0;
                prompt.Text = "释放数据。。。";
            }));
            errorCheck(commitChange("release"));

            Dispatcher.BeginInvoke((Action)(() =>
            {
                pbBar.Value = 30;
            }));

            string msg = "dummy";
            int i = 1;
            Dispatcher.BeginInvoke((Action)(() =>
            {
                prompt.Text = "上传数据, 第" + i + "次尝试，请勿关闭此窗口";
            }));
            msg = push();

            while (msg != "")
            {
                i += 1;
                MessageBox.Show("点击确认上传数据", "提示", MessageBoxButton.OK);
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    prompt.Text = "上传数据, 第" + i + "次尝试，请勿关闭此窗口";
                }));
                msg = push();
            }
            logger.Info("Push success after " + i + "tries");
            MessageBox.Show("数据上传成功！", "提示", MessageBoxButton.OK);
            Environment.Exit(0);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            MessageBox.Show("请勿在数据同步或使用过程中退出", "提示", MessageBoxButton.OK);
        }
        public MainWindow()
        {
            InitializeComponent();
            logger.Info("");
            logger.Info("=======================");
            logger.Info("=======================");
            logger.Info("=======================");
            logger.Info("=======================");
            logger.Info("Program started");

            Task.Run(MainTasks);
        }
    }
}
