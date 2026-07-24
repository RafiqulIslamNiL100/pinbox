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
    };

    public static string T(string key)
    {
        if (Strings.TryGetValue(key, out var pair))
            return Lang == "zh" ? pair.zh : pair.en;
        return key;
    }
}
