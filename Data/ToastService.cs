/* In the name of God, the Merciful, the Compassionate */

using System;

namespace SqlHealthAssessment.Data
{
    public class ToastService
    {
        public event Action<ToastNotification>? OnShow;

        public void ShowSuccess(string title, string message = "", int duration = 3000)
        {
            Show(new ToastNotification
            {
                Type = ToastType.Success,
                Title = title,
                Message = message,
                Duration = duration
            });
        }

        public void ShowError(string title, string message = "", int duration = 5000)
        {
            Show(new ToastNotification
            {
                Type = ToastType.Error,
                Title = title,
                Message = message,
                Duration = duration
            });
        }

        public void ShowWarning(string title, string message = "", int duration = 4000)
        {
            Show(new ToastNotification
            {
                Type = ToastType.Warning,
                Title = title,
                Message = message,
                Duration = duration
            });
        }

        public void ShowInfo(string title, string message = "", int duration = 3000)
        {
            Show(new ToastNotification
            {
                Type = ToastType.Info,
                Title = title,
                Message = message,
                Duration = duration
            });
        }

        private void Show(ToastNotification toast)
        {
            OnShow?.Invoke(toast);
        }
    }

    public class ToastNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ToastType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Duration { get; set; } = 3000;
        public bool IsVisible { get; set; }
    }

    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }
}
