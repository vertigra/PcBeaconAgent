using PcBeaconAgent.Service.Configuration;
using PcBeaconAgent.Service.Interfaces;
using System;
using System.IO;

namespace PcBeaconAgent.Service.Services
{
    public class BeaconAnnouncementService : IBeaconAnnouncementService
    {
        public string ApiKey { get; }

        public BeaconAnnouncementService(AppSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.Server.ApiKey))
            {
                ApiKey = settings.Server.ApiKey;
            }
            else
            {
                string keyPath = "server.key";
                if (File.Exists(keyPath))
                {
                    ApiKey = File.ReadAllText(keyPath);
                }
                else
                {
                    ApiKey = Guid.NewGuid().ToString("N");
                    File.WriteAllText(keyPath, ApiKey);
                }
            }
        }
    }
}
