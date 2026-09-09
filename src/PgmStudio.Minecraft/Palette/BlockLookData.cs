namespace PgmStudio.Minecraft.Palette;

/// <summary>
/// What a block <b>looks like</b> beyond the one colour it averages to — the measured table behind
/// <see cref="BlockLook"/>.
///
/// <para><b>Where it comes from.</b> Each row is measured off that block's own 1.8 texture, on the face it
/// shows: <c>contrast</c> is the standard deviation of luma over the opaque pixels, <c>colours</c> the count
/// of distinct RGB values in the sprite's 256, and the flags name how the sprite is constructed. A block
/// whose top and sides are one sprite answers the same on both; one whose faces differ answers differently,
/// which is the whole reason both are here — a terrain theme paints the surface bucket on the top face and
/// the wall bucket on the side, so two blocks can be interchangeable in one and distinct in the other.</para>
///
/// <para><b>Why it is a table and not a computation.</b> The textures are Mojang's and are not in this
/// repository, so nothing here can re-derive these numbers — which is exactly what makes them a result worth
/// committing, the same division <see cref="BlockPaletteData"/>'s mean colours already sit under.</para>
/// </summary>
internal static class BlockLookData
{
    /// <summary>A block's look on one face: the sprite it shows there and what that sprite reads as.</summary>
    internal readonly record struct Face(string Texture, double Contrast, int Colours, string[] Construction);

