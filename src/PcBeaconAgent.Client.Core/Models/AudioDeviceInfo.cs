namespace PcBeaconAgent.Client.Core.Models
{
    // Имена свойств совпадают 1:1 с серверным AudioDeviceDto — ни клиент, ни сервер
    // не настраивают PropertyNamingPolicy для AppJsonSerializerContext, поэтому
    // обе стороны по умолчанию используют точное совпадение регистра (PascalCase).
    public record AudioDeviceInfo(string Id, string FullName);
}