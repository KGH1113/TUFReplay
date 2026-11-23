using System;
using UnityEngine;

namespace TUFReplay.Shared.Unity;

internal static class SpritePngEncoder
{
  /// <summary>
  /// Copies a sprite into a PNG. Game textures are not CPU-readable, so the texture is drawn into a
  /// temporary render texture and read back once. Must run on the main thread.
  /// </summary>
  public static byte[] Encode(Sprite sprite)
  {
    if (
      sprite.packed
      && (sprite.packingMode != SpritePackingMode.Rectangle || sprite.packingRotation != SpritePackingRotation.None)
    )
      throw new NotSupportedException("Sprites packed tightly or rotated into an atlas cannot be copied.");

    Texture2D texture = sprite.texture;
    // An unpacked sprite's textureRect is trimmed to its mesh; rect keeps the transparent margin.
    Rect rect = sprite.packed ? sprite.textureRect : sprite.rect;
    int width = Mathf.RoundToInt(rect.width);
    int height = Mathf.RoundToInt(rect.height);
    RenderTexture target = RenderTexture.GetTemporary(
      texture.width,
      texture.height,
      0,
      RenderTextureFormat.ARGB32,
      RenderTextureReadWrite.Default
    );
    RenderTexture previous = RenderTexture.active;
    var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
    try
    {
      Graphics.Blit(texture, target);
      RenderTexture.active = target;
      copy.ReadPixels(new Rect(Mathf.Round(rect.x), Mathf.Round(rect.y), width, height), 0, 0, false);
      copy.Apply(false);
      return copy.EncodeToPNG();
    }
    finally
    {
      RenderTexture.active = previous;
      RenderTexture.ReleaseTemporary(target);
      UnityEngine.Object.Destroy(copy);
    }
  }
}
