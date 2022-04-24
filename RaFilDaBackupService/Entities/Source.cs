using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaFilDaBackupService.Entities
{
    public class Source
    {
        public int Id { get; init; }
        public int ConfigID { get; set; }
        public string Path { get; set; }
    }
}
