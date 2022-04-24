using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService.Entities
{
    public class Log
    {
        public int Id { get; init; }
        public int CompConfID { get; set; }
        public DateTime Date { get; init; } = DateTime.UtcNow;
        public string Type { get; set; }
        public bool IsError { get; set; }
        public string Message { get; set; }
    }
}
