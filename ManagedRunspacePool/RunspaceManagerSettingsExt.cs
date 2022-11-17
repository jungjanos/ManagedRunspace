using System.Threading.Tasks;

namespace ManagedRunspacePool2
{
    static class RunspaceManagerSettingsExt
    {
        static Task _never = new TaskCompletionSource<bool>().Task; // never completes

        public static Task CreateRenewTimer(this RunspaceManagerSettings settings)
            =>
            settings.RenewInterval.HasValue
            ? Task.Delay(settings.RenewInterval.Value)
            : _never;

        public static bool IsPeriodicRenewConfigured(this RunspaceManagerSettings settings)
            => settings.RenewInterval != null;
    }
}
