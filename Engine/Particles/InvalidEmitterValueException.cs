using System;

namespace SE.Particles
{
    public class InvalidEmitterValueException : Exception
    {
        public InvalidEmitterValueException(string msg) : base(msg) { }
        public InvalidEmitterValueException(Exception inner, string msg = null) : base(msg, inner) { }
    }
}
