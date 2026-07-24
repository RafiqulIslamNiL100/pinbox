using System.Collections.Generic;

namespace Pinbox.Services;

public record ItemTemplate(string Subject, string Text);

public static class TemplateService
{
    public static readonly List<ItemTemplate> Templates = new()
    {
        new ItemTemplate("Apology for delay", "Hi {name}, sorry for the delayed response — this slipped past me. I'll have an update for you shortly."),
        new ItemTemplate("Thank you", "Hi {name}, thank you so much for reaching out — really appreciate it!"),
        new ItemTemplate("Refund policy", "Hi {name}, thanks for reaching out — refunds are processed within 5–7 business days once the return is received."),
        new ItemTemplate("Order shipped", "Hi {name}, good news — your order has shipped and should arrive within 2–4 business days."),
        new ItemTemplate("Follow-up", "Hi {name}, just following up on this — let me know if you have any questions!"),
    };
}
