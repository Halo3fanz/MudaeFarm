using System;
using System.Runtime.InteropServices;
//using Windows.UI.Notifications;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MimeKit;

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

        public void SendToast(string s)
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
                return;
            }

        }
    }
}