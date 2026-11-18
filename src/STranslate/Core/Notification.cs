using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using STranslate.Helpers;
using STranslate.Plugin;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;

namespace STranslate.Core;

public class Notification(ILogger<Notification> logger) : INotification
{
    internal bool legacy = !Win32Helper.IsNotificationSupported();

    private readonly ConcurrentDictionary<string, NotificationActionEntry> _notificationActions = new();
    private const int MaxActionCount = 256;
    private static readonly TimeSpan ActionTtl = TimeSpan.FromMinutes(30);

    private sealed class NotificationActionEntry(Action action, DateTimeOffset createdAt)
    {
        public Action Action { get; } = action;
        public DateTimeOffset CreatedAt { get; } = createdAt;
    }

    internal void Install()
    {
        if (!legacy)
        {
            ToastNotificationManagerCompat.OnActivated -= OnActivated;
            ToastNotificationManagerCompat.OnActivated += OnActivated;
        }
    }

    internal void Uninstall()
    {
        if (!legacy)
        {
            _notificationActions.Clear();
            ToastNotificationManagerCompat.OnActivated -= OnActivated;
            ToastNotificationManagerCompat.Uninstall();
        }
    }

    private void OnActivated(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        var actionId = toastArgs.Argument;

        // 仅执行一次并立刻移除，避免闭包长期滞留在字典中
        if (_notificationActions.TryRemove(actionId, out var entry))
        {
            try
            {
                entry.Action?.Invoke();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification action execution error");
            }
        }
    }

    private void CleanupNotificationActions()
    {
        var now = DateTimeOffset.UtcNow;

        // 先按 TTL 清理过期项
        foreach (var pair in _notificationActions)
        {
            if (now - pair.Value.CreatedAt > ActionTtl)
            {
                _notificationActions.TryRemove(pair.Key, out _);
            }
        }

        // 再按上限清理最旧项，防止极端场景无限增长
        if (_notificationActions.Count <= MaxActionCount)
            return;

        var removeCount = _notificationActions.Count - MaxActionCount;
        var oldestKeys = _notificationActions
            .OrderBy(x => x.Value.CreatedAt)
            .Take(removeCount)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in oldestKeys)
        {
            _notificationActions.TryRemove(key, out _);
        }
    }

    public void Show(string title, string subTitle, string? iconPath = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ShowInternal(title, subTitle, iconPath);
        });
    }

    public void ShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string? iconPath = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ShowInternalWithButton(title, buttonText, buttonAction, subTitle, iconPath);
        });
    }

    private void ShowInternal(string title, string subTitle, string? iconPath = null)
    {
        // Handle notification for win7/8/early win10
        //if (legacy)
        //{
        //    LegacyShow(title, subTitle, iconPath);
        //    return;
        //}

        // Using Windows Notification System
        var Icon = !File.Exists(iconPath)
            ? Path.Combine(Constant.ProgramDirectory, "Images\\app.png")
            : iconPath;

        try
        {
            new ToastContentBuilder()
                .AddText(title, hintMaxLines: 1)
                .AddText(subTitle)
                .AddAppLogoOverride(new Uri(Icon))
                .Show();
        }
        catch (InvalidOperationException e)
        {
            // Temporary fix for the Windows 11 notification issue
            // Possibly from 22621.1413 or 22621.1485, judging by post time of #2024
            logger.LogError(e, "Notification InvalidOperationException Error");
            //LegacyShow(title, subTitle, iconPath);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Notification Error");
            //LegacyShow(title, subTitle, iconPath);
        }
    }

    private void ShowInternalWithButton(string title, string buttonText, Action buttonAction, string subTitle, string? iconPath = null)
    {
        // Handle notification for win7/8/early win10
        if (legacy)
        {
            //LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
            return;
        }

        // Using Windows Notification System
        var Icon = !File.Exists(iconPath)
            ? Path.Combine(Constant.ProgramDirectory, "Images\\app.png")
            : iconPath;

        try
        {
            var guid = Guid.NewGuid().ToString();
            new ToastContentBuilder()
                .AddText(title, hintMaxLines: 1)
                .AddText(subTitle)
                .AddButton(buttonText, ToastActivationType.Background, guid)
                .AddAppLogoOverride(new Uri(Icon))
                .Show();
            _notificationActions.AddOrUpdate(
                guid,
                new NotificationActionEntry(buttonAction, DateTimeOffset.UtcNow),
                (key, oldValue) => new NotificationActionEntry(buttonAction, DateTimeOffset.UtcNow));
            CleanupNotificationActions();
        }
        catch (InvalidOperationException e)
        {
            // Temporary fix for the Windows 11 notification issue
            // Possibly from 22621.1413 or 22621.1485, judging by post time of #2024
            logger.LogError(e, "Notification InvalidOperationException Error");
            //LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Notification Error");
            //LegacyShowWithButton(title, buttonText, buttonAction, subTitle, iconPath);
        }
    }

    //private static void LegacyShow(string title, string subTitle, string? iconPath)
    //{
    //    var msg = new Msg();
    //    msg.Show(title, subTitle, iconPath);
    //}

    //private static void LegacyShowWithButton(string title, string buttonText, Action buttonAction, string subTitle, string? iconPath)
    //{
    //    var msg = new MsgWithButton();
    //    msg.Show(title, buttonText, buttonAction, subTitle, iconPath);
    //}
}
