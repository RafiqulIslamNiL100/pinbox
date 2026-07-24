using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class MainView : UserControl
{
    public event EventHandler? SignedOut;

    private Window? _owner;
    private AuthSession? _session;
    private List<PinboxPage> _pages = new();
    private string? _currentPageId;
    private bool _multiSelect;
    private bool _revealPictures = true;
    private readonly HashSet<string> _selectedIds = new();
    private System.Threading.CancellationTokenSource? _flashCts;

    public MainView()
    {
        InitializeComponent();
        Loc.LanguageChanged += OnLanguageChanged;
    }

    public void Initialize(Window owner) => _owner = owner;

    public void EnterAs(AuthSession session)
    {
        _session = session;
        _pages = PageStore.ListPages(session.UserId);
        _currentPageId = _pages.FirstOrDefault()?.Id;
        _multiSelect = false;
        _selectedIds.Clear();
        RenderPageTabs();
        RenderItems();
        ApplyLocalization();
    }

    private void OnLanguageChanged()
    {
        ApplyLocalization();
        RenderPageTabs();
        RenderItems();
    }

    private void ApplyLocalization()
    {
        SearchBox.Watermark = Loc.T("search_items");
        AddItemButton.Content = Loc.T("add_item");
        NewPageButton.Text = Loc.T("new_page");
    }

    // ---------------- page tabs ----------------

    private void RenderPageTabs()
    {
        PageTabsPanel.Children.Clear();
        foreach (var page in _pages)
        {
            var isActive = page.Id == _currentPageId;
            var border = new Border
            {
                Padding = new Thickness(10, 6),
                CornerRadius = new CornerRadius(7),
                Background = isActive ? (IBrush?)Application.Current!.Resources["SurfaceBrush"] : null,
                BorderBrush = isActive ? (IBrush?)Application.Current!.Resources["BorderBrush2"] : null,
                BorderThickness = new Thickness(isActive ? 1 : 0),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            stack.Children.Add(new TextBlock
            {
                Text = page.Name,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = isActive ? (IBrush?)Application.Current!.Resources["TextBrush"] : (IBrush?)Application.Current!.Resources["TextDimBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            });
            stack.Children.Add(new TextBlock
            {
                Text = page.Items.Count.ToString(),
                FontSize = 10,
                Foreground = (IBrush?)Application.Current!.Resources["TextDimBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            });
            border.Child = stack;

            var pageId = page.Id;
            border.ContextMenu = BuildPageContextMenu(pageId);
            border.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(border).Properties.IsRightButtonPressed)
                {
                    _currentPageId = pageId;
                    _selectedIds.Clear();
                    RenderPageTabs();
                    RenderItems();
                }
            };

            PageTabsPanel.Children.Add(border);
        }
    }

    private ContextMenu BuildPageContextMenu(string pageId)
    {
        var index = _pages.FindIndex(p => p.Id == pageId);
        var menu = new ContextMenu();
        var items = new List<object>();

        var moveLeft = new MenuItem { Header = "Move left", IsEnabled = index > 0 };
        moveLeft.Click += (_, _) => { _pages = PageStore.ReorderPage(_session!.UserId, pageId, -1); RenderPageTabs(); };
        var moveRight = new MenuItem { Header = "Move right", IsEnabled = index < _pages.Count - 1 };
        moveRight.Click += (_, _) => { _pages = PageStore.ReorderPage(_session!.UserId, pageId, 1); RenderPageTabs(); };
        var rename = new MenuItem { Header = "Rename…" };
        rename.Click += async (_, _) =>
        {
            var dlg = new PromptWindow("Rename page", _pages[index].Name);
            var result = await dlg.ShowDialog<string?>(_owner);
            if (!string.IsNullOrWhiteSpace(result))
            {
                _pages = PageStore.RenamePage(_session!.UserId, pageId, result);
                RenderPageTabs();
            }
        };
        var setHotkey = new MenuItem { Header = "Set page hotkey…" };
        setHotkey.Click += async (_, _) =>
        {
            var dlg = new PromptWindow("Page hotkey (e.g. Ctrl+Alt+1)", _pages[index].Hotkey ?? "");
            var result = await dlg.ShowDialog<string?>(_owner);
            if (result != null)
            {
                _pages = PageStore.SetPageHotkey(_session!.UserId, pageId, string.IsNullOrWhiteSpace(result) ? null : result);
                PageHotkeysChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        var delete = new MenuItem { Header = "Delete page", IsEnabled = _pages.Count > 1 };
        delete.Click += (_, _) =>
        {
            _pages = PageStore.DeletePage(_session!.UserId, pageId);
            if (_currentPageId == pageId) _currentPageId = _pages.FirstOrDefault()?.Id;
            RenderPageTabs();
            RenderItems();
        };

        items.Add(moveLeft);
        items.Add(moveRight);
        items.Add(rename);
        items.Add(setHotkey);
        items.Add(new Separator());
        items.Add(delete);

        menu.ItemsSource = items;
        return menu;
    }

    public event EventHandler? PageHotkeysChanged;

    private async void OnNewPageClick(object? sender, PointerPressedEventArgs e)
    {
        if (_session is null) return;
        var dlg = new PromptWindow("New page name", "");
        var result = await dlg.ShowDialog<string?>(_owner);
        if (!string.IsNullOrWhiteSpace(result))
        {
            _pages = PageStore.AddPage(_session.UserId, result);
            _currentPageId = _pages.Last().Id;
            RenderPageTabs();
            RenderItems();
        }
    }

    public List<PinboxPage> Pages => _pages;

    // ---------------- item list ----------------

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => RenderItems();

    private void RenderItems()
    {
        ItemList.Children.Clear();
        var page = _pages.FirstOrDefault(p => p.Id == _currentPageId);
        if (page is null)
        {
            MsgCount.Text = "0 " + (Loc.Lang == "zh" ? "已保存项目" : "saved items");
            return;
        }

        var query = (SearchBox.Text ?? "").Trim().ToLowerInvariant();
        var filtered = page.Items.Where(i =>
            string.IsNullOrEmpty(query) ||
            i.Subject.ToLowerInvariant().Contains(query) ||
            i.Text.ToLowerInvariant().Contains(query) ||
            i.Labels.Any(l => l.ToLowerInvariant().Contains(query))
        ).ToList();

        var pinned = filtered.Where(i => i.IsFavorite).ToList();
        var rest = filtered.Where(i => !i.IsFavorite).ToList();

        if (pinned.Count > 0)
        {
            ItemList.Children.Add(SectionLabel(Loc.T("pinned")));
            foreach (var item in pinned) ItemList.Children.Add(BuildRow(page, item));
        }
        if (rest.Count > 0 || pinned.Count == 0)
        {
            if (pinned.Count > 0) ItemList.Children.Add(SectionLabel(Loc.T("all_items")));
            foreach (var item in rest) ItemList.Children.Add(BuildRow(page, item));
        }

        if (filtered.Count == 0)
        {
            ItemList.Children.Add(new TextBlock
            {
                Text = Loc.Lang == "zh" ? "还没有保存的项目。" : "No saved items yet.",
                Foreground = (IBrush?)Application.Current!.Resources["TextDimBrush"],
                FontSize = 13,
                Margin = new Thickness(10, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        MsgCount.Text = $"{page.Items.Count} " + (Loc.Lang == "zh" ? "已保存项目" : $"saved item{(page.Items.Count == 1 ? "" : "s")}");
        UpdateBulkBar();
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 10.5,
        FontWeight = FontWeight.Bold,
        Foreground = (IBrush?)Application.Current!.Resources["TextDimBrush"],
        Margin = new Thickness(8, 10, 8, 4),
    };

    private Border BuildRow(PinboxPage page, PinboxItem item)
    {
        var textBrush = (IBrush?)Application.Current!.Resources["TextBrush"];
        var textDim = (IBrush?)Application.Current!.Resources["TextDimBrush"];
        var surface2 = (IBrush?)Application.Current!.Resources["Surface2Brush"];
        var accentSoft = (IBrush?)Application.Current!.Resources["AccentSoftBrush"];
        var accentInk = (IBrush?)Application.Current!.Resources["AccentInkBrush"];
        var gold = (IBrush?)Application.Current!.Resources["GoldBrush"];

        var root = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(8), Cursor = new Cursor(StandardCursorType.Hand) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };

        // leading control: checkbox (multi-select) or star (normal)
        Control leading;
        if (_multiSelect)
        {
            var isSelected = _selectedIds.Contains(item.Id);
            leading = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(4),
                Background = isSelected ? accentSoft : null,
                BorderBrush = (IBrush?)Application.Current!.Resources["BorderBrush2"],
                BorderThickness = new Thickness(1.3),
                Margin = new Thickness(0, 9, 0, 0),
                Child = isSelected ? new TextBlock { Text = "✓", FontSize = 10, Foreground = accentInk, HorizontalAlignment = HorizontalAlignment.Center } : null,
            };
        }
        else
        {
            leading = new TextBlock
            {
                Text = item.IsFavorite ? "★" : "☆",
                Foreground = item.IsFavorite ? gold : textDim,
                FontSize = 13,
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
        }
        Grid.SetColumn(leading, 0);

        // type icon
        Control typeIcon;
        if (item.Type == ItemType.Picture)
        {
            var img = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = surface2 };
            if (_revealPictures && item.ImageFileName != null && _session != null)
            {
                try
                {
                    var path = PageStore.GetImagePath(_session.UserId, item.ImageFileName);
                    if (File.Exists(path))
                    {
                        using var stream = File.OpenRead(path);
                        img.Child = new Image { Source = new Bitmap(stream), Stretch = Stretch.UniformToFill };
                    }
                }
                catch { /* corrupt or missing image - just show the empty placeholder */ }
            }
            else
            {
                img.Child = new TextBlock { Text = "🔒", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }
            typeIcon = img;
        }
        else
        {
            typeIcon = new Border
            {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = accentSoft,
                Child = new TextBlock { Text = "Aa", FontSize = 13, Foreground = accentInk, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
        }
        Grid.SetColumn(typeIcon, 1);
        typeIcon.Margin = new Thickness(9, 0, 0, 0);

        // body
        var body = new StackPanel { Margin = new Thickness(9, 0, 0, 0) };
        var topLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        topLine.Children.Add(new TextBlock { Text = item.Subject, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = textBrush });
        foreach (var label in item.Labels)
        {
            topLine.Children.Add(new Border
            {
                Background = surface2, CornerRadius = new CornerRadius(20), Padding = new Thickness(7, 1),
                Child = new TextBlock { Text = label, FontSize = 9.5, FontWeight = FontWeight.Bold, Foreground = textDim },
            });
        }
        body.Children.Add(topLine);

        var previewText = item.Type == ItemType.Picture ? (item.ImageFileName ?? "") : item.Text;
        body.Children.Add(new TextBlock
        {
            Text = previewText, FontSize = 11.5, Foreground = textDim, FontFamily = new FontFamily("Consolas"),
            TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1,
        });

        if (item.UsageCount > 0)
        {
            body.Children.Add(new TextBlock
            {
                Text = $"Used {item.UsageCount} time{(item.UsageCount == 1 ? "" : "s")}",
                FontSize = 10, Foreground = textDim, Margin = new Thickness(0, 2, 0, 0),
            });
        }
        Grid.SetColumn(body, 2);

        // actions
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, VerticalAlignment = VerticalAlignment.Center, IsVisible = !_multiSelect };
        var editText = new TextBlock { Text = Loc.T("edit"), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = textDim, Cursor = new Cursor(StandardCursorType.Hand) };
        var dupText = new TextBlock { Text = Loc.T("duplicate"), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = textDim, Cursor = new Cursor(StandardCursorType.Hand) };
        var delText = new TextBlock { Text = Loc.T("delete"), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = (IBrush?)Application.Current!.Resources["DangerBrush"], Cursor = new Cursor(StandardCursorType.Hand) };
        actions.Children.Add(editText);
        actions.Children.Add(dupText);
        actions.Children.Add(delText);
        Grid.SetColumn(actions, 3);

        grid.Children.Add(leading);
        grid.Children.Add(typeIcon);
        grid.Children.Add(body);
        grid.Children.Add(actions);
        root.Child = grid;

        if (_selectedIds.Contains(item.Id)) root.Background = accentSoft;

        root.PointerEntered += (_, _) => { if (!_selectedIds.Contains(item.Id)) root.Background = surface2; };
        root.PointerExited += (_, _) => { if (!_selectedIds.Contains(item.Id)) root.Background = null; };

        root.PointerPressed += async (_, e) =>
        {
            if (e.Source is Control c && (c == editText || c == dupText || c == delText)) return;

            if (_multiSelect)
            {
                if (_selectedIds.Contains(item.Id)) _selectedIds.Remove(item.Id);
                else _selectedIds.Add(item.Id);
                RenderItems();
                return;
            }

            if (e.GetCurrentPoint(root).Properties.IsLeftButtonPressed && leading == e.Source)
            {
                _pages = PageStore.ToggleFavorite(_session!.UserId, page.Id, item.Id);
                RenderItems();
                return;
            }

            await SendItemAsync(page, item);
        };

        editText.PointerPressed += (_, e) => { e.Handled = true; OpenEditItem(page, item); };
        dupText.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _pages = PageStore.DuplicateItem(_session!.UserId, page.Id, item.Id);
            RenderItems();
        };
        delText.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _pages = PageStore.DeleteItem(_session!.UserId, page.Id, item.Id);
            RenderItems();
        };

        return root;
    }

    private async Task SendItemAsync(PinboxPage page, PinboxItem item)
    {
        if (_owner is null || _session is null) return;
        try
        {
            string payload;
            if (item.Type == ItemType.Picture && item.ImageFileName != null)
            {
                // Pictures are sent by putting the image file itself on the
                // clipboard as a bitmap where possible; fall back to copying
                // the file path as text if that fails.
                var path = PageStore.GetImagePath(_session.UserId, item.ImageFileName);
                await PasteService.SendImageAsync(_owner, path);
            }
            else
            {
                await PasteService.SendTextAsync(_owner, item.Text);
            }

            _pages = PageStore.RecordUsage(_session.UserId, page.Id, item.Id);
            Flash($"Sent \"{item.Subject}\"", false);

            var settings = AppSettingsService.Load();
            if (settings.NotificationsEnabled)
                ToastService.Show("Pinbox", $"Sent \"{item.Subject}\" to the active window");

            RenderItems();
        }
        catch (Exception ex)
        {
            Flash(ex.Message, true);
        }
    }

    private void Flash(string text, bool isError)
    {
        _flashCts?.Cancel();
        _flashCts = new System.Threading.CancellationTokenSource();
        var token = _flashCts.Token;

        FlashText.Text = text;
        FlashText.Foreground = isError
            ? (IBrush?)Application.Current!.Resources["DangerBrush"]
            : (IBrush?)Application.Current!.Resources["AccentInkBrush"];
        FlashToast.IsVisible = true;

        _ = Task.Delay(2500, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            Dispatcher.UIThread.Post(() => FlashToast.IsVisible = false);
        });
    }

    // ---------------- multi-select / bulk ----------------

    private void OnMultiSelectToggle(object? sender, RoutedEventArgs e)
    {
        _multiSelect = MultiSelectToggle.IsChecked == true;
        _selectedIds.Clear();
        RenderItems();
    }

    private void UpdateBulkBar()
    {
        BulkBar.IsVisible = _multiSelect && _selectedIds.Count > 0;
        BulkCountText.Text = $"{_selectedIds.Count} selected";
    }

    private void OnBulkDeleteClick(object? sender, PointerPressedEventArgs e)
    {
        if (_session is null || _currentPageId is null) return;
        foreach (var id in _selectedIds.ToList())
            _pages = PageStore.DeleteItem(_session.UserId, _currentPageId, id);
        _selectedIds.Clear();
        RenderItems();
    }

    // ---------------- add / edit ----------------

    private async void OnAddItemClick(object? sender, RoutedEventArgs e) => await OpenAddItemAsync();

    private async Task OpenAddItemAsync()
    {
        if (_session is null || _currentPageId is null) return;
        var dlg = new AddEditItemWindow(_session, _currentPageId, null);
        var changed = await dlg.ShowDialog<bool>(_owner);
        if (changed)
        {
            _pages = PageStore.ListPages(_session.UserId);
            RenderItems();
        }
    }

    private async void OpenEditItem(PinboxPage page, PinboxItem item)
    {
        if (_session is null) return;
        var dlg = new AddEditItemWindow(_session, page.Id, item);
        var changed = await dlg.ShowDialog<bool>(_owner);
        if (changed)
        {
            _pages = PageStore.ListPages(_session.UserId);
            RenderItems();
        }
    }

    // ---------------- bottom nav ----------------

    private void OnOpenFolderClick(object? sender, PointerPressedEventArgs e)
    {
        if (_session is null) return;
        try
        {
            var dir = PageStore.DataRootForExport(_session.UserId);
            System.IO.Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch { /* non-fatal */ }
    }

    private void OnToggleRevealClick(object? sender, PointerPressedEventArgs e)
    {
        _revealPictures = !_revealPictures;
        EyeButton.Text = _revealPictures ? "👁" : "🚫";
        RenderItems();
    }

    private async void OnSettingsClick(object? sender, PointerPressedEventArgs e)
    {
        if (_session is null) return;
        var dlg = new SettingsWindow(_session);
        dlg.SignOutRequested += (_, _) => SignedOut?.Invoke(this, EventArgs.Empty);
        dlg.HotkeysChanged += (_, _) => PageHotkeysChanged?.Invoke(this, EventArgs.Empty);
        await dlg.ShowDialog(_owner);
        RenderItems();
    }
}
