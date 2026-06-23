namespace PcBeaconAgent.Client.Core.Helpres
{
    public static class UrlHelpers
    {
        public static string BuildUrl(string ip, int port, string path) => $"http://{ip}:{port}/api/{path}";
    }
}
