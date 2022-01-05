using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Services
{
    public class RunDataService : IRunDataService
    {
        private readonly ILogger<RunDataService> logger;
        private readonly AppSettings appSettings;
        public DirectoryData DirectoryData { get; set; }

        public RunDataService(ILogger<RunDataService> logger, AppSettings appSettings  )
        {
            this.logger = logger;
            this.appSettings = appSettings;
        }
    }
}
