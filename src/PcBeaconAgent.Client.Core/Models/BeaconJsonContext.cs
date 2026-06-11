using System.Text.Json.Serialization;

namespace PcBeaconAgent.Client.Core.Models
{
    [JsonSerializable(typeof(BeaconDevice))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    public partial class BeaconJsonContext : JsonSerializerContext
    {
    }
}
