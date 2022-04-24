using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService
{
    public class Config
    {
        public int Id { get; init; }
        public string Name { get; set; }
        public int UserID { get; set; }
        public int RetentionSize { get; set; }
        public string BackupFrequency { get; set; }
        public string Cron { get; set; }
        public string TimeZone { get; set; }
        public int PackageSize { get; set; }
        public int BackupType { get; set; }
        public bool FileType { get; set; }
    }
}
