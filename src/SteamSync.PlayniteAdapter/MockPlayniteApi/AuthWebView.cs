using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Playnite.SDK;
using Playnite.SDK.Events;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Windows.Forms;
using System.Threading;

namespace SteamSync.PlayniteAdapter.MockPlayniteApi
{
    public class AuthWebView : IWebView
    {
        private Form _form;
        private WebView2 _webView;
        private ManualResetEvent _navEvent = new ManualResetEvent(false);

        public bool CanExecuteJavascriptInMainFrame => true;
        public Window WindowHost => null;
        public event EventHandler<WebViewLoadingChangedEventArgs> LoadingChanged;

        public AuthWebView()
        {
            _form = new Form { Width = 800, Height = 600, StartPosition = FormStartPosition.CenterScreen, Text = "Web Auth" };
            _webView = new WebView2 { Dock = DockStyle.Fill };
            _form.Controls.Add(_webView);
            
            _form.Load += async (s, e) => 
            {
                await _webView.EnsureCoreWebView2Async(null);
                _webView.NavigationCompleted += (ss, ee) => 
                {
                    _navEvent.Set();
                    LoadingChanged?.Invoke(this, new WebViewLoadingChangedEventArgs { IsLoading = false });
                };
            };
        }

        public void Open()
        {
            _form.Show();
        }

        public bool? OpenDialog()
        {
            return _form.ShowDialog() == DialogResult.OK;
        }

        public void Navigate(string url)
        {
            _webView.Source = new Uri(url);
        }

        public void NavigateAndWait(string url)
        {
            if (_form.InvokeRequired)
            {
                _form.Invoke(new Action(() => NavigateAndWaitInternal(url)));
            }
            else
            {
                NavigateAndWaitInternal(url);
            }
        }
        
        private void NavigateAndWaitInternal(string url)
        {
            _navEvent.Reset();
            _webView.Source = new Uri(url);
            while (!_navEvent.WaitOne(50))
            {
                System.Windows.Forms.Application.DoEvents();
            }
        }

        public string GetPageText() => GetPageTextAsync().GetAwaiter().GetResult();
        public async Task<string> GetPageTextAsync() => await _webView.ExecuteScriptAsync("document.body.innerText;");
        
        public string GetPageSource() => GetPageSourceAsync().GetAwaiter().GetResult();
        public async Task<string> GetPageSourceAsync() => await _webView.ExecuteScriptAsync("document.documentElement.outerHTML;");
        
        public string GetCurrentAddress() => _webView.Source?.ToString() ?? "";

        public void DeleteDomainCookies(string domain) {}
        public void DeleteDomainCookiesRegex(string domainRegex) {}
        public void DeleteCookies(string url, string name) {}
        public List<HttpCookie> GetCookies() => new List<HttpCookie>();
        public void SetCookies(string url, string domain, string name, string value, string path, DateTime expires) {}
        public void SetCookies(string url, HttpCookie cookie) {}

        public void Close()
        {
            if (_form.InvokeRequired) _form.Invoke(new Action(() => _form.Close()));
            else _form.Close();
        }

        public async Task<JavaScriptEvaluationResult> EvaluateScriptAsync(string script)
        {
            var res = await _webView.ExecuteScriptAsync(script);
            return new JavaScriptEvaluationResult { Success = true, Result = res };
        }

        public void Dispose()
        {
            _webView?.Dispose();
            _form?.Dispose();
        }
    }
}
