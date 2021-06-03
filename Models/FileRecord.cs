using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace MCloudServer
{
    public class FileRecord
    {
        [Key]
        public int id { get; set; }

        public int size { get; set; }

        public string path { get; set; }
    }
}
