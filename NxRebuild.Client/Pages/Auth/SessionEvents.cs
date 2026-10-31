namespace NxRebuild.Client.Pages.Auth {
    // Auth/SessionEvents.cs(新規)
    public class SessionEvents {
        private int _fired;
        public event Action? Unauthorized;
        public void RaiseUnauthorized() {
            if (Interlocked.Exchange(ref _fired, 1) == 0) Unauthorized?.Invoke();   // 多重発火の防止
        }
    }
}
