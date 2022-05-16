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
using System.Net.Http.Headers;

namespace RaFilDaBackupService
{
    public class HttpTools
    {
        private HttpClient _httpClient = new HttpClient(new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator });
        public HttpTools()
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Program.TOKEN);
        }

        public int GetCompConfigID(int confId)
        {
            try
            {
                string URL = Program.API_URL + "Daemon/GetCompConfByCompID&ConfID?confId=" + confId + "&compId=" + Program.ID;
                HttpResponseMessage response = _httpClient.GetAsync(URL).Result;
                Task<string> idData;
                using (HttpContent content = response.Content)
                {
                    idData = content.ReadAsStringAsync();
                }
                var configData = JsonSerializer.Deserialize<List<CompConf>>(idData.Result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var t = new GenericHttpTools<CompConf>();
                t.UpdateFile(configData, @"..\CompConf.json");
                return configData[0].Id;
            }
            catch
            {
                var t1 = new GenericHttpTools<CompConf>();
                List<CompConf> compConfs = new List<CompConf>(t1.LoadFile(@"..\CompConf.json"));
                var t2 = new GenericHttpTools<ConfigInfo>();
                List<ConfigInfo> configs = new List<ConfigInfo>(t2.LoadFile(@"..\configs.json"));

                int id = 0;
                foreach (var compConf in compConfs)
                {
                    foreach (var config in configs)
                    {
                        if(compConf.ConfigID == config.Config.Id)
                        {
                            id = compConf.Id;
                        }
                    }
                }
                return id;
            }
        }

        public int GetID()
        {
            var c = new List<Computer>();

            while(c.Count == 0)
            {
                try
                {
                    string MAC = NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(nic => nic.GetPhysicalAddress().ToString())
                    .FirstOrDefault();
                    string URL = Program.API_URL + "Computers/GetComputersByMAC/" + MAC;
                    HttpResponseMessage response = _httpClient.GetAsync(URL).Result;
                    Task<string> userData;
                    using (HttpContent content = response.Content)
                    {
                        userData = content.ReadAsStringAsync();
                    }

                    if (userData.Result == "[]")
                    {
                        var newComputer = new StringContent(JsonSerializer.Serialize(CreateComputerInfo()), Encoding.UTF8, "application/json");
                        _httpClient.PostAsync(Program.API_URL + "Computers", newComputer).Wait();

                        HttpResponseMessage newResponse = _httpClient.GetAsync(URL).Result;
                        using (HttpContent content = newResponse.Content)
                        {
                            userData = content.ReadAsStringAsync();
                        }
                    }

                    c = JsonSerializer.Deserialize<List<Computer>>(userData.Result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                }
                catch
                {
                    var t = new GenericHttpTools<CompConf>();
                    var l = new List<CompConf>(t.LoadFile(@"..\CompConf.json"));
                    if(l.Count > 0)
                        c.Add(new Computer() { Id = l[0].CompID });
                }

                if (c.Count == 0)
                    Thread.Sleep(10000);
            }
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
            c.LastSeen = DateTime.Now.ToString();
            return c;
        }

        public List<ConfigInfo> GetConfigs()
        {
            List<ConfigInfo> result = new List<ConfigInfo>();
            var t = new GenericHttpTools<ConfigInfo>();
            Task<string> inputData;

            try
            {
                string URL = Program.API_URL + "Daemon/" + Program.ID;

                HttpResponseMessage response = _httpClient.GetAsync(URL).Result;
                using (HttpContent content = response.Content)
                {
                    inputData = content.ReadAsStringAsync();
                }

                result = JsonSerializer.Deserialize<List<ConfigInfo>>(inputData.Result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true });

                t.UpdateFile(result, @"..\configs.json");
            }
            catch
            {
                result = t.LoadFile(@"..\configs.json");
            }

            return result;
        }
    }
}
