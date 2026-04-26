using OpenTK.Graphics.OpenGL4;
using Axle.Core.AxleMath;
using Axle.Core.Utility;

namespace Axle.Graphics;

public sealed class QuadRenderer : IDisposable
{
    // ── Colored pipeline ─────────────────────────────────────────────────────
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _shader;
    private readonly int _uResolutionLoc;
    private readonly int _uCameraOffsetLoc;

    private readonly float[] _batch;
    private int _batchOffset;

    private const int FloatsPerVertex = 6;   // x, y, r, g, b, a
    // Two triangles (TL-TR-BR, TL-BR-BL) → 6 verts per quad
    private const int VerticesPerQuad = 6;
    private const int FloatsPerQuad = FloatsPerVertex * VerticesPerQuad;

    // ── Textured pipeline ────────────────────────────────────────────────────
    private readonly int _texVao;
    private readonly int _texVbo;
    private readonly int _texShader;
    private readonly int _texUResolutionLoc;
    private readonly int _texUCameraOffsetLoc;
    private readonly int _texUTexLoc;

    private readonly float[] _texBatch;
    private int _texBatchOffset;
    private int _currentTexHandle = -1;

    private const int FloatsPerTexVertex = 4;   // x, y, u, v
    private const int FloatsPerTexQuad   = FloatsPerTexVertex * VerticesPerQuad;

    // ── Shared ───────────────────────────────────────────────────────────────
    private const int MaxQuads = 4096;

    private int _viewportW;
    private int _viewportH;
    private float _cameraOffsetX;
    private float _cameraOffsetY;

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

    // ── Textured shader sources ───────────────────────────────────────────────
    private const string TexVertSrc = """
        #version 330 core
        layout(location = 0) in vec2 aPosition;
        layout(location = 1) in vec2 aTexCoord;
        out vec2 vTexCoord;
        uniform vec2 uResolution;
        uniform vec2 uCameraOffset;
        void main()
        {
            vec2 ndc = ((aPosition - uCameraOffset) / uResolution) * 2.0;
            ndc.y = -ndc.y;
            gl_Position = vec4(ndc, 0.0, 1.0);
            vTexCoord = aTexCoord;
        }
        """;

    private const string TexFragSrc = """
        #version 330 core
        in  vec2 vTexCoord;
        out vec4 FragColor;
        uniform sampler2D uTex;
        void main() { FragColor = texture(uTex, vTexCoord); }
        """;

