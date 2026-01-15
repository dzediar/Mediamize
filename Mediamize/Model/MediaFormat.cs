using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Mediamize.Model
{
    /// <summary>
    /// Media format
    /// </summary>
    public class MediaFormat
    {
        public string Id { get; set; }
        public string Ext { get; set; }
        public string Resolution { get; set; }
        public string Note { get; set; } // Codec info, bitrate, etc.
        public bool IsAudio { get; set; } // Pour le Grouping
        public string FormatType => IsAudio ? $"AUDIO" : "VIDEO";
        public string Format => IsAudio ? $"Audio - {Ext}" : $"Video - {Resolution} {Ext}";
        public string Description => string.Join(" ", Note.Split(' ', '\t').Select(x => x.Trim()).Where(x => x != ""));
        public string DisplayName => $"{Format} ({Description})";
    }
}
