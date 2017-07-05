using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public static class EventHandlingExtensions
    {
        public static async Task RunEventHandler<T>(this IEnumerable<T> eh, Func<T, Task> action, Action<Exception> errorHandler)
        {
            if(eh == null)
            {
                throw new ArgumentNullException("eh");
            }
            if(action == null)
            {
                throw new ArgumentNullException("action");

            }
            if(errorHandler == null)
            {
                throw new ArgumentNullException("errorHandler");

            }
            foreach (var h in eh)
            {
                try
                {
                    await action(h);
                }
                catch (Exception ex)
                {
                    errorHandler(ex);
                }
            }
        }
    }
}
