using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService.Entities
{
    public class ConfigInfo
    {
        public Config Config { get; set; }
        public List<Destination> Destinations { get; set; }
        public List<Source> Sources { get; set; }
    }
}
