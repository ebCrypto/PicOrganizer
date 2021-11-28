using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Models
{
    public class ReportSummary
    {
        public int CountFiles { get; set; }
        public int  CountDateTime { get; set; }
        public int CountLatitude { get; set; }
        public int CountLongitude { get; set; }
    }
}
