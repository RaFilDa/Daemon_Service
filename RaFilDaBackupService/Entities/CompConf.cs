using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService.Entities
{
    public class CompConf
    {
        public int Id { get; set; }
        public int ConfigID { get; set; }
        public int CompID { get; set; }
        public bool Updated { get; set; }
    }
}
