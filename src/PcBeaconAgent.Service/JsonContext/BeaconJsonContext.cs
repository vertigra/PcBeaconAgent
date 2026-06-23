using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Service.Endpoints;
using System.Text.Json.Serialization;

namespace PcBeaconAgent.Service.JsonContext
{
    [JsonSerializable(typeof(PairRequest))]
    [JsonSerializable(typeof(PairResponse))]
    [JsonSerializable(typeof(SimpleMessageResponse))]
    [JsonSerializable(typeof(BeaconDevice))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    internal partial class BeaconJsonContext : JsonSerializerContext
    {
    }
}
