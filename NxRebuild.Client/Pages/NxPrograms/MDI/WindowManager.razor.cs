using Microsoft.AspNetCore.Components;

namespace NxRebuild.Client.Pages.NxPrograms.MDI {
    public class WindowManagerBase {
        public List<WindowInfo> Windows { get; set; } = new();
        public double ScreenWidth { get; set; }
        public double ScreenHeight { get; set; }

        public event Action? OnWindowsChanged;

        private int _zCounter = 100;

        // ★ drag/resize 中は再描画しないためのフラグ
        public bool IsDragging { get; set; } = false;

        public WindowManagerBase() {
            Console.WriteLine("WindowManagerBase created");
        }

        public void Restore(Guid id) {
            var w = Windows.FirstOrDefault(x => x.Id == id);
            if (w != null) {
                w.IsMinimized = false;
                BringToFront(w);
            }

            if (!IsDragging)
                OnWindowsChanged?.Invoke();
        }

        public void NotifyWindowsChanged() {
            if (!IsDragging)
                OnWindowsChanged?.Invoke();
        }

        public void Open<T>(string title, Dictionary<string, object>? parameters = null)
            where T : IComponent {
            Windows.Add(new WindowInfo {
                Title = title,
                ComponentType = typeof(T),
                Parameters = parameters,
                X = 120 + Windows.Count * 20,
                Y = 80 + Windows.Count * 20,
                Z = ++_zCounter
            });

            if (!IsDragging)
                OnWindowsChanged?.Invoke();
        }

        public void Close(Guid id) {
            Windows.RemoveAll(w => w.Id == id);

            if (!IsDragging)
                OnWindowsChanged?.Invoke();
        }

        public void BringToFront(WindowInfo win) {
            win.Z = ++_zCounter;

            if (!IsDragging)
                OnWindowsChanged?.Invoke();
        }
    }
}
