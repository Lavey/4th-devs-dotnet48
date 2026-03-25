using System;
using System.IO;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Newtonsoft.Json;

namespace FourthDevs.Browser.Browser
{
    public static class BrowserManager
    {
        private static IWebDriver _driver;

        public static string GetProfileDir()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "data", "chrome-profile");
        }

        public static bool SessionExists()
        {
            string dir = GetProfileDir();
            return Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0;
        }

        public static void Launch(bool headless)
        {
            if (_driver != null) return;

            var opts = new ChromeOptions();
            string profileDir = GetProfileDir();
            Directory.CreateDirectory(profileDir);

            opts.AddArgument($"--user-data-dir={profileDir}");
            opts.AddArgument("--disable-blink-features=AutomationControlled");
            opts.AddExcludedArgument("enable-automation");

            if (headless)
            {
                opts.AddArgument("--headless=new");
                opts.AddArgument("--window-size=1280,800");
                opts.AddArgument("--no-sandbox");
                opts.AddArgument("--disable-dev-shm-usage");
            }
            else
            {
                opts.AddArgument("--start-maximized");
            }

            _driver = new ChromeDriver(opts);
        }

        public static IWebDriver GetDriver()
        {
            if (_driver == null) throw new InvalidOperationException("Browser not launched.");
            return _driver;
        }

        public static void Close()
        {
            if (_driver == null) return;
            try { _driver.Quit(); } catch { }
            _driver = null;
        }

        public static string TakeScreenshot(string name = null)
        {
            string screenshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "screenshots");
            Directory.CreateDirectory(screenshotsDir);
            string filename = (name ?? $"screenshot-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}") + ".png";
            string filepath = Path.Combine(screenshotsDir, filename);
            var ss = ((ITakesScreenshot)_driver).GetScreenshot();
            ss.SaveAsFile(filepath);
            return filepath;
        }

        public static string Navigate(string url)
        {
            _driver.Navigate().GoToUrl(url);
            Thread.Sleep(1500);
            return _driver.Title;
        }

        public static string ExecuteScript(string code)
        {
            var result = ((IJavaScriptExecutor)_driver).ExecuteScript(code);
            if (result == null) return "null";
            if (result is string s) return s;
            return JsonConvert.SerializeObject(result);
        }
    }
}
