using System;
using System.Runtime.InteropServices;
//using Windows.UI.Notifications;
using Microsoft.Extensions.Logging;

namespace MudaeFarm
{
    public interface INotificationSender
    {
        void SendToast(string s);
    }

    public class NotificationSender : INotificationSender
    {
        readonly ILogger<NotificationSender> _logger;
        //readonly ToastNotifier _notifier;
        readonly bool _windows;
        readonly bool _linux;

        public NotificationSender(ILogger<NotificationSender> logger)
        {
            //_notifier = ToastNotificationManager.CreateToastNotifier("MudaeFarm");
            _logger   = logger;
            _windows  = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            _linux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }

        public async void SendToast(string s)
        {
            // toast notifications are windows-specific
            if (_windows) 
            {
                try
                {
                    //var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);
                    //var toastNodes = toastXml.GetElementsByTagName("text");

                    //toastNodes[0].AppendChild(toastXml.CreateTextNode(s));

                    //_notifier.Show(new ToastNotification(toastXml));
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Could not send Windows toast notification.");
                }            
            }
            else if (_linux)
            {
                var channel = DiscordNotificationChannel.channel;

                if (channel == null)
                {
                    var guild = DiscordNotificationChannel.guild;
                    if (guild == null)
                    {
                        _logger.LogWarning("Can't send notification because Guild is null");
                        return;
                    }
                    else
                    {
                        _logger.LogWarning("Can't find channel claim-notifications, creating new channel...");
                        DiscordNotificationChannel.channel = await guild.CreateTextChannelAsync("claim-notifications", c => c.Topic = "Get notified of MudaeFarm claims here.");
                        return;
                    }
                }

                await channel.SendMessageAsync(s);
                
                return;
            }

        }
    }
}