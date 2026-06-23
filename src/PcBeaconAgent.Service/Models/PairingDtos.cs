namespace PcBeaconAgent.Service.Models;

public record PairRequest(string Pin);
public record PairResponse(string ApiKey);
public record SimpleMessageResponse(string Message);

