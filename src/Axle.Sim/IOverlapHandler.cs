namespace Axle.Sim;

using Axle.Ecs;

/// <summary>
/// Handles a confirmed AABB overlap between two entities.
/// </summary>
public interface IOverlapHandler
{
    void Handle(int entityA, int entityB, CommandBuffer buffer);
}
