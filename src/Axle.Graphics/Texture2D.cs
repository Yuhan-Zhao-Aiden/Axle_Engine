using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace Axle.Graphics;

public sealed class Texture2D : IDisposable
{
    /// <summary>OpenGL texture handle.</summary>
    public int Handle { get; }

    public int Width  { get; }
    public int Height { get; }

    private Texture2D(int handle, int width, int height)
    {
        Handle = handle;
        Width = width;
        Height = height;
    }


    public static Texture2D Load(string absolutePath, int requiredSize = 32)
    {
        ImageResult image;
        using (var stream = File.OpenRead(absolutePath))
            image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        if (image.Width != requiredSize || image.Height != requiredSize)
            throw new InvalidOperationException(
                $"Tile sprite must be {requiredSize}x{requiredSize}, " +
                $"got {image.Width}x{image.Height}: {absolutePath}");

        int handle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, handle);

        GL.TexImage2D(
            TextureTarget.Texture2D, 0,
            PixelInternalFormat.Rgba,
            image.Width, image.Height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte,
            image.Data);

        // Nearest-neighbor: crisp pixel art, no blur
        GL.TexParameter(TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        // Clamp-to-edge: no wrap-around artefacts at tile borders
        GL.TexParameter(TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0);

        return new Texture2D(handle, image.Width, image.Height);
    }

    public void Dispose() => GL.DeleteTexture(Handle);
}
