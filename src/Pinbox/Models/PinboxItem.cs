using System.Collections.Generic;

namespace Pinbox.Models;

public enum ItemType
{
    Text,
    Picture,
}

public class PinboxItem
{
    public string Id { get; set; } = "";
    public string Subject { get; set; } = "";
    public ItemType Type { get; set; } = ItemType.Text;
    public string Text { get; set; } = "";

    // Relative path under the page's images folder. Only the path is stored
    // here - never the image bytes - so the JSON data file stays small no
    // matter how many pictures are saved.
    public string? ImageFileName { get; set; }

    public List<string> Labels { get; set; } = new();
    public bool IsFavorite { get; set; }
    public int UsageCount { get; set; }
    public long? LastUsedAt { get; set; }
    public long CreatedAt { get; set; }
}
