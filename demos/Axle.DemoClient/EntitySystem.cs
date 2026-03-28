namespace Axle.System;

using Axle.Ecs;
using Axle.Core.AxleMath;
using Axle.Core.Utility;

public class EntitySystem
{
    private readonly CommandBuffer.Writer _writer;

    public EntitySystem(CommandBuffer.Writer writer)
    {
        _writer = writer;
    }

    public void CreateScene()
    {
        Target a = Target.CreateTemp(_writer.RecordCreateEntity());
        _writer.RecordAddComponent<Transform>(a, new(new Vector2f(0f, 0f)));
        _writer.RecordAddComponent<RenderRect>(a, new(
            new Vector2f(32f, 32f),
            new Color4(255, 255, 255)
        ));

        Target b = Target.CreateTemp(_writer.RecordCreateEntity());
        _writer.RecordAddComponent<Transform>(b, new(new Vector2f(32f, 0f)));
        _writer.RecordAddComponent<RenderRect>(b, new(
            new Vector2f(32f, 32f),
            new Color4(255, 0, 0, 0.5f)
        ));

        Target c = Target.CreateTemp(_writer.RecordCreateEntity());
        _writer.RecordAddComponent<Transform>(c, new(new Vector2f(-32f, 0f)));
        _writer.RecordAddComponent<RenderRect>(c, new(
            new Vector2f(32f, 32f),
            new Color4(0, 255, 0, 0.5f)
        ));

        Target d = Target.CreateTemp(_writer.RecordCreateEntity());
        _writer.RecordAddComponent<Transform>(d, new(new Vector2f(0f, 32f)));
        _writer.RecordAddComponent<RenderRect>(d, new(
            new Vector2f(32f, 32f),
            new Color4(0, 0, 255, 0.5f)
        ));


        Target e = Target.CreateTemp(_writer.RecordCreateEntity());
        _writer.RecordAddComponent<Transform>(e, new(new Vector2f(0f, -32f)));
        _writer.RecordAddComponent<RenderRect>(e, new(
            new Vector2f(32f, 32f),
            new Color4(0, 255, 255, 0.5f)
        ));
    }
}