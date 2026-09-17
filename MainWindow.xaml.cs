using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TwentyFourAqSSTool.Models;
using TwentyFourAqSSTool.Services;

namespace TwentyFourAqSSTool;

public partial class MainWindow : Window
{
    private readonly ToolService _service = new();
    private List<ToolEntry> _tools = new();
    private string _currentCategory = "OrbDiff";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _tools = await _service.LoadToolsAsync();
            Log("24aq SS Tools initialized.");
            Log($"Loaded {_tools.Count} tool entries.");
            RenderCategory(_currentCategory);
        }
        catch (Exception ex)
        {
            SetStatus("Initialization error", "ERROR");
            Log("ERROR: " + ex.Message);
        }
    }

    private void CategoryTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CategoryTabs.SelectedItem is not TabItem tab)
            return;

        _currentCategory = tab.Header?.ToString() ?? "Others";
        SearchBox.Text = "";
        RenderCategory(_currentCategory);
        Log($"Category switched -> {_currentCategory}");
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RenderCategory(_currentCategory, SearchBox.Text);
    }

    private void RenderCategory(string category, string? filter = null)
    {
        ToolsPanel.Children.Clear();

        IEnumerable<ToolEntry> entries = _tools.Where(x =>
            x.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter))
        {
            entries = entries.Where(x =>
                x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var list = entries.ToList();

        if (list.Count == 0)
        {
            ToolsPanel.Children.Add(new TextBlock
            {
                Text = "No matching tools found.",
                Foreground = new SolidColorBrush(Color.FromRgb(82, 115, 93)),
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(14)
            });
            return;
        }

        foreach (var tool in list)
            ToolsPanel.Children.Add(BuildCard(tool));
    }

    private Border BuildCard(ToolEntry tool)
    {
        var accent = Color.FromRgb(57, 255, 136);
        var cardBg = new SolidColorBrush(Color.FromRgb(12, 21, 15));
        var cardHover = new SolidColorBrush(Color.FromRgb(15, 34, 22));
        var borderNormal = new SolidColorBrush(Color.FromRgb(29, 70, 41));
        var borderHover = new SolidColorBrush(accent);

        var name = new TextBlock
        {
            Text = tool.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(233, 255, 240))
        };

        var desc = new TextBlock
        {
            Text = tool.Description,
            Foreground = new SolidColorBrush(Color.FromRgb(94, 135, 108)),
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 10, 0),
            MaxWidth = 230
        };

        var badgeBg = tool.SourceType.ToUpperInvariant() switch
        {
            "PS1" => Color.FromRgb(20, 64, 39),
            "WEB" => Color.FromRgb(14, 76, 40),
            "LINK" => Color.FromRgb(37, 66, 43),
            _ => Color.FromRgb(21, 79, 43)
        };

        var badge = new Border
        {
            Background = new SolidColorBrush(badgeBg),
            BorderBrush = new SolidColorBrush(Color.FromRgb(41, 118, 65)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3, 7, 3),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = tool.SourceType.ToUpperInvariant(),
                Foreground = new SolidColorBrush(Color.FromRgb(85, 255, 151)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                FontWeight = FontWeights.Bold
            }
        };

        var textStack = new StackPanel();
        textStack.Children.Add(name);
        textStack.Children.Add(desc);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(textStack);
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);

        var card = new Border
        {
            Width = 300,
            Height = 68,
            Margin = new Thickness(6),
            Padding = new Thickness(14, 10, 10, 10),
            CornerRadius = new CornerRadius(8),
            Background = cardBg,
            BorderBrush = borderNormal,
            BorderThickness = new Thickness(1),
            Child = grid,
            Cursor = Cursors.Hand,
            Tag = tool
        };

        card.MouseEnter += (_, _) =>
        {
            card.Background = cardHover;
            card.BorderBrush = borderHover;
        };

        card.MouseLeave += (_, _) =>
        {
            card.Background = cardBg;
            card.BorderBrush = borderNormal;
        };

        card.MouseLeftButtonUp += ToolCard_Click;
        return card;
    }

    private async void ToolCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border card || card.Tag is not ToolEntry tool)
            return;

        try
        {
            var localPath = _service.GetToolPath(tool);

            if (File.Exists(localPath))
            {
                SetStatus($"Launching {tool.Name}", "RUNNING");
                Log($"launch -> {tool.Name}");
                _service.Launch(tool);
                SetStatus("Ready", "IDLE");
                return;
            }

            if (_service.HasDownload(tool))
            {
                SetStatus($"Downloading {tool.Name}", "BUSY");
                Log($"download -> {tool.Name}");
                Log($"source   -> {tool.DownloadUrl}");

                var progress = new Progress<double>(p =>
                    StatusSubtitle.Text = $"{tool.Name}: {p:P0}");

                var path = await _service.DownloadAsync(tool, progress);
                Log($"saved    -> {path}");

                if (File.Exists(_service.GetToolPath(tool)))
                {
                    Log($"launch -> {tool.Name}");
                    _service.Launch(tool);
                }
                else
                {
                    Log($"downloaded, but no launchable file was found -> {tool.Name}");
                    _service.OpenToolFolder(tool);
                }

                SetStatus("Ready", "IDLE");
                return;
            }

            if (_service.HasHomePage(tool))
            {
                Log($"open web -> {tool.Name}");
                _service.OpenHomePage(tool);
                return;
            }

            MessageBox.Show(
                $"{tool.Name} is listed, but no official URL is configured yet.\n\n" +
                "Open tools.json and set downloadUrl or homePage.",
                "24aq SS Tools",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SetStatus("Error", "ERROR");
            Log("ERROR: " + ex.Message);

            if (_service.HasHomePage(tool))
            {
                var result = MessageBox.Show(
                    ex.Message + "\n\nOpen the GitHub page instead?",
                    "24aq SS Tools",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                    _service.OpenHomePage(tool);
            }
            else
            {
                MessageBox.Show(ex.Message, "24aq SS Tools",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SetStatus(string title, string badge)
    {
        StatusTitle.Text = title;
        StatusBadge.Text = badge;

        StatusSubtitle.Text = badge == "IDLE"
            ? "Select a tool to launch or download it."
            : badge == "ERROR"
                ? "Check the activity console for details."
                : "Operation in progress...";
    }

    private void Log(string message)
    {
        ConsoleBox.AppendText(
            $"[{DateTime.Now:HH:mm:ss}]  {message}{Environment.NewLine}");
        ConsoleBox.ScrollToEnd();
    }

    private void OpenInstall_Click(object sender, RoutedEventArgs e)
    {
        _service.OpenInstallFolder();
        Log("opened install folder");
    }

    private void OpenCmd_Click(object sender, RoutedEventArgs e)
    {
        _service.OpenCmd();
        Log("opened cmd in install folder");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Delete all files downloaded by this launcher?",
                "24aq SS Tools",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _service.ClearDownloadedFiles();
        Log("downloaded tool files cleared");
    }
}