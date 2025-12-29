using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mediamize.Model
{
    /// <summary>
    /// Download job
    /// </summary>
    public class DownloadJob
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public MediaFormat SelectedFormat { get; set; }
        public string Status { get; set; } = "En attente";
    }
}
