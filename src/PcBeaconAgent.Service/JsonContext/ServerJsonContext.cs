using PcBeaconAgent.Client.Core.Models.Common;
using PcBeaconAgent.Service.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace PcBeaconAgent.Service.JsonContext
{
    [JsonSerializable(typeof(PairRequest))]
    [JsonSerializable(typeof(PairResponse))]
    [JsonSerializable(typeof(SimpleMessageResponse))]
    [JsonSerializable(typeof(BeaconDevice))]
    [JsonSerializable(typeof(List<AudioDeviceDto>))] 
    [JsonSerializable(typeof(DefaultDeviceDto))]
    [JsonSerializable(typeof(MessageDto))]
    [JsonSerializable(typeof(List<DisplayDeviceDtos>))]
    [JsonSerializable(typeof(DisableRequest))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    internal partial class ServerJsonContext : JsonSerializerContext
    {
    }
}
