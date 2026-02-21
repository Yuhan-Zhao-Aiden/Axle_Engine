namespace Axle.Core;

public class ComponentAbsentException : Exception
{
    public ComponentAbsentException(string msg) : base(msg) { }
}