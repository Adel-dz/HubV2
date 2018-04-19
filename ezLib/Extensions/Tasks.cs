using System;
using System.Threading;
using System.Threading.Tasks;

namespace easyLib.Extensions
{
    public static class Tasks
    {
        public static Task OnError(this Task task , Action<Task> action)
        {
            return task.ContinueWith(action ,
                CancellationToken.None ,
                TaskContinuationOptions.OnlyOnFaulted ,
                TaskScheduler.FromCurrentSynchronizationContext());
        }
        
        public static Task OnSuccess(this Task task , Action<Task> action)
        {
            return task.ContinueWith(action ,
                CancellationToken.None ,
                TaskContinuationOptions.OnlyOnRanToCompletion ,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static Task OnSuccess<T>(this Task<T> task , Action<Task<T>> action)
        {
            return task.ContinueWith(action ,
                CancellationToken.None ,
                TaskContinuationOptions.OnlyOnRanToCompletion ,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static Task OnSuccess(this Task task , Action action)
        {
            return task.ContinueWith(t => action() ,
                CancellationToken.None ,
                TaskContinuationOptions.OnlyOnRanToCompletion ,
                TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
