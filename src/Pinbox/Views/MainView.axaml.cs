using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class MainView : UserControl
{
    public event EventHandler? SignedOut;

    private Window? _owner;
    private PublicUser? _user;
    private DispatcherTimer? _flashTimer;

    public MainView()
    {
        InitializeComponent();
    }

    public void Initialize(Window owner)
    {
        _owner = owner;
    }

    public void EnterAs(PublicUser user)
    {
        _user = user;
        AcctInitial.Text = string.IsNullOrEmpty(user.Name) ? "?" : user.Name[..1].ToUpperInvariant();
        AcctName.Text = user.Name;
        Refresh();
    }

    private void Refresh()
    {
        if (_user is null) return;
        var messages = MessageStore.List(_user.Id);

        MessageList.Children.Clear();

        if (messages.Count == 0)
        {
            MessageList.Children.Add(new TextBlock
            {
                Text = "No saved messages yet — write one above and click Save.",
                Foreground = (IBrush?)Application.Current!.Resources["TextDimBrush"],
                FontSize = 13,
                Margin = new Thickness(10, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        for (int i = 0; i < messages.Count; i++)
        {
            MessageList.Children.Add(BuildRow(messages[i], i + 1));
        }

        MsgCount.Text = $"{messages.Count} saved message{(messages.Count == 1 ? "" : "s")}";
    }

    private Border BuildRow(SavedMessage message, int serial)
    {
        var accent = (IBrush?)Application.Current!.Resources["AccentBrush"];
        var accentInk = (IBrush?)Application.Current!.Resources["AccentInkBrush"];
        var accentSoft = (IBrush?)Application.Current!.Resources["AccentSoftBrush"];
        var textBrush = (IBrush?)Application.Current!.Resources["TextBrush"];
        var textDim = (IBrush?)Application.Current!.Resources["TextDimBrush"];
        var surface2 = (IBrush?)Application.Current!.Resources["Surface2Brush"];

        var root = new Border
        {
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 10),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        var serialBadge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(6),
            Background = surface2,
            Child = new TextBlock
            {
                Text = serial.ToString(),
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Foreground = textDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Margin = new Thickness(0, 0, 12, 0),
        };
        Grid.SetColumn(serialBadge, 0);

        // ---- view mode ----
        var textBlock = new TextBlock
        {
            Text = message.Text,
            Foreground = textBrush,
            FontSize = 13.5,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(textBlock, 1);

        var editIcon = MakeTextButton("Edit", textDim!);
        var delIcon = MakeTextButton("Delete", textDim!);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        actions.Children.Add(editIcon);
        actions.Children.Add(delIcon);
        Grid.SetColumn(actions, 2);

        // ---- edit mode (hidden until Edit clicked) ----
        var editBox = new TextBox { Text = message.Text, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(editBox, 1);
        var saveIcon = MakeTextButton("Save", accent!);
        var cancelIcon = MakeTextButton("Cancel", textDim!);
        saveIcon.IsVisible = false;
        cancelIcon.IsVisible = false;
        var editActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        editActions.Children.Add(saveIcon);
        editActions.Children.Add(cancelIcon);
        Grid.SetColumn(editActions, 2);

        // ---- delete-confirm mode (hidden until delete clicked) ----
        var confirmText = new TextBlock { Text = "Delete this message?", Foreground = textBrush, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        Grid.SetColumn(confirmText, 1);
        var confirmYes = MakeTextButton("Delete", (IBrush)Application.Current!.Resources["DangerBrush"]!);
        var confirmNo = MakeTextButton("Cancel", textDim!);
        confirmYes.IsVisible = false;
        confirmNo.IsVisible = false;
        var confirmActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        confirmActions.Children.Add(confirmYes);
        confirmActions.Children.Add(confirmNo);
        Grid.SetColumn(confirmActions, 2);

        grid.Children.Add(serialBadge);
        grid.Children.Add(textBlock);
        grid.Children.Add(actions);
        grid.Children.Add(editBox);
        grid.Children.Add(editActions);
        grid.Children.Add(confirmText);
        grid.Children.Add(confirmActions);

        root.Child = grid;

        void SetMode(string mode)
        {
            textBlock.IsVisible = mode == "view";
            actions.IsVisible = mode == "view";
            editBox.IsVisible = mode == "edit";
            editActions.IsVisible = mode == "edit";
            confirmText.IsVisible = mode == "delete";
            confirmActions.IsVisible = mode == "delete";
            root.Background = mode == "view" ? null : surface2;
        }

        root.PointerEntered += (_, _) => { if (textBlock.IsVisible) root.Background = surface2; };
        root.PointerExited += (_, _) => { if (textBlock.IsVisible) root.Background = null; };

        root.PointerPressed += async (_, e) =>
        {
            if (!textBlock.IsVisible) return; // only send in view mode
            await SendMessageAsync(message.Text, serial);
        };

        editIcon.PointerPressed += (_, e) => { e.Handled = true; SetMode("edit"); editBox.Focus(); };
        cancelIcon.PointerPressed += (_, e) => { e.Handled = true; editBox.Text = message.Text; SetMode("view"); };
        saveIcon.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            var next = (editBox.Text ?? "").Trim();
            if (next.Length == 0) return;
            try
            {
                MessageStore.Update(_user!.Id, message.Id, next);
                Refresh();
            }
            catch (AuthException) { /* ignore empty */ }
        };

        delIcon.PointerPressed += (_, e) => { e.Handled = true; SetMode("delete"); };
        confirmNo.PointerPressed += (_, e) => { e.Handled = true; SetMode("view"); };
        confirmYes.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            MessageStore.Remove(_user!.Id, message.Id);
            Refresh();
        };

        return root;
    }

    private static TextBlock MakeTextButton(string text, IBrush color)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
    }

    private async Task SendMessageAsync(string text, int serial)
    {
        if (_owner is null) return;
        try
        {
            await PasteService.SendTextAsync(_owner, text);
            Flash($"Sent message #{serial}", false);
        }
        catch (Exception ex)
        {
            Flash(ex.Message, true);
        }
    }

    private void Flash(string text, bool isError)
    {
        var danger = (IBrush?)Application.Current!.Resources["DangerBrush"];
        var accent = (IBrush?)Application.Current!.Resources["AccentBrush"];

        FlashText.Text = text;
        FlashText.Foreground = isError ? danger : accent;
        FlashText.IsVisible = true;

        _flashTimer?.Stop();
        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _flashTimer.Tick += (_, _) => { FlashText.IsVisible = false; _flashTimer?.Stop(); };
        _flashTimer.Start();
    }

    private void OnAddMessage(object? sender, RoutedEventArgs e)
    {
        if (_user is null) return;
        var text = (NewMessageBox.Text ?? "").Trim();
        if (text.Length == 0) return;
        MessageStore.Add(_user.Id, text);
        NewMessageBox.Text = "";
        Refresh();
    }

    private void OnNewMessageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnAddMessage(sender, new RoutedEventArgs());
    }

    private void OnSignOut(object? sender, PointerPressedEventArgs e)
    {
        _user = null;
        SignedOut?.Invoke(this, EventArgs.Empty);
    }
}
