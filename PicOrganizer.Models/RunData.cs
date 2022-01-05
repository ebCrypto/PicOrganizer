using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Models
{
    public class RunData
    {
        public DirectoryData[] Directories { get; set;  }

    }

    public class DirectoryData
    {
        public string Name { get; set; }
        public DateTime MostRecentMedia { get; set; }
    }
}
