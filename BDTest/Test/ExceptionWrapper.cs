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
        
        [JsonProperty]
        public string TypeName { get; set; }

        private string _asString;
        [JsonProperty] 
        public string AsString {
            get
            {
                return _asString ?? $"{TypeName}: {Message}";
            }
            set => _asString = value;
        }


        [JsonConstructor]
        private ExceptionWrapper()
        {
            
        }

        public ExceptionWrapper(Exception exception)
        {
            TypeName = exception.GetType().Name;
            Message = exception.Message;
            StackTrace = exception.StackTrace;
            AsString = exception.ToString();
        }
    }
}