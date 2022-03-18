using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Tests
{
    public class VerifiableLogger<T> : ILogger<T>//Replace T with your type
    {
        public int CalledCount { get; set; }
        public int ExceptionCalledCount { get; set; }

        //boiler plate, required to implement ILogger<T>
        IDisposable ILogger.BeginScope<TState>(TState state) => throw new NotImplementedException();
        bool ILogger.IsEnabled(LogLevel logLevel) => throw new NotImplementedException();

        //Meaningful method, this get's called when you use .LogInformation()
        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            this.CalledCount++;
            if (exception != null)
                ExceptionCalledCount++;
        }
    }
}
