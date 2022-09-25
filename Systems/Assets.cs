using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Assets", menuName = "ScriptableObjects/Assets", order = 3)]
public class Assets : ScriptableObject {
    public class AssetInfo {
        public string folderPath;
        public Layer layer;
        public Tag[] tags;

        public AssetInfo(string folderPath, Layer layer = Layer.None, Tag[] tags = null) {
            this.folderPath = folderPath;
            this.layer      = layer;
            this.tags       = tags;
        }
    }
    public static readonly Dictionary<string, AssetInfo> ASSET_INFO = new Dictionary<string, AssetInfo>() {
       //| Name                           | Folder Path  | Layer                | Tags                                           

        // Level Blocks
        { "Basic"          , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Stop }                         ) },
        { "RedButton"      , new AssetInfo("BlockMisc"   , Layer.Misc                                                            ) },
        { "GreenButton"    , new AssetInfo("BlockMisc"   , Layer.Misc                                                            ) },
        { "BlueButton"     , new AssetInfo("BlockMisc"   , Layer.Misc                                                            ) },
        { "ForceButton"    , new AssetInfo("BlockMisc"   , Layer.Misc                                                            ) },
        { "CollectRed"     , new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "CollectGreen"   , new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "CollectBlue"    , new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "CollectForce"   , new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "CollectTime"    , new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "CollectFragment", new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "CollectLength"  , new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "CollectDig"     , new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "CollectSong"    , new AssetInfo("Collectables", Layer.Collect                                                         ) },
        { "RedCrystal"     , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Push, Tag.Connect, Tag.Float } ) },
        { "GreenCrystal"   , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Push, Tag.Connect, Tag.Float } ) },
        { "BlueCrystal"    , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Push, Tag.Connect, Tag.Float } ) },
        { "ForceCrystal"   , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Push, Tag.Connect, Tag.Float } ) },
        { "Dig"            , new AssetInfo("Blocks"      , Layer.Dig            , new Tag[] { Tag.Connect }                      ) },
        { "Gate"           , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Stop }                         ) },
        { "GateSlot"       , new AssetInfo("BlockMisc"   , Layer.Misc                                                            ) },
        { "Ground"         , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Stop, Tag.Tile }               ) },
        { "GroundBG1"      , new AssetInfo("Blocks"      , Layer.Bg1            , new Tag[] { Tag.Tile }                         ) },
        { "GroundBG2"      , new AssetInfo("Blocks"      , Layer.Bg2            , new Tag[] { Tag.Tile }                         ) },
        { "GroundFG"       , new AssetInfo("Blocks"      , Layer.Fg             , new Tag[] { Tag.Connect }                      ) },
        { "Panel"          , new AssetInfo("BlockMisc"   , Layer.Misc                                                            ) },
        { "GatePanel"      , new AssetInfo("BlockMisc"   , Layer.Misc                                                            ) },
        { "Pipe"           , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Stop, Tag.Connect }            ) },
        { "Piston"         , new AssetInfo("Blocks"      , Layer.Piston         , new Tag[] { Tag.Stop }                         ) },
        { "PistonArm"      , new AssetInfo("Blocks"      , Layer.Piston         , new Tag[] { Tag.Stop }                         ) },
        { "Player"         , new AssetInfo("Player"      , Layer.Player         , new Tag[] { Tag.Player, Tag.Push, Tag.Connect }) },
        { "Rock"           , new AssetInfo("Blocks"      , Layer.Block          , new Tag[] { Tag.Push, Tag.Connect }            ) },
        { "Support"        , new AssetInfo("Blocks"      , Layer.Support        , new Tag[] { Tag.Tile }                         ) },
        { "Tunnel"         , new AssetInfo("Blocks"      , Layer.Tunnel         , new Tag[] { Tag.Connect, Tag.Stop }            ) },
        { "TunnelDoor"     , new AssetInfo("BlockMisc"   , Layer.Misc                                                            ) },
        { "SupportCrystal" , new AssetInfo("Blocks"      , Layer.SupportCrystal                                                  ) },

        // Other
        { "BG"                  , new AssetInfo("Blocks"   ) },
        { "BlueCrystalBreak"    , new AssetInfo("Blocks"   ) },
        { "ButtonBullet"        , new AssetInfo("BlockMisc") },
        { "ColorSymbol_Block"   , new AssetInfo("UI"       ) },
        { "CrystalActivation"   , new AssetInfo("Blocks"   ) },
        { "CrystalFloatOutline" , new AssetInfo("Blocks"   ) },
        { "CrystalGlowOutline"  , new AssetInfo("Blocks"   ) },
        { "GateInfoOutline"     , new AssetInfo("Blocks"   ) },
        { "GatePanelInfoOutline", new AssetInfo("BlockMisc") },
        { "LevelTemplate"       , new AssetInfo("Other"    ) },
        { "RedPanel"            , new AssetInfo("BlockMisc") },
        { "GreenPanel"          , new AssetInfo("BlockMisc") },
        { "BluePanel"           , new AssetInfo("BlockMisc") },
        { "ForcePanel"          , new AssetInfo("BlockMisc") },
        { "PanelInfoOutline"    , new AssetInfo("BlockMisc") },
        { "PistonHead"          , new AssetInfo("BlockMisc") },
        { "PistonHeadInfo"      , new AssetInfo("Blocks"   ) },
        { "PistonInfo"          , new AssetInfo("Blocks"   ) },
        { "Robot"               , new AssetInfo("Blocks"   ) },
        { "Screen"              , new AssetInfo("Other"    ) },
        { "Test"                , new AssetInfo("Blocks"   ) },
        { "UndoOutline"         , new AssetInfo("Blocks"   ) }
    };
    
    public static readonly Dictionary<string, Color32> MAP_COLORS = new Dictionary<string, Color32>() {
        // Color Index
        { "0"            , new Color32(255,  37,  45, 255) },
        { "1"            , new Color32( 63, 255,  49, 255) },
        { "2"            , new Color32( 49, 143, 255, 255) },
        { "3"            , new Color32(169,  49, 255, 255) },
        { "4"            , new Color32(255, 255,  49, 255) },
        { "5"            , new Color32( 49, 252, 255, 255) },
        { "6"            , new Color32(213, 144, 179, 255) },
        { "7"            , new Color32(140,  90,  70, 255) },

        { "Basic"        , new Color32(210, 210, 210, 200) },
        { "Border"       , new Color32(150, 255,   0,   1) },
        { "Dig"          , new Color32(140, 120, 110,  50) },
        { "DigAlt"       , new Color32(140,  90,  70, 255) },
        { "Edge"         , new Color32(  0,   0,   0, 140) },
        { "Empty"        , new Color32(  0,   0,   0, 175) },
        { "Gate"         , new Color32(102, 102, 112, 255) },
        { "GatePanel"    , new Color32( 36,  36,  41, 255) },
        { "GatePanelIcon", new Color32(242, 242, 242, 255) },
        { "Ground"       , new Color32(190, 190, 190, 200) },
        { "Pipe"         , new Color32(210, 210, 210, 200) },
        { "Piston"       , new Color32( 44,  44,  50, 255) },
        { "Player"       , new Color32(213, 144, 179, 255) },
        { "Rock"         , new Color32(112, 109, 107, 255) },
        { "Screen"       , new Color32(  0, 255,   0,   1) },
        { "Song"         , new Color32(242, 242, 242, 255) },
        { "Tunnel"       , new Color32(255, 255, 255, 255) },
        { "TunnelAlt"    , new Color32(102, 102, 112, 255) },
        { "TunnelEnd"    , new Color32(255,   0, 255, 255) }
    };
    public const byte MAP_BUTTON_ALPHA = 200;
    
    // Defines tiling conditions for different variations of adjacent blocks (block exists to the [Right, Up, Left, Down] of a given block)
    public static readonly bool[][][] SPRITE_TYPES = new bool[][][] {
        // Center
        new bool[][] {
            new bool[] {  true,  true,  true,  true }
        },
        // Pipe
        new bool[][] {
            new bool[] {  true, false,  true, false },
            new bool[] { false,  true, false,  true }
        },
        // Corner
        new bool[][] {
            new bool[] {  true,  true, false, false },
            new bool[] { false,  true,  true, false },
            new bool[] { false, false,  true,  true },
            new bool[] {  true, false, false,  true }
        },
        // End
        new bool[][] {
            new bool[] { false, false,  true, false },
            new bool[] { false, false, false,  true },
            new bool[] {  true, false, false, false },
            new bool[] { false,  true, false, false }
        },
        // Side
        new bool[][] {
            new bool[] {  true, false,  true,  true },
            new bool[] {  true,  true, false,  true },
            new bool[] {  true,  true,  true, false },
            new bool[] { false,  true,  true,  true }
        },
        // Single
        new bool[][] {
            new bool[] { false, false, false, false }
        }
    };

    [Header("Menu")]
    public Sprite[] confirmButtons;
    public Sprite[] saveButtons;
    public Sprite[] volumeIcons;
    public Sprite[] brightnessIcons;
    public Sprite[] menuDots;
    public Sprite[] checkBoxes;
    public Color highlightColor;
    public Color inactiveColor;
    public Color backgroundColor;

    [Header("UI")]
    public GameObject lengthMeterPiece;
    public Sprite dreamCloudParticlesSprite;
    public Sprite[] dreamCloudSprites;
    public Sprite[] dreamCloudOutlineSprites;
    public Sprite[] dreamCloudExpandSprites;
    public Sprite[] dreamCloudExpandOutlineSprites;
    public Sprite[] cloudBlipSprites;
    public Sprite[] lengthMeterSprites;
    public Sprite[] resetMeterSprites;
    public GameObject undoOutline;
    public Sprite[] undoOutlineSprites;
    public Sprite[] thinkShowSprites;
    public Sprite[] thinkHideSprites;
    public Sprite[] gridSprites;
    public Sprite[] transitionSprites;
    public Sprite[] menuBGSprites;
    public GameObject gateInfoOutline;
    public GameObject panelInfoOutline;
    public Color32 infoOutlineColor;
    public GameObject tunnelDoorOutline;
    public Sprite[] numbers;
    public Sprite[] lengthItemOutlines;

    [Header("Worm")]
    public Color32 wormColor;
    public Color32 eyeColor;
    public GameObject wormEye;
    public GameObject bullet;
    public GameObject wormHead;
    public GameObject wormBody;
    public GameObject wormMouth;
    public Sprite[] cornerSprites;
    public Sprite[] cornerSkeletonSprites;
    public Sprite[] secondCornerSprites;
    public Sprite[] growSprites;
    public Sprite[] growSkeletonSprites;
    public GameObject headTrigger;

    [Header("Blocks")]
    public GameObject crystalActivation;
    public Sprite[] crystalActivationSprites;
    public GameObject crystalFloatOutline;
    public GameObject crystalGlowOutline;
    public Sprite[] redCrystalOutlineSprites;
    public Sprite[] greenCrystalOutlineSprites;
    public Sprite[] blueCrystalOutlineSprites;
    public Sprite[] redSupportCrystalSprites;
    public Sprite[] greenSupportCrystalSprites;
    public Sprite[] blueSupportCrystalSprites;
    public GameObject blueCrystalBreak;
    public Sprite[] blueCrystalBreakSprites;
    public Sprite[] tunnelDoorSprites;
    public Sprite[] gateLightSprites;
    public Sprite[] digOutlineSprites;
    public Sprite[] digHoleSprites;
    public Sprite digBlankSprite;
    public GameObject testBlock;

    [Header("BlockMisc")]
    public Sprite[] buttonSprites;
    public Sprite[] panelSprites;
    public Sprite[] gatePanelSprites;

    [Header("Particles")]
    public GameObject airDustParticles;
    public GameObject groundDustParticles;
    public GameObject groundLandingParticles;
    public GameObject groundMovingParticles;
    public GameObject bulletParticles;

    [Header("Other")]
    public GameObject levelTemplate;
    public Material spriteDefault;
    public Material spriteLitDefault;
    public Material crystalGlow;
    public Material buttonGlow;
    public AnimationCurve curve;
    public AnimationCurve altCurve;
    public Color32 backDropColor;
    public Color32[] gameColors;

    public static AssetInfo GetAssetInfo(string assetName) {
        return ASSET_INFO[assetName];
    }

