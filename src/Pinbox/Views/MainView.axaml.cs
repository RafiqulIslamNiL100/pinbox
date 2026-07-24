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
using Avalonia.Platform.Storage;
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

    private IBrush? Brush(string key) => this.TryFindResource(key, out var v) ? v as IBrush : null;

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
                Background = isActive ? Brush("SurfaceBrush") : null,
                BorderBrush = isActive ? Brush("BorderBrush2") : null,
                BorderThickness = new Thickness(isActive ? 1 : 0),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            stack.Children.Add(new TextBlock
            {
                Text = page.Name,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = isActive ? Brush("TextBrush") : Brush("TextDimBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            stack.Children.Add(new TextBlock
            {
                Text = page.Items.Count.ToString(),
                FontSize = 10,
                Foreground = Brush("TextDimBrush"),
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

    private void OnSearchFocus(object? sender, GotFocusEventArgs e)
    {
        SearchBoxBorder.BorderBrush = Brush("AccentBrush");
        if (Brush("AccentBrush") is SolidColorBrush accent)
        {
            var c = accent.Color;
            SearchBoxBorder.BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0, OffsetY = 0, Blur = 0, Spread = 3,
                Color = Color.FromArgb(46, c.R, c.G, c.B),
            });
        }
    }

    private void OnSearchUnfocus(object? sender, RoutedEventArgs e)
    {
        SearchBoxBorder.BorderBrush = Brush("BorderBrush2");
        SearchBoxBorder.BoxShadow = default;
    }

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
                Foreground = Brush("TextDimBrush"),
                FontSize = 13,
                Margin = new Thickness(10, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        MsgCount.Text = $"{page.Items.Count} " + (Loc.Lang == "zh" ? "已保存项目" : $"saved item{(page.Items.Count == 1 ? "" : "s")}");
        UpdateBulkBar();
    }

    private TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 10.5,
        FontWeight = FontWeight.Bold,
        Foreground = Brush("TextDimBrush"),
        Margin = new Thickness(8, 10, 8, 4),
    };

    private Border BuildRow(PinboxPage page, PinboxItem item)
    {
        var textBrush = Brush("TextBrush");
        var textDim = Brush("TextDimBrush");
        var textFaint = Brush("TextFaintBrush") ?? textDim;
        var surface2 = Brush("Surface2Brush");
        var surface3 = Brush("Surface3Brush") ?? surface2;
        var accentSoft = Brush("AccentSoftBrush");
        var accentInk = Brush("AccentInkBrush");
        var accent = Brush("AccentBrush");
        var gold = Brush("GoldBrush");
        var mauve = Brush("MauveBrush");

        // compact, single-line row: icon, subject + trimmed preview, then
        // either label chips (at rest) or quick actions (on hover) on the right.
        var root = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 5), Cursor = new Cursor(StandardCursorType.Hand) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };

        // leading control: checkbox (multi-select) or star (normal)
        Control leading;
        if (_multiSelect)
        {
            var isSelected = _selectedIds.Contains(item.Id);
            leading = new Border
            {
                Width = 15, Height = 15, CornerRadius = new CornerRadius(4),
                Background = isSelected ? accentSoft : null,
                BorderBrush = Brush("BorderBrush2"),
                BorderThickness = new Thickness(1.3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = isSelected ? new TextBlock { Text = "✓", FontSize = 9, Foreground = accentInk, HorizontalAlignment = HorizontalAlignment.Center } : null,
            };
        }
        else
        {
            leading = new TextBlock
            {
                Text = item.IsFavorite ? "★" : "☆",
                Foreground = item.IsFavorite ? gold : textFaint,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
        }
        Grid.SetColumn(leading, 0);

        // type icon
        Control typeIcon;
        if (item.Type == ItemType.Picture)
        {
            var img = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(6), ClipToBounds = true, Background = surface3, VerticalAlignment = VerticalAlignment.Center };
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
                img.Child = new TextBlock { Text = "🔒", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }
            typeIcon = img;
        }
        else
        {
            typeIcon = new Border
            {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(6), Background = accentSoft,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "Aa", FontSize = 10, Foreground = accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
        }
        Grid.SetColumn(typeIcon, 1);
        typeIcon.Margin = new Thickness(8, 0, 0, 0);

        // body: subject (bold, natural width) + preview (dim, trimmed to whatever's left) on one line
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        var subjectText = new TextBlock
        {
            Text = item.Subject, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(subjectText, 0);

        var previewSource = item.Type == ItemType.Picture ? (item.ImageFileName ?? "") : item.Text;
        var previewBlock = new TextBlock
        {
            Text = previewSource.Replace('\n', ' '), FontSize = 11.5, Foreground = textFaint,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1, VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(previewBlock, 1);

        body.Children.Add(subjectText);
        body.Children.Add(previewBlock);
        Grid.SetColumn(body, 2);

        // right side: label chips at rest, quick actions on hover (same cell, one visible at a time)
        var metaPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        foreach (var label in item.Labels)
        {
            metaPanel.Children.Add(new Border
            {
                Background = surface3, CornerRadius = new CornerRadius(20), Padding = new Thickness(7, 2),
                Child = new TextBlock { Text = label, FontSize = 9.5, FontWeight = FontWeight.Bold, Foreground = textDim },
            });
        }

        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        var editText = new TextBlock { Text = Loc.T("edit"), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = textDim, Cursor = new Cursor(StandardCursorType.Hand) };
        var dupText = new TextBlock { Text = Loc.T("duplicate"), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = textDim, Cursor = new Cursor(StandardCursorType.Hand) };
        var delText = new TextBlock { Text = Loc.T("delete"), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = mauve, Cursor = new Cursor(StandardCursorType.Hand) };
        if (!_multiSelect)
        {
            actionsPanel.Children.Add(editText);
            actionsPanel.Children.Add(dupText);
            actionsPanel.Children.Add(delText);
        }

        var swap = new Grid();
        swap.Children.Add(metaPanel);
        swap.Children.Add(actionsPanel);
        Grid.SetColumn(swap, 3);

        grid.Children.Add(leading);
        grid.Children.Add(typeIcon);
        grid.Children.Add(body);
        grid.Children.Add(swap);
        root.Child = grid;

        if (_selectedIds.Contains(item.Id)) root.Background = accentSoft;

        root.PointerEntered += (_, _) =>
        {
            if (!_selectedIds.Contains(item.Id)) root.Background = surface2;
            if (!_multiSelect) { metaPanel.IsVisible = false; actionsPanel.IsVisible = true; }
        };
        root.PointerExited += (_, _) =>
        {
            if (!_selectedIds.Contains(item.Id)) root.Background = null;
            metaPanel.IsVisible = true;
            actionsPanel.IsVisible = false;
        };

        root.ContextMenu = BuildItemContextMenu(page, item);

        root.PointerPressed += async (_, e) =>
        {
            if (e.Source is Control c && (c == editText || c == dupText || c == delText)) return;
            // Right-click (and anything but the primary button) only opens the
            // context menu above - it must never trigger a send.
            if (!e.GetCurrentPoint(root).Properties.IsLeftButtonPressed) return;

            if (_multiSelect)
            {
                if (_selectedIds.Contains(item.Id)) _selectedIds.Remove(item.Id);
                else _selectedIds.Add(item.Id);
                RenderItems();
                return;
            }

            if (leading == e.Source)
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

    private ContextMenu BuildItemContextMenu(PinboxPage page, PinboxItem item)
    {
        var menu = new ContextMenu();
        var items = new List<object>();

        var view = new MenuItem { Header = Loc.T("view") };
        view.Click += (_, _) => ViewItem(item);
        var edit = new MenuItem { Header = Loc.T("edit") };
        edit.Click += (_, _) => OpenEditItem(page, item);
        var copy = new MenuItem { Header = Loc.T("copy") };
        copy.Click += async (_, _) => await CopyItemAsync(item);
        var duplicate = new MenuItem { Header = Loc.T("duplicate") };
        duplicate.Click += (_, _) =>
        {
            _pages = PageStore.DuplicateItem(_session!.UserId, page.Id, item.Id);
            RenderItems();
        };
        var moveUp = new MenuItem { Header = Loc.T("move_up") };
        moveUp.Click += (_, _) =>
        {
            _pages = PageStore.ReorderItem(_session!.UserId, page.Id, item.Id, -1);
            RenderItems();
        };
        var moveDown = new MenuItem { Header = Loc.T("move_down") };
        moveDown.Click += (_, _) =>
        {
            _pages = PageStore.ReorderItem(_session!.UserId, page.Id, item.Id, 1);
            RenderItems();
        };
        var delete = new MenuItem { Header = Loc.T("delete") };
        delete.Click += (_, _) =>
        {
            _pages = PageStore.DeleteItem(_session!.UserId, page.Id, item.Id);
            RenderItems();
        };

        items.Add(view);
        items.Add(edit);
        items.Add(copy);
        items.Add(duplicate);
        items.Add(new Separator());
        items.Add(moveUp);
        items.Add(moveDown);
        items.Add(new Separator());
        items.Add(delete);

        menu.ItemsSource = items;
        return menu;
    }

    private void ViewItem(PinboxItem item)
    {
        if (item.Type == ItemType.Picture && item.ImageFileName != null && _session != null)
        {
            try
            {
                var path = PageStore.GetImagePath(_session.UserId, item.ImageFileName);
                if (File.Exists(path))
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch { /* non-fatal - just can't preview right now */ }
        }
        else
        {
            new ViewItemWindow(item.Subject, item.Text).Show(_owner);
        }
    }

    private async Task CopyItemAsync(PinboxItem item)
    {
        if (_owner?.Clipboard is null) return;
        try
        {
            if (item.Type == ItemType.Picture && item.ImageFileName != null && _session != null)
            {
                var path = PageStore.GetImagePath(_session.UserId, item.ImageFileName);
                var topLevel = TopLevel.GetTopLevel(_owner);
                var file = topLevel is not null ? await topLevel.StorageProvider.TryGetFileFromPathAsync(path) : null;
                var data = new DataObject();
                if (file is not null) data.Set(DataFormats.Files, new List<IStorageItem> { file });
                await _owner.Clipboard.SetDataObjectAsync(data);
            }
            else
            {
                await _owner.Clipboard.SetTextAsync(item.Text);
            }
            Flash($"Copied \"{item.Subject}\"", false);
        }
        catch (Exception ex)
        {
            Flash(ex.Message, true);
        }
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
        FlashText.Foreground = isError ? Brush("MauveBrush") : Brush("AccentInkBrush");
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
        RenderPageTabs();
        RenderItems();
    }
}
