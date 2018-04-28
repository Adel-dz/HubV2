using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace easyLib.Extensions
{
    /*
     * Version: 1
     */
    public static class Tasks
    {
        public static Task OnError(this Task task , Action<Task> action)
        {
            Assert(task != null);
            Assert(action != null);

            return task.ContinueWith(action ,
                CancellationToken.None ,
                TaskContinuationOptions.OnlyOnFaulted ,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static Task OnSuccess(this Task task , Action<Task> action)
        {
            Assert(task != null);
            Assert(action != null);

            return task.ContinueWith(action ,
                CancellationToken.None ,
                TaskContinuationOptions.OnlyOnRanToCompletion ,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static Task OnSuccess<T>(this Task<T> task , Action<Task<T>> action)
        {
            Assert(task != null);
            Assert(action != null);

            return task.ContinueWith(action ,
                CancellationToken.None ,
                TaskContinuationOptions.OnlyOnRanToCompletion ,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static Task OnSuccess(this Task task , Action action)
        {
            Assert(task != null);
            Assert(action != null);

            return task.ContinueWith(t => action() ,
                CancellationToken.None ,
                TaskContinuationOptions.OnlyOnRanToCompletion ,
                TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
