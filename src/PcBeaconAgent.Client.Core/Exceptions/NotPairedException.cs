using System;

namespace PcBeaconAgent.Client.Core.Exceptions
{
    public class NotPairedException : Exception
    {
        public NotPairedException() : base("The device is not paired with the server.") { }
    }
}
