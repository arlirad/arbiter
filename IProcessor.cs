using System;

namespace Arbiter {
    [System.AttributeUsage(System.AttributeTargets.Class)]  
    public class NameAttribute : System.Attribute {  
        public string Name;  
    
        public NameAttribute(string name) {  
            Name = name;  
        }  
    }  

    public interface IProcessor {
        void Process(Stream stream, Request request, Response response);
    }
}