using System.Text.Json.Serialization;
using PcBeaconAgent.Service.Endpoints;

namespace PcBeaconAgent.Service.JsonContext;

[JsonSerializable(typeof(PairRequest))]
[JsonSerializable(typeof(PairResponse))]
[JsonSerializable(typeof(SimpleMessageResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}