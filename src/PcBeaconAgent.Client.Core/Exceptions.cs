using System;

namespace PcBeaconAgent.Client.Core
{
    public class NotPairedException : Exception
    {
        public NotPairedException() : base("The device is not paired with the server.") { }
    }
}
