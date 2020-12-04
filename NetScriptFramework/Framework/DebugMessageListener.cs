using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetScriptFramework
{
    /// <summary>
    /// A debug message listener.
    /// </summary>
    public abstract class DebugMessageListener
    {
        /// <summary>
        /// Called when message is received.
        /// </summary>
        /// <param name="sender">The sender plugin.</param>
        /// <param name="message">The message.</param>
        public abstract void OnMessage(Plugin sender, string message);
    }
}
