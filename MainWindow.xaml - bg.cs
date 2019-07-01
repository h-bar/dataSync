using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;
using System.Diagnostics;
using System.ComponentModel;

using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace dataSync
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BackgroundWorker pullWorker = new BackgroundWorker();
        private readonly BackgroundWorker pushWorker = new BackgroundWorker();
        private readonly BackgroundWorker syncWorker = new BackgroundWorker();

        // static string user = "美好家园";
        // static string email = "mhju@ch.ch";
        // static string user = "宋兰英";
        // static string email = "sly@ch.ch";
        private static string user = "张言";
        private static string email = "zy@ch.ch";
        private static Identity id = new Identity(user, email);

        private static string rootPath = @"C:\Users\663ya\Downloads\宝如珠宝首饰管理系统单机版v8.0";
        private static string repoDir = "Data";
        private static string thefile = "thefile";
        private static string dbfile = "leader.db";
        private static string app = "lg2004.exe";

        private static string repoPath = System.IO.Path.Combine(rootPath, repoDir);
        private static string thefilePath = System.IO.Path.Combine(repoPath, thefile);
        private static string dbfilePath = System.IO.Path.Combine(repoPath, dbfile);
        private static string appPath = System.IO.Path.Combine(rootPath, app);

        private static Repository repo = new Repository(repoPath);
        private static StreamWriter thefileWriter = File.AppendText(thefilePath);
        private static CredentialsHandler credential = new CredentialsHandler(
        (url, usernameFromUrl, types) =>
            new UsernamePasswordCredentials()
            {
                Username = "h-bar@qq.com",
                Password = "myGTpasswd32@"
            });
        private static LibGit2Sharp.Signature signature = new LibGit2Sharp.Signature(id, DateTimeOffset.Now);

        private void pull()
        {
            if (!pullWorker.IsBusy && !pushWorker.IsBusy && !syncWorker.IsBusy)
            {
                pullWorker.RunWorkerAsync();
            }
        }

        private void push()
        {
            if (!pullWorker.IsBusy && !pushWorker.IsBusy && !syncWorker.IsBusy)
            {
                pushWorker.RunWorkerAsync();
            }
        }

        private void sync()
        {
            if (!pullWorker.IsBusy && !pushWorker.IsBusy && !syncWorker.IsBusy)
            {
                syncWorker.RunWorkerAsync();
            }
        }

        private void pull_work(object sender, DoWorkEventArgs e)
        {
            LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions();
            options.FetchOptions = new FetchOptions();
            options.FetchOptions.CredentialsProvider = credential;
            BackgroundWorker worker = sender as BackgroundWorker;
            worker.ReportProgress(1);
            try
            {
                Console.WriteLine("Pulling from remote");
                var result = Commands.Pull(repo, signature, options);
                Console.WriteLine(result.Status);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Pull failed: " + exception.Message);
                e.Result = false;
            }
            Console.WriteLine("Pull success");
            e.Result = true;
        }

        private void pull_progress(object sender, ProgressChangedEventArgs e)
        {
            syncStatus.Text = "数据更新中。。。";
        }

        private void pull_finished(object sender, RunWorkerCompletedEventArgs e)
        {
            if ((bool)e.Result)
            {
                syncStatus.Text = "数据更新完成";
            } else
            {
                syncStatus.Text = "数据更新失败，请点击手动同步重试";
            }
        }

        private bool push()
        {
            LibGit2Sharp.PushOptions options = new LibGit2Sharp.PushOptions();
            options.CredentialsProvider = credential;
            syncStatus.Text = "数据上传中。。。";
            try
            {
                Console.WriteLine("Pushing to remote");
                repo.Network.Push(repo.Head, options);
            }
            catch (Exception e)
            {
                syncStatus.Text = "数据上传失败，请点击手动同步重试";
                Console.WriteLine("Push failed: " + e.ToString());
                return false;
            }
            syncStatus.Text = "数据上传完成";
            Console.WriteLine("Push success");
            return true;
        }

        private bool commitChange(string status)
        {
            thefileWriter.Write("a");
            thefileWriter.Flush();
            repo.Index.Add(thefile);
            repo.Index.Add(dbfile);
            repo.Commit(user + '-' + status, signature, signature);
            return push();
        }

        private bool dataAvailablity()
        {
            string lastAuthor = repo.Head.Tip.Author.Name;
            string lastCommit = repo.Head.Tip.Message;
            if (lastAuthor != user && lastCommit.Contains("lock"))
            {
                dataStatus.Text = lastAuthor + "正在使用数据文件";
                return false;
            }

            dataStatus.Text = "数据文件可以使用";
            return true;
        }

        private void launch()
        {
           
            ProcessStartInfo brSystem = new ProcessStartInfo();
            brSystem.FileName = appPath;
            brSystem.WorkingDirectory = rootPath;

            Console.WriteLine("Launch the software");
            using (Process proc = Process.Start(brSystem))
            {
                proc.WaitForExit();
            }
            Console.WriteLine("The software is endded");
        }

        public MainWindow()
        {
            InitializeComponent();
            Console.WriteLine("Program started");
            pullWorker.WorkerReportsProgress = true;
            pullWorker.DoWork += pull_work;
            pullWorker.RunWorkerCompleted += pull_finished;
            pullWorker.ProgressChanged += pull_progress;
            //if (!(pull())) { return; }
            //if (!(dataAvailablity())) { return; }
            launchButton.IsEnabled = true;
        }

        private void SyncData(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Sync button pressed");
            pull();
            //push();
        }

        private void LaunchApp(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Launch button pressed");
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
