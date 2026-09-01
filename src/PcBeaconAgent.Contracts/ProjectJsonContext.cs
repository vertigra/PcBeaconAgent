using PcBeaconAgent.Contracts.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace PcBeaconAgent.Contracts
{
    [JsonSerializable(typeof(PairRequestDto))]
    [JsonSerializable(typeof(PairResponseDto))]
    [JsonSerializable(typeof(BeaconDevice))]
    [JsonSerializable(typeof(List<AudioDeviceDto>))] 
    [JsonSerializable(typeof(DefaultDeviceDto))]
    [JsonSerializable(typeof(List<DisplayDeviceDto>))]
    [JsonSerializable(typeof(DisplayListResponseDto))]
    [JsonSerializable(typeof(DisableRequestDto))]
    [JsonSerializable(typeof(MessageDto))]
    [JsonSerializable(typeof(TextTransferRequestDto))]
    [JsonSerializable(typeof(TextTransferResponseDto))]
    [JsonSerializable(typeof(FileTransferResponseDto))]
    [JsonSerializable(typeof(List<LauncherDto>))]
    [JsonSerializable(typeof(LaunchResponseDto))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    public partial class ProjectJsonContext : JsonSerializerContext
    {
    }
}
