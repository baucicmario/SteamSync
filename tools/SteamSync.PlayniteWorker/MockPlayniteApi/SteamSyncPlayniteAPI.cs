using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SteamSync.PlayniteAdapter.MockPlayniteApi
{
    public class SteamSyncPlayniteAPI : IPlayniteAPI
    {
        public SteamSyncPlayniteAPI()
        {
            WebViews = new MockWebViewFactory();
            Database = new MockGameDatabaseAPI();
        }

        public IMainViewAPI MainView => throw new NotImplementedException();
        public IGameDatabaseAPI Database { get; }
        public IDialogsFactory Dialogs => throw new NotImplementedException();
        public IPlaynitePathsAPI Paths => throw new NotImplementedException();
        public INotificationsAPI Notifications => new MockNotificationsAPI();
        public IPlayniteInfoAPI ApplicationInfo => new MockApplicationInfo();
        public IWebViewFactory WebViews { get; }
        public IResourceProvider Resources => throw new NotImplementedException();
        public IUriHandlerAPI UriHandler => throw new NotImplementedException();
        public IPlayniteSettingsAPI ApplicationSettings => throw new NotImplementedException();
        public IAddons Addons => throw new NotImplementedException();
        public IEmulationAPI Emulation => throw new NotImplementedException();

        public void AddConvertersSupport(Plugin source, AddConvertersSupportArgs args) {}
        public void AddCustomElementSupport(Plugin source, AddCustomElementSupportArgs args) {}
        public void AddSettingsSupport(Plugin source, AddSettingsSupportArgs args) {}
        public string ExpandGameVariables(Game game, string inputString) => inputString;
        public string ExpandGameVariables(Game game, string inputString, string emulatorDir) => inputString;
        public GameAction ExpandGameVariables(Game game, GameAction action) => action;
        public void InstallGame(Guid gameId) {}
        public void StartGame(Guid gameId) {}
        public void UninstallGame(Guid gameId) {}
    }

    public class MockWebViewFactory : IWebViewFactory
    {
        public IWebView CreateView(int width, int height) => new AuthWebView();
        public IWebView CreateView(int width, int height, System.Windows.Media.Color windowColor) => new AuthWebView();
        public IWebView CreateView(WebViewSettings settings) => new AuthWebView();
        public IWebView CreateOffscreenView() => new AuthWebView();
        public IWebView CreateOffscreenView(WebViewSettings settings) => new AuthWebView();
    }
    
    public class MockNotificationsAPI : INotificationsAPI
    {
        public void Add(NotificationMessage message) {}
        public void Add(string id, string text, NotificationType type) {}
        public void Remove(string id) {}
        public void RemoveAll() {}
        public void Clear() {}
        public event EventHandler<NotificationMessage> MessageAdded;
        public event EventHandler<NotificationMessage> MessageRemoved;
        public event EventHandler MessagesCleared;
        public ObservableCollection<NotificationMessage> Messages => new ObservableCollection<NotificationMessage>();
        public int Count => 0;
    }
    
    public class MockApplicationInfo : IPlayniteInfoAPI
    {
        public Version ApplicationVersion => new Version(6, 14, 0);
        public ApplicationMode Mode => ApplicationMode.Desktop;
        public bool ThrowAllErrors => false;
        public bool IsPortable => false;
        public bool InOfflineMode => false;
        public bool IsDebugBuild => false;
    }
}
