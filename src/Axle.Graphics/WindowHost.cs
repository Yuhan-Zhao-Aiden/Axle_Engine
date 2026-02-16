using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;

namespace Axle.Graphics;

public class WindowHost : GameWindow 
{
    private double _accum;
    private int _frames;

    public WindowHost(int width = 1280, int height = 720, string title = "Axle Engine")
    : base(GameWindowSettings.Default, new NativeWindowSettings
    {
        ClientSize = new Vector2i(width, height),
        Title = title
    })
    {
        VSync = VSyncMode.Off;
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.ClearColor(0.08f, 0.09f, 0.10f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        SwapBuffers();

        _accum += args.Time;
        _frames++;

        if (_accum >= 1.0)
        {
            var fps = _frames/_accum;
            Title = $"FPS: {fps:F1}";
            _accum = 0;
            _frames = 0;
        } 
    }
}
