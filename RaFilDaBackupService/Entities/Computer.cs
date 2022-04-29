using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService.Entities
{
    public class Computer
    {
        public int Id { get; init; }
        public string Name { get; set; }
        public string MAC { get; set; }
        public string IP { get; set; }
        public string LastSeen { get; set; }
    }
}
