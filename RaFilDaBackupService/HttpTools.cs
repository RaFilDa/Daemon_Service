using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using RaFilDaBackupService.Entities;

namespace RaFilDaBackupService
{
    public class HttpTools
    {
        public HttpClientHandler handler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };

        public int GetCompConfigID(int confId)
        {
            var httpClient = new HttpClient(handler);
            string URL = Program.API_URL + "Daemon/GetCompConfByCompID&ConfID?confId=" + confId + "&compId=" + GetID();
            HttpResponseMessage response = httpClient.GetAsync(URL).Result;
            Task<string> idData;
            using (HttpContent content = response.Content)
            {
                idData = content.ReadAsStringAsync();
            }
            var configData = JsonSerializer.Deserialize<List<Computer>>(idData.Result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return configData[0].Id;
        }

        public int GetID()
        {
            var httpClient = new HttpClient(handler);
            string MAC = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault();
            string URL = Program.API_URL + "Computers/GetComputersByMAC/" + MAC;
            HttpResponseMessage response = httpClient.GetAsync(URL).Result;
            Task<string> userData;
            using (HttpContent content = response.Content)
            {
                userData = content.ReadAsStringAsync();
            }

            if (userData.Result == "[]")
            {
                var newComputer = new StringContent(JsonSerializer.Serialize(CreateComputerInfo()), Encoding.UTF8, "application/json");
                httpClient.PostAsync(Program.API_URL + "Computers", newComputer);

                HttpResponseMessage newResponse = httpClient.GetAsync(URL).Result;
                using (HttpContent content = newResponse.Content)
                {
                    userData = content.ReadAsStringAsync();
                }
            }

            var c = JsonSerializer.Deserialize<List<Computer>>(userData.Result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return c[0].Id;
        }

        public Computer CreateComputerInfo()
        {
            var c = new Computer();
            c.Name = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            c.MAC = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault();
            string externalIpString = new WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim();
            var externalIp = IPAddress.Parse(externalIpString);
            c.IP = externalIp.ToString();
            c.LastSeen = DateTime.Now;
            return c;
        }

        public List<ConfigInfo> GetConfigs()
        {
            List<ConfigInfo> result = new List<ConfigInfo>();
            var t = new GenericHttpTools<ConfigInfo>();
            Task<string> inputData;

            while(result.Count == 0)
            {
                try
                {
                    int ID = GetID();

                    var httpClient = new HttpClient(handler);

                    string URL = Program.API_URL + "Daemon/" + ID;

                    HttpResponseMessage response = httpClient.GetAsync(URL).Result;
                    using (HttpContent content = response.Content)
                    {
                        inputData = content.ReadAsStringAsync();
                    }

                    result = JsonSerializer.Deserialize<List<ConfigInfo>>(inputData.Result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    t.UpdateFile(result, @"..\configs.json");
                }
                catch
                {
                    result = new List<ConfigInfo>(t.LoadFile(@"..\configs.json"));
                }

                if (result.Count == 0)
                    Thread.Sleep(10000);
            }

            return result;
        }
    }
}
