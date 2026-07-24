using System.Collections.Generic;

namespace Pinbox.Models;

public class PinboxPage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public string? Hotkey { get; set; } // e.g. "Ctrl+Alt+1"
    public List<PinboxItem> Items { get; set; } = new();
}
