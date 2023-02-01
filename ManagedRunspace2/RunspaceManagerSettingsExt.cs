namespace ManagedRunspace2
{
    static class RunspaceManagerSettingsExt
    {
        public static bool IsPeriodicRenewConfigured(this ManagedRunspaceSettings settings)
            => settings.RenewInterval != null;
    }
}
