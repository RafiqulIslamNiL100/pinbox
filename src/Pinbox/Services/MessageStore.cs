using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pinbox.Models;

namespace Pinbox.Services;

public static class MessageStore
{
    private static string FilePath =>
        Path.Combine(AppPaths.DataDirectory, "messages.json");

    private class StoreShape
    {
        public Dictionary<string, List<SavedMessage>> ByUser { get; set; } = new();
    }

    private static StoreShape Load()
    {
        if (!File.Exists(FilePath)) return new StoreShape();
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<StoreShape>(json) ?? new StoreShape();
        }
        catch
        {
            return new StoreShape();
        }
    }

    private static void Save(StoreShape shape)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var json = JsonSerializer.Serialize(shape, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    public static List<SavedMessage> List(string userId)
    {
        var store = Load();
        return store.ByUser.TryGetValue(userId, out var list) ? list : new List<SavedMessage>();
    }

    public static List<SavedMessage> Add(string userId, string text)
    {
        var clean = (text ?? "").Trim();
        if (string.IsNullOrEmpty(clean))
            throw new AuthException("Message text cannot be empty.");

        var store = Load();
        if (!store.ByUser.TryGetValue(userId, out var list))
        {
            list = new List<SavedMessage>();
            store.ByUser[userId] = list;
        }

        list.Add(new SavedMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = clean,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });

        Save(store);
        return list;
    }

    public static List<SavedMessage> Update(string userId, string id, string text)
    {
        var clean = (text ?? "").Trim();
        if (string.IsNullOrEmpty(clean))
            throw new AuthException("Message text cannot be empty.");

        var store = Load();
        var list = store.ByUser.TryGetValue(userId, out var l) ? l : new List<SavedMessage>();
        var msg = list.FirstOrDefault(m => m.Id == id);
        if (msg is not null) msg.Text = clean;

        store.ByUser[userId] = list;
        Save(store);
        return list;
    }

    public static List<SavedMessage> Remove(string userId, string id)
    {
        var store = Load();
        var list = store.ByUser.TryGetValue(userId, out var l) ? l : new List<SavedMessage>();
        list = list.Where(m => m.Id != id).ToList();

        store.ByUser[userId] = list;
        Save(store);
        return list;
    }
}
