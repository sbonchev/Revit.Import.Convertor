﻿
using System.Windows.Threading;

namespace Revit.Import.Convertor.UI.BL
{
    internal static class ProcessDispatcher
    {
        internal static void Execute(Action act, DispatcherPriority priority = DispatcherPriority.Background)
        {
            var dispatcher = MyApp.AppWindow?.Dispatcher;
            if (dispatcher is null)
                return;

            // ---Marshall to Main Thread:
            dispatcher.Invoke(act, priority);
        }
    }
}