#if UNITY_EDITOR
    public static GameObject GetPrefab(string name) {
        return AssetDatabase.LoadAssetAtPath("Assets/Prefabs/" + ASSET_INFO[name].folderPath + "/" + name + ".prefab", typeof(GameObject)) as GameObject;
    }
    public static Dictionary<string, GameObject> GetPrefabs() {
        Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        foreach (string key in ASSET_INFO.Keys)
            prefabs.Add(key, GetPrefab(key));

        return prefabs;
    }
    public static Sprite GetSprite(string path) {
        return (Sprite)AssetDatabase.LoadAssetAtPath("Assets/Sprites/" + path + ".png", typeof(Sprite));
    }
    public static Dictionary<string, List<Sprite>> GetTileSprites() {
        Dictionary<string, List<Sprite>> sprites = new Dictionary<string, List<Sprite>>();
        foreach (string guid in AssetDatabase.FindAssets("t:sprite", new[] { "Assets/Sprites/Blocks/Tiling" })) {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            string folder = path.Split('/')[4];

            sprites.TryGetValue(folder, out List<Sprite> spriteList);
            if (spriteList == null) {
                spriteList = new List<Sprite>();
                sprites.Add(folder, spriteList);
            }

            spriteList.Add((Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite)));
        }
        return sprites;
    }
    public static void SaveSprite(Texture2D texture, string path) {
        path += ".png";
        AssetDatabase.DeleteAsset(path);
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.Refresh();

        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.spritePixelsPerUnit = 1;
        ti.isReadable = true;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
        AssetDatabase.Refresh();
    }
#endif
}
