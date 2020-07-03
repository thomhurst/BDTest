using System;
using Newtonsoft.Json;

namespace BDTest.Test
{
    public class ExceptionWrapper
    {
        [JsonProperty]
        public string Message { get; set; }
        [JsonProperty]
        public string StackTrace { get; set; }

        [JsonConstructor]
        private ExceptionWrapper()
        {
            
        }
        
        public ExceptionWrapper(Exception exception)
        {
            Message = exception.Message;
            StackTrace = exception.StackTrace;
        }
    }
}