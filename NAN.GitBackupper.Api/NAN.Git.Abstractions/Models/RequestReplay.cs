using System.Collections.Generic;

namespace NAN.Git.Models;

public class RequestReplay
{
    public bool IsSended { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; }

    /// <summary>Размеры локальных каталогов бэкапа по ключу DisplayKey (байты), если были посчитаны после синхронизации.</summary>
    public Dictionary<string, long> RepositoryBackupSizesBytes { get; set; }

    /// <summary>Сумма размеров выбранных репозиториев (байты); только при полном успехе бэкапа.</summary>
    public long? LastBackupSelectedTotalBytes { get; set; }

    /// <summary>Размер созданного ZIP (байты); только при успехе и включённом ZIP.</summary>
    public long? LastBackupZipArchiveBytes { get; set; }

    public static RequestReplay Sending { get; }

    static RequestReplay()
    {
        Sending = new RequestReplay
        {
            IsSended = false,
            Message = "Sending",
            IsSuccess = false,
        };
    }

    public static RequestReplay Error(Exception ex) =>
        new()
        {
            IsSuccess = false,
            IsSended = true,
            Message = ex.InnerException?.Message ?? ex.Message,
        };
}
