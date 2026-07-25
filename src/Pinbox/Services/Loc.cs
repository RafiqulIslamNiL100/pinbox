using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Pinbox.Services;

public static class Loc
{
    public static event Action? LanguageChanged;

    private static string SettingsPath =>
        Path.Combine(AppPaths.DataDirectory, "language.json");

    private static string _lang = "en";
    public static string Lang
    {
        get => _lang;
        set
        {
            if (_lang == value) return;
            _lang = value == "zh" ? "zh" : "en";
            SaveLang();
            LanguageChanged?.Invoke();
        }
    }

    static Loc()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("lang", out var l))
                    _lang = l.GetString() == "zh" ? "zh" : "en";
            }
        }
        catch { /* default to en */ }
    }

    private static void SaveLang()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var existing = new Dictionary<string, object>();
            if (File.Exists(SettingsPath))
            {
                try
                {
                    existing = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(SettingsPath)) ?? new();
                }
                catch { /* start fresh */ }
            }
            existing["lang"] = _lang;
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(existing));
        }
        catch { /* non-fatal */ }
    }

    private static readonly Dictionary<string, (string en, string zh)> Strings = new()
    {
        ["welcome_back"] = ("Welcome back", "欢迎回来"),
        ["signin_lede"] = ("Sign in to load your pages and saved items.", "登录以加载您的页面和已保存项目。"),
        ["email"] = ("Email", "电子邮件"),
        ["password"] = ("Password", "密码"),
        ["sign_in"] = ("Sign in", "登录"),
        ["no_account"] = ("Don't have an account?", "还没有账户？"),
        ["sign_up"] = ("Sign up", "注册"),
        ["create_account"] = ("Create your account", "创建您的账户"),
        ["signup_lede"] = ("Create a login, then activate with a key.", "创建登录信息，然后使用密钥激活。"),
        ["name"] = ("Name", "姓名"),
        ["already_have"] = ("Already have one?", "已经有账户了？"),
        ["enter_key"] = ("Enter your key", "输入您的密钥"),
        ["activate_lede"] = ("Pinbox is locked until you activate it with a key.", "在激活密钥之前，Pinbox 处于锁定状态。"),
        ["unique_key"] = ("Unique key", "唯一密钥"),
        ["activate"] = ("Activate", "激活"),
        ["saved_items"] = ("Saved items", "已保存项目"),
        ["add_item"] = ("+ Add item", "+ 添加项目"),
        ["new_page"] = ("+ New page", "+ 新建页面"),
        ["search_items"] = ("Search items…", "搜索项目…"),
        ["pinned"] = ("Pinned", "已置顶"),
        ["all_items"] = ("All items", "全部项目"),
        ["edit"] = ("Edit", "编辑"),
        ["delete"] = ("Delete", "删除"),
        ["duplicate"] = ("Duplicate", "复制"),
        ["view"] = ("View", "查看"),
        ["copy"] = ("Copy", "复制文本"),
        ["move_up"] = ("Move up", "上移"),
        ["move_down"] = ("Move down", "下移"),
        ["cancel"] = ("Cancel", "取消"),
        ["save_item"] = ("Save item", "保存项目"),
        ["subject"] = ("Subject", "主题"),
        ["type"] = ("Type", "类型"),
        ["text"] = ("Text", "文本"),
        ["picture"] = ("Picture", "图片"),
        ["content"] = ("Content", "内容"),
        ["labels"] = ("Labels", "标签"),
        ["sign_out"] = ("Sign out", "退出登录"),
        ["settings"] = ("Settings", "设置"),
        ["general"] = ("General", "通用"),
        ["account_data"] = ("Account & data", "账户和数据"),
        ["global_hotkey"] = ("Global hotkey", "全局快捷键"),
        ["start_with_windows"] = ("Start with Windows", "开机自启"),
        ["compact_mode"] = ("Compact mode", "简洁模式"),
        ["notifications"] = ("Windows notifications", "系统通知"),
        ["theme"] = ("Theme", "主题"),
        ["light"] = ("Light", "浅色"),
        ["dark"] = ("Dark", "深色"),
        ["system"] = ("System", "跟随系统"),
        ["license"] = ("License", "许可证"),
        ["language"] = ("Language", "语言"),
        ["auto_lock"] = ("Auto-lock", "自动锁定"),
        ["off"] = ("Off", "关闭"),
        ["export"] = ("Export", "导出"),
        ["import"] = ("Import", "导入"),
        ["enter_pin"] = ("Enter your PIN", "输入您的密码"),
        ["locked_after_idle"] = ("Locked after being idle.", "空闲后已锁定。"),
        ["drag_drop_hint"] = ("Drag & drop an image here, or browse files", "将图片拖放到此处，或浏览文件"),
        ["preview_image"] = ("Preview image", "预览图片"),
        ["start_from_template"] = ("Start from a template…", "从模板开始…"),
        ["window_locked_title"] = ("Pinbox is locked", "Pinbox 已锁定"),
        ["renew_to_continue"] = ("Renew your key to keep using Pinbox.", "续订您的密钥以继续使用 Pinbox。"),

        // Sign in / sign up / activate
        ["signin_lede2"] = ("Sign in to load your saved messages.", "登录以加载您已保存的消息。"),
        ["signup_lede2"] = ("Your saved messages are stored on this PC.", "您保存的消息存储在此电脑上。"),
        ["email_watermark"] = ("you@example.com", "you@example.com"),
        ["password_watermark"] = ("Your password", "您的密码"),
        ["password_watermark_new"] = ("At least 8 characters", "至少 8 个字符"),
        ["name_watermark"] = ("Your name", "您的姓名"),
        ["create_account_btn"] = ("Create account", "创建账户"),
        ["already_have_account"] = ("Already have an account?", "已经有账户了？"),
        ["dont_have_account"] = ("Don't have an account?", "还没有账户？"),
        ["key_watermark"] = ("XXXX-XXXX-XXXX-XXXX", "XXXX-XXXX-XXXX-XXXX"),

        // Settings descriptions and extra labels
        ["save"] = ("Save", "保存"),
        ["account"] = ("Account", "账户"),
        ["active"] = ("Active", "有效"),
        ["desc_hotkey"] = ("Opens Pinbox from anywhere", "从任何地方打开 Pinbox"),
        ["desc_startup"] = ("Launch Pinbox on login", "登录时启动 Pinbox"),
        ["desc_compact"] = ("Use a smaller window by default", "默认使用较小的窗口"),
        ["desc_notifications"] = ("Show a system toast when a message is sent", "发送消息时显示系统通知"),
        ["desc_theme"] = ("Override the Windows light/dark setting", "覆盖 Windows 的浅色/深色设置"),
        ["desc_license"] = ("Renews automatically while active", "有效期内自动续订"),
        ["desc_language"] = ("Translates Pinbox's own interface only", "仅翻译 Pinbox 自身的界面"),
        ["desc_autolock"] = ("Require a PIN after being idle", "空闲后需要输入密码"),
        ["set_pin"] = ("Set PIN", "设置密码"),
        ["desc_setpin"] = ("Required if auto-lock is on", "开启自动锁定时必填"),
        ["backup"] = ("Backup", "备份"),
        ["desc_backup"] = ("Export or import your pages and items", "导出或导入您的页面和项目"),
        ["mins_5"] = ("5 minutes", "5 分钟"),
        ["mins_15"] = ("15 minutes", "15 分钟"),
        ["autolock_5"] = ("5m", "5分"),
        ["autolock_15"] = ("15m", "15分"),

        // Add / edit item
        ["add_item_title"] = ("Add item", "添加项目"),
        ["edit_item_title"] = ("Edit item", "编辑项目"),
        ["subject_watermark"] = ("e.g. Refund policy", "例如：退款政策"),
        ["placeholder_hint"] = ("Use {name} as a placeholder — Pinbox will ask you to fill it in before sending.", "使用 {name} 作为占位符 — Pinbox 会在发送前让您填写。"),
        ["drop_image_here"] = ("Drag & drop an image here", "将图片拖放到此处"),
        ["browse_files"] = ("Browse files…", "浏览文件…"),

        // Misc
        ["ok"] = ("OK", "确定"),
        ["close"] = ("Close", "关闭"),
        ["unlock"] = ("Unlock", "解锁"),
        ["new_page_name"] = ("New page name", "新页面名称"),
        ["rename_page"] = ("Rename page", "重命名页面"),
        ["move_left"] = ("Move left", "左移"),
        ["move_right"] = ("Move right", "右移"),
        ["set_page_hotkey"] = ("Set page hotkey…", "设置页面快捷键…"),
        ["delete_page"] = ("Delete page", "删除页面"),
        ["delete_selected"] = ("Delete selected", "删除所选"),
        ["no_saved_items"] = ("No saved items yet.", "还没有保存的项目。"),
        ["saved_item_singular"] = ("saved item", "已保存项目"),
        ["saved_items_plural"] = ("saved items", "已保存项目"),
        ["managed_by_key"] = ("Managed by your activation key", "由您的激活密钥管理"),
        ["key_expired_title"] = ("Your key has expired", "您的密钥已过期"),
        ["key_expired_lede"] = ("Enter a new key to keep using Pinbox.", "输入新密钥以继续使用 Pinbox。"),
        ["access_unavailable"] = ("Access unavailable", "无法访问"),
        ["enter_key_first"] = ("Enter a key first.", "请先输入密钥。"),
    };

    public static string T(string key)
    {
        if (Strings.TryGetValue(key, out var pair))
            return Lang == "zh" ? pair.zh : pair.en;
        return key;
    }
}
