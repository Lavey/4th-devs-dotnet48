using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FourthDevs.Browser.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;

namespace FourthDevs.Browser.Tools
{
    internal static class BrowserTools
    {
        public static List<LocalToolDefinition> CreateBrowserTools()
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "navigate",
                    Description = "Navigate to a URL. Returns page title, final URL, and a preview of the page text.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            url = new { type = "string", description = "URL to navigate to" }
                        },
                        required = new[] { "url" }
                    }),
                    Handler = async (args) =>
                    {
                        string url = args["url"]?.ToString();
                        if (string.IsNullOrEmpty(url))
                            return JsonConvert.SerializeObject(new { error = "url is required" });

                        try
                        {
                            var driver = Browser.BrowserManager.GetDriver();
                            driver.Navigate().GoToUrl(url);
                            await Task.Delay(1500);

                            string title = driver.Title;
                            string finalUrl = driver.Url;
                            string bodyText = (string)((IJavaScriptExecutor)driver)
                                .ExecuteScript("return document.body ? document.body.innerText : ''");
                            string preview = bodyText != null && bodyText.Length > 500
                                ? bodyText.Substring(0, 500)
                                : bodyText ?? string.Empty;

                            return JsonConvert.SerializeObject(new
                            {
                                title,
                                url = finalUrl,
                                status = "ok",
                                textPreview = preview
                            });
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                },

                new LocalToolDefinition
                {
                    Name = "evaluate",
                    Description = "Execute JavaScript in the browser. PREFERRED for data extraction. Returns result as a string.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            code = new { type = "string", description = "JavaScript code to execute" }
                        },
                        required = new[] { "code" }
                    }),
                    Handler = async (args) =>
                    {
                        string code = args["code"]?.ToString();
                        if (string.IsNullOrEmpty(code))
                            return JsonConvert.SerializeObject(new { error = "code is required" });

                        await Task.CompletedTask;
                        try
                        {
                            var driver = Browser.BrowserManager.GetDriver();
                            var result = ((IJavaScriptExecutor)driver).ExecuteScript(code);
                            return SerializeJsResult(result);
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                },

                new LocalToolDefinition
                {
                    Name = "click",
                    Description = "Click an element by CSS selector or visible text. Returns page state after click.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            selector = new { type = "string", description = "CSS selector or visible text of the element to click" }
                        },
                        required = new[] { "selector" }
                    }),
                    Handler = async (args) =>
                    {
                        string selector = args["selector"]?.ToString();
                        if (string.IsNullOrEmpty(selector))
                            return JsonConvert.SerializeObject(new { error = "selector is required" });

                        await Task.CompletedTask;
                        try
                        {
                            var driver = Browser.BrowserManager.GetDriver();
                            IWebElement element = FindElement(driver, selector);
                            element.Click();
                            System.Threading.Thread.Sleep(1000);

                            return JsonConvert.SerializeObject(new
                            {
                                title = driver.Title,
                                url = driver.Url,
                                status = "clicked"
                            });
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                },

                new LocalToolDefinition
                {
                    Name = "type_text",
                    Description = "Clear and type text into an input element by CSS selector. Optionally press Enter.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            selector = new { type = "string", description = "CSS selector of the input element" },
                            text = new { type = "string", description = "Text to type" },
                            pressEnter = new { type = "boolean", description = "Whether to press Enter after typing" }
                        },
                        required = new[] { "selector", "text" }
                    }),
                    Handler = async (args) =>
                    {
                        string selector = args["selector"]?.ToString();
                        string text = args["text"]?.ToString() ?? string.Empty;
                        bool pressEnter = args["pressEnter"]?.Value<bool>() ?? false;

                        if (string.IsNullOrEmpty(selector))
                            return JsonConvert.SerializeObject(new { error = "selector is required" });

                        await Task.CompletedTask;
                        try
                        {
                            var driver = Browser.BrowserManager.GetDriver();
                            IWebElement element = FindElement(driver, selector);
                            element.Clear();
                            element.SendKeys(text);
                            if (pressEnter)
                            {
                                element.SendKeys(Keys.Return);
                                System.Threading.Thread.Sleep(1500);
                            }

                            return JsonConvert.SerializeObject(new
                            {
                                title = driver.Title,
                                url = driver.Url,
                                status = "typed"
                            });
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                },

                new LocalToolDefinition
                {
                    Name = "take_screenshot",
                    Description = "Take a screenshot of the current browser viewport and save it to disk.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "Optional filename (without extension)" }
                        },
                        required = new string[0]
                    }),
                    Handler = async (args) =>
                    {
                        string name = args["name"]?.ToString();
                        await Task.CompletedTask;
                        try
                        {
                            string path = Browser.BrowserManager.TakeScreenshot(name);
                            return JsonConvert.SerializeObject(new
                            {
                                path,
                                message = "Screenshot saved to " + path
                            });
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                },

                new LocalToolDefinition
                {
                    Name = "get_page_text",
                    Description = "Get the full text content of the current page. Use when you need to see the full page content.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[0]
                    }),
                    Handler = async (args) =>
                    {
                        await Task.CompletedTask;
                        try
                        {
                            var driver = Browser.BrowserManager.GetDriver();
                            string text = (string)((IJavaScriptExecutor)driver)
                                .ExecuteScript("return document.body ? document.body.innerText : ''");
                            return text ?? string.Empty;
                        }
                        catch (Exception ex)
                        {
                            return JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }
                }
            };
        }

        private static IWebElement FindElement(IWebDriver driver, string selector)
        {
            // Try CSS selector first
            try
            {
                return driver.FindElement(By.CssSelector(selector));
            }
            catch (NoSuchElementException) { }

            // Try by link text
            try
            {
                return driver.FindElement(By.LinkText(selector));
            }
            catch (NoSuchElementException) { }

            // Try by partial link text
            try
            {
                return driver.FindElement(By.PartialLinkText(selector));
            }
            catch (NoSuchElementException) { }

            // Try by XPath containing text
            return driver.FindElement(By.XPath($"//*[contains(text(),'{selector}')]"));
        }

        private static string SerializeJsResult(object result)
        {
            if (result == null) return "null";
            if (result is string s) return s;
            if (result is bool b) return b.ToString().ToLower();
            if (result is long l) return l.ToString();
            if (result is double d) return d.ToString();
            if (result is ReadOnlyCollection<object> list)
                return JsonConvert.SerializeObject(list);
            return JsonConvert.SerializeObject(result);
        }
    }
}
