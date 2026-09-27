using System.Runtime.CompilerServices;
using System.Text;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Api.Services;

/// <summary>One picture the eye drew, as both answers the read gives: the image, and the text that says where
/// the eye stood and what fills the frame.</summary>
internal sealed record EyeShot(byte[] Png, string Text);

/// <summary>
/// The eye's scenes and pictures, kept for as long as the world they were drawn from is kept.
///
/// <para>A world is held by <see cref="BuiltWorlds"/> and let go when it falls out of it, so everything here is
/// keyed on the world instance and goes with it: a board that changes builds a new world, which starts with
/// no pictures. Per world there is a scene for each of the two ways of drawing it, and the last
/// <see cref="PicturesPerWorld"/> pictures asked of it.</para>
///
/// <para><b>One picture is drawn at a time.</b> A picture is seconds of every core, and a gallery asks for a
/// dozen at once; drawn together they would take as long and hold the server for all of it.</para>
/// </summary>
internal static class EyeRenders
{
    private const int PicturesPerWorld = 24;

    private static readonly ConditionalWeakTable<BuiltWorld, Held> Worlds = new();
    private static readonly SemaphoreSlim Drawing = new(1, 1);

    private sealed class Held(BlockTextureSet textures)
    {
        public BlockTextureSet Textures { get; } = textures;
        public EyeScene?[] Scenes { get; } = new EyeScene?[2];
        public Remembered<EyeShot?> Shots { get; } = new(PicturesPerWorld);
    }

    /// <summary>Waits for the turn to draw, which the returned handle gives back when disposed.</summary>
    public static async Task<IDisposable> TurnAsync(CancellationToken ct)
    {
        await Drawing.WaitAsync(ct);
        return new Turn();
    }

    private sealed class Turn : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) Drawing.Release();
        }
    }

    /// <summary>The picture <paramref name="asked"/> names over <paramref name="world"/> — the one already drawn
    /// where it has been asked before, else <paramref name="draw"/>'s over the world's scene. Null where the
    /// picture has nothing to draw, which is remembered the same way.</summary>
    public static EyeShot? Of(BuiltWorld world, BlockTextureSet textures, bool flat, string asked,
                              Func<EyeScene, EyeShot?> draw)
    {
        if (!Worlds.TryGetValue(world, out var held) || !ReferenceEquals(held.Textures, textures))
        {
            held = new Held(textures);
            Worlds.AddOrUpdate(world, held);
        }
        var slot = flat ? 1 : 0;
        return held.Shots.Of(Encoding.UTF8.GetBytes(asked),
            () => draw(held.Scenes[slot] ??= EyeScene.Of(world.World, textures, flat)));
    }
}
