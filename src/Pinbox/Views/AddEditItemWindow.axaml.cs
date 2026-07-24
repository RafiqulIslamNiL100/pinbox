using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Pinbox.Models;
using Pinbox.Services;

namespace Pinbox.Views;

public partial class AddEditItemWindow : Window
{
    private static readonly string[] SuggestedLabels = { "urgent", "seasonal", "internal" };

    private readonly AuthSession _session;
    private readonly string _pageId;
    private readonly PinboxItem? _existing;
    private readonly HashSet<string> _selectedLabels = new();
    private string? _pendingImagePath;
    private bool _isPicture;

    public AddEditItemWindow() : this(new AuthSession(), "", null) { }

    public AddEditItemWindow(AuthSession session, string pageId, PinboxItem? existing)
    {
        InitializeComponent();
        _session = session;
        _pageId = pageId;
        _existing = existing;

        TitleText.Text = existing == null ? "Add item" : "Edit item";
        SubjectBox.Text = existing?.Subject ?? "";

        if (existing != null)
        {
            foreach (var l in existing.Labels) _selectedLabels.Add(l);
        }
        RenderLabelChips();

        _isPicture = existing?.Type == ItemType.Picture;
        if (existing?.Type == ItemType.Text) TextBox.Text = existing.Text;
        SetMode(_isPicture);

        if (existing?.Type == ItemType.Picture)
        {
            DropStatusText.Text = "Existing image: " + existing.ImageFileName;
            PreviewButton.IsVisible = true;
        }

        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);
        DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void SetMode(bool picture)
    {
        _isPicture = picture;
        TextTypeBtn.IsChecked = !picture;
        PicTypeBtn.IsChecked = picture;
        TextBox.IsVisible = !picture;
        PlaceholderHint.IsVisible = !picture;
        TemplateLink.IsVisible = !picture;
        DropZone.IsVisible = picture;
        PreviewButton.IsVisible = picture && (_pendingImagePath != null || _existing?.ImageFileName != null);
    }

    private void OnTextTypeClick(object? sender, RoutedEventArgs e) => SetMode(false);
    private void OnPicTypeClick(object? sender, RoutedEventArgs e) => SetMode(true);

    private IBrush? Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) ? v as IBrush : null;

    private void RenderLabelChips()
    {
        LabelsPanel.Children.Clear();
        foreach (var label in SuggestedLabels)
        {
            var isSel = _selectedLabels.Contains(label);
            var chip = new Border
            {
                Background = isSel ? Brush("AccentSoftBrush") : null,
                BorderBrush = Brush("BorderBrush2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(9, 4),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    Foreground = isSel ? Brush("AccentInkBrush") : Brush("TextDimBrush"),
                },
            };
            chip.PointerPressed += (_, _) =>
            {
                if (!_selectedLabels.Add(label)) _selectedLabels.Remove(label);
                RenderLabelChips();
            };
            LabelsPanel.Children.Add(chip);
        }
    }

    private void OnTemplateClick(object? sender, PointerPressedEventArgs e)
    {
        var flyout = new Flyout();
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(4) };
        foreach (var tmpl in TemplateService.Templates)
        {
            var item = new TextBlock { Text = tmpl.Subject, Padding = new Thickness(8, 6), Cursor = new Cursor(StandardCursorType.Hand) };
            item.PointerPressed += (_, _) =>
            {
                SubjectBox.Text = tmpl.Subject;
                TextBox.Text = tmpl.Text;
                flyout.Hide();
            };
            panel.Children.Add(item);
        }
        flyout.Content = panel;
        flyout.ShowAt(TemplateLink);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        var first = files?.FirstOrDefault();
        if (first != null)
        {
            _pendingImagePath = first.Path.LocalPath;
            DropStatusText.Text = System.IO.Path.GetFileName(_pendingImagePath);
            PreviewButton.IsVisible = true;
        }
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" } },
            },
        });

        var first = files.FirstOrDefault();
        if (first != null)
        {
            _pendingImagePath = first.Path.LocalPath;
            DropStatusText.Text = System.IO.Path.GetFileName(_pendingImagePath);
            PreviewButton.IsVisible = true;
        }
    }

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = _pendingImagePath;
            if (path == null && _existing?.ImageFileName != null)
                path = PageStore.GetImagePath(_session.UserId, _existing.ImageFileName);

            if (path != null && System.IO.File.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { /* non-fatal - just can't preview right now */ }
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        ErrorBox.IsVisible = false;
        try
        {
            var subject = SubjectBox.Text ?? "";
            var labels = _selectedLabels.ToList();

            if (_isPicture)
            {
                if (_existing == null)
                {
                    if (_pendingImagePath == null)
                    {
                        ErrorText.Text = "Choose an image first.";
                        ErrorBox.IsVisible = true;
                        return;
                    }
                    PageStore.AddPictureItem(_session.UserId, _pageId, subject, _pendingImagePath, labels);
                }
                // Editing a picture's file itself isn't supported in v1 - only
                // subject/labels can change via the text-item update path
                // below being skipped; picture edits mainly rename/label.
            }
            else
            {
                if (_existing == null)
                {
                    PageStore.AddTextItem(_session.UserId, _pageId, subject, TextBox.Text ?? "", labels);
                }
                else
                {
                    PageStore.UpdateTextItem(_session.UserId, _pageId, _existing.Id, subject, TextBox.Text ?? "", labels);
                }
            }

            Close(true);
        }
        catch (AuthException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorBox.IsVisible = true;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
