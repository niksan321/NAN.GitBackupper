using Microsoft.AspNetCore.SignalR;

namespace NAN.GitBackupper.Api.Hubs;

/// <summary>Сервер пушит события прогресса бекапа через <see cref="IBackupProgressBroadcaster"/>.</summary>
public sealed class BackupProgressHub : Hub
{
}
