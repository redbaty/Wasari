using Oakton.Resources;
using Wolverine.Persistence.Durability;

namespace Wasari.Daemon.Extensions;

internal static class HostExtension
{
    public static async Task ResetWolverine(this IHost host)
    {
        // Programmatically apply any outstanding message store
        // database changes
        await host.SetupResources();

        // Teardown the database message storage
        await host.TeardownResources();

        // Clear out any database message storage
        // also tries to clear out any messages held
        // by message brokers connected to your Wolverine app
        await host.ResetResourceState();

        var store = host.Services.GetRequiredService<IMessageStore>();

        // Rebuild the database schema objects
        // and delete existing message data
        // This is good for testing
        await store.Admin.RebuildAsync();

        // Remove all persisted messages
        await store.Admin.ClearAllAsync();
    }
}