using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Services
{
    public class DirectoryNameService: IDirectoryNameService
    {
        public string MakeName (DateTime dt)
        {
            return dt.ToString ("yyyy-MM");
        }
    }
}
