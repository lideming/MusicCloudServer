using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace MCloudServer
{
    [Table("file")]
    public class StoredFile
    {
        [Key]
        public int id { get; set; }

        public long size { get; set; }

        public string path { get; set; }
    }
}
