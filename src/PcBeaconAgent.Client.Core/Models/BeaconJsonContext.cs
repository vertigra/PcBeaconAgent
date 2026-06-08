using PcBeaconAgent.Client.Core.Models;
using System.Text.Json.Serialization;

namespace PcBeaconAgent.Client.Core.Models
{
    // Говорим серверному компилятору сгенерировать код сериализации 
    // для модели, пришедшей из Core проекта
    [JsonSerializable(typeof(BeaconDevice))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false)]
    public partial class BeaconJsonContext : JsonSerializerContext
    {
    }
}