    public QuadRenderer()
    {
        // ── Colored pipeline ─────────────────────────────────────────────────
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

        // ── Textured pipeline ─────────────────────────────────────────────────
        _texBatch = new float[MaxQuads * FloatsPerTexQuad];

        int texVert = CompileShader(ShaderType.VertexShader,   TexVertSrc);
        int texFrag = CompileShader(ShaderType.FragmentShader, TexFragSrc);

        _texShader = GL.CreateProgram();
        GL.AttachShader(_texShader, texVert);
        GL.AttachShader(_texShader, texFrag);
        GL.LinkProgram(_texShader);
        GL.GetProgram(_texShader, GetProgramParameterName.LinkStatus, out int texLinked);
        if (texLinked == 0)
            throw new InvalidOperationException(
                $"Textured shader link error:\n{GL.GetProgramInfoLog(_texShader)}");
        GL.DeleteShader(texVert);
        GL.DeleteShader(texFrag);

        _texUResolutionLoc   = GL.GetUniformLocation(_texShader, "uResolution");
        _texUCameraOffsetLoc = GL.GetUniformLocation(_texShader, "uCameraOffset");
        _texUTexLoc          = GL.GetUniformLocation(_texShader, "uTex");

        _texVao = GL.GenVertexArray();
        _texVbo = GL.GenBuffer();

        GL.BindVertexArray(_texVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _texVbo);

        GL.BufferData(BufferTarget.ArrayBuffer,
            MaxQuads * FloatsPerTexQuad * sizeof(float),
            IntPtr.Zero,
            BufferUsageHint.DynamicDraw);

        int texStride = FloatsPerTexVertex * sizeof(float);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, texStride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, texStride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    public void Begin(Camera camera)
    {
        _batchOffset      = 0;
        _texBatchOffset   = 0;
        _currentTexHandle = -1;
        _viewportW        = camera.ViewportWidth;
        _viewportH        = camera.ViewportHeight;
        // Subtract the camera world centre so that world position (WorldX, WorldY)
        // maps to NDC (0,0) = screen centre. When WorldX/Y = 0 this is a no-op,
        // preserving the original convention where world origin is screen centre.
        _cameraOffsetX    = camera.WorldX;
        _cameraOffsetY    = camera.WorldY;
    }

    /// <summary>
    /// Enqueues a filled rectangle. (x, y) is the top-left corner in pixel space.
    /// Flushes any pending textured quads first to preserve draw order.
    /// </summary>
    public void DrawQuad(float x, float y, Vector2f size, Color4 color)
    {
        if (_texBatchOffset > 0)
            FlushTextured();

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

    /// <summary>
    /// Enqueues a textured rectangle. (x, y) is the top-left corner in pixel space.
    /// Flushes any pending colored quads first to preserve draw order.
    /// Automatically flushes and re-binds when the texture changes.
    /// </summary>
    public void DrawTexturedQuad(float x, float y, Vector2f size, Texture2D texture, bool flipX = false)
    {
        if (_batchOffset > 0)
            Flush();

        // Texture switch mid-batch: flush what we have, then switch
        if (_currentTexHandle != texture.Handle && _texBatchOffset > 0)
            FlushTextured();

        if (_texBatchOffset + FloatsPerTexQuad > _texBatch.Length)
            FlushTextured();

        _currentTexHandle = texture.Handle;

        float x1 = x,          y1 = y;
        float x2 = x + size.X, y2 = y + size.Y;

        // U coordinates are swapped when flipX is true to mirror the texture horizontally.
        float u0 = flipX ? 1f : 0f;
        float u1 = flipX ? 0f : 1f;

        // The vertex shader negates Y (ndc.y = -ndc.y), so y1 is the visual TOP of the
        // quad. StbImageSharp stores row 0 (top of image) first; OpenGL places the first
        // bytes at v=0. Therefore v=0 must map to y1 (top) and v=1 to y2 (bottom).
        // Triangle 1: TL → TR → BR
        EmitTex(x1, y1, u0, 0f);
        EmitTex(x2, y1, u1, 0f);
        EmitTex(x2, y2, u1, 1f);
        // Triangle 2: TL → BR → BL
        EmitTex(x1, y1, u0, 0f);
        EmitTex(x2, y2, u1, 1f);
        EmitTex(x1, y2, u0, 1f);
    }

    /// <summary>Submits all pending quads (colored and textured) to the GPU.</summary>
    public void End()
    {
        Flush();
        FlushTextured();
    }

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

    private void FlushTextured()
    {
        if (_texBatchOffset == 0) return;

        int vertexCount = _texBatchOffset / FloatsPerTexVertex;

        GL.UseProgram(_texShader);
        GL.Uniform2(_texUResolutionLoc,   (float)_viewportW, (float)_viewportH);
        GL.Uniform2(_texUCameraOffsetLoc, _cameraOffsetX,   _cameraOffsetY);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _currentTexHandle);
        GL.Uniform1(_texUTexLoc, 0);

        GL.BindVertexArray(_texVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _texVbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer,
            IntPtr.Zero,
            _texBatchOffset * sizeof(float),
            _texBatch);

        GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
        GL.BindVertexArray(0);

        _texBatchOffset = 0;
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

    private void EmitTex(float x, float y, float u, float v)
    {
        _texBatch[_texBatchOffset++] = x;
        _texBatch[_texBatchOffset++] = y;
        _texBatch[_texBatchOffset++] = u;
        _texBatch[_texBatchOffset++] = v;
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
        GL.DeleteVertexArray(_texVao);
        GL.DeleteBuffer(_texVbo);
        GL.DeleteProgram(_texShader);
    }
}