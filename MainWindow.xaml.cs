using Leaf.xNet;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AmazonScraperUI
{
    class Worker
    {
        public static List<string> Proxies = new List<string>();
        public static int TotalProxies = 0;

        public static int ThreadCount = 0;
        public static List<Thread> WorkerThreads = new List<Thread>();
        public static List<Thread> ScraperThreads = new List<Thread>();

        public static string NodeID = "";
        public static string Discount = "";
        public static List<string> ProductIDs = new List<string>();

        public static List<string> InUse_ProductIDs = new List<string>();
        public static List<string> ValidProductIDs = new List<string>();
        public static int ProductIndex = -1;
        public static int ProxyIndex = -1;

        public static bool RateLimited = false;

        public static string ResultsFile = "";

        /// <summary>
        /// Used to parse HTML
        /// </summary>
        /// <param name="response"></param>
        /// <param name="attritube"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static string ParseResponse(string response, string attritube, char left, char right)
        {
            try
            {
                if (response.Contains(attritube))
                {
                    int start = response.IndexOf(attritube) + attritube.Length;
                    if (response[start] == left)
                    {
                        int end = response.IndexOf(right, start + 1);

                        int length = end - start - 1;
                        return response.Substring(start + 1, length);
                    }
                }
            }
            catch { }
            return "";
        }
        public static string ParseResponse(string response, int start, string left, string right)
        {
            try
            {
                if (response.IndexOf(left) != -1)
                {
                    start = response.IndexOf(left) + left.Length;
                    int end = response.IndexOf(right, start + 1);
                    if (end == -1)
                        return "";
                    int length = end - start;
                    return response.Substring(start, length);
                }
            }
            catch { }
            return "";
        }
        /// <summary>
        /// Thread that updates the Status in the UI
        /// </summary>
        public static void UpdateActiveThreads()
        {
            while(true)
            {
                int AliveThreads = 0;
                foreach (Thread thread in WorkerThreads)
                    if (thread.IsAlive)
                        AliveThreads++;
                AliveThreads--; //One thread is this one
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log.MainWin.ActiveBots = AliveThreads.ToString();
                    Log.MainWin.Checked = ProductIndex.ToString();
                    Log.MainWin.ValidProducts = ValidProductIDs.Count.ToString();

                    if (AliveThreads == 0)
                        Log.MainWin.ResetBot();
                });
                Thread.Sleep(100);
            }
        }
        /// <summary>
        /// Stops the scraping threads when rate limited
        /// </summary>
        public static void StopScraperThreads()
        {
            if (ScraperThreads.Count > 0)
            {
                foreach (Thread ScraperThread in ScraperThreads)
                    ScraperThread.Abort();
                ScraperThreads.Clear();
            }
        }
         /// <summary>
         /// Extracts ProductIDs from pages
         /// </summary>
         /// <param name="Page"></param>
        public static void ExtractProductIDs(int Page)
        {
            var request = new HttpRequest();
            request.KeepAlive = true;
            request.IgnoreProtocolErrors = true;
            request.ConnectTimeout = 5000;
            request.AllowAutoRedirect = true;
            request.UseCookies = true;
            request.UserAgentRandomize();
            //request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36";
            request.AddHeader("Accept", "*/*");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            string URL = "";
            if (Discount == "")
                URL = $"https://www.amazon.com/mn/search?_encoding=UTF8&node={NodeID};page={Page}&pct-off=0-100";
            else URL = $"https://www.amazon.com/mn/search?_encoding=UTF8&node={NodeID}&pct-off={Discount};page={Page}";
            string Data = "";
            try
            {
                Data = request.Get(URL).ToString();
                if (Data.Contains("<title dir=\"ltr\">Robot Check</title>") || Data.Contains("To discuss automated access to Amazon data please contact api-services-support@amazon.com."))
                {
                    Log.Write($"\nRate limited by Amazon.com (Page Number : {Page}) Stopping scraping process");
                    RateLimited = true;
                    return;
                }
                int check = Data.IndexOf("<span data-component-type=\"s-product-image\" class=\"rush-component\">");
                if (check < 0)
                    return;
            }
            catch { return; }

            int start = Data.IndexOf("<span data-component-type=\"s-product-image\" class=\"rush-component\">");
            if (start < 0)
                return;
            int end = Data.IndexOf("<span cel_widget_id=\"MAIN-PAGINATION\"");
            if (end < 0) return;
            Data = Data.Substring(start, end - start);
            int Iterator = 0;
            string Pattern = "<span data-component-type=\"s-product-image\" class=\"rush-component\">";
            while ((Iterator = Data.IndexOf(Pattern, Iterator)) != -1)
            {
                string ProductID = ParseResponse(Data.Substring(Iterator), Pattern.Length, "/dp/", "/");
                ProductIDs.Add(ProductID);
                Iterator += Pattern.Length;
            }
        }
        /// <summary>
        /// Checks the NodeID for links and saves all the links found.
        /// </summary>
        /// <returns></returns>
        public static void CheckNode()
        {
            try
            {
                var exectime = new System.Diagnostics.Stopwatch();
                exectime.Start();

                var request = new HttpRequest();
                request.KeepAlive = true;
                request.IgnoreProtocolErrors = true;
                request.ConnectTimeout = 5000;
                request.AllowAutoRedirect = true;
                request.UseCookies = true;
                request.UserAgentRandomize();
                //request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36";
                request.AddHeader("Accept", "*/*");
                request.AddHeader("Upgrade-Insecure-Requests", "1");
                string URL = "https://www.amazon.com/mn/search?_encoding=UTF8&node={NodeID}&pct-off={Discount}";
                if (Discount == "")
                    URL = $"https://www.amazon.com/mn/search?_encoding=UTF8&node={NodeID}&pct-off=0-100";
                else
                {
                    if (Discount.Contains("-"))
                        URL = $"https://www.amazon.com/mn/search?_encoding=UTF8&node={NodeID}&pct-off={Discount}";
                    else Discount += "-100"; ;
                }
                Thread.Sleep(300);
                try
                {
                    string GetData = request.Get(URL).ToString();
                    if (GetData.Contains("<title dir=\"ltr\">Robot Check</title>") || GetData.Contains("To discuss automated access to Amazon data please contact api-services-support@amazon.com."))
                    {
                        Log.Write("\nRate limited by Amazon.com. Please try again after sometime or goto Amazon.com and solve captcha");
                        return;
                    }
                    else RateLimited = false;
                    int start = GetData.IndexOf("<span data-component-type=\"s-product-image\" class=\"rush-component\">");
                    if (start < 0)
                    {
                        Log.Write("\nNo links from this Node ID. Please verify the Node ID is a Leaf Node.");
                        return;
                    }
                    string Pattern = "<li class=\"a-disabled\">";
                    int Pages = int.Parse(ParseResponse(GetData.Substring(GetData.LastIndexOf(Pattern)), 0, Pattern, "</li>"));

                    Log.Write($"\nFound {Pages} pages of results");
                    Log.Write($"\nExtracting Products from pages...(Might take a while)");

                    List<Thread> Threads = new List<Thread>();
                    int CurrentPage = 1;
                    int Itterations = 1;
                    while (CurrentPage <= Pages)
                    {
                        Task.Factory.StartNew(delegate
                        {
                            for (int i = 0; i < 30; i++)
                            {
                                if (Itterations * (i + 1) > Pages)
                                    break;
                                else
                                {
                                    CurrentPage++;
                                    Threads.Add(new Thread(() =>
                                    {
                                        ExtractProductIDs(Itterations * (i + 1));
                                    }));
                                    Threads[i].Start();
                                }
                            }
                        }).Wait();
                        while (Threads.Exists(x => x.IsAlive == true))
                        {
                            if (RateLimited)
                            {
                                StopScraperThreads();
                                break;
                            }
                            Thread.Sleep(100);
                        }
                        if (RateLimited) break;
                        Itterations++;
                        Threads.Clear();
                    }
                    exectime.Stop();
                    ProductIDs = ProductIDs.Distinct().ToList();
                    Threads.Clear();
                    Log.Write($"\nExtracted {ProductIDs.Count} products (ExecTime : {exectime.ElapsedMilliseconds})");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Log.MainWin.TextLinks = ProductIDs.Count.ToString();
                    });
                }
                catch (Exception ex)
                {
                    Log.Write("\nFailed to reach Amazon.");
                    return;
                }
            }
            catch { }
        }
        /// <summary>
        /// Checks the Product ID on CamelCamelCamel.com
        /// </summary>
        /// <param name="ProductID"></param>
        class CamelData
        {
            public string ProductName;
            public string CamelURL;
            public string AmazonURL;

            public float AmazonCurrentPrice;
            public float AmazonAvgPrice;

            public float ThirdPartyCurrentPrice;
            public float ThirdPartyAvgPrice;

        }
        public static void GetCamelData(string ProductID)
        {
            string URL = $"https://camelcamelcamel.com/product/{ProductID}";
            var request = new HttpRequest();
            request.KeepAlive = true;
            request.IgnoreProtocolErrors = true;
            request.ConnectTimeout = 15000;
            request.AllowAutoRedirect = true;
            request.UseCookies = true;
            request.UserAgentRandomize();
            //request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36";
            request.AddHeader("Accept", "*/*");
            request.AddHeader("Upgrade-Insecure-Requests", "1");
            Interlocked.Increment(ref ProxyIndex);
            if (ProxyIndex >= (Proxies.Count))
                ProxyIndex = 0;
            request.Proxy = HttpProxyClient.Parse(Proxies[ProxyIndex]);
            HttpResponse Response;
            string Data = "";
            try
            {
                Response = request.Get(URL);
                Data = Response.ToString();
            }
            catch (Exception ex)
            {
                Log.Write("\nCamel failed to respond");
                return;
            }

            //Check failiure

            bool CloudFlare = Data.Contains("<title>Just a moment...</title>");
            if (CloudFlare)
            {
                Log.Write("\nCloudflare Blocked Access");
                return;
            }

            //Data about amazon
            int start = Data.IndexOf("This is the price charged for New products when Amazon itself is the seller.");
            if (start == -1)
            {
                Log.Write("\nNot enough data about item");
                return;
            }
            if (Data.IndexOf("Sorry, we have no data for this price type.", start) != -1 || Data.IndexOf("The requested product is not yet in our database") > 0)
            {
                Log.Write("\nItem out of stock / Not enough data");
                return;
            }

            //Amazon.com Capture
            int Iterator = 0;
            try
            {
                string Name = "";
                string CamelLink = "";
                string AmazonLink = "";
                string Capture = "";
                Iterator = Data.IndexOf("<td>Current</td>", Iterator) + 16;
                string CurrentPrice = Iterator != 15 ? ParseResponse(Data.Substring(Iterator), 0, "<td>", "</td>") : "N/A";

                Iterator = Data.IndexOf("<td>Highest", Iterator) + 11;
                string HighestPrice = Iterator != 10 ? ParseResponse(Data.Substring(Iterator), 0, "<td>", "</td>") : "N/A";

                Iterator = Data.IndexOf("<td>Lowest", Iterator) + 11;
                string LowestPrice = Iterator != 10 ? ParseResponse(Data.Substring(Iterator), 0, "<td>", "</td>") : "N/A";

                Iterator = Data.IndexOf("<td>Average", Iterator) + 11;
                string AveragePrice = Iterator != 10 ? ParseResponse(Data.Substring(Iterator), 0, "<td>", "</td>") : "N/A";

                Name = ParseResponse(Data, Data.IndexOf("<title>"), "<title>", " | ");
                CamelLink = Response.Address.ToString();
                AmazonLink = $"https://www.amazon.com/dp/{CamelLink.Substring(36)}";

                try
                {
                    if (CurrentPrice != "N/A" && AveragePrice != "N/A" && CurrentPrice != "Not in Stock")
                    {
                        if (float.Parse(CurrentPrice.Substring(1)) < float.Parse(AveragePrice.Substring(1)))
                        {
                            ValidProductIDs.Add(CamelLink);
                            int PercentageOff = (int)((1 - float.Parse(CurrentPrice.Substring(1)) / float.Parse(AveragePrice.Substring(1))) * 100);
                            Capture = $"Current : {CurrentPrice} Average : {AveragePrice} ({PercentageOff}% OFF)";
                            File.AppendAllText(ResultsFile, $"{Name} ({CamelLink} | {AmazonLink})\n\t[{Capture}]\n\n");
                            Log.Write($"\n{Name} [{Capture}]({CamelLink} | {AmazonLink})");
                        }
                    }
                }
                catch (Exception Ex)
                {
                    return;
                }
            }
            catch (Exception Ex)
            {
                return;
            }

            //3rd PArty Capture
            Iterator = 0;
            try
            {
                string Name = "";
                string CamelLink = "";
                string AmazonLink = "";
                string Capture = "";
                Iterator = Data.IndexOf("<td>Current</td>", Iterator) + 16;
                string CurrentPrice = Iterator != 15 ? ParseResponse(Data.Substring(Iterator), 0, "<td>", "</td>") : "N/A";

                Iterator = Data.IndexOf("<td>Highest", Iterator) + 11;
                string HighestPrice = Iterator != 10 ? ParseResponse(Data.Substring(Iterator), 0, "<td>", "</td>") : "N/A";

                Iterator = Data.IndexOf("<td>Lowest", Iterator) + 11;
                string LowestPrice = Iterator != 10 ? ParseResponse(Data.Substring(Iterator), 0, "<td>", "</td>") : "N/A";

                Iterator = Data.IndexOf("<td>Average", Iterator) + 11;
                string AveragePrice = Iterator != 10 ? ParseResponse(Data.Substring(Iterator), 0, "<td>", "</td>") : "N/A";

                Name = ParseResponse(Data, Data.IndexOf("<title>"), "<title>", " | ");
                CamelLink = Response.Address.ToString();
                AmazonLink = $"https://www.amazon.com/dp/{CamelLink.Substring(36)}";

                try
                {
                    if (CurrentPrice != "N/A" && AveragePrice != "N/A" && CurrentPrice != "Not in Stock")
                    {
                        if (float.Parse(CurrentPrice.Substring(1)) < float.Parse(AveragePrice.Substring(1)))
                        {
                            ValidProductIDs.Add(CamelLink);
                            int PercentageOff = (int)((1 - float.Parse(CurrentPrice.Substring(1)) / float.Parse(AveragePrice.Substring(1))) * 100);
                            Capture = $"Current : {CurrentPrice} Average : {AveragePrice} ({PercentageOff}% OFF)";
                            File.AppendAllText(ResultsFile, $"{Name} ({CamelLink} | {AmazonLink})\n\t[{Capture}]\n\n");
                            Log.Write($"\n{Name} [{Capture}]({CamelLink} | {AmazonLink})");
                        }
                    }
                }
                catch (Exception Ex)
                {
                    return;
                }
            }
            catch (Exception Ex)
            {
                return;
            }
        }
        /// <summary>
        /// Function that Loads the threads to call GetCamelData
        /// </summary>
        public static void CheckProducts()
        {
            while (true)
            {
                if (ProductIndex >= (ProductIDs.Count))
                    break;
                Interlocked.Increment(ref ProductIndex);
                try
                {
                    if (InUse_ProductIDs.Contains(ProductIDs[ProductIndex]))
                        continue;
                    InUse_ProductIDs.Add(ProductIDs[ProductIndex]);
                    GetCamelData(ProductIDs[ProductIndex]);
                }
                catch { }
            }
        }
    }
    /// <summary>
    /// Class used to write log data to a Textbox object
    /// </summary>
    class Log
    {
        public static MainWindow MainWin;
        public static void Write(string Message, bool IsError = false)
        {
            string FormatChar = "";
            if (Message[0] >= 9 && Message[0] <= 12) //9 is \t and 12 is \r in ASCII, It covers all formatting characters
            {
                FormatChar = Message[0].ToString();
                Message = Message.Substring(1);
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (IsError)
                    MainWin.TextConsole.AppendText($"{FormatChar}[{DateTime.Now.ToString("hh:mm:ss")}] ERROR : {Message}");
                else MainWin.TextConsole.AppendText($"{FormatChar}[{DateTime.Now.ToString("hh:mm:ss")}] : {Message}");
                MainWin.TextConsole.ScrollToEnd();
            });
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Log.MainWin = ((MainWindow)Application.Current.MainWindow);
            Log.Write("Log Initialized");
        }
        public TextBox TextConsole
        {
            get { return ConsoleText; }
        }

        public string TextLinks
        {
            get { return TextLinksCount.Text; }
            set { TextLinksCount.Text = value; }
        }

        public string ActiveBots
        {
            get { return ActiveThreadsText.Text; }
            set { ActiveThreadsText.Text = value; }
        }

        public string CurrentStatus
        {
            get { return CurrentStatusText.Text; }
            set { CurrentStatusText.Text = value; }
        }

        public string Checked
        {
            get { return CheckedText.Text; }
            set { CheckedText.Text = value; }
        }
        public string ValidProducts
        {
            get { return ValidProductsText.Text; }
            set { ValidProductsText.Text = value; }
        }

        public void ResetBot()
        {
            Worker.ProductIndex = -1;
            Worker.ProxyIndex = -1;
            if (Worker.WorkerThreads.Count > 0)
            {
                foreach (Thread WorkerThread in Worker.WorkerThreads)
                    WorkerThread.Abort();
                Worker.WorkerThreads.Clear();
            }
            ActiveBots = "0";
            StartButton.Content = "Start";
            CurrentStatus = "Stopped";
        }

        private void ProxyButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            string fileName;
            openFileDialog.Title = "Select Proxy List";
            openFileDialog.DefaultExt = "txt";
            openFileDialog.Filter = "Text files|*.txt";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.ShowDialog();
            fileName = openFileDialog.FileName;
            if (File.Exists(fileName))
            {
                Worker.Proxies = new List<string>(File.ReadAllLines(fileName));
                Worker.TotalProxies = Worker.Proxies.Count;
                TextProxiesCount.Text = Worker.TotalProxies.ToString();
            }
        }

        private void NodeIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Regex regex = new Regex("[0-9]");
            if (regex.IsMatch(((TextBox)sender).Text))
                Worker.NodeID = ((TextBox)sender).Text;
            else ((TextBox)sender).Text = "0";
        }
        private void DiscountText_TextChanged(object sender, TextChangedEventArgs e)
        {
            Worker.Discount = ((TextBox)sender).Text;
        }

        private void SaveLinks_Click(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(delegate 
            {
                new Thread(() =>
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    string filePath;
                    saveFileDialog.Title = "Select save Location";
                    saveFileDialog.DefaultExt = "txt";
                    saveFileDialog.AddExtension = true;
                    saveFileDialog.FileName = "Links_" + DateTime.Now.ToString("dddd, dd MMMM yyyy HH-mm-ss") + ".txt";
                    saveFileDialog.RestoreDirectory = true;
                    saveFileDialog.ShowDialog();
                    filePath = saveFileDialog.FileName;
                    foreach (string Product in new List<string>(Worker.ProductIDs))
                        File.AppendAllText(filePath, $"https://amazon.com/dp/{Product}/" + "\n");
                }).Start();
            }).Wait();            
        }

        private void LoadLinks_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            string fileName;
            openFileDialog.Title = "Select Links List";
            openFileDialog.DefaultExt = "txt";
            openFileDialog.Filter = "Text files|*.txt";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.ShowDialog();
            fileName = openFileDialog.FileName;
            if (File.Exists(fileName))
            {
                List<string> Links = new List<string>(File.ReadAllLines(fileName));
                Worker.ProductIDs.Clear();
                string Pattern = "https://amazon.com/dp/";
                foreach (string Link in Links)
                    if (Link.Contains(Pattern))
                        Worker.ProductIDs.Add(Link.Substring(Pattern.Length, Link.Length - 2));

                TextLinksCount.Text = Worker.ProductIDs.Count.ToString();
            }
        }

        private void GetLinks_Click(object sender, RoutedEventArgs e)
        {
            Log.Write("\nScraping Amazon for Links...");
            Worker.ProductIDs.Clear();
            Thread Task = new Thread(Worker.CheckNode);
            Task.IsBackground = false;
            Task.Start();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Content.ToString() == "Start")
            {
                Worker.ResultsFile = "Results\\" + DateTime.Now.ToString("dddd, dd MMMM yyyy HH-mm-ss") + ".txt";
                if (!Directory.Exists("Results"))
                    Directory.CreateDirectory("Results");

                Worker.ProductIndex = -1;
                Worker.ProxyIndex = -1;

                Task.Factory.StartNew(delegate
                {
                    Worker.WorkerThreads.Add(new Thread(Worker.UpdateActiveThreads));
                    Worker.WorkerThreads[0].Name = "Active Bots";
                    Worker.WorkerThreads[0].IsBackground = true;
                    for (int i = 1; i <= Worker.ThreadCount; i++)
                    {
                        Worker.WorkerThreads.Add(new Thread(Worker.CheckProducts));
                        Worker.WorkerThreads[i].Name = "Worker " + i;
                        Worker.WorkerThreads[i].IsBackground = true;
                        Worker.WorkerThreads[i].Start();
                    }
                }).Wait();
                Worker.WorkerThreads[0].Start();
                ((Button)sender).Content = "Pause";
                CurrentStatus = "Working";
            }
            else
            {
                foreach (Thread WorkerThread in Worker.WorkerThreads)
                    WorkerThread.Abort();
                Worker.WorkerThreads.Clear();
                ActiveBots = "0";
                ((Button)sender).Content = "Start";
                CurrentStatus = "Stopped";
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetBot();
        }

        private void BotsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            const int Max = 500;
            int SliderValue = (int)(((Slider)sender).Value * (Max / 10));
            BotsText.Text = SliderValue.ToString();
            Worker.ThreadCount = SliderValue;
        }        
    }
}
