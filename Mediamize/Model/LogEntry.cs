using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Mediamize.Model
{
    /// <summary>
    /// Log entry
    /// </summary>
    /// <param name="Text"></param>
    /// <param name="Color"></param>
    public record LogEntry(string Text, Brush Color);
}