    /// <summary>Keyed <c>(id &lt;&lt; 4) | data</c>, the same key <see cref="BlockPalette"/> uses.</summary>
    internal static readonly Dictionary<int, (Face Top, Face Side)> ByBlock = new()
    {
        [(1 << 4) | 0] = (new("stone", 11.8, 5, ["ramped"]),
            new("stone", 11.8, 5, ["ramped"])),   // one sprite on every face   // Stone
        [(1 << 4) | 1] = (new("stone_granite", 35.4, 38, []),
            new("stone_granite", 35.4, 38, [])),   // one sprite on every face   // Granite
        [(1 << 4) | 2] = (new("stone_granite_smooth", 24.5, 36, ["bevelled"]),
            new("stone_granite_smooth", 24.5, 36, ["bevelled"])),   // one sprite on every face   // Polished Granite
        [(1 << 4) | 3] = (new("stone_diorite", 35.7, 85, []),
            new("stone_diorite", 35.7, 85, [])),   // one sprite on every face   // Diorite
        [(1 << 4) | 4] = (new("stone_diorite_smooth", 36.3, 73, ["bevelled"]),
            new("stone_diorite_smooth", 36.3, 73, ["bevelled"])),   // one sprite on every face   // Polished Diorite
        [(1 << 4) | 5] = (new("stone_andesite", 23.5, 213, ["dithered"]),
            new("stone_andesite", 23.5, 213, ["dithered"])),   // one sprite on every face   // Andesite
        [(1 << 4) | 6] = (new("stone_andesite_smooth", 20.4, 189, ["bevelled", "dithered"]),
            new("stone_andesite_smooth", 20.4, 189, ["bevelled", "dithered"])),   // one sprite on every face   // Polished Andesite
        [(2 << 4) | 0] = (new("grass_top", 17.7, 66, []),
            new("grass_side", 27.9, 40, [])),   // Grass Block
        [(3 << 4) | 0] = (new("dirt", 23.1, 7, ["ramped"]),
            new("dirt", 23.1, 7, ["ramped"])),   // one sprite on every face   // Dirt
        [(3 << 4) | 1] = (new("coarse_dirt", 26.0, 77, ["inlaid"]),
            new("coarse_dirt", 26.0, 77, ["inlaid"])),   // one sprite on every face   // Coarse Dirt
        [(3 << 4) | 2] = (new("dirt_podzol_top", 16.0, 123, []),
            new("dirt_podzol_side", 26.2, 58, ["inlaid"])),   // Podzol
        [(4 << 4) | 0] = (new("cobblestone", 29.2, 79, []),
            new("cobblestone", 29.2, 79, [])),   // one sprite on every face   // Cobblestone
        [(5 << 4) | 0] = (new("planks_oak", 26.2, 7, ["tiled", "masonry", "bevelled", "ramped", "grained-x"]),
            new("planks_oak", 26.2, 7, ["tiled", "masonry", "bevelled", "ramped", "grained-x"])),   // one sprite on every face   // Oak Planks
        [(5 << 4) | 1] = (new("planks_spruce", 16.8, 9, ["tiled", "masonry", "bevelled", "ramped", "grained-x"]),
            new("planks_spruce", 16.8, 9, ["tiled", "masonry", "bevelled", "ramped", "grained-x"])),   // one sprite on every face   // Spruce Planks
        [(5 << 4) | 2] = (new("planks_birch", 21.7, 9, ["tiled", "masonry", "bevelled", "ramped", "grained-x"]),
            new("planks_birch", 21.7, 9, ["tiled", "masonry", "bevelled", "ramped", "grained-x"])),   // one sprite on every face   // Birch Planks
        [(5 << 4) | 3] = (new("planks_jungle", 25.5, 8, ["tiled", "masonry", "bevelled", "ramped", "grained-x"]),
            new("planks_jungle", 25.5, 8, ["tiled", "masonry", "bevelled", "ramped", "grained-x"])),   // one sprite on every face   // Jungle Planks
        [(5 << 4) | 4] = (new("planks_acacia", 13.3, 9, ["tiled", "masonry", "bevelled", "ramped", "grained-x"]),
            new("planks_acacia", 13.3, 9, ["tiled", "masonry", "bevelled", "ramped", "grained-x"])),   // one sprite on every face   // Acacia Planks
        [(5 << 4) | 5] = (new("planks_big_oak", 8.7, 7, ["tiled", "masonry", "bevelled", "ramped", "grained-x"]),
            new("planks_big_oak", 8.7, 7, ["tiled", "masonry", "bevelled", "ramped", "grained-x"])),   // one sprite on every face   // Dark Oak Planks
        [(12 << 4) | 0] = (new("sand", 14.1, 140, []),
            new("sand", 14.1, 140, [])),   // one sprite on every face   // Sand
        [(12 << 4) | 1] = (new("red_sand", 8.7, 176, ["dithered"]),
            new("red_sand", 8.7, 176, ["dithered"])),   // one sprite on every face   // Red Sand
        [(13 << 4) | 0] = (new("gravel", 22.2, 54, []),
            new("gravel", 22.2, 54, [])),   // one sprite on every face   // Gravel
        [(15 << 4) | 0] = (new("iron_ore", 20.4, 8, ["inlaid", "ramped"]),
            new("iron_ore", 20.4, 8, ["inlaid", "ramped"])),   // one sprite on every face   // Iron Ore
        [(16 << 4) | 0] = (new("coal_ore", 25.8, 8, ["inlaid", "ramped"]),
            new("coal_ore", 25.8, 8, ["inlaid", "ramped"])),   // one sprite on every face   // Coal Ore
        [(19 << 4) | 0] = (new("sponge", 22.8, 206, ["dithered"]),
            new("sponge", 22.8, 206, ["dithered"])),   // one sprite on every face   // Sponge
        [(19 << 4) | 1] = (new("sponge_wet", 25.2, 231, ["dithered"]),
            new("sponge_wet", 25.2, 231, ["dithered"])),   // one sprite on every face   // Wet Sponge
        [(22 << 4) | 0] = (new("lapis_block", 9.5, 191, ["bevelled", "dithered"]),
            new("lapis_block", 9.5, 191, ["bevelled", "dithered"])),   // one sprite on every face   // Lapis Lazuli Block
        [(24 << 4) | 0] = (new("sandstone_top", 6.3, 208, ["flat", "dithered"]),
            new("sandstone_normal", 16.1, 191, ["masonry", "dithered"])),   // Sandstone
        [(24 << 4) | 2] = (new("sandstone_top", 6.3, 208, ["flat", "dithered"]),
            new("sandstone_smooth", 13.7, 92, ["masonry", "bevelled"])),   // Smooth Sandstone
        [(35 << 4) | 0] = (new("wool_colored_white", 18.7, 48, []),
            new("wool_colored_white", 18.7, 48, [])),   // one sprite on every face   // White Wool
        [(35 << 4) | 1] = (new("wool_colored_orange", 7.1, 22, ["flat"]),
            new("wool_colored_orange", 7.1, 22, ["flat"])),   // one sprite on every face   // Orange Wool
        [(35 << 4) | 2] = (new("wool_colored_magenta", 12.2, 36, []),
            new("wool_colored_magenta", 12.2, 36, [])),   // one sprite on every face   // Magenta Wool
        [(35 << 4) | 3] = (new("wool_colored_light_blue", 15.6, 38, []),
            new("wool_colored_light_blue", 15.6, 38, [])),   // one sprite on every face   // Light Blue Wool
        [(35 << 4) | 4] = (new("wool_colored_yellow", 15.6, 33, []),
            new("wool_colored_yellow", 15.6, 33, [])),   // one sprite on every face   // Yellow Wool
        [(35 << 4) | 5] = (new("wool_colored_lime", 11.0, 35, []),
            new("wool_colored_lime", 11.0, 35, [])),   // one sprite on every face   // Lime Wool
        [(35 << 4) | 6] = (new("wool_colored_pink", 15.6, 37, []),
            new("wool_colored_pink", 15.6, 37, [])),   // one sprite on every face   // Pink Wool
        [(35 << 4) | 7] = (new("wool_colored_gray", 5.7, 16, ["panelled", "flat"]),
            new("wool_colored_gray", 5.7, 16, ["panelled", "flat"])),   // one sprite on every face   // Gray Wool
        [(35 << 4) | 8] = (new("wool_colored_silver", 13.4, 41, []),
            new("wool_colored_silver", 13.4, 41, [])),   // one sprite on every face   // Light Gray Wool
        [(35 << 4) | 9] = (new("wool_colored_cyan", 8.1, 29, []),
            new("wool_colored_cyan", 8.1, 29, [])),   // one sprite on every face   // Cyan Wool
        [(35 << 4) | 10] = (new("wool_colored_purple", 9.4, 36, []),
            new("wool_colored_purple", 9.4, 36, [])),   // one sprite on every face   // Purple Wool
        [(35 << 4) | 11] = (new("wool_colored_blue", 5.5, 32, ["panelled", "flat"]),
            new("wool_colored_blue", 5.5, 32, ["panelled", "flat"])),   // one sprite on every face   // Blue Wool
        [(35 << 4) | 12] = (new("wool_colored_brown", 5.1, 21, ["panelled", "flat"]),
            new("wool_colored_brown", 5.1, 21, ["panelled", "flat"])),   // one sprite on every face   // Brown Wool
        [(35 << 4) | 13] = (new("wool_colored_green", 5.4, 19, ["panelled", "flat"]),
            new("wool_colored_green", 5.4, 19, ["panelled", "flat"])),   // one sprite on every face   // Green Wool
        [(35 << 4) | 14] = (new("wool_colored_red", 7.4, 33, ["flat"]),
            new("wool_colored_red", 7.4, 33, ["flat"])),   // one sprite on every face   // Red Wool
        [(35 << 4) | 15] = (new("wool_colored_black", 5.3, 18, ["panelled", "flat"]),
            new("wool_colored_black", 5.3, 18, ["panelled", "flat"])),   // one sprite on every face   // Black Wool
        [(42 << 4) | 0] = (new("iron_block", 18.5, 38, ["tiled", "panelled", "grained-x"]),
            new("iron_block", 18.5, 38, ["tiled", "panelled", "grained-x"])),   // one sprite on every face   // Iron Block
        [(43 << 4) | 0] = (new("stone_slab_top", 15.9, 8, ["ramped"]),
            new("stone_slab_side", 20.6, 12, ["inlaid", "tiled", "bevelled", "ramped", "grained-x"])),   // Double Stone Slab
        [(43 << 4) | 8] = (new("stone_slab_top", 15.9, 8, ["ramped"]),
            new("stone_slab_side", 20.6, 12, ["inlaid", "tiled", "bevelled", "ramped", "grained-x"])),   // Double Stone Slab
        [(43 << 4) | 9] = (new("sandstone_top", 6.3, 208, ["flat", "dithered"]),
            new("sandstone_normal", 16.1, 191, ["masonry", "dithered"])),   // Double Sandstone Slab
        [(45 << 4) | 0] = (new("brick", 29.7, 62, ["tiled", "masonry"]),
            new("brick", 29.7, 62, ["tiled", "masonry"])),   // one sprite on every face   // Bricks
        [(48 << 4) | 0] = (new("cobblestone_mossy", 35.8, 105, ["bevelled"]),
            new("cobblestone_mossy", 35.8, 105, ["bevelled"])),   // one sprite on every face   // Mossy Cobblestone
        [(49 << 4) | 0] = (new("obsidian", 11.8, 73, ["masonry"]),
            new("obsidian", 11.8, 73, ["masonry"])),   // one sprite on every face   // Obsidian
        [(79 << 4) | 0] = (new("ice", 18.0, 3, ["ramped"]),
            new("ice", 18.0, 3, ["ramped"])),   // one sprite on every face   // Ice
        [(80 << 4) | 0] = (new("snow", 6.9, 4, ["bevelled", "flat", "ramped"]),
            new("snow", 6.9, 4, ["bevelled", "flat", "ramped"])),   // one sprite on every face   // Snow Block
        [(82 << 4) | 0] = (new("clay", 6.7, 94, ["flat"]),
            new("clay", 6.7, 94, ["flat"])),   // one sprite on every face   // Clay
        [(88 << 4) | 0] = (new("soul_sand", 24.5, 98, ["bevelled"]),
            new("soul_sand", 24.5, 98, ["bevelled"])),   // one sprite on every face   // Soul Sand
        [(95 << 4) | 0] = (new("glass_white", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_white", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // White Stained Glass
        [(95 << 4) | 1] = (new("glass_orange", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_orange", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Orange Stained Glass
        [(95 << 4) | 2] = (new("glass_magenta", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_magenta", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Magenta Stained Glass
        [(95 << 4) | 3] = (new("glass_light_blue", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_light_blue", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Light Blue Stained Glass
        [(95 << 4) | 4] = (new("glass_yellow", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_yellow", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Yellow Stained Glass
        [(95 << 4) | 5] = (new("glass_lime", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_lime", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Lime Stained Glass
        [(95 << 4) | 6] = (new("glass_pink", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_pink", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Pink Stained Glass
        [(95 << 4) | 7] = (new("glass_gray", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_gray", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Gray Stained Glass
        [(95 << 4) | 8] = (new("glass_silver", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_silver", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Light Gray Stained Glass
        [(95 << 4) | 9] = (new("glass_cyan", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_cyan", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Cyan Stained Glass
        [(95 << 4) | 10] = (new("glass_purple", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_purple", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Purple Stained Glass
        [(95 << 4) | 11] = (new("glass_blue", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_blue", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Blue Stained Glass
        [(95 << 4) | 12] = (new("glass_brown", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_brown", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Brown Stained Glass
        [(95 << 4) | 13] = (new("glass_green", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_green", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Green Stained Glass
        [(95 << 4) | 14] = (new("glass_red", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_red", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Red Stained Glass
        [(95 << 4) | 15] = (new("glass_black", 0.0, 1, ["tiled", "panelled", "flat", "ramped"]),
            new("glass_black", 0.0, 1, ["tiled", "panelled", "flat", "ramped"])),   // one sprite on every face   // Black Stained Glass
        [(98 << 4) | 0] = (new("stonebrick", 18.0, 42, ["masonry", "bevelled"]),
            new("stonebrick", 18.0, 42, ["masonry", "bevelled"])),   // one sprite on every face   // Stone Bricks
        [(98 << 4) | 1] = (new("stonebrick_mossy", 17.6, 78, ["inlaid", "bevelled"]),
            new("stonebrick_mossy", 17.6, 78, ["inlaid", "bevelled"])),   // one sprite on every face   // Mossy Stone Bricks
        [(98 << 4) | 2] = (new("stonebrick_cracked", 19.7, 55, ["masonry", "bevelled"]),
            new("stonebrick_cracked", 19.7, 55, ["masonry", "bevelled"])),   // one sprite on every face   // Cracked Stone Bricks
        [(98 << 4) | 3] = (new("stonebrick_carved", 22.4, 39, ["bevelled"]),
            new("stonebrick_carved", 22.4, 39, ["bevelled"])),   // one sprite on every face   // Chiseled Stone Bricks
        [(99 << 4) | 0] = (new("mushroom_block_skin_brown", 11.7, 58, []),
            new("mushroom_block_skin_brown", 11.7, 58, [])),   // one sprite on every face   // Brown Mushroom Block
        [(99 << 4) | 15] = (new("mushroom_block_skin_stem", 11.9, 27, ["masonry", "grained-y"]),
            new("mushroom_block_skin_stem", 11.9, 27, ["masonry", "grained-y"])),   // one sprite on every face   // Mushroom Stem
        [(103 << 4) | 0] = (new("melon_top", 23.2, 11, ["ramped"]),
            new("melon_side", 27.1, 52, ["tiled", "masonry", "grained-y"])),   // Melon
        [(110 << 4) | 0] = (new("mycelium_top", 10.6, 60, []),
            new("mycelium_side", 27.0, 47, ["inlaid"])),   // Mycelium
        [(112 << 4) | 0] = (new("nether_brick", 8.2, 24, ["tiled", "masonry", "grained-x"]),
            new("nether_brick", 8.2, 24, ["tiled", "masonry", "grained-x"])),   // one sprite on every face   // Nether Bricks
        [(121 << 4) | 0] = (new("end_stone", 21.6, 25, []),
            new("end_stone", 21.6, 25, [])),   // one sprite on every face   // End Stone
        [(133 << 4) | 0] = (new("emerald_block", 21.4, 113, ["bevelled"]),
            new("emerald_block", 21.4, 113, ["bevelled"])),   // one sprite on every face   // Emerald Block
        [(155 << 4) | 0] = (new("quartz_block_top", 5.3, 38, ["panelled", "bevelled", "flat"]),
            new("quartz_block_side", 5.3, 38, ["panelled", "bevelled", "flat"])),   // Quartz Block
        [(155 << 4) | 1] = (new("quartz_block_chiseled_top", 7.8, 32, ["flat"]),
            new("quartz_block_chiseled", 8.1, 32, ["bevelled"])),   // Chiseled Quartz Block
        [(159 << 4) | 0] = (new("hardened_clay_stained_white", 2.1, 52, ["panelled", "flat"]),
            new("hardened_clay_stained_white", 2.1, 52, ["panelled", "flat"])),   // one sprite on every face   // White Stained Clay
        [(159 << 4) | 1] = (new("hardened_clay_stained_orange", 2.1, 68, ["panelled", "flat"]),
            new("hardened_clay_stained_orange", 2.1, 68, ["panelled", "flat"])),   // one sprite on every face   // Orange Stained Clay
        [(159 << 4) | 2] = (new("hardened_clay_stained_magenta", 2.2, 69, ["panelled", "flat"]),
            new("hardened_clay_stained_magenta", 2.2, 69, ["panelled", "flat"])),   // one sprite on every face   // Magenta Stained Clay
        [(159 << 4) | 3] = (new("hardened_clay_stained_light_blue", 1.4, 44, ["panelled", "flat"]),
            new("hardened_clay_stained_light_blue", 1.4, 44, ["panelled", "flat"])),   // one sprite on every face   // Light Blue Stained Clay
        [(159 << 4) | 4] = (new("hardened_clay_stained_yellow", 2.2, 70, ["panelled", "flat"]),
            new("hardened_clay_stained_yellow", 2.2, 70, ["panelled", "flat"])),   // one sprite on every face   // Yellow Stained Clay
        [(159 << 4) | 5] = (new("hardened_clay_stained_lime", 2.1, 62, ["panelled", "flat"]),
            new("hardened_clay_stained_lime", 2.1, 62, ["panelled", "flat"])),   // one sprite on every face   // Lime Stained Clay
        [(159 << 4) | 6] = (new("hardened_clay_stained_pink", 2.2, 72, ["panelled", "flat"]),
            new("hardened_clay_stained_pink", 2.2, 72, ["panelled", "flat"])),   // one sprite on every face   // Pink Stained Clay
        [(159 << 4) | 7] = (new("hardened_clay_stained_gray", 1.0, 39, ["panelled", "flat"]),
            new("hardened_clay_stained_gray", 1.0, 39, ["panelled", "flat"])),   // one sprite on every face   // Gray Stained Clay
        [(159 << 4) | 8] = (new("hardened_clay_stained_silver", 1.6, 51, ["panelled", "flat"]),
            new("hardened_clay_stained_silver", 1.6, 51, ["panelled", "flat"])),   // one sprite on every face   // Light Gray Stained Clay
        [(159 << 4) | 9] = (new("hardened_clay_stained_cyan", 1.9, 67, ["panelled", "flat"]),
            new("hardened_clay_stained_cyan", 1.9, 67, ["panelled", "flat"])),   // one sprite on every face   // Cyan Stained Clay
        [(159 << 4) | 10] = (new("hardened_clay_stained_purple", 2.2, 70, ["panelled", "flat"]),
            new("hardened_clay_stained_purple", 2.2, 70, ["panelled", "flat"])),   // one sprite on every face   // Purple Stained Clay
        [(159 << 4) | 11] = (new("hardened_clay_stained_blue", 1.4, 45, ["panelled", "flat"]),
            new("hardened_clay_stained_blue", 1.4, 45, ["panelled", "flat"])),   // one sprite on every face   // Blue Stained Clay
        [(159 << 4) | 12] = (new("hardened_clay_stained_brown", 1.4, 47, ["panelled", "flat"]),
            new("hardened_clay_stained_brown", 1.4, 47, ["panelled", "flat"])),   // one sprite on every face   // Brown Stained Clay
        [(159 << 4) | 13] = (new("hardened_clay_stained_green", 1.3, 47, ["panelled", "flat"]),
            new("hardened_clay_stained_green", 1.3, 47, ["panelled", "flat"])),   // one sprite on every face   // Green Stained Clay
        [(159 << 4) | 14] = (new("hardened_clay_stained_red", 2.1, 66, ["panelled", "flat"]),
            new("hardened_clay_stained_red", 2.1, 66, ["panelled", "flat"])),   // one sprite on every face   // Red Stained Clay
        [(159 << 4) | 15] = (new("hardened_clay_stained_black", 0.9, 32, ["panelled", "flat"]),
            new("hardened_clay_stained_black", 0.9, 32, ["panelled", "flat"])),   // one sprite on every face   // Black Stained Clay
        [(165 << 4) | 0] = (new("slime", 9.6, 242, ["dithered"]),
            new("slime", 9.6, 242, ["dithered"])),   // one sprite on every face   // Slime Block
        [(168 << 4) | 0] = (new("prismarine_rough", 27.0, 227, ["dithered"]),
            new("prismarine_rough", 27.0, 227, ["dithered"])),   // one sprite on every face   // Prismarine
        [(168 << 4) | 1] = (new("prismarine_bricks", 25.1, 233, ["bevelled", "dithered"]),
            new("prismarine_bricks", 25.1, 233, ["bevelled", "dithered"])),   // one sprite on every face   // Prismarine Bricks
        [(168 << 4) | 2] = (new("prismarine_dark", 18.3, 215, ["masonry", "bevelled", "dithered"]),
            new("prismarine_dark", 18.3, 215, ["masonry", "bevelled", "dithered"])),   // one sprite on every face   // Dark Prismarine
        [(170 << 4) | 0] = (new("hay_block_top", 15.7, 160, ["dithered"]),
            new("hay_block_side", 31.6, 205, ["tiled", "masonry", "dithered"])),   // Hay Bale
        [(172 << 4) | 0] = (new("hardened_clay", 3.6, 82, ["panelled", "flat"]),
            new("hardened_clay", 3.6, 82, ["panelled", "flat"])),   // one sprite on every face   // Hardened Clay
        [(173 << 4) | 0] = (new("coal_block", 7.5, 6, ["flat", "ramped"]),
            new("coal_block", 7.5, 6, ["flat", "ramped"])),   // one sprite on every face   // Coal Block
        [(174 << 4) | 0] = (new("ice_packed", 7.7, 92, ["panelled", "flat"]),
            new("ice_packed", 7.7, 92, ["panelled", "flat"])),   // one sprite on every face   // Packed Ice
        [(179 << 4) | 0] = (new("red_sandstone_top", 3.1, 86, ["panelled", "flat"]),
            new("red_sandstone_normal", 7.6, 128, ["masonry", "flat"])),   // Red Sandstone
        [(181 << 4) | 8] = (new("red_sandstone_top", 3.1, 86, ["panelled", "flat"]),
            new("red_sandstone_normal", 7.6, 128, ["masonry", "flat"])),   // Double Red Sandstone Slab
    };
}
