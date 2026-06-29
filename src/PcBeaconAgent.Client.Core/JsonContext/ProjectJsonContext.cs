using PcBeaconAgent.Client.Core.Models.Common;
using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace PcBeaconAgent.Client.Core.JsonContext
{
    [JsonSerializable(typeof(PairRequestDto))]
    [JsonSerializable(typeof(PairResponseDto))]
    [JsonSerializable(typeof(BeaconDevice))]
    [JsonSerializable(typeof(List<AudioDeviceDto>))] 
    [JsonSerializable(typeof(DefaultDeviceDto))]
    [JsonSerializable(typeof(List<DisplayDeviceDto>))]
    [JsonSerializable(typeof(DisableRequestDto))]
    [JsonSerializable(typeof(MessageDto))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    public partial class ProjectJsonContext : JsonSerializerContext
    {
    }
}
