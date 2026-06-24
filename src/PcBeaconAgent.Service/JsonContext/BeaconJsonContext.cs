using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Service.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DefaultDeviceResponse = PcBeaconAgent.Service.Models.DefaultDeviceResponse;


namespace PcBeaconAgent.Service.JsonContext
{
    [JsonSerializable(typeof(PairRequest))]
    [JsonSerializable(typeof(PairResponse))]
    [JsonSerializable(typeof(SimpleMessageResponse))]
    [JsonSerializable(typeof(BeaconDevice))]
    [JsonSerializable(typeof(List<AudioDeviceDto>))] 
    [JsonSerializable(typeof(DefaultDeviceResponse))]
    [JsonSerializable(typeof(MessageResponse))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    internal partial class BeaconJsonContext : JsonSerializerContext
    {
    }
}
