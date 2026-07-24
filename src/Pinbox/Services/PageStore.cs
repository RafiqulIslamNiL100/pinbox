using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pinbox.Models;

namespace Pinbox.Services;

public static class PageStore
{
    private static string UserDir(string userId) =>
        Path.Combine(AppPaths.DataDirectory, "pages", userId);

    private static string PagesFilePath(string userId) =>
        Path.Combine(UserDir(userId), "pages.json");

    private static string ImagesDir(string userId) =>
        Path.Combine(UserDir(userId), "images");

    private static List<PinboxPage> Load(string userId)
    {
        var path = PagesFilePath(userId);
        if (!File.Exists(path))
        {
            var defaultPages = new List<PinboxPage>
            {
                new() { Id = Guid.NewGuid().ToString("N"), Name = "My replies", Order = 0 },
            };
            Save(userId, defaultPages);
            return defaultPages;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<PinboxPage>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static void Save(string userId, List<PinboxPage> pages)
    {
        Directory.CreateDirectory(UserDir(userId));
        var json = JsonSerializer.Serialize(pages, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(PagesFilePath(userId), json);
    }

    public static List<PinboxPage> ListPages(string userId) =>
        Load(userId).OrderBy(p => p.Order).ToList();

    public static List<PinboxPage> AddPage(string userId, string name)
    {
        var pages = Load(userId);
        pages.Add(new PinboxPage
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled page" : name.Trim(),
            Order = pages.Count == 0 ? 0 : pages.Max(p => p.Order) + 1,
        });
        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> RenamePage(string userId, string pageId, string newName)
    {
        var pages = Load(userId);
        var page = pages.FirstOrDefault(p => p.Id == pageId);
        if (page != null) page.Name = newName.Trim();
        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> DeletePage(string userId, string pageId)
    {
        var pages = Load(userId);
        pages.RemoveAll(p => p.Id == pageId);
        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> ReorderPage(string userId, string pageId, int direction)
    {
        var pages = Load(userId).OrderBy(p => p.Order).ToList();
        var index = pages.FindIndex(p => p.Id == pageId);
        var swapWith = index + direction;
        if (index < 0 || swapWith < 0 || swapWith >= pages.Count) return pages;

        (pages[index].Order, pages[swapWith].Order) = (pages[swapWith].Order, pages[index].Order);
        Save(userId, pages);
        return pages.OrderBy(p => p.Order).ToList();
    }

    public static List<PinboxPage> SetPageHotkey(string userId, string pageId, string? hotkey)
    {
        var pages = Load(userId);
        var page = pages.FirstOrDefault(p => p.Id == pageId);
        if (page != null) page.Hotkey = hotkey;
        Save(userId, pages);
        return pages;
    }

    /// Copies an image file into this user's images folder and returns the
    /// new item. The source file is left untouched; only a copy is stored.
    public static List<PinboxPage> AddPictureItem(string userId, string pageId, string subject, string sourceImagePath, List<string>? labels = null)
    {
        var pages = Load(userId);
        var page = pages.FirstOrDefault(p => p.Id == pageId) ?? throw new AuthException("Page not found.");

        var itemId = Guid.NewGuid().ToString("N");
        var ext = Path.GetExtension(sourceImagePath);
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        var fileName = itemId + ext;

        Directory.CreateDirectory(ImagesDir(userId));
        File.Copy(sourceImagePath, Path.Combine(ImagesDir(userId), fileName), overwrite: true);

        page.Items.Add(new PinboxItem
        {
            Id = itemId,
            Subject = string.IsNullOrWhiteSpace(subject) ? Path.GetFileName(sourceImagePath) : subject.Trim(),
            Type = ItemType.Picture,
            ImageFileName = fileName,
            Labels = labels ?? new(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });

        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> AddTextItem(string userId, string pageId, string subject, string text, List<string>? labels = null)
    {
        var clean = (text ?? "").Trim();
        if (string.IsNullOrEmpty(clean))
            throw new AuthException("Item text cannot be empty.");

        var pages = Load(userId);
        var page = pages.FirstOrDefault(p => p.Id == pageId) ?? throw new AuthException("Page not found.");

        page.Items.Add(new PinboxItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Subject = string.IsNullOrWhiteSpace(subject) ? "Untitled" : subject.Trim(),
            Type = ItemType.Text,
            Text = clean,
            Labels = labels ?? new(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });

        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> UpdateTextItem(string userId, string pageId, string itemId, string subject, string text, List<string> labels)
    {
        var pages = Load(userId);
        var item = pages.FirstOrDefault(p => p.Id == pageId)?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            item.Subject = string.IsNullOrWhiteSpace(subject) ? item.Subject : subject.Trim();
            item.Text = text.Trim();
            item.Labels = labels;
        }
        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> DeleteItem(string userId, string pageId, string itemId)
    {
        var pages = Load(userId);
        var page = pages.FirstOrDefault(p => p.Id == pageId);
        var item = page?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item?.ImageFileName != null)
        {
            try { File.Delete(Path.Combine(ImagesDir(userId), item.ImageFileName)); } catch { /* best effort */ }
        }
        page?.Items.RemoveAll(i => i.Id == itemId);
        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> DuplicateItem(string userId, string pageId, string itemId)
    {
        var pages = Load(userId);
        var page = pages.FirstOrDefault(p => p.Id == pageId);
        var item = page?.Items.FirstOrDefault(i => i.Id == itemId);
        if (page != null && item != null)
        {
            var copyId = Guid.NewGuid().ToString("N");
            string? copiedImage = null;
            if (item.ImageFileName != null)
            {
                var ext = Path.GetExtension(item.ImageFileName);
                copiedImage = copyId + ext;
                try
                {
                    File.Copy(
                        Path.Combine(ImagesDir(userId), item.ImageFileName),
                        Path.Combine(ImagesDir(userId), copiedImage), overwrite: true);
                }
                catch { copiedImage = item.ImageFileName; }
            }

            page.Items.Add(new PinboxItem
            {
                Id = copyId,
                Subject = item.Subject + " (copy)",
                Type = item.Type,
                Text = item.Text,
                ImageFileName = copiedImage,
                Labels = new List<string>(item.Labels),
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        }
        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> ToggleFavorite(string userId, string pageId, string itemId)
    {
        var pages = Load(userId);
        var item = pages.FirstOrDefault(p => p.Id == pageId)?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null) item.IsFavorite = !item.IsFavorite;
        Save(userId, pages);
        return pages;
    }

    public static List<PinboxPage> RecordUsage(string userId, string pageId, string itemId)
    {
        var pages = Load(userId);
        var item = pages.FirstOrDefault(p => p.Id == pageId)?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            item.UsageCount++;
            item.LastUsedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        Save(userId, pages);
        return pages;
    }

    public static string GetImagePath(string userId, string fileName) =>
        Path.Combine(ImagesDir(userId), fileName);

    public static string DataRootForExport(string userId) => UserDir(userId);
}
