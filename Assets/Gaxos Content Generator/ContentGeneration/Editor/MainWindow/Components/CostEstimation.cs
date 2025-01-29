using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContentGeneration.Editor.MainWindow.Components
{
    public static class CostEstimation
    {
        const float minSecondsBetweenEstimations = 3;
        static DateTime lastRequestTimeStamp = DateTime.MinValue;
        static CancellationTokenSource lastCancellationTokenSource;
        public static async Task<string> WillRequestEstimation(Func<Task<string>> estimation)
        {
            if (lastCancellationTokenSource != null)
            {
                lastCancellationTokenSource.Cancel();
                lastCancellationTokenSource = null;
            }
            
            var waitTimeSpan = lastRequestTimeStamp.AddSeconds(minSecondsBetweenEstimations) - DateTime.Now;
            if (waitTimeSpan.TotalSeconds > 0)
            {
                lastCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = lastCancellationTokenSource.Token;
                try
                {
                    await Task.Delay(waitTimeSpan, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return null;
                }
                lastCancellationTokenSource = null;
            }

            lastRequestTimeStamp = DateTime.Now;
            var ret = await estimation();
            return ret;
        }
    }
}