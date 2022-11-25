using System.Threading.Tasks;

namespace ManagedRunspacePool2
{
    static class RunspaceManagerSettingsExt
    {
        static Task _never = new TaskCompletionSource<bool>().Task; // never completes

        public static Task CreateRenewTimer(this ManagedRunspaceSettings settings)
            =>
            settings.RenewInterval.HasValue
            ? Task.Delay(settings.RenewInterval.Value)
            : _never;

        public static bool IsPeriodicRenewConfigured(this ManagedRunspaceSettings settings)
            => settings.RenewInterval != null;
    }
}
