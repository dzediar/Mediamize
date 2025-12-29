using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using zComp.Core.Model;
using System.IO;

namespace Mediamize.Model
{
    /// <summary>
    /// Mediamize local config
    /// </summary>
    [DataContract]
    public class MMLocalConfiguration : LocalConfiguration
    {
        [DataMember]
        public string YtDlpPath { get; set; } = "";
        
        [DataMember]
        public string FfmpegPath { get; set; } = "";

        [DataMember]
        public string DenoPath { get; set; } = "";        

        [DataMember] 
        public string OutputPath { get; set; } = "";

        [DataMember] 
        public bool AddMetadata { get; set; } = true;

        [DataMember]
        public bool RemoveSpecialChars { get; set; } = true;

        [DataMember] 
        public string LastURL { get; set; } = null;

        public bool IsValid() => 
            !string.IsNullOrEmpty(YtDlpPath) &&
            !string.IsNullOrEmpty(DenoPath) &&
            !string.IsNullOrEmpty(FfmpegPath) &&
            !string.IsNullOrEmpty(OutputPath) &&
            File.Exists(YtDlpPath) &&
            File.Exists(FfmpegPath) &&
            File.Exists(DenoPath) &&
            Directory.Exists(OutputPath);
    }
}
