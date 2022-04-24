using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace RaFilDaBackupService
{
    public class GenericHttpTools<T>
    {
        public List<T> LoadFile(string path)
        {
            List<T> dataInFile = new List<T>();

            if (!File.Exists(path))
            {
                File.Create(path).Close();
                return dataInFile;
            }

            StreamReader sr = new StreamReader(path);
            string data = sr.ReadToEnd();
            sr.Close();

            if(data != "")
                dataInFile = JsonSerializer.Deserialize<List<T>>(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return dataInFile;
        }

        public void UpdateFile(List<T> allConfigs, string path)
        {
            if (!File.Exists(path))
            {
                File.Create(path).Close();
                return;
            }

            string jsonConfigs = "";

            jsonConfigs += JsonSerializer.Serialize(allConfigs, new JsonSerializerOptions { WriteIndented = true });

            StreamWriter sw = new StreamWriter(path);
            sw.WriteLine(jsonConfigs);
            sw.Close();
        }
    }
}
