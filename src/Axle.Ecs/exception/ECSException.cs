namespace Axle.Ecs;

public class InvalidEntityException : ArgumentException 
{
    public InvalidEntityException(string msg) : base(msg) {}
}

public class ComponentAbsentException : InvalidOperationException
{
    public ComponentAbsentException(string msg) : base(msg) {}
}

public class StoreNotRegisteredException : InvalidOperationException
{
    public StoreNotRegisteredException(string msg) : base(msg) {}
}