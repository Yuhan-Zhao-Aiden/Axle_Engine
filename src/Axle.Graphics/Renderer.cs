using OpenTK.Graphics.OpenGL4;
using Axle.Core.AxleMath;
using Axle.Core.Utility;

namespace Axle.Graphics;

public sealed class QuadRenderer : IDisposable
{
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _shader;
    private readonly int _uResolutionLoc;
    private readonly int _uCameraOffsetLoc;

    private readonly float[] _batch;
    private int _batchOffset;   
    private int _viewportW;
    private int _viewportH;
    private float _cameraOffsetX;
    private float _cameraOffsetY;

    private const int FloatsPerVertex = 6;
    // Two triangles (TL-TR-BR, TL-BR-BL) → 6 verts per quad
    private const int VerticesPerQuad = 6;
    private const int FloatsPerQuad = FloatsPerVertex * VerticesPerQuad;
    private const int MaxQuads = 4096;

    private const string VertSrc = """
        #version 330 core
        layout(location = 0) in vec2 aPosition;
        layout(location = 1) in vec4 aColor;
        out vec4 vColor;
        uniform vec2 uResolution;
        uniform vec2 uCameraOffset;
        void main()
        {
            vec2 ndc = ((aPosition - uCameraOffset) / uResolution) * 2.0;
            ndc.y = -ndc.y;
            gl_Position = vec4(ndc, 0.0, 1.0);
            vColor = aColor;
        }
        """;

    private const string FragSrc = """
        #version 330 core
        in  vec4 vColor;
        out vec4 FragColor;
        void main() { FragColor = vColor; }
        """;

    public QuadRenderer()
    {
        _batch = new float[MaxQuads * FloatsPerQuad];

        int vert = CompileShader(ShaderType.VertexShader, VertSrc);
        int frag = CompileShader(ShaderType.FragmentShader, FragSrc);

        _shader = GL.CreateProgram();
        GL.AttachShader(_shader, vert);
        GL.AttachShader(_shader, frag);
        GL.LinkProgram(_shader);
        GL.GetProgram(_shader, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
            throw new InvalidOperationException(
                $"Shader link error:\n{GL.GetProgramInfoLog(_shader)}");
        GL.DeleteShader(vert);
        GL.DeleteShader(frag);

        _uResolutionLoc    = GL.GetUniformLocation(_shader, "uResolution");
        _uCameraOffsetLoc  = GL.GetUniformLocation(_shader, "uCameraOffset");

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

        GL.BufferData(BufferTarget.ArrayBuffer,
            MaxQuads * FloatsPerQuad * sizeof(float),
            IntPtr.Zero,
            BufferUsageHint.DynamicDraw);

        int stride = FloatsPerVertex * sizeof(float);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    public void Begin(Camera camera)
    {
        _batchOffset    = 0;
        _viewportW      = camera.ViewportWidth;
        _viewportH      = camera.ViewportHeight;
        // Subtract the camera world centre so that world position (WorldX, WorldY)
        // maps to NDC (0,0) = screen centre. When WorldX/Y = 0 this is a no-op,
        // preserving the original convention where world origin is screen centre.
        _cameraOffsetX  = camera.WorldX;
        _cameraOffsetY  = camera.WorldY;
    }

    /// <summary>
    /// Enqueues a filled rectangle. (x, y) is the top-left corner in pixel space.
    /// </summary>
    public void DrawQuad(float x, float y, Vector2f size, Color4 color)
    {
        if (_batchOffset + FloatsPerQuad > _batch.Length)
            Flush(); 

        float x1 = x, y1 = y;
        float x2 = x + size.X, y2 = y + size.Y;
        float r = color.R, g = color.G, b = color.B, a = color.A;

        // Triangle 1: TL → TR → BR
        Emit(x1, y1, r, g, b, a);
        Emit(x2, y1, r, g, b, a);
        Emit(x2, y2, r, g, b, a);
        // Triangle 2: TL → BR → BL
        Emit(x1, y1, r, g, b, a);
        Emit(x2, y2, r, g, b, a);
        Emit(x1, y2, r, g, b, a);
    }

    public void End() => Flush();

    private void Flush()
    {
        if (_batchOffset == 0) return;

        int vertexCount = _batchOffset / FloatsPerVertex;

        GL.UseProgram(_shader);
        GL.Uniform2(_uResolutionLoc,   (float)_viewportW, (float)_viewportH);
        GL.Uniform2(_uCameraOffsetLoc, _cameraOffsetX,   _cameraOffsetY);

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer,
            IntPtr.Zero,
            _batchOffset * sizeof(float),
            _batch);

        GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
        GL.BindVertexArray(0);

        _batchOffset = 0;
    }

    private void Emit(float x, float y, float r, float g, float b, float a)
    {
        _batch[_batchOffset++] = x;
        _batch[_batchOffset++] = y;
        _batch[_batchOffset++] = r;
        _batch[_batchOffset++] = g;
        _batch[_batchOffset++] = b;
        _batch[_batchOffset++] = a;
    }

    private static int CompileShader(ShaderType type, string src)
    {
        int sh = GL.CreateShader(type);
        GL.ShaderSource(sh, src);
        GL.CompileShader(sh);
        GL.GetShader(sh, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException(
                $"Shader compile error ({type}):\n{GL.GetShaderInfoLog(sh)}");
        return sh;
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        GL.DeleteProgram(_shader);
    }
}