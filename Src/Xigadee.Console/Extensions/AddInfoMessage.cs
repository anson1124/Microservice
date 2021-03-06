﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xigadee
{
    public static partial class ConsoleExtensionMethods     
    {
        /// <summary>
        /// This method can be called by an external process to update the info messages displayed in the menu.
        /// </summary>
        /// <param name="menu">The menu.</param>
        /// <param name="message">The info message</param>
        /// <param name="refresh">The refresh option flag.</param>
        /// <param name="type">The log type.</param>
        public static ConsoleMenu AddInfoMessage(this ConsoleMenu menu, string message, bool refresh = false, EventLogEntryType type = EventLogEntryType.Information)
        {
            menu.ContextInfo.Add(new ErrorInfo() { Type = type, Message = message });

            if (refresh)
                menu.Refresh();

            return menu;
        }
    }
}
