using Serilog;
using Serilog.Core;

namespace Syndical.Library
{
    /// <summary>
    /// Globals (Logger, etc.)
    /// </summary>
    public class Globals
    {
        /// <summary>
        /// Global logger
        /// </summary>
        public static Logger Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }
}