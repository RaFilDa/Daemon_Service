using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService.Entities
{
    public class Destination
    {
        public int Id { get; init; }
        public int ConfigID { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public string IP { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
