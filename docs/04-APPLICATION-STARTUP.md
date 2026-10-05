# 04 — Application startup

## Startup chain

Start at src/SteamSync.UI/Program.cs.

<pre>
Program.Main(args)
  -> BuildAvaloniaApp()
  -> StartWithClassicDesktopLifetime(args)
  -> App.OnFrameworkInitializationCompleted()
  -> new MainWindow { DataContext = new MainViewModel() }
</pre>

## Step 1 — Program.cs

Main is the real process entry point. It is marked [STAThread].

BuildAvaloniaApp configures:

- Avalonia Application
- platform detection
- developer tools in DEBUG
- Inter font
- trace logging

There is no framework DI container here.

## Step 2 — App.axaml.cs

Initialize loads the XAML application resources.

OnFrameworkInitializationCompleted creates MainWindow and gives it a MainViewModel.

That is the handoff from the UI framework into application composition.

## Step 3 — MainViewModel

src/SteamSync.UI/ViewModels/MainViewModel.cs is one of the most important files.

Its constructor is effectively the composition root. It creates:

1. SyncLogger
2. GameDetectionService
3. SteamInjectorService
4. SteamSyncDbContext
5. GameRepository
6. AppSettings from JSON
7. SteamGridDbClient
8. ArtworkManager
9. UninstalledImageProcessor
10. GameListViewModel
11. SettingsViewModel

It then sets CurrentPage to GameListViewModel.

## Why the logger has a dispatcher callback

The logger can be called by core services during background work. The callback uses Avalonia's UI dispatcher to add lines to the UI-bound ObservableCollection.

That keeps the UI collection on the UI thread.

## View selection

MainWindow binds a ContentControl to CurrentPage.

ViewLocator maps ViewModel types to View types by convention, for example:

- MainViewModel → MainWindow
- GameListViewModel → GameListView
- SettingsViewModel → SettingsView

This is navigation by changing the current ViewModel rather than manually loading XAML pages.

## Interview wording

A good explanation is:

> The application has a manual composition root in MainViewModel. It constructs the core services and passes them into GameListViewModel. Avalonia then binds those ViewModels to XAML views.

Avoid saying that the repository uses a full dependency-injection framework.

## Checkpoint

Trace SyncLogger from construction to the log panel in GameListView.axaml.
