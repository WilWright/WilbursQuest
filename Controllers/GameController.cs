using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Experimental.Rendering.LWRP;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum Tag { Player, Stop, Push, Connect, Float, Tile }
public enum Layer { Player, Block, Misc, Support, SupportCrystal, Piston, Tunnel, Collect, CollectDestroy, Bg1, Bg2, Fg, Dig, None }
public enum MoveType { Player, Gravity, Block }
public enum ColorIndex { Red, Green, Blue, Force, Time, Fragment, Length, Dig }
public enum PromptIndex { Shoot, Grow, Undo, Reset, Force, Map, Grid, Inventory, RoomInfo, Move }
public enum Activation { Off, On, Alt }

public class GameController : MonoBehaviour {
    #region Fields

    class Bullet {
        public GameObject bulletObject;
        public SpriteRenderer spriteRenderer;
        public Light2D light;

        public Bullet(GameObject bulletObject, SpriteRenderer spriteRenderer, Light2D light) {
            this.bulletObject   = bulletObject;
            this.spriteRenderer = spriteRenderer;
            this.light          = light;
        }
    }

    public class OffScreenActivation {
        public GameObject activationObject;
        public SpriteRenderer spriteRenderer;

        public OffScreenActivation(GameObject activationObject) {
            this.activationObject = activationObject;
            spriteRenderer = activationObject.GetComponent<SpriteRenderer>();
        }
    }

    public class FloatObject {
        public GameObject floatObject;
        public int offset;
        public int direction;
        public bool local;
        public Vector3 anchor;
    }

    public class FallData {
        public Data data;
        public bool falling;
        public int fallCount;

        public FallData(Data data) {
            this.data = data;
        }
    }

    public class ColorObjects {
        public ColorField[] colorFields;

        public ColorObjects(int length) {
            colorFields = new ColorField[length];
        }

        public class ColorField {
            public Color32 color;
            public List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
            public List<Image> images = new List<Image>();
            public List<Light2D> lights = new List<Light2D>();

            public void UpdateColors() {
                foreach (SpriteRenderer sr in spriteRenderers) sr.color = color;
                foreach (Image i in images) i.color  = color;
                foreach (Light2D l in lights) l.color  = color;
            }
        }
    }

    [Header("Game")]
    public Light2D globalLight;
    public Camera gameCamera;
    public GameObject cameraHolder;
    public GameObject gameObjectsHolder;
    public GameObject playerObjectsHolder;
    public GameObject playerHolder;
    public GameObject cameraBorder;
    public StandaloneRoom standaloneRoom;

    public Image fadeOverlay;
    public static Activation fadeState;
    public GameObject[] cutsceneBars;
    static Vector2 cutsceneBarPos;

    public static SpriteMask digMask;
    public static Dictionary<LevelBlock, SpriteRenderer> digOutlines = new Dictionary<LevelBlock, SpriteRenderer>();
    static Dictionary<SpriteRenderer, IEnumerator> foregroundCoroutines = new Dictionary<SpriteRenderer, IEnumerator>();
    const float FG_SHOW_SPEED = 2f;
    const float FG_ALPHA_MIN = 130;
    static StasisMachineArc[] stasisMachineArcs;

    public static Screen currentScreen;
    const float SCREEN_MOVE_SPEED = 5;
    static IEnumerator screenMoveCoroutine;
    static int priorityCameraShake;
    static IEnumerator cameraShakeCoroutine;

    [Header("UI")]
    public GameObject gameUI;
    public GameObject devUI;

    public GameObject transition;
    public SpriteRenderer transitionSpriteRenderer;
    public static Activation transitionState;
    static IEnumerator transitionCoroutine;

    public Text[] popUps;

    public GameObject cloudAnchor;
    public static GameObject blipAnchor;
    public GameObject cloudBlipsHolder;
    static LevelBlock.BlockItem[][] cloudBlips;
    static IEnumerator[] cloudBlipCoroutines;

    public GameObject dreamCloud;
    public SpriteMask dreamCloudMask;
    public SpriteRenderer[] dreamCloudSpriteRenderers;
    public SpriteRenderer[] dreamSpriteRenderers;
    public ParticleSystem[] dreamCloudParticles;
    public DreamSequence[] dreams;

    public GameObject promptCloud;
    public ParticleSystem[] promptCloudParticles;
    public static PromptIndex currentPromptIndex = (PromptIndex)(-1);
    const float PROMPT_CLOUD_START_WAIT = 0.2f;
    const float PROMPT_CLOUD_BLIP_WAIT = 0.25f;
    const float PROMPT_CLOUD_CYCLE_WAIT = 0.5f;
    static bool triggerRoomInfoPrompt;

    public GameObject offScreenActivationPrefab;
    static GameObject offScreenActivationsHolder;
    public Sprite[] offScreenActivationSprites;
    static int offScreenActivationCount;
    static Queue<OffScreenActivation> offScreenActivationPool;

    public GameObject lengthMeter;
    static GameObject[] lengthMeterObjects;
    static Image[][] lengthMeterImages;
    public Image resetImage;
    static int resetIndex;

    public Image songIndexImage;
    public Text songIndexText;
    static IEnumerator songIndexCoroutine;
    
    static Bullet[] bullets;
    static Queue<Bullet> bulletPool = new Queue<Bullet>();
    public static int activeBullets;
    const int BULLET_AMOUNT = 50;
    static List<Coordinates[]> activeRedCrystals = new List<Coordinates[]>();
    
    static float undoPercent;
    static float undoWait;
    static Dictionary<GameObject, IEnumerator> undoCoroutines = new Dictionary<GameObject, IEnumerator>();

    // Developer Settings
    public static bool demoMode           = false;
    public static bool devMode            = true;
    public static bool showUI             = true;
    public static bool instantUI          = false;
    public static bool enableScreenshake  = true;
    public static bool toggleButtons      = false;
    public static bool enableCameraSnap   = false;
    public static bool enableColorSymbols = false;
    public static bool enableParticles    = true;
    public static bool savePlayer         = true;
    public static bool saveRoom           = true;

    // States
    public static bool initialized;
    public static bool levelInitialized;
    public static bool initData;
    public static bool initialStart;
    public static bool generatingBlocks;
    public static bool disableEditors;
    public static bool disableScreenSwitching;
    public static int  gameStateFlags;
    public static bool resolvedGameState = true;
    public static bool canPause = true;
    public static bool paused = true;
    public static bool inEndingRoom;
    public static bool cameraMoving;
    public static bool resetting;
    public static bool shooting;
    public static bool cancelBullets;

    // Game Info
    public static string originRoom = "Origin";
    public static string startRoom;
    public static string currentRoom;
    public static int enterTunnelID;
    public const int SAVE_SLOT_AMOUNT = 3;
    public static int currentSave = 1;
    public static int demoIndex = 1;
    public const float GRID_SIZE = 7f;
    public const int BLOCK_SIZE = 7;
    public const float BLOCK_MOVE_SPEED = 10;
    public const float BULLET_MOVE_SPEED = 20;
    public const float BLOCK_FALL_SPEED_MAX = 50;
    public const float BLOCK_FALL_STEP = 5;
    public const int BLOCK_FLOAT_STEPS = 20;
    public const float BLOCK_FLOAT_SPEED = 5.5f;
    public static Coordinates gravityDirection = Coordinates.Down;
    public static Coordinates forceDirection;

    public static int colorCycle = -1;
    public const int CRYSTAL_COLORS = 3;
    public const int COLLECTABLE_TYPES = 9;
    public static ColorObjects[] colorObjects = new ColorObjects[2];
    public static List<LevelBlock>[] colorSymbols = new List<LevelBlock>[2];

    static int playerMoveBlockedBuffer;
    static int playedSongIndex = -1;

    public static FallData[] fallDatas;
    static Data[] forceDatas;
    static bool[] forceMovingData;

    static List<FloatObject> floatObjects = new List<FloatObject>();
    static float[] floatStepValues;
    static IEnumerator cycleFloatingCoroutine;

    static List<IEnumerator> dustCoroutines = new List<IEnumerator>();
    static bool[] usedAirDustCoords;

    public GameObject tunnelResetOutline;
    public static List<SpriteRenderer> resetOutlines = new List<SpriteRenderer>();
    static List<OffScreenActivation> offScreenResetOutlines = new List<OffScreenActivation>();

    public static MapController.RoomData.Tunnel nextTunnel;
    public static MapController.RoomData.Tunnel resetTunnel;
    public static LevelBlock.BlockItem tunnelResetItem;

    static Light2D[][][] supportCrystals;
    static IEnumerator supportCrystalsCoroutine;
    const float SUPPORT_CRYSTAL_CYCLE_WAIT = 2f;
    const float SUPPORT_CRYSTAL_COLOR_WAIT = 0.5f;
    const float SUPPORT_CRYSTAL_FLOW_WAIT = 0.02f;
    const float SUPPORT_CRYSTAL_GLOW_SPEED = 5f;

    // Global Instances
    public Assets assetsModule;
    public static Assets Assets;
    public static GameController Game;
    public static AudioController Audio;
    public static MenuController Menu;
    public static InputController Input;
    public static LevelData Level;
    public static PlayerController Player;
    public static GridSystem Grid;
    public static ParticlePooler Particles;
    public static DevController Dev;

    #endregion

    #region Initialization

    void Awake() {
        Cursor.visible = devMode;
        initialStart = true;
        InitData();
        DontDestroyOnLoad(gameObject);

        Assets    = assetsModule;
        Audio     = gameCamera.GetComponent<AudioController>();
        Game      = this;
        Menu      = GetComponent<MenuController>();
        Input     = GetComponent<InputController>();
        Player    = GetComponent<PlayerController>();
        Particles = GetComponent<ParticlePooler>();
        Dev       = GetComponent<DevController>();
        
        Audio    .Init();
        Menu     .Init();
        Particles.Init();

        if (standaloneRoom.room != null && standaloneRoom.room != "") {
            startRoom = standaloneRoom.room;
            standaloneRoom.room = null;
        }

        for (int i = 0; i < colorObjects.Length; i++) {
            colorObjects[i] = new ColorObjects(Menu.settingsData.colorIndices.Length);
            for (int j = 0; j < colorObjects[i].colorFields.Length; j++)
                colorObjects[i].colorFields[j] = new ColorObjects.ColorField();
        }

        Input .Init();
        Player.Init();
        Player.Map.mapButtons.SetActive(false);
        UpdateAbilityInfo();

        if (!devMode) {
            devUI.SetActive(false);
            Dev.enabled = false;
        }
        else
            Dev.Init();

        InitCloudBlips();
        InitLengthMeter();
        InitBullets();
        InitFloating();

        tunnelResetItem = new LevelBlock.BlockItem(tunnelResetOutline, tunnelResetOutline.GetComponent<SpriteRenderer>());
        AddColorObject(ColorIndex.Time, tunnelResetItem.spriteRenderer                                                  , null, null, false);
        AddColorObject(ColorIndex.Time, tunnelResetItem.blockObject.transform.GetChild(0).GetComponent<SpriteRenderer>(), null, null, false);

        offScreenActivationPool = new Queue<OffScreenActivation>();
        offScreenActivationsHolder = offScreenActivationPrefab.transform.parent.gameObject;
        for (int i = 0; i < 50; i++) {
            OffScreenActivation osa = new OffScreenActivation(Instantiate(offScreenActivationPrefab, offScreenActivationsHolder.transform));
            offScreenActivationPool.Enqueue(osa);
        }

        Menu.InitInventoryColors();
        for (int i = 1; i < dreamSpriteRenderers.Length; i++)
            AddColorObject(i - 1, dreamSpriteRenderers[i], null, null, false);

        foreach (ColorObjects co in colorObjects) {
            for (int i = 0; i < co.colorFields.Length; i++)
                SetGameColor(i, GetGameColor((ColorIndex)i));
        }

        cutsceneBarPos = cutsceneBars[0].transform.localPosition;
        cameraBorder.SetActive(true);
        initialized = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(StartUp());

        IEnumerator StartUp() {
            SetFade(true, true);

            yield return new WaitUntil(() => fadeState == Activation.On);

            LoadRoom(startRoom);

            yield return new WaitUntil(() => levelInitialized);
            yield return new WaitForSeconds(0.2f);

            SetFade(false);
        }
    }

    public static void LoadRoom(string room) {
        levelInitialized = false;
        Player.headTrigger.SetActive(false);
        CleanUp();

        if (room.Contains(":")) {
            string[] roomID = room.Split(':');
            room = roomID[0];

            MapController.RoomData rd = Player.Map.GetRoomData(room);
            nextTunnel = rd?.tunnels[int.Parse(roomID[1])];
        }
        else {
            if (initialStart)
                nextTunnel = null;
        }

        inEndingRoom = room == "Ending";

        if (initialStart) SceneManager.LoadScene(room);
        else              Game.StartCoroutine(CheckPopUps());

        IEnumerator CheckPopUps() {
            yield return new WaitUntil(() => transitionState == Activation.On);

            string popUp = null;
            switch (room) {
                case "Fall"    : popUp = "SaveInfo"; break;
                case "DemoEndA": popUp = "DemoEndA"; break;
                case "DemoEndB": popUp = "DemoEndB"; break;
            }

            if (popUp != null && !Player.playerData.popUps.Contains(popUp)) {
                Text popUpText = null;
                foreach (Text t in Game.popUps) {
                    if (t.name == popUp) {
                        popUpText = t;
                        break;
                    }
                }

                canPause = false;
                if (paused)
                    Pause(false, true);

                yield return new WaitForSeconds(1);

                SetPopUp(popUpText, true);

                yield return new WaitForSeconds(1.5f);

                Input.ActivateContinueButton(true);

                yield return new WaitUntil(() => Input.continueButton.completed);
                yield return new WaitForSeconds(0.2f);

                Input.ActivateContinueButton(false);
                SetPopUp(popUpText, false);
                Player.playerData.popUps.Add(popUp);

                yield return new WaitForSeconds(1);

                canPause = true;
            }
            SceneManager.LoadScene(room);
        }
    }

    public static void CleanUp() {
        Particles.ResetParticles();
        if (dustCoroutines != null) {
            foreach (IEnumerator ie in dustCoroutines)
                Game.StopCoroutine(ie);
        }

        if (InputController.currentPrompt != null)
            Input.ResetPrompt(InputController.currentPrompt);

        playedSongIndex = -1;
        CycleFloating(false);
        CycleSupportCrystals(false);
        Audio.ClearPositionalAudio();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        GameObject level = GameObject.FindGameObjectWithTag("Level");
        if (level == null)
            return;

        globalLight.intensity = inEndingRoom ? 1 : 0.5f;
        currentRoom = level.name;
        Grid = new GridSystem(LoadGrid(level.name, currentSave));
        LoadBlocks(level.name, currentSave, Grid);
        
        resetTunnel = nextTunnel;
        if (resetTunnel != null) {
            Data tunnelData = Grid.GetData(resetTunnel.connectedBlocks[0], Layer.Tunnel);
            ApplyData(tunnelData.blockData, tunnelResetItem.blockObject);
        }

        foreach (Screen s in Level.screens) {
            SpriteRenderer[] srs = s.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sr in srs)
                sr.enabled = false;
        }
        
        InitSupportCrystals();

        if (!inEndingRoom) {
            // Create air dust particles and particles that fall from ground
            dustCoroutines.Clear();
            usedAirDustCoords = new bool[Level.airDusts.Length];
            GameObject airDustHolder = new GameObject("AirDustHolder");

            int particleAmount = usedAirDustCoords.Length / 20;
            for (int i = 0; i < particleAmount; i++)
                CreateAirDustParticles();

            IEnumerator groundDustCoroutine = Game.CreateGroundDustParticles(Level.groundDusts);
            dustCoroutines.Add(groundDustCoroutine);
            Game.StartCoroutine(groundDustCoroutine);
        }

        GetFallDatas();

        Player.BuildWorm(nextTunnel);
        Player.Map.InitRoom(currentRoom);

        UpdateRoomColorObjects();
        UpdateColorSymbols();

        levelInitialized = true;
    }

    #endregion

    #region General
    
    public static Coordinates GetCoordinates(Vector3 position, bool snapToGrid = true) {
        if (snapToGrid) return new Coordinates(GetGridCoordinate(position.x), GetGridCoordinate(position.y));
        else            return new Coordinates((int)position.x, (int)position.y);
    }
    public static int GetGridCoordinate(float value) {
        return Mathf.RoundToInt(SnapToGrid(value) / GRID_SIZE);
    }
    public static float SnapToGrid(float worldCoordinate) {
        float sign = Mathf.Sign(worldCoordinate);
        float remainder = Mathf.Abs(worldCoordinate) % GRID_SIZE;
        return remainder <= GRID_SIZE / 2 ? worldCoordinate - sign * remainder : worldCoordinate - sign * remainder + sign * GRID_SIZE;
    }
    public static Vector2 GetGridPosition(Coordinates coordinates) {
        return GetVector(coordinates, GRID_SIZE);
    }
    public static Vector2 GetVector(Coordinates coordinates, float multiplier = 1) {
        Vector2 vector = new Vector2(coordinates.x, coordinates.y);
        return vector * multiplier; 
    }

    public static Color32 GetGameColor(ColorIndex colorIndex) {
        return GetGameColor((int)colorIndex);
    }
    public static Color32 GetGameColor(int colorIndex) {
        return Assets.gameColors[Menu.settingsData.colorIndices[colorIndex]];
    }
    public static void SetGameColor(int colorIndex, Color32 color) {
        foreach (ColorObjects co in colorObjects) {
            co.colorFields[colorIndex].color = color;
            co.colorFields[colorIndex].UpdateColors();
        }
        
        Texture2D map = Player.Map.mapMaster.texture;
        foreach (MapController.RoomData rd in Player.Map.mapData.roomDatas) {
            if (rd.colorPixels == null || rd.colorPixels[colorIndex] == null)
                continue;

            foreach (Coordinates c in rd.colorPixels[colorIndex])
                map.SetPixel(c.x, c.y, color);
        }
        Color32 buttonColor = color; buttonColor.a = Assets.MAP_BUTTON_ALPHA;
        foreach (MapController.RoomData rd in Player.Map.mapData.roomDatas) {
            if (rd.buttonColorPixels == null || rd.buttonColorPixels[colorIndex] == null)
                continue;

            foreach (Coordinates c in rd.buttonColorPixels[colorIndex])
                map.SetPixel(c.x, c.y, buttonColor);
        }

        if ((ColorIndex)colorIndex == ColorIndex.Blue) {
            foreach (ParticlePooler.Particle p in Particles.particles["BlueCrystalBreakParticles"])
                SetParticleColor(p.particleSystem);
            foreach (ParticlePooler.Particle p in Particles.activeParticles) {
                if (p.particleObject.name == "BlueCrystalBreakParticles")
                    SetParticleColor(p.particleSystem);
            }

            void SetParticleColor(ParticleSystem ps) {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(color);
            }
        }
    }
    public static void AddColorObject(ColorIndex colorIndex, SpriteRenderer spriteRenderer = null, Light2D light = null, Image image = null, bool isRoomObject = true) {
        AddColorObject((int)colorIndex, spriteRenderer, light, image, isRoomObject);
    }
    public static void AddColorObject(int colorIndex, SpriteRenderer spriteRenderer = null, Light2D light = null, Image image = null, bool isRoomObject = true) {
        if (colorIndex == -1)
            return;
        
        ColorObjects.ColorField colorField = colorObjects[isRoomObject ? 1 : 0].colorFields[colorIndex];
        if (spriteRenderer != null) colorField.spriteRenderers.Add(spriteRenderer);
        if (light          != null) colorField.lights         .Add(light         );
        if (image          != null) colorField.images         .Add(image         );
    }
    public static void UpdateRoomColorObjects() {
        foreach (ColorObjects.ColorField cf in colorObjects[1].colorFields)
            cf.UpdateColors();
    }

    public static void AddColorSymbols(LevelBlock levelBlock, bool isRoomObject = true) {
        int index = isRoomObject ? 1 : 0;
        if (colorSymbols[index] == null)
            colorSymbols[index] = new List<LevelBlock>();
        colorSymbols[index].Add(levelBlock);

        foreach (LevelBlock.BlockItem bi in levelBlock.GetColorSymbolItems())
            bi.blockObject.transform.eulerAngles = Vector3.zero;
    }
    public static void UpdateColorSymbols() {
        foreach (List<LevelBlock> levelBlocks in colorSymbols) {
            if (levelBlocks == null)
                continue;

            foreach (LevelBlock lb in levelBlocks)
                lb.SetColorSymbols(enableColorSymbols);
        }
    }

    public static int ConvertColorNameToIndex(string name) {
        if (name.Contains("Red"   )) return 0;
        if (name.Contains("Green" )) return 1;
        if (name.Contains("Blue"  )) return 2;
        if (name.Contains("Force" )) return 3;
        if (name.Contains("Time"  )) return 4;
        if (name.Contains("Frag"  )) return 5;
        if (name.Contains("Length")) return 6;
        if (name.Contains("Player")) return 6;
        if (name.Contains("Dig"   )) return 7;
        return -1;
    }

    public static int CycleCurrentCrystalColor() {
        if (++colorCycle >= CRYSTAL_COLORS) colorCycle = 0;
        return colorCycle;
    }

    // Used when player control needs to be locked while something is happening
    public static void FlagGameState(bool flag) {
        gameStateFlags += flag ? 1 : -1;
        resolvedGameState = gameStateFlags == 0;
    }

    public static void StartAction() {
        offScreenActivationCount = 0;
        Level.RecordPuzzleData();
    }

    // Get blocks affected by gravity
    public static void GetFallDatas() {
        List<Data> fallList = Level.puzzleData[(int)PuzzleIndex.Rock];
        if (fallList == null)
            fallList = new List<Data>();

        fallDatas = new FallData[fallList.Count + 1];
        for (int i = 0; i < fallList.Count; i++)
            fallDatas[i + 1] = new FallData(fallList[i]);
    }
    public static FallData GetFallData(Data data) {
        if (!data.blockData.IsPrimary())
            data = Grid.GetData(data.blockData.connectedBlocks[0], data.layer);

        foreach (FallData fd in fallDatas) {
            if (fd.data == data)
                return fd;
        }
        return null;
    }
    static void GetForceData() {
        List<Data> forceList = new List<Data>();
        List<Data> crystalData = Level.puzzleData[(int)PuzzleIndex.Crystal];
        if (crystalData != null) {
            foreach (Data d in crystalData) {
                if (d.blockData.blockName[0] == 'F')
                    forceList.Add(d);
            }
        }
        if (forceList.Count > 0) {
            forceDatas = forceList.ToArray();
            forceMovingData = new bool[forceDatas.Length];
        }
        else {
            forceDatas = null;
            forceMovingData = null;
        }
    }

    // Get data adjacent to coordinates
    public static Data[][] GetNearData(Coordinates coordinates, GridSystem gridSystem) {
        Data[][] nearData = new Data[4][];

        for (int i = 0; i < Coordinates.FacingDirection.Length; i++)
            nearData[i] = gridSystem.GetDatas(coordinates + Coordinates.FacingDirection[i]);

        return nearData;
    }

    // Check which adjecent blocks are of the same type
    public static bool[] GetNearBlocks(Coordinates coordinates, string blockName, Layer layer, GridSystem gridSystem) {
        Data[] nearBlocksFound = new Data[4];
        bool[] nearBlocksNamed = new bool[4];

        for (int i = 0; i < Coordinates.FacingDirection.Length; i++)
            nearBlocksFound[i] = gridSystem.GetData(coordinates + Coordinates.FacingDirection[i], layer);

        for (int i = 0; i < nearBlocksFound.Length; i++) {
            if (nearBlocksFound[i] != null && nearBlocksFound[i].blockData.blockName == blockName)
                nearBlocksNamed[i] = true;
        }

        return nearBlocksNamed;
    }

    public static float GetCurve(float time) {
        return Assets.curve.Evaluate(time);
    }
    public static int GetCurveIndex(float start, float end, float time) {
        return Mathf.RoundToInt(Mathf.Lerp(start, end, GetCurve(time)));
    }
    
    public static void ApplyData(BlockData blockData, GameObject block) {
        ApplyFacing     (blockData.facing     , block);
        ApplyCoordinates(blockData.coordinates, block);
    }
    public static void ApplyCoordinates(Coordinates coords, GameObject block) {
        block.transform.position = GetGridPosition(coords);
    }
    public static void ApplyFacing(int facing, GameObject block) {
        block.transform.eulerAngles = Vector3.forward * facing * 90;
    }

    // Get all scenes included in build
    public static string[] GetSceneNames() {
        string[] sceneNames = new string[SceneManager.sceneCountInBuildSettings - 1];
        for (int i = 1; i < SceneManager.sceneCountInBuildSettings; i++)
            sceneNames[i - 1] = SceneUtility.GetScenePathByBuildIndex(i).Split('/')[2].Split('.')[0];
        return sceneNames;
    }

    #endregion

    #region Blocks

    public static bool MoveBlock(Data data, Coordinates direction, MoveType moveType, float moveSpeed = 0) {
        FlagGameState(true);

        List<Data> moveDatas = new List<Data>();
        List<Coordinates[]> clearedConnections = new List<Coordinates[]>();
        bool checkedPlayer = false;

        if (!CanMove(data.blockData.coordinates)) {
            MoveBlocked(moveType);
            return false;
        }

        if (moveType == MoveType.Player && direction.y == 0)
            Player.HeadBumpCheck(data);

        foreach (Data d in moveDatas) {
            Grid.RemoveData(d);
            if (d.connectedData != null) {
                foreach (Data cd in d.connectedData)
                    Grid.RemoveData(cd);
            }
        }

        bool crystalMoved = false;
        bool rockMoved    = false;
        List<Data> playGroundParticlesList = new List<Data>();
        foreach (Data d in moveDatas) {
            if (moveType != MoveType.Gravity) {
                if (!crystalMoved && d.HasTag(Tag.Float))
                    crystalMoved = true;

                if (d.blockData.blockName == "Rock") {
                    if (direction.x != 0) {
                        playGroundParticlesList.Add(d);
                        if (d.connectedData != null) {
                            foreach (Data cd in d.connectedData)
                                playGroundParticlesList.Add(cd);
                        }
                    }
                    rockMoved = true;
                }
            }
            else {
                // Fall data was pushed by another gravity block instead of falling by itself
                FallData fd = GetFallData(d);
                if (fd != null)
                    fd.fallCount++;
            }

            d.SetMoving(true, direction);
            d.blockData.coordinates += direction;
            for (int i = 0; i < d.blockData.connectedBlocks.Length; i++)
                d.blockData.connectedBlocks[i] += direction;

            if (d.layer != Layer.Player) {
                ColorIndex colorIndex = (ColorIndex)(-1);
                if (d.HasTag(Tag.Float))
                    colorIndex = (ColorIndex)ConvertColorNameToIndex(d.blockData.blockName);
                CheckOffScreenAction(d, colorIndex, Activation.Alt);
            }

            Grid.AddData(d);
            if (d.connectedData != null) {
                foreach (Data cd in d.connectedData) {
                    cd.blockData.coordinates += direction;
                    Grid.AddData(cd);
                }
            }
        }

        Game.StartCoroutine(InitMove());
        return true;

        IEnumerator InitMove() {
            yield return new WaitWhile(() => Player.headBump);

            if (crystalMoved) PlayRandomSound(AudioController.crystalMove);
            if (rockMoved   ) PlayRandomSound(AudioController.rockMove   );

            PlayGroundMovingParticles(playGroundParticlesList, direction.x);
            MoveBlocks(direction, moveDatas, moveSpeed);
            if (moveType != MoveType.Gravity)
                CheckLevelButtons();
        }

        bool CanMove(Coordinates next) {
            Data[] datas = Grid.GetData(next, Layer.Block, Layer.Player, Layer.Piston, Layer.Tunnel);
            foreach (Data d in datas) {
                if (d == null)
                    continue;
                
                if (d.HasTag(Tag.Push)) {
                    if (d.moving)
                        return false;

                    if (clearedConnections.Contains(d.blockData.connectedBlocks)) return true;
                        clearedConnections.Add     (d.blockData.connectedBlocks);
                    moveDatas.Add(Grid.GetData(d.blockData.connectedBlocks[0], d.layer));
                }

                switch (d.layer) {
                    case Layer.Block:
                        if (d.HasTag(Tag.Stop)) {
                            if (moveType == MoveType.Player && d.blockData.blockName == "Gate")
                                return d.blockData.state == (int)Activation.On;
                            return false;
                        }
                        if (!d.HasTag(Tag.Push))
                            return false;
                        if (moveType == MoveType.Gravity && d.HasTag(Tag.Float))
                            return false;
                        break;

                    case Layer.Tunnel:
                        return false;

                    case Layer.Player:
                        if (moveType == MoveType.Player)
                            return false;
                        if (Player.playerData.ignoreGravity || Player.devFlying)
                            return false;

                        if (!checkedPlayer) {
                            Player.InsideBlockCheck();
                            CheckForegrounds();
                            checkedPlayer = true;
                        }
                        break;

                    case Layer.Piston:
                        return d.blockData.state != (int)Activation.On;
                }

                foreach (Coordinates c in d.blockData.connectedBlocks) {
                    if (!CanMove(c + direction))
                        return false;
                }
                break;
            }
            return true;
        }
    }
    public static void MoveBlocks(Coordinates direction, List<Data> moveDatas, float moveSpeed) {
        Game.StartCoroutine(Game.ieMoveBlocks(direction, moveDatas, moveSpeed));
    }
    IEnumerator ieMoveBlocks(Coordinates direction, List<Data> moveDatas, float moveSpeed) {
        Vector2[] positions = new Vector2[moveDatas.Count];
        for (int i = 0; i < moveDatas.Count; i++) {
            Data d = moveDatas[i];
            positions[i] = d.blockObject.transform.position;

            if (d.HasTag(Tag.Player)) {
                foreach (Data cd in d.connectedData)
                    cd.blockObject.transform.SetParent(d.blockObject.transform);
            }
        }

        if (moveSpeed == 0) moveSpeed = BLOCK_MOVE_SPEED;
        float time = 0;
        int moveIndex = 0;
        while (time < 1) {
            int distance = GetCurveIndex(0, BLOCK_SIZE, time);
            if (distance >= moveIndex + 1) {
                moveIndex = distance;
                Vector2 vector = GetVector(direction, moveIndex);
                for (int i = 0; i < moveDatas.Count; i++) {
                    if (moveDatas[i].moving)
                        moveDatas[i].blockObject.transform.position = positions[i] + vector;
                }
            }

            time += Time.deltaTime * moveSpeed;
            yield return null;
        }

        foreach (Data d in moveDatas) {
            if (!d.moving)
                continue;
            
            d.ApplyData();
            d.SetMoving(false);
            if (d.HasTag(Tag.Player)) {
                foreach (Data cd in d.connectedData)
                    cd.blockObject.transform.SetParent(d.blockObject.transform.parent.transform);
            }
        }

        UpdatePistonsIfBlocked();

        FlagGameState(false);
    }
    
    public static void MoveBlocked(MoveType moveType) {
        if (moveType == MoveType.Player) {
            if (++playerMoveBlockedBuffer > 3 || !Player.holdingMove)
                    playerMoveBlockedBuffer = 0;

            if (playerMoveBlockedBuffer == 0 && (Player.wormEye.playingAnimation == null || !Player.wormEye.IsCurrentAnimation(Animation.EyeNod))) {
                Player.WakeUp();
                PlayRandomSound(AudioController.playerBlocked, true);
                Player.PlayAnimation(Animation.EyeNod, Animation.HeadNod, Animation.MouthDefault, true);
            }

            // History is recorded at the start of a move, so erase it
            if (Level.playerHistory.Count > 1) {
                Level.puzzleHistory.Pop();
                Level.playerHistory.Pop();
            }
        }

        FlagGameState(false);
    }
    
    public static void ApplyGravity(bool forceGravity = false) {
        ApplyForce();

        foreach (FallData fd in fallDatas) {
            if (fd.falling || fd.data.moving)
                continue;

            fd.falling = true;
            Game.StartCoroutine(Game.ieApplyGravity(fd, forceGravity));
        }
    }
    IEnumerator ieApplyGravity(FallData fallData, bool forceGravity) {
        FlagGameState(true);
        
        Data data = fallData.data;
        if (fallData.data.HasTag(Tag.Player) && (Player.playerData.ignoreGravity || Player.devFlying)) {
            fallData.falling = false;
            FlagGameState(false);
            yield break;
        }

        // Move all blocks affected by gravity down 1 space at a time until they can't
        while (true) {
            float moveSpeed = Mathf.Clamp(BLOCK_MOVE_SPEED + Mathf.Pow(BLOCK_FALL_STEP * fallData.fallCount, 2), BLOCK_MOVE_SPEED, BLOCK_FALL_SPEED_MAX);
            if (MoveBlock(data, gravityDirection, MoveType.Gravity, moveSpeed)) yield return new WaitWhile(() => data.moving);
            else                                                                break;
        }

        bool reapply = false;
        bool createLandingParticles = false;
        foreach (FallData fd in fallDatas) {
            if (fd.fallCount == 0 || (fd != fallData && fd.falling))
                continue;
            
            bool skip = true;
            foreach (Coordinates c in fd.data.blockData.connectedBlocks) {
                // Make sure data has actually landed and not collided with another data that is also falling
                Data[] datas = Grid.GetData(c + gravityDirection, Layer.Block, Layer.Player, Layer.Piston, Layer.Tunnel);
                foreach (Data d in datas) {
                    if (d != null) {
                        switch (d.layer) {
                            case Layer.Block:
                                if (d.HasTag(Tag.Push)) {
                                    if (d.blockData.connectedBlocks == fd.data.blockData.connectedBlocks)
                                        continue;
                                    if (!d.HasTag(Tag.Float) && GetFallData(d).falling)
                                        continue;
                                }
                                else {
                                    if (d.blockData.blockName == "Ground")
                                        createLandingParticles = true;
                                }
                                break;

                            case Layer.Player:
                                if (fallDatas[0].falling)
                                    continue;
                                break;

                            case Layer.Piston:
                                if (d.blockData.state == (int)Activation.Off)
                                    continue;
                                break;
                        }

                        skip = false;
                        break;
                    }
                }
                if (!skip)
                    break;
            }
            if (skip)
                continue;

            if (fd.data.HasTag(Tag.Player)) {
                if (!Player.wormHead.IsCurrentAnimation(Animation.EatOpen)) {
                    Player.PlayAnimation(Player.wormEye.IsCurrentAnimation(Animation.Roll) ? Animation.None : Animation.EyeDefault, Animation.HeadDefault);
                    Player.Look(false);
                    Player.LookCheck();
                }

                if (fd.fallCount < 2)
                    createLandingParticles = false;
            }
            else {
                if (forceGravity) {
                    // Rock was falling from a force crystal moving downwards and shouldn't actually fall/land
                    PlayRandomSound(AudioController.rockMove);
                    continue;
                }

                float pitch = Mathf.Clamp(1 - 0.1f * ((fd.fallCount - 1) / 2), 0.4f, 1);
                PlayPitchedSound(AudioController.rockLand, AudioController.GetRandomPitch(pitch), true, true);
                ShakeCamera(fd.fallCount, fd.data.blockData.connectedBlocks.Length);
            }

            if (createLandingParticles) {
                // Instead of creating particles under every data that hits a ground block,
                // group datas together that land at the same level and create a larger dust cloud
                // at the center of the group
                List<List<Data>> landDataList = new List<List<Data>>();
                foreach (Coordinates c in fd.data.blockData.connectedBlocks) {
                    Data d = Grid.GetData(c, fd.data.layer);
                    Data[] datas = Grid.GetData(c + gravityDirection, Layer.Block, Layer.Tunnel);
                    if (datas[1] == null && datas[0] != null && datas[0].blockData.blockName == "Ground") {
                        bool foundList = false;
                        foreach (var list in landDataList) {
                            foreach (Data listD in list) {
                                Coordinates adjacentCoords = new Coordinates(Mathf.Abs(listD.blockData.coordinates.x - d.blockData.coordinates.x),
                                                                                       listD.blockData.coordinates.y - d.blockData.coordinates.y);
                                if (adjacentCoords == Coordinates.Right) {
                                    list.Add(d);
                                    foundList = true;
                                    break;
                                }
                            }
                            if (foundList)
                                break;
                        }
                        if (!foundList)
                            landDataList.Add(new List<Data>() { d });
                    }
                }

                CreateLandingParticles(landDataList, fd.fallCount);
            }

            fd.fallCount = 0;
            fd.falling   = false;

            // Reapply gravity after any data has landed, because it might be a part of a group that landed together on a ledge
            // while some of that data is not actually supported by any blocks
            reapply = true; 
        }
        if (reapply)
            ApplyGravity();

        fallData.falling = false;
        FlagGameState(false);
    }
    bool BlocksFalling() {
        if (fallDatas == null)
            return false;

        foreach (FallData fd in fallDatas) {
            if (fd.falling)
                return true;
        }
        return false;
    }

    public static void ApplyForce() {
        if (forceDatas == null || forceDirection == Coordinates.Zero)
            return;

        for (int i = 0; i < forceDatas.Length; i++) {
            if (forceMovingData[i] || forceDatas[i].moving)
                continue;

            forceMovingData[i] = true;
            Game.StartCoroutine(Game.ieApplyForce(forceDatas[i], i));
        }
    }
    IEnumerator ieApplyForce(Data data, int forceIndex) {
        FlagGameState(true);
        
        while (true) {
            if (MoveBlock(data, forceDirection, MoveType.Block)) {
                // Make sure blocks supported by force crystals move accurately with crystal move direction
                if (forceDirection != gravityDirection) {
                    yield return new WaitWhile(() => data.moving);
                    ApplyGravity();
                }
                else {
                    ApplyGravity(true);
                    yield return new WaitWhile(() => data.moving);
                }
            }
            else
                break;
        }

        forceMovingData[forceIndex] = false;
        FlagGameState(false);
    }
    public static bool BlocksForceMoving() {
        if (forceMovingData == null)
            return false;

        foreach (bool b in forceMovingData) {
            if (b)
                return true;
        }
        return false;
    }

    public static void InitFloating() {
        floatStepValues = new float[BLOCK_FLOAT_STEPS];
        for (int i = 0; i < floatStepValues.Length; i++)
            floatStepValues[i] = Mathf.Lerp(-0.5f, 0.5f, GetCurve((float)i / BLOCK_FLOAT_STEPS));
    }
    public static void InitFloatObject(GameObject go, bool local = true) {
        FloatObject fo = new FloatObject {
            floatObject = go,
            offset      = Random.Range(0, BLOCK_FLOAT_STEPS),
            local       = local,
            direction   = Random.Range(0, 2) == 0 ? 1 : -1,
            anchor      = local ? go.transform.localPosition : go.transform.position
        };

        floatObjects.Add(fo);
    }
    public static void CycleFloating(bool enable) {
        if (cycleFloatingCoroutine != null)
            Game.StopCoroutine(cycleFloatingCoroutine);

        if (enable) {
            cycleFloatingCoroutine = Game.ieCycleFloating();
            Game.StartCoroutine(cycleFloatingCoroutine);
        }
    }
    IEnumerator ieCycleFloating() {
        float time = 0;
        while (true) {
            if (time >= 1) {
                time = 0;

                foreach (FloatObject fo in floatObjects) {
                    if (fo.offset <= 0)                     fo.direction =  1;
                    if (fo.offset >= BLOCK_FLOAT_STEPS - 1) fo.direction = -1;
                    fo.offset += fo.direction;

                    Vector3 pos = fo.anchor; pos.y += floatStepValues[fo.offset];
                    if (fo.local) fo.floatObject.transform.localPosition = pos;
                    else          fo.floatObject.transform.position      = pos;
                }
            }
            else
                time += Time.deltaTime * BLOCK_FLOAT_SPEED;

            yield return null;
        }
    }
    
    public static void ApplyTiling(Data data, GridSystem gridSystem) {
        if (data.blockData.spriteState >= Assets.SPRITE_TYPES.Length)
            data.blockData.spriteState = -1;

        // Check if block already has set tiling
        if (data.blockData.spriteState != -1)
            return;

        // Use seperate sprite for tunnel entrance instead of usual end type sprite
        if (data.layer == Layer.Tunnel && data.blockData.IsPrimary()) {
            if (data.blockData.connectedBlocks.Length < 2)
                return;

            data.blockData.facing = Coordinates.GetFacing(data.blockData.coordinates - data.blockData.connectedBlocks[1]);
            data.blockData.spriteState = 0;
            return;
        }

        // If made of more than 1 block, only tile with connected blocks, else tile with blocks of same type
        bool[] nearBlocks = null;
        if (data.HasTag(Tag.Connect)) {
            nearBlocks = new bool[4];
            foreach (Coordinates c in data.blockData.connectedBlocks) {
                if (c != data.blockData.coordinates) {
                    Coordinates direction = c - data.blockData.coordinates;
                    for (int j = 0; j < Coordinates.FacingDirection.Length; j++) {
                        if (direction == Coordinates.FacingDirection[j]) {
                            nearBlocks[j] = true;
                            break;
                        }
                    }
                }
            }
        }
        else
            nearBlocks = GetNearBlocks(data.blockData.coordinates, data.blockData.blockName, data.layer, gridSystem);
        
        if (nearBlocks != null) {
            // Match adjacent blocks to sprite types to determine which type of tiling it has
            bool done = false;
            for (int blockType = 0; blockType < Assets.SPRITE_TYPES.Length; blockType++) {
                for (int facingType = 0; facingType < Assets.SPRITE_TYPES[blockType].Length; facingType++) {
                    bool equal = true;
                    for (int i = 0; i < nearBlocks.Length; i++) {
                        if (nearBlocks[i] != Assets.SPRITE_TYPES[blockType][facingType][i]) {
                            equal = false;
                            break;
                        }
                    }
                    if (!equal)
                        continue;

                    if (data.blockData.spriteState < 0)
                        data.blockData.spriteState = blockType;
                    data.blockData.facing = facingType;

                    // Set end of tunnel facing to face next tunnel data instead of away from it
                    // so player can move through it accurately
                    // Normally an end piece faces away from adjacent data so the flat side matches up,
                    // but a tunnel end piece is the same as a pipe piece so it still matches
                    Coordinates[] cb = data.blockData.connectedBlocks;
                    if (data.layer == Layer.Tunnel && data.blockData.coordinates == cb[cb.Length - 1])
                        data.blockData.facing = Coordinates.GetFacing(cb[cb.Length - 2] - data.blockData.coordinates);

                    done = true;
                    break;
                }
                if (done)
                    break;
            }
        }
    }

    static void InitSupportCrystals() {
        List<Coordinates>[][] scc = Grid.GetGridData().supportCrystalCoordinates;
        if (scc == null)
            return;

        supportCrystals = new Light2D[scc.Length][][];
        for (int i = 0; i < scc.Length; i++) {
            supportCrystals[i] = new Light2D[scc[i].Length][];
            AudioIndex audioIndex = (AudioIndex)i;
            for (int j = 0; j < supportCrystals[i].Length; j++) {
                if (scc[i][j] != null) {
                    supportCrystals[i][j] = new Light2D[scc[i][j].Count];
                    for (int k = 0; k < scc[i][j].Count; k++) {
                        Coordinates coordinates = scc[i][j][k];
                        Data crystalData = Grid.GetData(coordinates, Layer.SupportCrystal);
                        LevelBlock.BlockItem primaryItem = crystalData.levelBlock.GetBlockItem("Primary");
                        AddColorObject(i, primaryItem.spriteRenderer, primaryItem.light);
                        Audio.InitPositionalAudio(audioIndex, crystalData);
                        supportCrystals[i][j][k] = primaryItem.light;
                    }
                }
            }
        }

        CycleSupportCrystals(true);
    }
    static void CycleSupportCrystals(bool enable) {
        if (supportCrystalsCoroutine != null)
            Game.StopCoroutine(supportCrystalsCoroutine);

        if (enable) {
            supportCrystalsCoroutine = Game.ieCycleSupportCrystals();
            Game.StartCoroutine(supportCrystalsCoroutine);
        }
    }
    IEnumerator ieCycleSupportCrystals() {
        while (true) {
            for (int i = 0; i < supportCrystals.Length; i++) {
                if (!Player.playerData.colors[i]) {
                    Audio.PlaySupportCrystal(i);
                    for (int j = 0; j < supportCrystals[i].Length; j++) {
                        if (supportCrystals[i][j] != null) {
                            for (int k = 0; k < supportCrystals[i][j].Length; k++)
                                StartCoroutine(SupportCrystalGlow(supportCrystals[i][j][k]));
                        }
                        yield return new WaitForSeconds(SUPPORT_CRYSTAL_FLOW_WAIT);
                    }
                }
                yield return new WaitForSeconds(SUPPORT_CRYSTAL_COLOR_WAIT);
            }
            yield return new WaitForSeconds(SUPPORT_CRYSTAL_CYCLE_WAIT);
        }
    }
    // Pulse through crystals backwards away from flower
    static void UnpowerSupportCrystals(ColorIndex colorIndex) {
        PlayPitchedSound(AudioController.powerDown, AudioController.GetRandomPitch(1.5f));
        Game.StartCoroutine(Game.ieUnpowerSupportCrystals(colorIndex));
    }
    IEnumerator ieUnpowerSupportCrystals(ColorIndex colorIndex) {
        Light2D[][] sc = supportCrystals[(int)colorIndex];
        for (int i = sc.Length - 1; i >= 0; i--) {
            if (sc[i] != null) {
                for (int j = 0; j < sc[i].Length; j++)
                    StartCoroutine(SupportCrystalGlow(sc[i][j]));
            }
            yield return new WaitForSeconds(SUPPORT_CRYSTAL_FLOW_WAIT);
        }
    }
    IEnumerator SupportCrystalGlow(Light2D light) {
        float fromIntensity = 0.30f;
        float toIntensity   = 1.15f;

        float time = 0;
        while (time < 1) {
            light.intensity = Mathf.Lerp(fromIntensity, toIntensity, GetCurve(time));

            time += Time.deltaTime * SUPPORT_CRYSTAL_GLOW_SPEED;
            yield return null;
        }
        light.intensity = toIntensity;

        time = 0;
        while (time < 1) {
            light.intensity = Mathf.Lerp(toIntensity, fromIntensity, GetCurve(time));

            time += Time.deltaTime * SUPPORT_CRYSTAL_GLOW_SPEED;
            yield return null;
        }
        light.intensity = fromIntensity;
    }

    #endregion

    #region Abilities

    public static void InitBullets() {
        bullets = new Bullet[BULLET_AMOUNT];
        GameObject bulletHolder = new GameObject("BulletHolder");
        bulletHolder.transform.SetParent(Game.gameObjectsHolder.transform);
        bulletHolder.transform.localPosition = Vector3.zero;
        for (int i = 0; i < bullets.Length; i++) {
            GameObject bullet = Instantiate(Assets.bullet, bulletHolder.transform);
            bullet.SetActive(false);
            bullets[i] = new Bullet(bullet, bullet.GetComponent<SpriteRenderer>(), bullet.GetComponent<Light2D>());
            bulletPool.Enqueue(bullets[i]);
        }
    }

    public static void UpdateAbilityInfo() {
        Player.UpdateInventory();
        Menu.UpdateInventoryUI(Player.playerData);
        Menu.UpdateControlLocks(Player.playerData);
        Input.UpdateToggleButtons(Player.playerData);
    }
    
    public static void Eat(Data eatData) {
        PlayerController.PlayerData pd = Player.playerData;
        Player.PlayMouthAnimation();

        if (eatData.blockData.blockName != "GateSlot")
            DestroyCollectable(eatData, true);

        // Check type of collectable and activate ability
        int abilityIndex = -1;
        switch (eatData.blockData.blockName) {
            case "GateSlot":
                PlayRandomSound(AudioController.collectFragment);
                SetGateSlot(eatData, Activation.Off);
                SetGates(Activation.Off);
                Player.AddToInventory(InventoryIndex.Fragment, 1);
                break;

            case "CollectFragment":
                PlayRandomSound(AudioController.collectFragment);
                Player.AddToInventory(InventoryIndex.Fragment, 1);
                break;

            case "CollectLength":
                PlayRandomSound(AudioController.collectLength);
                Player.AddToInventory(InventoryIndex.Grow, 1);
                if (pd.lengthIncrements % PlayerController.LENGTH_INCREMENT_AMOUNT != 0)
                    break;

                abilityIndex = (int)AbilityIndex.Grow;
                if (!pd.abilities[abilityIndex]) {
                     pd.abilities[abilityIndex] = true;
                    ActivatePromptSequence(PromptIndex.Grow, ColorIndex.Length);
                    Menu.UnlockControl(ControlIndex.Grow, true);
                    Input.UnlockToggleButton(Action.Grow, true);
                }
                else
                    AddToLengthMeter(false);
                break;

            case "CollectForce":
                PlayRandomSound(AudioController.collectColor);
                pd.abilities[(int)AbilityIndex.Force] = true;
                ActivatePromptSequence(PromptIndex.Force, ColorIndex.Force);
                Player.AddToInventory(InventoryIndex.Force);
                Menu.UnlockControl(ControlIndex.Force, true);
                Input.UnlockToggleButton(Action.Force, true);
                break;

            case "CollectDig":
                PlayRandomSound(AudioController.collectColor);
                pd.abilities[(int)AbilityIndex.Dig] = true;
                ActivatePromptSequence((PromptIndex)(-1), ColorIndex.Dig);
                Player.AddToInventory(InventoryIndex.Dig);
                Player.Map.RevealMapDigEntrances();
                break;

            case "CollectTime":
                PlayRandomSound(AudioController.collectTime);
                abilityIndex = (int)AbilityIndex.Undo;
                if (!pd.abilities[abilityIndex]) {
                     pd.abilities[abilityIndex] = true;

                    // Retroactively add undo to history so ability stays when undoing after collection
                    int puzzleIndex = (int)PuzzleIndex.Collectable;
                    BlockData.Diff diff = eatData.blockData.GetDiff();
                    foreach (List<BlockData.Diff>[] history in Level.puzzleHistory) {
                        List<BlockData.Diff> h = history[puzzleIndex];
                        for (int i = 0; i < h.Count; i++) {
                            if (h[i].coordinates == eatData.blockData.coordinates)
                                h[i] = diff;
                        }
                    }
                    foreach (PlayerController.PlayerData.Diff d in Level.playerHistory)
                        d.abilities[abilityIndex] = true;

                    ActivatePromptSequence(PromptIndex.Undo, ColorIndex.Time);
                    Player.AddToInventory(InventoryIndex.Undo);
                    Menu.UnlockControl(ControlIndex.BackUndo, true);
                }
                else {
                    abilityIndex = (int)AbilityIndex.Reset;
                    pd.abilities[abilityIndex] = true;
                    Level.resetPlayerRecord.abilities[abilityIndex] = true;
                    ActivatePromptSequence(PromptIndex.Reset, ColorIndex.Time);
                    Player.AddToInventory(InventoryIndex.Reset);
                    Menu.UnlockControl(ControlIndex.Reset, true);
                }
                break;

            case "CollectSong":
                PlayRandomSound(AudioController.collectLength);

                // When undoing/resetting after collecting a song and recollecting it don't play it again, just unlock it
                if (playedSongIndex == -1) {
                    for (int i = 0; i < pd.songs.Length; i++) {
                        if (!pd.songs[i]) {
                            ActivateSong(i);
                            break;
                        }
                    }

                    ActivatePromptSequence((PromptIndex)(-2), (ColorIndex)(-1));
                }
                else
                    ActivateSong(playedSongIndex);

                void ActivateSong(int index) {
                    pd.songs[index] = true;
                    pd.currentSongIndex = playedSongIndex = index;
                }
                break;

            // Crystal Colors
            default:
                PlayRandomSound(AudioController.collectColor);
                int colorIndex = ConvertColorNameToIndex(eatData.blockData.blockName);
                pd.abilities[(int)AbilityIndex.Shoot] = true;
                pd.colors[colorIndex] = true;
                UnpowerSupportCrystals((ColorIndex)colorIndex);
                ActivatePromptSequence(PromptIndex.Shoot, (ColorIndex)colorIndex);
                Player.AddToInventory((InventoryIndex)colorIndex);
                Menu.UnlockControl(ControlIndex.SelectShoot, true);
                break;
        }
    }
    
    public static void PlaceFragment(Data slotData) {
        FlagGameState(true);
        Game.StartCoroutine(Game.iePlaceFragment(slotData));
    }
    IEnumerator iePlaceFragment(Data slotData) {
        Player.AddToInventory(InventoryIndex.Fragment, -1);
        PlayRandomSound(AudioController.placeFragment);
        Player.PlayMouthAnimation();

        // Use the fragment sprite attached to the slot for placement instead of a separate one
        LevelBlock.BlockItem fragmentItem = slotData.levelBlock.GetBlockItem("SlotFragment");
        GameObject f = fragmentItem.blockObject;
        f.transform.position = Player.wormHead.data.blockObject.transform.position;
        fragmentItem.spriteRenderer.enabled = fragmentItem.light.enabled = true;
        f.SetActive(true);

        Coordinates direction = Coordinates.FacingDirection[Player.wormHead.data.blockData.facing];
        Vector2 position = f.transform.position;
        float time = 0;
        int moveIndex = 0;
        while (time < 1) {
            int distance = Mathf.RoundToInt(Mathf.Lerp(0, BLOCK_SIZE, time));
            if (distance >= moveIndex + 1) {
                moveIndex = distance;
                f.transform.position = position + GetVector(direction, moveIndex);
            }

            time += Time.deltaTime * BULLET_MOVE_SPEED;
            yield return null;
        }
        f.transform.position = slotData.blockObject.transform.position;

        SetGateSlot(slotData, Activation.On);
        SetGates(GateSlotsActivated() ? Activation.On : Activation.Alt);
        
        yield return new WaitForSeconds(0.2f);

        FlagGameState(false);
    }
    
    public static void Shoot(Coordinates origin, int facing) {
        activeBullets++;
        Game.StartCoroutine(Game.ieShoot(origin, facing));
    }
    IEnumerator ieShoot(Coordinates origin, int facing) {
        if (!shooting) {
            FlagGameState(true);

            shooting = true;
            Player.ResetAnimation();
            Player.ColorEyes();

            // Recall bullets from buttons
            if (DeactivateBulletButtons()) {
                yield return new WaitUntil(() => gameStateFlags == 1);

                activeBullets--;
                shooting = cancelBullets = false;
                FlagGameState(false);
                yield break;
            }

            PlayRandomSound(AudioController.playerShoot);
            Player.PlayMouthAnimation();
        }

        // Init Bullet
        Bullet b = bulletPool.Dequeue();
        GameObject bullet = b.bulletObject;
        b.light.color = b.spriteRenderer.color = GetGameColor(colorCycle);
        bullet.SetActive(true);
        ApplyCoordinates(origin, bullet);
        Coordinates direction = Coordinates.FacingDirection[facing];

        // Move projectile and check for button/block collisions
        Data hitData = null;
        Coordinates nextBlock = origin;
        bool enteredPipe = false;
        while (hitData == null) {
            if (cancelBullets)
                break;

            nextBlock += direction;
            Data[] hitDatas = Grid.GetData(nextBlock, Layer.Block, Layer.Tunnel, Layer.Piston, Layer.Player, Layer.Misc);
            Data buttonData = null;
            int nextFacing = -1;
            foreach (Data d in hitDatas) {
                if (d != null) {
                    switch (d.layer) {
                        case Layer.Block:
                            switch (d.blockData.blockName) {
                                case "Gate":
                                    if (d.blockData.state == (int)Activation.On)
                                        continue;
                                    break;

                                case "Pipe":
                                    switch (d.blockData.spriteState) {
                                        // Cross
                                        case 0:
                                            EnterPipe();
                                            continue;

                                        // Straight
                                        case 1:
                                        case 3:
                                            if ((facing % 2) - (d.blockData.facing % 2) == 0) {
                                                EnterPipe();
                                                continue;
                                            }
                                            break;

                                        //Corner
                                        case 2:
                                            for (int i = 0; i < 2; i++) {
                                                int checkFacing = facing + i + 1;
                                                if (checkFacing >= Coordinates.FacingDirection.Length)
                                                    checkFacing -= Coordinates.FacingDirection.Length;

                                                if (d.blockData.facing == checkFacing) {
                                                    nextFacing = checkFacing;

                                                    if ((facing % 2) - (nextFacing % 2) == 0) {
                                                        if (++nextFacing >= Coordinates.FacingDirection.Length)
                                                            nextFacing -= Coordinates.FacingDirection.Length;
                                                    }
                                                    break;
                                                }
                                            }
                                            if (nextFacing != -1) {
                                                EnterPipe();
                                                continue;
                                            }
                                            break;
                                    }

                                    void EnterPipe() {
                                        if (!enteredPipe) {
                                            PlayRandomSound(AudioController.pipeEnter);
                                            enteredPipe = true;
                                        }
                                    }
                                    break;
                            }
                            break;

                        case Layer.Piston:
                            if (d.blockData.state == (int)Activation.Off)
                                continue;
                            break;

                        case Layer.Misc:
                            int colorIndex = ConvertColorNameToIndex(d.blockData.blockName);
                            if (colorIndex != -1 && colorIndex < CRYSTAL_COLORS && Player.playerData.colors[colorIndex])
                                buttonData = d;
                            continue;
                    }

                    hitData = d;
                    break;
                }
            }

            if (hitData != null)
                b.light.enabled = false;

            bool moving = true;
            StartCoroutine(MoveBullet(hitData == null ? BLOCK_SIZE : 4));
            yield return new WaitWhile(() => moving);

            if (hitData == null) {
                if (enteredPipe && Grid.GetData(nextBlock, Layer.Block) == null) {
                    PlayRandomSound(AudioController.pipeExit);
                    enteredPipe = false;
                }

                ApplyCoordinates(nextBlock, bullet);
                if (nextFacing != -1) {
                    facing = nextFacing;
                    direction = Coordinates.FacingDirection[facing];
                }
            }

            IEnumerator MoveBullet(int maxDistance) {
                Vector2 position = bullet.transform.position;
                float time = 0;
                int moveIndex = 0;
                while (time < 1) {
                    int distance = Mathf.RoundToInt(Mathf.Lerp(0, maxDistance, time));
                    if (distance >= moveIndex + 1) {
                        moveIndex = distance;
                        bullet.transform.position = position + GetVector(direction, moveIndex);

                        if (buttonData != null && moveIndex > 2) {
                            SetButton(buttonData, Activation.Alt);
                            UpdatePanelLights(Activation.Off);
                            buttonData = null;
                        }
                    }

                    time += Time.deltaTime * BULLET_MOVE_SPEED;
                    yield return null;
                }
                bullet.transform.position = position + GetVector(direction, maxDistance);
                moving = false;
            }
        }

        bullet.SetActive(false);
        b.light.enabled = true;
        bulletPool.Enqueue(b);
        activeBullets--;

        // Check what projectile hit, if crystal then activate it
        bool notUnlocked = false;
        if (hitData != null) {
            switch (hitData.blockData.blockName) {
                // Get ends of red crystal and shoot another projectile from them, red crystals cannot be activated twice in same shoot action
                case "RedCrystal":
                    if (!Player.playerData.colors[(int)ColorIndex.Red]) {
                        notUnlocked = true;
                        break;
                    }

                    if (!activeRedCrystals.Contains(hitData.blockData.connectedBlocks)) {
                        activeRedCrystals.Add(hitData.blockData.connectedBlocks);
                        PlayRandomSound(AudioController.redShoot);
                        ActivateCrystals(hitData, ColorIndex.Red);
                        foreach (Coordinates c in hitData.blockData.connectedBlocks) {
                            Data d = Grid.GetData(c, Layer.Block);
                            switch (d.blockData.spriteState) {
                                case 3:
                                    Shoot(c, d.blockData.facing);
                                    break;

                                case 5:
                                    Shoot(c, d.blockData.facing);
                                    Shoot(c, Coordinates.GetFacing(-Coordinates.FacingDirection[d.blockData.facing]));
                                    break;
                            }
                        }
                    }
                    break;

                // Move green crystal
                case "GreenCrystal":
                    if (!Player.playerData.colors[(int)ColorIndex.Green]) {
                        notUnlocked = true;
                        break;
                    }

                    ActivateCrystals(hitData, ColorIndex.Green);
                    PlayRandomSound(AudioController.greenMove);
                    
                    if (MoveBlock(hitData, direction, MoveType.Block)) {
                        yield return new WaitUntil(() => !hitData.moving);
                        ApplyGravity();
                    }
                    else
                        PlayRandomSound(AudioController.playerBlocked);
                    break;

                // Destroy blue crystal
                case "BlueCrystal":
                    if (!Player.playerData.colors[(int)ColorIndex.Blue]) {
                        notUnlocked = true;
                        break;
                    }

                    ActivateCrystals(hitData, ColorIndex.Blue);

                    yield return new WaitForSeconds(0.2f);

                    DestroyBlueCrystal(hitData, true);
                    CheckLevelButtons();
                    ApplyGravity();
                    break;

                case "ForceCrystal":
                    notUnlocked = true;
                    break;
            }

            // Play activation animation
            void ActivateCrystals(Data crystalData, ColorIndex colorIndex) {
                PlayRandomSound(AudioController.crystalActivate);

                List<LevelBlock.BlockItem> crystalActivations = crystalData.levelBlock.GetBlockItems("CrystalActivation");
                if (colorIndex == ColorIndex.Red) {
                    foreach (LevelBlock.BlockItem bi in crystalActivations)
                        bi.animator.SetBool("LoopActivate", true);
                }
                else {
                    foreach (LevelBlock.BlockItem bi in crystalActivations)
                        bi.animator.SetTrigger("Activate");
                }

                if (!crystalData.blockData.IsPrimary())
                    crystalData = Grid.GetData(crystalData.blockData.connectedBlocks[0], Layer.Block);
                CheckOffScreenAction(crystalData, colorIndex, (Activation)3);
            }
        }

        // Create particles away from block hit, or in a circle if cancelled
        if ((hitData != null && !hitData.HasTag(Tag.Float)) || notUnlocked || (hitData == null && cancelBullets)) {
            PlayRandomSound(AudioController.bulletHit);
            ParticlePooler.Particle p = Particles.GetParticle("BulletParticles");
            if (p != null) {
                p.particleObject.transform.position = bullet.transform.position;
                ApplyFacing(facing, p.particleObject);

                List<Color32> colors = new List<Color32>();
                for (int i = 0; i < CRYSTAL_COLORS; i++) {
                    if (Player.playerData.colors[i])
                        colors.Add(colorObjects[0].colorFields[i].color);
                }

                ParticleSystem ps = p.particleSystem;
                var main = ps.main;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                if (cancelBullets)
                    shape.shapeType = ParticleSystemShapeType.Sphere;

                int emitCount = 9 / colors.Count;
                foreach (Color32 c in colors) {
                    Color newColor = c;
                    main.startColor = new ParticleSystem.MinMaxGradient(newColor);
                    ps.Emit(emitCount);
                }
                Particles.PlayParticle(p);
            }
        }

        // No more active projectiles
        if (shooting && activeBullets == 0) {
            foreach (Coordinates[] cc in activeRedCrystals) {
                StartCoroutine(DeactivateCrystal(cc[0], cc.Length > 1 ? 0 : 0.2f));

                IEnumerator DeactivateCrystal(Coordinates coordinates, float delay) {
                    yield return new WaitForSeconds(delay);

                    List<LevelBlock.BlockItem> crystalActivations = Grid.GetData(coordinates, Layer.Block).levelBlock.GetBlockItems("CrystalActivation");
                    foreach (LevelBlock.BlockItem bi in crystalActivations)
                        bi.animator.SetBool("LoopActivate", false);
                }
            }
            activeRedCrystals.Clear();
            
            shooting = cancelBullets = false;
            FlagGameState(false);
        }
    }
    public static void SetBulletColor(Color32 color) {
        foreach (Bullet b in bullets) {
            if (b.bulletObject.activeSelf)
                b.light.color = b.spriteRenderer.color = color;
        }
    }
    
    public static void Undo(bool active) {
        if (active) Game.StartCoroutine(Game.ieUndo());
        else        undoPercent = undoWait = 0;
    }
    IEnumerator ieUndo() {
        FlagGameState(true);

        if (Level.puzzleHistory.Count == 1) {
            FlagGameState(true);
            MoveBlocked(MoveType.Player);

            yield return new WaitForSeconds(0.5f);

            FlagGameState(false);
            yield break;
        }
        
        PlayPitchedSound(AudioController.playerUndo, AudioController.GetRandomPitch(undoPercent + 1));
        Player.ResetAnimation();
        Player.UndoEyes();
        Player.WakeUp();

        // Go through history to a previous unique state
        // States can be repeated when the player does something but nothing changes,
        // such as jumping straight up or shooting at a wall
        List<BlockData.Diff>[] puzzleDataRecord = null;
        PlayerController.PlayerData.Diff playerRecord = default;
        bool sameState = true;
        while (sameState && Level.puzzleHistory.Count > 0) {
            puzzleDataRecord = Level.puzzleHistory.Pop();
            playerRecord     = Level.playerHistory.Pop();
            for (int i = 0; i < puzzleDataRecord.Length; i++) {
                if (puzzleDataRecord[i] == null)
                    continue;

                if (i < Level.puzzleData.Length) {
                    for (int j = 0; j < puzzleDataRecord[i].Count; j++) {
                        if (!Level.puzzleData[i][j].CompareDiff(puzzleDataRecord[i][j])) {
                            sameState = false;
                            break;
                        }
                    }
                }
                else {
                    for (int j = 0; j < puzzleDataRecord[i].Count; j++) {
                        if (!Player.worm[j].data.CompareDiff(puzzleDataRecord[i][j])) {
                            sameState = false;
                            break;
                        }
                    }
                }

                if (!sameState)
                    break;
            }
        }
        ApplyPuzzleDataRecord(puzzleDataRecord, playerRecord, Activation.On);

        offScreenActivationCount = 0;
        Player.LookCheck();

        // Undoing gets faster the longer it's held down
        if (undoPercent < 1)
            undoPercent = Mathf.Clamp(undoPercent + (undoPercent * 0.5f), 0.01f, 1);
        undoWait += Mathf.Lerp(0.35f, 0.05f, undoPercent);
        while (undoWait > 0) {
            if (InputController.Get(Action.Undo, PressType.Up)) {
                Undo(false);
                break;
            }

            undoWait -= Time.deltaTime;
            yield return null;
        }

        FlagGameState(false);
    }
    void ApplyPuzzleDataRecord(List<BlockData.Diff>[] puzzleDataRecord, PlayerController.PlayerData.Diff playerRecord, Activation instant) {
        List<Data> moveData = new List<Data>();
        List<Data> miscData = new List<Data>();
        int forceState = -1;

        for (int i = 0; i < puzzleDataRecord.Length; i++) {
            if (puzzleDataRecord[i] == null)
                continue;

            PuzzleIndex puzzleIndex = (PuzzleIndex)i;
            if (puzzleIndex != PuzzleIndex.Player) {
                for (int j = 0; j < puzzleDataRecord[i].Count; j++) {
                    Data d = Level.puzzleData[i][j];
                    BlockData.Diff diff = puzzleDataRecord[i][j];

                    switch (puzzleIndex) {
                        case PuzzleIndex.Rock   :
                        case PuzzleIndex.Crystal:
                            ColorIndex colorIndex = (ColorIndex)ConvertColorNameToIndex(d.blockData.blockName);
                            switch (colorIndex) {
                                case ColorIndex.Blue:
                                    if (d.blockData.destroyed != diff.destroyed && instant == Activation.On)
                                        CheckOffScreenAction(d, ColorIndex.Time, (Activation)3);
                                    DestroyBlueCrystal(d, diff.destroyed, instant);
                                    break;

                                case ColorIndex.Force:
                                    if (d.blockData.state != diff.state && instant == Activation.On)
                                        CheckOffScreenAction(d, ColorIndex.Time, (Activation)3);
                                    if (forceState == -1)
                                        forceState = diff.state;
                                    break;
                            }

                            if (d.blockData.coordinates != diff.coordinates) {
                                Coordinates offset = diff.coordinates - d.blockData.coordinates;

                                if (instant == Activation.On) {
                                    // Get most significant normalized direction from last state so the off screen action arrow icon accurately represents move direction
                                    // Usually needed when a block is pushed off a ledge and falls
                                    Coordinates direction = new Coordinates(Mathf.Clamp(offset.x, -1, 1), Mathf.Clamp(offset.y, -1, 1));
                                    if (Mathf.Abs(offset.x) <= Mathf.Abs(offset.y)) direction.x = 0;
                                    else                                            direction.y = 0;
                                    d.SetMoving(false, direction);
                                    CheckOffScreenAction(d, ColorIndex.Time, Activation.Alt);
                                }

                                moveData.Add(d);
                                Grid.RemoveData(d);
                                if (d.connectedData != null) {
                                    foreach (Data cd in d.connectedData) {
                                        Grid.RemoveData(cd);
                                        cd.blockData.coordinates += offset;
                                    }
                                }
                                for (int c = 0; c < d.blockData.connectedBlocks.Length; c++)
                                    d.blockData.connectedBlocks[c] += offset;
                            }

                            d.ApplyDiff(diff);
                            break;

                        case PuzzleIndex.Button:
                            if (d.blockData.state != diff.state) {
                                miscData.Add(d);
                                if (instant == Activation.On) {
                                    Activation a = (Activation)diff.state;
                                    if (a == Activation.Alt) a = Activation.On;
                                    CheckOffScreenAction(d, ColorIndex.Time, a);
                                }
                            }

                            SetButton(d, (Activation)diff.state, instant);
                            break;

                        case PuzzleIndex.Piston:
                            d.blockData.state = diff.state;
                            break;

                        case PuzzleIndex.Panel:
                            if (d.blockData.state != diff.state && d.blockData.state != (int)Activation.Alt) {
                                miscData.Add(d);
                                if (instant == Activation.On)
                                    CheckOffScreenAction(d, ColorIndex.Time, (Activation)diff.state);
                            }

                            Panel p = (Panel)d.levelBlock.GetBlockItem("Panel").script;
                            p.SetPanel((Activation)diff.state, instant);
                            break;

                        case PuzzleIndex.Dig:
                            d.blockData.destroyed = diff.destroyed;
                            SetDig(d, diff.state, instant);
                            break;

                        case PuzzleIndex.Fg:
                            SetForeground(d, (Activation)diff.state, instant);
                            break;

                        case PuzzleIndex.GateSlot:
                            if (d.blockData.state != diff.state) {
                                miscData.Add(d);
                                if (instant == Activation.On)
                                    CheckOffScreenAction(d, ColorIndex.Time, (Activation)diff.state);
                            }

                            SetGateSlot(d, (Activation)diff.state, instant);
                            break;

                        case PuzzleIndex.Gate:
                            if (d.blockData.state != diff.state) {
                                miscData.Add(d);
                                if (instant == Activation.On)
                                    CheckOffScreenAction(d, ColorIndex.Time, (Activation)diff.state);
                            }

                            SetGate(d, (Activation)diff.state, instant);
                            break;

                        case PuzzleIndex.Collectable:
                            if (d.blockData.destroyed != diff.destroyed) {
                                miscData.Add(d);
                                if (instant == Activation.On)
                                    CheckOffScreenAction(d, ColorIndex.Time, diff.destroyed ? Activation.Off : Activation.On);

                                // Usually an ability prompt is activated when a collectable is eaten, so turn off the feedback prompt when uneaten
                                // Fragments and length upgrades activate the inventory prompt though, so that one can stay displayed
                                if (InputController.currentPrompt != null && d.blockData.blockName != "CollectFragment" && d.blockData.blockName != "CollectLength")
                                    InputController.currentPrompt.promptHolders[1].SetActive(false);
                            }

                            DestroyCollectable(d, diff.destroyed, instant);
                            break;
                    }
                }
            }

            // Update visuals and other general states after individual data states have been applied
            switch (puzzleIndex) {
                case PuzzleIndex.Rock   :
                case PuzzleIndex.Crystal:
                    foreach (Data d in moveData) {
                        if (d.blockData.destroyed)
                            continue;

                        Grid.AddData(d);
                        if (d.connectedData != null) {
                            foreach (Data cd in d.connectedData)
                                Grid.AddData(cd);
                        }
                    }
                    if (forceState != -1)
                        SetForceDirection(forceState == 4 ? Coordinates.Zero : Coordinates.FacingDirection[forceState], instant);
                    break;

                case PuzzleIndex.Button:
                    Level.RecalculateButtonColorActivations();
                    break;

                case PuzzleIndex.Piston:
                    UpdatePistonArms();
                    break;

                case PuzzleIndex.Dig:
                    foreach (SpriteRenderer sr in digOutlines.Values)
                        sr.sprite.texture.Apply();
                    break;

                case PuzzleIndex.Player:
                    if (!levelInitialized)
                        continue;

                    PlayerController.PlayerData pd = Player.playerData;
                    Player.UpdateUndoOutline();
                    if (instant == Activation.On)
                        FlashUndoOutline(Player.undoSpriteRenderer);

                    List<PlayerController.Worm> worm = Player.worm;
                    foreach (PlayerController.Worm w in worm)
                        Grid.RemoveData(w.data);

                    int lengthDiff = worm.Count - puzzleDataRecord[i].Count;
                    if (lengthDiff != 0) {
                        pd.length = puzzleDataRecord[i].Count;

                        int amount = Mathf.Abs(lengthDiff);
                        if (lengthDiff > 0) {
                            for (int j = 0; j < amount; j++) {
                                Player.SetWormPieceActive(false);
                                IncrementLengthMeter(-1);
                            }
                        }
                        else {
                            for (int j = 0; j < amount; j++) {
                                Player.SetWormPieceActive(true);
                                IncrementLengthMeter(1);
                            }
                        }

                        Player.UpdateGradients(true);
                    }
                    
                    for (int j = 0; j < worm.Count; j++) {
                            worm[j].data.ApplyDiff(puzzleDataRecord[i][j]);
                            Grid.AddData(worm[j].data);
                            worm[j].data.ApplyData();
                    }
                    Player.UpdateConnections();

                    bool updateAbilities = UpdateAbilities();
                    pd.ApplyDiff(playerRecord);
                    Player.SetDigging(pd.digging, null, instant);
                    Player.UpdateHeadFlip();
                    if (updateAbilities)
                        UpdateAbilityInfo();

                    bool UpdateAbilities() {
                        int playerRecordMaxLength = playerRecord.GetMaxLength();
                        if (pd.GetMaxLength() != playerRecordMaxLength) {
                            UpdateLengthMeter(pd.length, playerRecordMaxLength);
                            return true;
                        }
                        if (pd.fragments != playerRecord.fragments)
                            return true;

                        for (int j = 0; j < playerRecord.abilities.Length; j++) {
                            if (pd.abilities[j] != playerRecord.abilities[j])
                                return true;
                        }
                        for (int j = 0; j < playerRecord.colors.Length; j++) {
                            if (pd.colors[j] != playerRecord.colors[j])
                                return true;
                        }

                        return false;
                    }
                    break;
            }
        }
        if (instant == Activation.On && (moveData.Count > 0 || miscData.Count > 0))
            StartCoroutine(ShowUndoOutlines(moveData, miscData));

        CheckLevelButtons(instant);
    }
    // Move the outline of blocks that were teleported and flash the outline of blocks with a state change
    IEnumerator ShowUndoOutlines(List<Data> moveData, List<Data> miscData) {
        FlagGameState(true);
        List<(LevelBlock.BlockItem undoItem, Vector2 pos)> moveItems = new List<(LevelBlock.BlockItem undoItem, Vector2 pos)>();
        foreach (Data d in moveData) {
            LevelBlock.BlockItem undoItem = d.levelBlock.GetBlockItem("UndoOutline");
            undoItem.blockObject.transform.localPosition += d.blockObject.transform.position - (Vector3)GetGridPosition(d.blockData.coordinates);
            undoItem.blockObject.SetActive(true);
            moveItems.Add((undoItem, undoItem.blockObject.transform.localPosition));
            d.ApplyData();
        }
        List<LevelBlock.BlockItem> miscItems = new List<LevelBlock.BlockItem>();
        foreach (Data d in miscData) {
            LevelBlock.BlockItem undoItem = d.levelBlock.GetBlockItem("UndoOutline");
            undoItem.blockObject.SetActive(true);
            miscItems.Add(undoItem);
        }
    
        Color32 fromColor = GetGameColor(ColorIndex.Time);
        Color32 toColor   = fromColor; toColor.a = 0;

        float moveTime  = 0;
        float moveSpeed = Mathf.Lerp(7.5f, 15f, undoPercent);
        while (moveTime < 1) {
            Color32 color = Color32.Lerp(fromColor, toColor, moveTime);

            foreach (var m in moveItems) {
                Vector2 pos = Vector2.Lerp(m.pos, Vector2.zero, moveTime);
                pos = new Vector2(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
                m.undoItem.blockObject.transform.localPosition = pos;

                m.undoItem.spriteRenderer.color = color;
            }
            foreach (LevelBlock.BlockItem bi in miscItems)
                bi.spriteRenderer.color = color;

            moveTime += Time.deltaTime * moveSpeed;
            yield return null;
        }

        foreach (var m in moveItems) {
            m.undoItem.blockObject.transform.localPosition = Vector3.zero;
            m.undoItem.spriteRenderer.color = toColor;
            m.undoItem.blockObject.SetActive(false);
        }
        foreach (LevelBlock.BlockItem bi in miscItems) {
            bi.spriteRenderer.color = toColor;
            bi.blockObject.SetActive(false);
        }

        FlagGameState(false);
    }
    
    public static void ResetRoom() {
        if (!resetting)
            return;

        PlayRandomSound(AudioController.playerReset);

        Level.RecordPuzzleData();
        Game.ApplyPuzzleDataRecord(Level.resetDataRecord, Level.resetPlayerRecord, Activation.On);

        if (nextTunnel != null)
            Player.SetToTunnel(resetTunnel);

        CheckLevelButtons(Activation.On);
        UpdatePistonsIfBlocked(Activation.On);
        ResolveReset();

        FlagGameState(true);
        Game.StartCoroutine(ResetBuffer());

        // Don't let player reset again until they let go of the button or enough time has passed
        IEnumerator ResetBuffer() {
            float time = 0;
            while (InputController.Get(Action.Reset)) {
                time += Time.deltaTime;
                if (time > 1) break;
                yield return null;
            }
            FlagGameState(false);
        }
    }
    public static void ResolveReset() {
        Player.ResetEyes(false);
        ShowResetMeter(false);
        resetting = false;
        Player.currentResetTime = 0;
    }

    #endregion

    #region Tunneling

    public static void EnterTunnel(MapController.RoomData.Tunnel tunnel, Data tunnelData, bool tunnelBack) {
        FlagGameState(true);
        Game.StartCoroutine(Game.ieEnterTunnel(tunnel, tunnelData, tunnelBack));
    }
    IEnumerator ieEnterTunnel(MapController.RoomData.Tunnel tunnel, Data tunnelData, bool tunnelBack) {
        if (tunnel == null || tunnel.connectedTunnel == null) {
            FlagGameState(false);

            if (devMode)
                Pause(true);

            yield break;
        }

        Player.ResetAnimation();
        
        nextTunnel = tunnel.connectedTunnel;
        bool updateMapRoom = !tunnel.visited;
        tunnel.visited = nextTunnel.visited = true;
        Player.Map.UpdateMapTunnel(tunnel, updateMapRoom);

        string room = nextTunnel.roomName;
        if (!tunnel.local) {
            MapController.RoomData nextRoom = Player.Map.GetRoomData(room);
            for (int i = 0; i < nextRoom.tunnels.Length; i++) {
                if (nextRoom.tunnels[i] == nextTunnel) {
                    Player.playerData.currentRoom = room + ":" + i;
                    break;
                }
            }

            if (tunnel.gatePanelIndex != -1)
                Player.playerData.gateTunnels[tunnel.gatePanelIndex] = true;

            disableScreenSwitching = true;
        }
        Player.playerData.ignoreGravity = true;

        // Move backwards into tunnel
        if (tunnelBack) {
            // Check if it's a local tunnel and the player is too long
            int tunnelCount = Player.GetTunnelPositions(tunnel).Count;
            if (!tunnel.local || (tunnel.local && Player.playerData.length - tunnelCount <= 0)) {
                PlayRandomSound(AudioController.playerTunnel);

                Grid.RemoveData(Player.wormHead.data);
                Grid.RemoveData(Player.worm[1] .data);
                ApplyGravity();

                // If the player head is starting in the entrance move 1 block backward,
                // otherwise move 2 blocks because the head is outside of the entrance
                int total = BLOCK_SIZE;
                if (Grid.GetData(Player.wormHead.data.blockData.coordinates, Layer.Tunnel) == null)
                    total += BLOCK_SIZE - 1;

                Vector2 position = Player.wormHead.data.blockObject.transform.position;
                Coordinates direction = -Coordinates.FacingDirection[Player.wormHead.data.blockData.facing];
                float time = 0;
                int moveIndex = 0;
                while (time < 1) {
                    int distance = Mathf.RoundToInt(Mathf.Lerp(0, total, time));
                    if (distance >= moveIndex + 1) {
                        moveIndex = distance;
                        Player.wormHead.data.blockObject.transform.position = position + GetVector(direction, moveIndex);
                        if (moveIndex > 2) {
                            for (int i = 1; i < Player.worm.Count; i++)
                                Player.worm[i].data.blockObject.SetActive(false);
                        }
                        if (moveIndex > 6) Player.worm[0].light.enabled = false;
                    }

                    time += Time.deltaTime * BLOCK_MOVE_SPEED;
                    yield return null;
                }
                Player.wormHead.data.blockObject.transform.position = position + GetVector(direction, total);
                Player.wormHead.data.SetMoving(false);

                yield return new WaitForSeconds(0.02f);

                CheckTemporaryOpenDoors();

                yield return new WaitWhile(() => BlocksFalling());

                if (tunnel.local) {
                    yield return new WaitForSeconds(0.05f * tunnelCount);

                    Player.SetToTunnel(nextTunnel);
                    FlagGameState(false);
                    yield break;
                }
                else {
                    yield return new WaitForSeconds(0.01f);
                    SetTransition(true, tunnel);
                }
            }
            else {
                MoveBlocked(MoveType.Player);
                yield break;
            }
        }
        else {
            // Set player to move through tunnel until all the way in
            // If it's a local tunnel, keep moving to the other entrance
            List<BlockData> positionData = Player.GetTunnelPositions(tunnel);
            Coordinates direction = Coordinates.Zero;
            int count = tunnel.local ? positionData.Count : Player.playerData.length + 1;

            for (int i = 0; i < count; i++) {
                if (i < Player.playerData.length) {
                    PlayPitchedSound(AudioController.playerTunnel, 1 + 0.1f * i);
                    Player.worm[i].light.enabled = false;
                }

                if (i < positionData.Count)
                    direction = -Coordinates.FacingDirection[positionData[i].facing];
                Player.MovePlayer(direction, true);

                yield return new WaitUntil(() => !Player.wormHead.data.moving);

                if (i < Player.playerData.length) {
                    ApplyGravity();
                    
                    yield return new WaitWhile(() => BlocksFalling());

                    if (!tunnel.local && Player.playerData.length - i == 2)
                        SetTransition(true, tunnel);
                }
            }

            if (tunnel.local) {
                Player.Map.UpdateMapTunnel(nextTunnel, updateMapRoom);
                ExitTunnel(nextTunnel, false);
                FlagGameState(false);
                yield break;
            }
        }
        
        if (!tunnel.local) {
            yield return new WaitUntil(() => transitionState == Activation.On);

            if (savePlayer) {
                Player.Map.SaveMap();
                SavePlayer(Player);
            }
            if (saveRoom)
                SaveGrid(currentRoom, currentSave, Grid);

            LoadRoom(room);
        }

        FlagGameState(false);
    }
    
    public static void ExitTunnel(MapController.RoomData.Tunnel tunnel, bool playTransition) {
        Game.StartCoroutine(Game.ieExitTunnel(tunnel, playTransition));
    }
    IEnumerator ieExitTunnel(MapController.RoomData.Tunnel tunnel, bool playTransition) {
        if (tunnel == null)
            yield break;

        FlagGameState(true);
        disableScreenSwitching = false;

        Data tunnelData = Grid.GetData(tunnel.connectedBlocks[0]       , Layer.Tunnel);
        Data doorData   = Grid.GetData(tunnelData.blockData.coordinates, Layer.Misc  );
        if (doorData == null) {
            FlagGameState(false);
            yield break;
        }

        // Check if tunnel was locked and open door
        LevelBlock.BlockItem panelItem = doorData.levelBlock.GetBlockItem("Panel");
        if (panelItem != null) {
            Data panelData = Grid.GetData(tunnel.connectedBlocks[1], Layer.Misc);
            if (panelData.blockData.state == (int)Activation.Off) {
                TunnelPanel tp = (TunnelPanel)panelItem.script;
                if (tp.lightColors != null && tp.lightColors.Length > 0) {
                    tp.SetPanel(Activation.Alt);
                    yield return new WaitForSeconds(0.3f);
                }
            }
        }

        Coordinates direction = Coordinates.FacingDirection[tunnelData.blockData.facing];
        if (direction.x != 0) {
            Player.playerData.headFlipped = direction.x < 0;
            Player.UpdateHeadFlip();
        }

        Player.worm[1].data.blockObject.SetActive(true);

        if (!tunnel.local) {
            Player.ResetHeadTrigger();

            if (playTransition) {
                yield return new WaitForSeconds(0.1f);
                SetTransition(false, tunnel);
            }
        }

        // Move player out one block and push any obstacles
        Coordinates moveCoords = tunnelData.blockData.coordinates + direction;
        Data moveBlock = Grid.GetData(moveCoords, Layer.Block);
        bool moved = false;
        if (moveBlock == null || MoveBlock(moveBlock, direction, MoveType.Player)) {
            moved = true;
            PlayRandomSound(AudioController.playerTunnel);
            Player.MovePlayer(direction, true);
            Player.wormHead.light.enabled = true;
        }
        if (moved) {
            yield return new WaitUntil(() => !Player.wormHead.data.moving);
            ApplyGravity();
        }
        for (int i = 2; i < Player.worm.Count; i++)
            Player.worm[i].data.blockObject.SetActive(true);

        if (!tunnel.local) {
            ThinkIndex thinkIndex = CheckThinkPromptActivation();
            Player.ActivateThinkOption(thinkIndex, thinkIndex == ThinkIndex.RoomInfo);
        }

        FlagGameState(false);
    }

    #endregion

    #region UI
    
    public static void Pause(bool active, bool ignoreCheck = false) {
        if (!ignoreCheck && (!levelInitialized || !canPause))
            return;
        
        if (devMode)
            Cursor.visible = active;

        PlayRandomSound(active ? AudioController.menuOpen : AudioController.menuClose);
        Input.EnablePromptActions(currentPromptIndex, active);
        Menu.ShowMenu(active);

        if (InputController.Get(Action.Shoot)) Game.StartCoroutine(PauseBuffer());
        else                                   paused = active;

        // Prevents shooting when selecting a menu item that closes the pause menu
        IEnumerator PauseBuffer() {
            yield return new WaitUntil(() => InputController.Get(Action.Shoot, PressType.None));
            paused = active;
        }
    }

    public static void SetTransition(bool active, MapController.RoomData.Tunnel setToTunnel = null) {
        if (setToTunnel != null) {
            Data tunnelData = Grid.GetData(setToTunnel.connectedBlocks[0], Layer.Tunnel);
            ApplyCoordinates(tunnelData.blockData.coordinates, Game.transition);
            Game.transition.transform.position += (Vector3)GetVector(Coordinates.FacingDirection[tunnelData.blockData.facing]);
        }

        transitionState = Activation.Alt;
        if (transitionCoroutine != null) Game.StopCoroutine(transitionCoroutine);
            transitionCoroutine  = Game.ieSetTransition(active);
        Game.StartCoroutine(transitionCoroutine);
    }
    IEnumerator ieSetTransition(bool active) {
        float time  = 0;
        float speed = 1.75f;
        if (active) {
            while (time < 1) {
                transitionSpriteRenderer.sprite = Assets.transitionSprites[GetCurveIndex(0, Assets.transitionSprites.Length - 1, time)];
                time += Time.deltaTime * speed;
                yield return null;
            }
            transitionSpriteRenderer.sprite = Assets.transitionSprites[Assets.transitionSprites.Length - 1];
        }
        else {
            while (time < 1) {
                transitionSpriteRenderer.sprite = Assets.transitionSprites[GetCurveIndex(Assets.transitionSprites.Length - 1, 0, time)];
                time += Time.deltaTime * speed;
                yield return null;
            }
            transitionSpriteRenderer.sprite = Assets.transitionSprites[0];
        }
        transitionState = active ? Activation.On : Activation.Off;
    }

    public static void SetFade(bool active, bool instant = false) {
        fadeState = Activation.Alt;
        if (active) {
            FlagGameState(true);
            Game.fadeOverlay.gameObject.SetActive(true);
        }

        Game.StartCoroutine(Game.ieSetFade(active, instant));
    }
    IEnumerator ieSetFade(bool active, bool instant) {
        float fromAlpha = 0;
        float toAlpha   = 1;
        if (!active) {
            fromAlpha = 1;
            toAlpha   = 0;
        }
        Color color = Color.black;

        if (!instant) {
            float time  = 0;
            float speed = 1.75f;
            while (time < 1) {
                color.a = Mathf.Lerp(fromAlpha, toAlpha, GetCurve(time));
                fadeOverlay.color = color;

                time += Time.deltaTime * speed;
                yield return null;
            }
        }
        color.a = toAlpha;
        fadeOverlay.color = color;

        fadeState = active ? Activation.On : Activation.Off;
        if (!active) {
            FlagGameState(false);
            Game.fadeOverlay.gameObject.SetActive(false);
        }
    }

    public static void SetCutscene(bool active) {
        FlagGameState(active);
        canPause = !active;
        Game.StartCoroutine(Game.SetCutsceneBarPosition(active ? Vector2.zero : cutsceneBarPos));
    }
    IEnumerator SetCutsceneBarPosition(Vector2 position) {
        Vector2 startPos = cutsceneBars[0].transform.localPosition;

        float time = 0;
        while (time < 1) {
            foreach (GameObject go in cutsceneBars)
                go.transform.localPosition = Vector2.Lerp(startPos, position, GetCurve(time));

            time += Time.deltaTime * 3.5f;
            yield return null;
        }
        foreach (GameObject go in cutsceneBars)
            go.transform.localPosition = position;
    }

    static void SetPopUp(Text popUp, bool active) {
        if (active)
            popUp.gameObject.SetActive(true);

        Game.StartCoroutine(Game.ieSetPopUp(popUp, active));
    }
    IEnumerator ieSetPopUp(Text popUp, bool active) {
        Vector2 pos = popUp.transform.localPosition;
        float fromY = pos.y - 30;
        float toY   = pos.y;
        float fromAlpha = 0;
        float toAlpha   = 1;
        if (!active) {
            fromY = pos.y;
            toY   = pos.y - 30;
            fromAlpha = 1;
            toAlpha   = 0;
        }
        Color color = popUp.color;

        float time = 0;
        while (time < 1) {
            float curveTime = GetCurve(time);
            pos.y = Mathf.Lerp(fromY, toY, curveTime);
            popUp.transform.localPosition = pos;
            color.a = Mathf.Lerp(fromAlpha, toAlpha, curveTime);
            popUp.color = color;

            time += Time.deltaTime * 3f;
            yield return null;
        }
        pos.y = toY;
        popUp.transform.localPosition = pos;
        color.a = toAlpha;
        popUp.color = color;

        if (!active)
            popUp.gameObject.SetActive(false);
    }

    // Blocks affected off screen show an icon indicating movement or state change on the side of the screen
    public static void CheckOffScreenAction(Data data, ColorIndex colorIndex, Activation activation, bool reset = false) {
        int[] bounds = currentScreen.screenData.bounds;
        Check(data.blockData.coordinates);
        if (data.connectedData != null) {
            foreach (Data d in data.connectedData)
                Check(d.blockData.coordinates);
        }

        void Check(Coordinates c) {
            if (offScreenActivationPool.Count == 0)
                return;

            Coordinates applyCoord = c;
            if      (c.y >= bounds[0]) applyCoord.y = bounds[0] - 1;
            else if (c.y <= bounds[1]) applyCoord.y = bounds[1] + 1;
            if      (c.x <= bounds[2]) applyCoord.x = bounds[2] + 1;
            else if (c.x >= bounds[3]) applyCoord.x = bounds[3] - 1;

            // Block isn't off screen
            if (applyCoord == c)
                return;

            Color32 color = Color.white;
            if ((int)colorIndex == -1) {
                switch (data.blockData.blockName) {
                    case "Gate"      :
                    case "GatePanel" :
                    case "Rock"      : color = Assets.MAP_COLORS[data.blockData.blockName]; break;
                    case "GateSlot"  :
                    case "Panel"     : color = Assets.MAP_COLORS["TunnelAlt"             ]; break;
                    case "PistonArm" : color = Assets.MAP_COLORS["Piston"                ]; break;
                }
            }
            else
                color = GetGameColor(colorIndex);

            OffScreenActivation osa = offScreenActivationPool.Dequeue();
            ApplyCoordinates(applyCoord, osa.activationObject);
            switch (activation) {
                case Activation.Alt: ApplyFacing(Coordinates.GetFacing(data.moveDirection), osa.activationObject); break; // Moving
                case (Activation)5 : ApplyFacing(data.blockData.facing                    , osa.activationObject); break; // Resetting

                case Activation.Off:
                case Activation.On :
                    if (data.levelBlock == null || data.levelBlock.layer == Layer.Misc)
                        break;

                    LevelBlock.BlockItem panelItem = data.levelBlock.GetBlockItem("Panel");
                    if (panelItem != null) {
                        Panel p = (Panel)panelItem.script;
                        if (p.inverted) {
                            switch (activation) {
                                case Activation.Off: activation = Activation.On ; break;
                                case Activation.On : activation = Activation.Off; break;
                            }
                        }
                    }
                    break;
            }
            osa.spriteRenderer.sprite = Game.offScreenActivationSprites[(int)activation];
            osa.activationObject.SetActive(true);
            
            if (reset) offScreenResetOutlines.Add(osa);
            else       Game.StartCoroutine(Game.ShowOffScreenActivation(osa, color, colorIndex == ColorIndex.Time ? Mathf.Lerp(5f, 15f, undoPercent) : 1));
        }
    }
    IEnumerator ShowOffScreenActivation(OffScreenActivation offScreenActivation, Color32 color, float speed) {
        Color32 fromColor = color; fromColor.a = 255;
        Color32 toColor   = color; toColor  .a = 0;
        offScreenActivation.spriteRenderer.sortingOrder = offScreenActivationCount++;

        float time = 0;
        while (time < 1) {
            color = Color32.Lerp(fromColor, toColor, GetCurve(time));
            offScreenActivation.spriteRenderer.color = color;

            time += Time.deltaTime * speed;
            yield return null;
        }
        offScreenActivation.spriteRenderer.color = toColor;
        offScreenActivation.activationObject.SetActive(false);
        offScreenActivationPool.Enqueue(offScreenActivation);
    }
    
    public static void InitLengthMeter() {
        lengthMeterObjects = new GameObject[PlayerController.WORM_BLOCK_AMOUNT];
        lengthMeterImages  = new Image[PlayerController.WORM_BLOCK_AMOUNT][];

        for (int i = 0; i < PlayerController.WORM_BLOCK_AMOUNT; i++) {
            GameObject backDrop = Instantiate(Assets.lengthMeterPiece, Game.lengthMeter.transform);
            lengthMeterObjects[i] = backDrop;
            float spriteWidth = Assets.lengthMeterPiece.GetComponent<RectTransform>().rect.width;
            backDrop.transform.localPosition = Vector2.right * (i * spriteWidth - i * spriteWidth / BLOCK_SIZE);

            GameObject meterPiece = Instantiate(backDrop, backDrop.transform);
            meterPiece.transform.localPosition = Vector3.right * -7.14f;

            lengthMeterImages[i] = new Image[] { meterPiece.GetComponent<Image>(), backDrop.GetComponent<Image>() };
            lengthMeterImages[i][1].color = Assets.backDropColor;

            if (i == 0)
                SetLengthSprite(lengthMeterImages[i], 3);
            else {
                if (i < Player.playerData.length - 1)
                    SetLengthSprite(lengthMeterImages[i], 2);
                else {
                    if (i == Player.playerData.length - 1) SetLengthSprite(lengthMeterImages[i], 1);
                    else                                   SetLengthSprite(lengthMeterImages[i], 0);
                }
            }
            if (i > Player.playerData.GetMaxLength() - 1)
                backDrop.SetActive(false);
        }

        Game.lengthMeter.SetActive(false);
    }
    public static void UpdateLengthMeter(int length, int maxLength) {
        for (int i = 0; i < PlayerController.WORM_BLOCK_AMOUNT; i++) {
            if (i == 0)
                SetLengthSprite(lengthMeterImages[i], 3);
            else {
                if (i < length - 1)
                    SetLengthSprite(lengthMeterImages[i], 2);
                else {
                    if (i == length - 1) SetLengthSprite(lengthMeterImages[i], 1);
                    else                 SetLengthSprite(lengthMeterImages[i], 0);
                }
            }
            lengthMeterObjects[i].SetActive(i <= maxLength - 1);
        }
    }
    public static void ShowLengthMeter(bool active) {
        if (!showUI)
            return;

        Game.lengthMeter.SetActive(active);
    }
    public static void AddToLengthMeter(bool promptActivation) {
        if (Player.playerData.GetMaxLength() > PlayerController.WORM_BLOCK_AMOUNT)
            return;

        FlagGameState(true);
        Game.StartCoroutine(Game.ieAddToLengthMeter(promptActivation));
    }
    IEnumerator ieAddToLengthMeter(bool promptActivation) {
        ShowLengthMeter(true);

        yield return new WaitForSeconds(0.4f);

        PlayRandomSound(AudioController.lengthMeterAdd);
        lengthMeterObjects[Player.playerData.GetMaxLength() - 1].SetActive(true);
        
        if (!promptActivation) {
            yield return new WaitForSeconds(0.4f);
            ShowLengthMeter(false);
        }

        FlagGameState(false);
    }
    public static void IncrementLengthMeter(int direction) {
        if (direction > 0) {
            SetLengthSprite(lengthMeterImages[Player.playerData.length - 2], 2);
            SetLengthSprite(lengthMeterImages[Player.playerData.length - 1], 1);
        }
        else {
            SetLengthSprite(lengthMeterImages[Player.playerData.length    ], 0);
            SetLengthSprite(lengthMeterImages[Player.playerData.length - 1], 1);
        }
    }
    // Change length meter piece, type = (0: Empty, 1: Head, 2: Body, 3: Tail)
    public static void SetLengthSprite(Image[] lengthMeterImages, int type) {
        foreach (Image i in lengthMeterImages)
            i.sprite = Assets.lengthMeterSprites[type];
    }
    
    public static void ShowResetMeter(bool active) {
        if (!showUI)
            return;

        Game.resetImage.gameObject.SetActive(active);
    }
    public static void SetResetMeterLength(float percent) {
        int index = (int)(percent * Assets.resetMeterSprites.Length / 100);
        if (resetIndex == index || index >= Assets.resetMeterSprites.Length)
            return;

        resetIndex = index;
        PlayPitchedSound(AudioController.playerResetIncrement, AudioController.GetRandomPitch(Mathf.Clamp(0.5f + index * 0.05f, 0.5f, 1.5f)));
        Game.resetImage.sprite = Assets.resetMeterSprites[index];
    }
    
    public static void FadeResetOutlines() {
        Game.StartCoroutine(Game.ieFadeResetOutlines());
    }
    IEnumerator ieFadeResetOutlines() {
        float fromAlpha = 0;
        float toAlpha    = 1;
        float fromIntensity = 0;
        float toIntensity   = 0.2f;
        Color32 timeColor = GetGameColor(ColorIndex.Time);
        Color32 eyeColor  = Assets.eyeColor;
        
        Level.resetHolder          .SetActive(true);
        tunnelResetItem.blockObject.SetActive(true);

        float time = 0;
        while (resetting) {
            float lerp = Mathf.Abs(Mathf.Sin(time));
            Player.wormEye.light.intensity = Mathf.Lerp(fromIntensity, toIntensity, lerp);
            Player.wormEye.spriteRenderer.color = Color32.Lerp(eyeColor, timeColor, lerp);
            
            Color color = timeColor; color.a = Mathf.Lerp(fromAlpha, toAlpha, lerp);
            foreach (SpriteRenderer sr in resetOutlines)
                sr.color = color;
            foreach (OffScreenActivation osa in offScreenResetOutlines)
                osa.spriteRenderer.color = color;

            time += Time.deltaTime * 3.5f;
            yield return null;
        }
        timeColor.a = 0;
        foreach (SpriteRenderer sr in resetOutlines)
            sr.color = timeColor;

        foreach (OffScreenActivation osa in offScreenResetOutlines) {
            osa.activationObject.SetActive(false);
            offScreenActivationPool.Enqueue(osa);
        }

        Level.resetHolder          .SetActive(false);
        tunnelResetItem.blockObject.SetActive(false);
    }
    // If a block's state is different from what it was when the room was entered it will show an outline when resetting
    public static void UpdateActiveResetOutlines() {
        offScreenResetOutlines.Clear();
        if (resetTunnel != null)
            CheckOffScreenAction(Grid.GetData(resetTunnel.connectedBlocks[0], Layer.Misc), ColorIndex.Time, (Activation)5, true);

        for (int i = 0; i < Level.resetDataRecord.Length; i++) {
            if (Level.resetDataRecord[i] == null)
                continue;

            PuzzleIndex puzzleIndex = (PuzzleIndex)i;
            switch (puzzleIndex) {
                case PuzzleIndex.Piston:
                case PuzzleIndex.Dig   :
                case PuzzleIndex.Fg    :
                case PuzzleIndex.Player:
                    continue;
            }

            for (int j = 0; j < Level.resetDataRecord[i].Count; j++) {
                Data d = Level.puzzleData[i][j];
                BlockData.Diff diff = Level.resetDataRecord[i][j];
                LevelBlock.BlockItem resetItem = d.levelBlock.GetBlockItem("ResetOutline");
                if (resetItem == null)
                    continue;

                resetItem.blockObject.SetActive(!d.blockData.CompareDiff(diff));

                switch (puzzleIndex) {
                    case PuzzleIndex.Rock   :
                    case PuzzleIndex.Crystal:
                        ColorIndex colorIndex = (ColorIndex)ConvertColorNameToIndex(d.blockData.blockName);
                        switch (colorIndex) {
                            case ColorIndex.Blue:
                                if (d.blockData.destroyed != diff.destroyed)
                                    CheckOffScreenAction(d, ColorIndex.Time, (Activation)3, true);
                                break;

                            case ColorIndex.Force:
                                if (d.blockData.state != GetForceState(forceDirection))
                                    CheckOffScreenAction(d, ColorIndex.Time, (Activation)3, true);
                                break;
                        }
                        if (d.blockData.coordinates != diff.coordinates)
                            CheckOffScreenAction(d, ColorIndex.Time, (Activation)4, true);
                        break;

                    case PuzzleIndex.Button  :
                    case PuzzleIndex.Panel   :
                    case PuzzleIndex.Gate    :
                    case PuzzleIndex.GateSlot:
                        if (d.blockData.state != diff.state) {
                            Activation a = (Activation)diff.state;
                            if (a == Activation.Alt) a = Activation.On;
                            CheckOffScreenAction(d, ColorIndex.Time, a, true);
                        }
                        break;

                    case PuzzleIndex.Collectable:
                        if (d.blockData.destroyed != diff.destroyed)
                            CheckOffScreenAction(d, ColorIndex.Time, diff.destroyed ? Activation.Off : Activation.On, true);
                        break;
                }
            }
        }
    }
    public static void FlashUndoOutline(SpriteRenderer spriteRenderer) {
        Game.StartCoroutine(Game.ieFlashUndoOutline(spriteRenderer));
    }
    IEnumerator ieFlashUndoOutline(SpriteRenderer spriteRenderer) {
        FlagGameState(true);

        Color32 fromColor = GetGameColor(ColorIndex.Time);
        Color32 toColor   = fromColor; toColor.a = 0;
        spriteRenderer.enabled = true;

        float time = 0;
        float fadeSpeed = Mathf.Lerp(5f, 15f, undoPercent);
        while (time < 1) {
            spriteRenderer.color = Color32.Lerp(fromColor, toColor, GetCurve(time));

            time += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        spriteRenderer.color   = toColor;
        spriteRenderer.enabled = false;

        FlagGameState(false);
    }

    public static void ShowSongIndex(int index) {
        Game.songIndexText.text = index + "";
        if (songIndexCoroutine != null)
            return;

        songIndexCoroutine = Game.ieShowSongIndex();
        Game.StartCoroutine(songIndexCoroutine);
    }
    IEnumerator ieShowSongIndex() {
        bool unFade = false;
        StartCoroutine(FadeSongIndex(true));

        yield return new WaitUntil(() => unFade);
        yield return new WaitForSeconds(0.5f);

        StartCoroutine(FadeSongIndex(false));

        IEnumerator FadeSongIndex(bool active) {
            float fromAlpha = 0;
            float toAlpha   = 1;
            if (!active) {
                fromAlpha = 1;
                  toAlpha = 0;
            }
            Color color = Color.white;

            float time  = 0;
            float speed = 2;
            while (time < 1) {
                color.a = Mathf.Lerp(fromAlpha, toAlpha, GetCurve(time));
                songIndexImage.color = songIndexText.color = color;

                time += Time.deltaTime * speed;
                yield return null;
            }
            color.a = toAlpha;
            songIndexImage.color = songIndexText.color = color;

            unFade = true;
            if (!active)
                songIndexCoroutine = null;
        }
    }

    public static void ActivateDream(bool active) {
        if (active) {
            if (Player.playerData.activatedDreams == null) {
                // Set all dreams except the first to be already activated so that the first one always gets played initially
                Player.playerData.activatedDreams = new bool[Game.dreams.Length];
                for (int i = 1; i < Player.playerData.activatedDreams.Length; i++)
                    Player.playerData.activatedDreams[i] = true;
            }

            // Select a random dream that hasn't been activated yet
            List<int> unactivatedDreams = new List<int>();
            for (int i = 0; i < Player.playerData.activatedDreams.Length; i++) {
                if (!Player.playerData.activatedDreams[i])
                    unactivatedDreams.Add(i);
            }
            int selectedDream = unactivatedDreams.Count == 0 ? 0 : unactivatedDreams[Random.Range(0, unactivatedDreams.Count)];
            Player.playerData.activatedDreams[selectedDream] = true;
            ActivateCloudBlips((PromptIndex)(-selectedDream - 1));

            // If all dreams have been activated, reset them all except the one that was just activated
            bool reset = true;
            foreach (bool b in Player.playerData.activatedDreams) {
                if (!b) {
                    reset = false;
                    break;
                }
            }
            if (reset) {
                Player.playerData.activatedDreams = new bool[Game.dreams.Length];
                Player.playerData.activatedDreams[selectedDream] = true;
            }
        }
        else {
            if (Game.dreamCloud.activeSelf)
                Game.StartCoroutine(Game.CloudPuffs((PromptIndex)(-1)));
        }
    }
    IEnumerator PlayDream(DreamSequence dreamSequence) {
        FlagGameState(false);

        dreamCloud.transform.parent.position = cloudAnchor.transform.position;
        dreamCloud.SetActive(true);
        StartCoroutine(CloudFloat(dreamCloud, cloudBlips[0][cloudBlips.Length - 1].spriteRenderer));

        Sprite[] cloudSprites        = Assets.dreamCloudExpandSprites;
        Sprite[] cloudOutlineSprites = Assets.dreamCloudExpandOutlineSprites;

        int dreamFrame  = 0;
        int dreamFrames = dreamSequence.dreamFrames[0].sprites.Length;
        int minPlayCount = 2;

        int cloudFrame  = 0;
        int cloudFrames = cloudSprites.Length;
        bool expand = true;

        while (dreamCloud.activeSelf) {
            for (int i = 0; i < dreamSpriteRenderers.Length; i++)
                dreamSpriteRenderers[i].sprite = dreamSequence.dreamFrames[i].sprites[dreamFrame];

            Sprite cloudSprite = cloudSprites[cloudFrame];
            dreamCloudMask.sprite = cloudSprite;
            for (int i = 0; i < dreamCloudSpriteRenderers.Length - 1; i++)
                dreamCloudSpriteRenderers[i].sprite = cloudSprite;
            dreamCloudSpriteRenderers[dreamCloudSpriteRenderers.Length - 1].sprite = cloudOutlineSprites[cloudFrame];

            if (++dreamFrame >= dreamFrames) {
                dreamFrame = 0;

                // Dream will play a certain number of times before letting player move
                if (--minPlayCount == 0)
                    FlagGameState(false);
            }
            // First the cloud will expand, then loop through its undulating animation
            if (++cloudFrame >= cloudFrames) {
                cloudFrame = 0;

                if (expand) {
                    cloudSprites        = Assets.dreamCloudSprites;
                    cloudOutlineSprites = Assets.dreamCloudOutlineSprites;
                    cloudFrames = cloudSprites.Length;
                    expand = false;
                }
            }

            yield return new WaitForSeconds(0.2f);
        }
        foreach (SpriteRenderer sr in dreamSpriteRenderers     ) sr.sprite = null;
        foreach (SpriteRenderer sr in dreamCloudSpriteRenderers) sr.sprite = null;

        if (minPlayCount > 0)
            FlagGameState(false);
    }

    // Cutscene Bars On -> Player Eye Glow / Animation -> Cutscene Bars Off -> Prompt Activation
    public static void ActivatePromptSequence(PromptIndex promptIndex, ColorIndex colorIndex) {
        Game.StartCoroutine(Game.ieActivatePromptSequence(promptIndex, colorIndex));
    }
    IEnumerator ieActivatePromptSequence(PromptIndex promptIndex, ColorIndex colorIndex) {
        FlagGameState(true);
        SetCutscene(true);

        yield return new WaitForSeconds(1);
        
        switch (colorIndex) {
            case (ColorIndex)(-1):
                Player.CollectEyes(Color.white);

                if (promptIndex == (PromptIndex)(-2)) {
                    yield return new WaitForSeconds(1);
                    Player.PlaySong();
                    yield return new WaitWhile(() => Player.singing);
                }
                break;

            case ColorIndex.Length:
                Player.CollectEyes(Assets.wormColor);
                // Wait for length upgrade if enough length collectables collected
                if (Player.playerData.lengthIncrements % PlayerController.LENGTH_INCREMENT_AMOUNT == 0)
                    yield return new WaitForSeconds(0.25f);
                break;

            case ColorIndex.Dig:
                Player.PlayBiteMouthAnimation();
                break;

            default:
                Player.CollectEyes(GetGameColor(colorIndex));
                break;
        }

        yield return new WaitForSeconds(1);

        SetCutscene(false);

        yield return new WaitForSeconds(0.5f);

        if (colorIndex == ColorIndex.Length)
            AddToLengthMeter(true);

        if ((int)promptIndex > -1)
            ActivatePromptCloud(promptIndex, true);
        FlagGameState(false);
    }
    public static void ActivatePromptCloud(PromptIndex promptIndex, bool active) {
        ResetCloudBlipCoroutines();

        if (active) {
            currentPromptIndex = promptIndex;

            Game.promptCloud.SetActive(true);
            ActivateCloudBlips(promptIndex);
        }
        else {
            Game.StartCoroutine(Game.CloudPuffs(currentPromptIndex));
            currentPromptIndex = (PromptIndex)(-1);

            // Turn off stasis machine after moving from game intro
            if (stasisMachineArcs != null) {
                PlayRandomSound(AudioController.powerDown);
                floatObjects.RemoveAt(0);
                Game.playerHolder.transform.localPosition = Vector3.zero;

                foreach (StasisMachineArc sma in stasisMachineArcs)
                    sma.Flicker();
            }
        }
    }
    // Check when certain rooms activate different think abilities in tutorial area
    public static ThinkIndex CheckThinkPromptActivation() {
        switch (currentRoom) {
            case "Gaps"    :
            case "Lift"    : return ThinkIndex.Grid;
            case "DeadEnds":
            case "Hidden"  : return ThinkIndex.Map;
        }
        if (triggerRoomInfoPrompt) {
            triggerRoomInfoPrompt = false;
            return ThinkIndex.RoomInfo;
        }
        return (ThinkIndex)(-1);
    }

    public static void ResetCloud() {
        if (currentPromptIndex == (PromptIndex)(-1) && !Game.dreamCloud.activeSelf)
            return;

        ResetCloudBlipCoroutines();
        for (int i = 0; i < cloudBlips[0].Length; i++) {
            foreach (LevelBlock.BlockItem[] blips in cloudBlips)
                blips[i].spriteRenderer.enabled = false;
        }

        if (currentPromptIndex != (PromptIndex)(-1)) {
            Input.ResetPrompt(InputController.currentPrompt);
            currentPromptIndex = (PromptIndex)(-1);
        }
        else
            Game.dreamCloud.SetActive(false);
    }
    // Dreams and prompts share the same cloud blips startup animation
    static void InitCloudBlips() {
        SpriteRenderer[] tempClouds = Game.cloudBlipsHolder.GetComponentsInChildren<SpriteRenderer>(true);
        cloudBlips    = new LevelBlock.BlockItem[2][];
        cloudBlips[0] = new LevelBlock.BlockItem[tempClouds.Length];
        cloudBlips[1] = new LevelBlock.BlockItem[tempClouds.Length];

        for (int i = 0; i < tempClouds.Length; i++) {
            LevelBlock.BlockItem blip = null;
            for (int j = 0; j < tempClouds.Length; j++) {
                SpriteRenderer sr = tempClouds[j];
                if (sr.sortingOrder == j) {
                    blip = cloudBlips[0][i] = new LevelBlock.BlockItem(sr.gameObject, sr, null, null, sr.GetComponent<ParticleSystem>());
                    break;
                }
            }
            blip.spriteRenderer.sortingOrder = -((tempClouds.Length - i) * 2) - 2;

            GameObject backDrop = Instantiate(blip.blockObject, blip.blockObject.transform);
            backDrop.transform.localPosition = Vector3.right;
            LevelBlock.BlockItem blipBack = cloudBlips[1][i] = new LevelBlock.BlockItem(backDrop, backDrop.GetComponent<SpriteRenderer>(), null, null, backDrop.GetComponent<ParticleSystem>());

            var main = blipBack.particles.main;
            main.startColor = blipBack.spriteRenderer.color = Assets.backDropColor;
            blipBack.spriteRenderer.sortingOrder = blip.spriteRenderer.sortingOrder - 1;
            blipBack.blockObject.GetComponent<ParticleSystemRenderer>().sortingOrder = -3;
        }
    }
    // Cloud blips start from the player head, then arc toward the center of the screen
    static void ActivateCloudBlips(PromptIndex promptIndex) {
        FlagGameState(true);
        
        Vector2 screenPos = GetVector(currentScreen.screenData      .coordinates);
        Vector2 playerPos = GetVector(Player.wormHead.data.blockData.coordinates);
        Vector2 direction = (screenPos - playerPos).normalized;
        if (direction == Vector2.zero)
            direction = new Vector2(Random.Range(-1, 1), 1).normalized;

        Vector2 perpendicular = Vector2.Perpendicular(direction).normalized * (direction.x < 0 ? -1 : 1);
        Vector2 startPos = blipAnchor.transform.position;
        Vector2 endPos   = (startPos + direction * GRID_SIZE * 2) + (perpendicular * GRID_SIZE * 1.5f);
        Game.cloudAnchor.transform.position = new Vector2(Mathf.RoundToInt(endPos.x), Mathf.RoundToInt(endPos.y));
        
        float[] lerps = new float[] { 0, 0.1f, 0.45f, 1};
        for (int i = 0; i < cloudBlips[0].Length; i++) {
            Vector2 pos = Vector2.Lerp(startPos, endPos, lerps[i]);
            cloudBlips[0][i].blockObject.transform.position = new Vector2(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
            if (i > 0)
                cloudBlips[0][i].blockObject.transform.position += (Vector3)perpendicular * 3.5f;
        }
        Game.promptCloud.transform.position = endPos;
        cloudBlipCoroutines = new IEnumerator[cloudBlips[0].Length + 1];
        cloudBlipCoroutines[cloudBlipCoroutines.Length - 1] = Game.ShowBlips(promptIndex);
        Game.StartCoroutine(cloudBlipCoroutines[cloudBlipCoroutines.Length - 1]);
    }
    // Progressively expand the blips which get bigger each time, then oscillate their sizes
    IEnumerator ShowBlips(PromptIndex promptIndex) {
        int[] sizes = new int[cloudBlips[0].Length];
        for (int i = 0; i < cloudBlips[0].Length; i++) {
            StartCoroutine(InitBlip(i));
            yield return new WaitForSeconds(PROMPT_CLOUD_START_WAIT);
        }
        PlayPitchedSound(AudioController.cloudBlip, AudioController.GetRandomPitch(cloudBlips[0].Length));

        int index = (int)promptIndex;
        if (index < 0) {
            index = -index - 1; // Negative promptIndex is used to indicate which dream to play
            StartCoroutine(PlayDream(dreams[index]));
        }
        else {
            Input.ActivatePrompt(promptIndex);
            StartCoroutine(CloudFloat(InputController.currentPrompt.promptHolders[0], cloudBlips[0][cloudBlips.Length - 1].spriteRenderer));

            yield return new WaitForSeconds(PROMPT_CLOUD_CYCLE_WAIT);

            FlagGameState(false);
        }

        while (true) {
            for (int i = 0; i < cloudBlips[0].Length; i++) {
                if (cloudBlipCoroutines[i] != null) StopCoroutine(cloudBlipCoroutines[i]);
                    cloudBlipCoroutines[i]  = ExpandBlip(i);
                StartCoroutine(cloudBlipCoroutines[i]);

                yield return new WaitForSeconds(PROMPT_CLOUD_BLIP_WAIT);
            }
            yield return new WaitForSeconds(PROMPT_CLOUD_CYCLE_WAIT);
        }

        IEnumerator InitBlip(int size) {
            for (int i = 0; i <= size; i++) {
                PlayPitchedSound(AudioController.cloudBlip, AudioController.GetRandomPitch(1 + i * 0.5f));
                foreach (LevelBlock.BlockItem[] blips in cloudBlips) {
                    blips[size].spriteRenderer.enabled = true;
                    blips[size].spriteRenderer.sprite  = Assets.cloudBlipSprites[i];
                }
                yield return new WaitForSeconds(PROMPT_CLOUD_BLIP_WAIT);
            }
            StartCoroutine(CloudFloat(cloudBlips[0][size].blockObject, cloudBlips[0][size].spriteRenderer));
        }
        IEnumerator ExpandBlip(int size) {
            PlayPitchedSound(AudioController.cloudBlip, AudioController.GetRandomPitch(1 + size * 0.2f));

            foreach (LevelBlock.BlockItem[] blips in cloudBlips)
                blips[size].spriteRenderer.sprite = Assets.cloudBlipSprites[size + 1];

            yield return new WaitForSeconds(PROMPT_CLOUD_BLIP_WAIT * 0.75f);

            foreach (LevelBlock.BlockItem[] blips in cloudBlips)
                blips[size].spriteRenderer.sprite = Assets.cloudBlipSprites[size];
        }
    }
    // Puff each blip and then the dream/prompt cloud away from the center of the sprite
    IEnumerator CloudPuffs(PromptIndex promptIndex) {
        ResetCloudBlipCoroutines();

        int index = (int)promptIndex;
        float speed = index == -1 ? 0.25f : 1;
        
        for (int i = 0; i < cloudBlips[0].Length; i++) {
            PlayPitchedSound(AudioController.cloudPuff, AudioController.GetRandomPitch(1 + i * 0.2f));
            foreach (LevelBlock.BlockItem[] blips in cloudBlips)
                blips[i].spriteRenderer.enabled = false;

            int size = i + 1;
            for (int y = -size; y <= size; y++) {
                for (int x = -size; x <= size; x++)
                    CreateCloudParticles(cloudBlips[0][i].particles, cloudBlips[1][i].particles, x, y, speed);
            }
            yield return new WaitForSeconds(PROMPT_CLOUD_BLIP_WAIT);
        }

        if (index == -1) {
            PlayPitchedSound(AudioController.cloudPuff, AudioController.GetRandomPitch(1 + cloudBlips[0].Length * 0.2f));
            dreamCloud.SetActive(false);
            Texture2D texture = Assets.dreamCloudParticlesSprite.texture;
            for (int y = texture.height - 1; y >= 0; y--) {
                for (int x = 0; x < texture.width; x++) {
                    if (texture.GetPixel(x, y).a == 0)
                        continue;

                    float posX = x - texture.width  / 2;
                    float posY = y - texture.height / 2;

                    CreateCloudParticles(dreamCloudParticles[0], dreamCloudParticles[1], posX, posY, speed);
                }
            }
            yield break;
        }

        PlayPitchedSound(AudioController.cloudPuff, AudioController.GetRandomPitch(1 + cloudBlips[0].Length * 0.2f));
        Input.prompts[index].promptHolders[0].SetActive(false);
        ControlPrompt[] cps = Input.prompts[index].gamePrompts;
        for (int i = 0; i < cps.Length; i++) {
            Texture2D texture = Input.prompts[index].gamePrompts[i].spriteRenderer.sprite.texture;
            for (int y = texture.height - 1; y >= 0; y--) {
                for (int x = 0; x < texture.width; x++) {
                    if (texture.GetPixel(x, y).a == 0)
                        continue;

                    float posX = x - texture.width  / 2;
                    float posY = y - texture.height / 2;

                    CreateCloudParticles(promptCloudParticles[i * 2], promptCloudParticles[i * 2 + 1], posX, posY, speed);
                }
            }
        }

        foreach (ControlPrompt cp in Input.prompts[index].gamePrompts)
            cp.ResetPrompt();

        yield return new WaitForSeconds(0.5f);

        // Restart feedback prompt input so player can still experiment with keys after completing prompt
        foreach (ControlPrompt cp in Input.prompts[index].playerPrompts) {
            if (!cp.transform.parent.gameObject.activeSelf)
                continue;

            cp.Press(false);
            cp.CheckInput();
        }
    }
    static void ResetCloudBlipCoroutines() {
        if (cloudBlipCoroutines != null) {
            foreach (IEnumerator ie in cloudBlipCoroutines) {
                if (ie != null)
                    Game.StopCoroutine(ie);
            }
        }
        cloudBlipCoroutines = null;
    }
    IEnumerator CloudFloat(GameObject go, SpriteRenderer checkEnabled) {
        Vector2 pos = go.transform.localPosition;
        Vector2[] positions = new Vector2[BLOCK_FLOAT_STEPS];
        for (int i = 0; i < BLOCK_FLOAT_STEPS; i++)
            positions[i] = pos + Vector2.up * floatStepValues[i];

        int floatIndex = (BLOCK_FLOAT_STEPS / 2) + (BLOCK_FLOAT_STEPS % 2);
        int direction = 1;
        while (checkEnabled.enabled) {
            go.transform.localPosition = positions[floatIndex];
            
            if (floatIndex <= 0)                     direction =  1;
            if (floatIndex >= BLOCK_FLOAT_STEPS - 1) direction = -1;
            floatIndex += direction;

            yield return new WaitForSeconds(0.1f);
        }
        go.transform.localPosition = pos;
    }

    #endregion

    #region Camera

    public static void ShakeCamera(int shakes, int intensity) {
        if (!enableScreenshake)
            return;

        // Don't play weaker screen shake if a stronger one is already playing
        int priority = shakes * intensity;
        if (priority <= priorityCameraShake)
            return;

        priorityCameraShake = priority;

        if (cameraShakeCoroutine != null) Game.StopCoroutine(cameraShakeCoroutine);
            cameraShakeCoroutine  = Game.ieShakeCamera(Mathf.Clamp(shakes, 1, 5), Mathf.Clamp(intensity, 1, 7));
        Game.StartCoroutine(cameraShakeCoroutine);
    }
    IEnumerator ieShakeCamera(int shakes, int intensity) {
        Vector3 originalPos = gameCamera.transform.localPosition;
        for (int i = 0; i < shakes + 1; i++) {
            Vector3 pos = gameCamera.transform.localPosition;
            Vector3 randomPos = originalPos;
            if (i < shakes)
                randomPos += new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)).normalized * Random.Range(Mathf.Max(intensity - 3, 1), intensity + 1);

            float time = 0;
            while (time < 1) {
                gameCamera.transform.localPosition = Vector3.Lerp(pos, randomPos, GetCurve(time));

                time += Time.deltaTime * 25;
                yield return null;
            }
        }
        gameCamera.transform.localPosition = originalPos;
        priorityCameraShake = 0;
    }

    #endregion

    #region Screens

    public static float GetScreenOrthographicSize(Screen screen) {
        return ((screen.screenData.size * 2) - 1) * GRID_SIZE * UnityEngine.Screen.height / UnityEngine.Screen.width * 0.5f;
    }

    // Interpolate camera to screen
    public static void GoToScreen(Screen screen, bool instant) {
        if (disableScreenSwitching || screen == currentScreen)
            return;
        
        currentScreen = screen;
        if (!screen.screenData.visited) {
            screen.screenData.visited = true;
            Player.Map.UpdateVisiblePixels(screen, true);
        }

        if (instant || enableCameraSnap) {
            Game.cameraHolder.transform.position = screen.transform.position;
            Game.gameCamera.orthographicSize = GetScreenOrthographicSize(screen);
        }
        else {
            if (screenMoveCoroutine != null) Game.StartCoroutine(screenMoveCoroutine);
                screenMoveCoroutine  = Game.ieGoToScreen(screen);
            Game.StartCoroutine(screenMoveCoroutine);
        }
    }
    IEnumerator ieGoToScreen(Screen screen) {
        cameraMoving = true;

        Vector3 fromPos = cameraHolder.transform.position;
        Vector3 toPos   = screen      .transform.position;
        toPos.z = fromPos.z;

        float fromSize = gameCamera.orthographicSize;
        float toSize   = GetScreenOrthographicSize(screen);

        float time = 0;
        while (time < 1) {
            float curveTime = GetCurve(time);
            cameraHolder.transform.position = Vector3.Lerp(fromPos , toPos , curveTime);
            gameCamera.orthographicSize     = Mathf  .Lerp(fromSize, toSize, curveTime);

            time += Time.deltaTime * SCREEN_MOVE_SPEED;
            yield return null;
        }

        cameraHolder.transform.position = toPos;
        gameCamera.orthographicSize     = toSize;
        cameraMoving = false;
    }

    #endregion

    #region Puzzle Activation

    // When instant == Activation.Alt that means to override and apply visuals,
    // most likely because the data is being initialized from level load,
    // otherwise don't apply anything because the data is already the requested state
    public static bool SetPuzzleActivation(Data data, Activation activation, Activation instant, bool setState = true) {
        int state = (int)activation;
        if (data.blockData.state == state && instant != Activation.Alt)
            return false;

        if (setState)
            data.blockData.state = state;

        return true;
    }

    public static void SetGateSlot(Data slotData, Activation activation, Activation instant = Activation.Off) {
        if (!SetPuzzleActivation(slotData, activation, instant))
            return;

        LevelBlock.BlockItem fragmentItem = slotData.levelBlock.GetBlockItem("SlotFragment");
        LevelBlock.BlockItem fragmentInfo = slotData.levelBlock.GetBlockItem("InfoItem"    );
        switch (activation) {
            case Activation.Off:
                if (instant == Activation.Off) {
                    Game.StartCoroutine(DeactivateDelay());

                    // Deactivate after player has moved over slot and eaten fragment
                    IEnumerator DeactivateDelay() {
                        yield return null;
                        yield return new WaitWhile(() => Player.wormHead.data.moving);

                        fragmentItem.blockObject.SetActive(false);
                        fragmentInfo.spriteRenderer.gameObject.SetActive(false);
                    }
                }
                else {
                    fragmentItem.blockObject.SetActive(false);
                    fragmentInfo.spriteRenderer.gameObject.SetActive(false);
                }
                break;

            case Activation.On :
                fragmentItem.blockObject.SetActive(true);
                fragmentInfo.spriteRenderer.gameObject.SetActive(true);
                break;
        }
        if (instant == Activation.Off)
            CheckOffScreenAction(slotData, (ColorIndex)(-1), activation);
    }
    public static bool GateSlotsActivated() {
        List<Data> slotDatas = Level.puzzleData[(int)PuzzleIndex.GateSlot];
        foreach (Data d in slotDatas) {
            if (d.blockData.state != (int)Activation.On)
                return false;
        }
        return true;
    }
    public static void SetGates(Activation activation, Activation instant = Activation.Off) {
        List<Data> gateDatas = Level.puzzleData[(int)PuzzleIndex.Gate];
        if (gateDatas == null)
            return;

        foreach (Data d in gateDatas)
            SetGate(d, activation, instant);
    }
    public static void SetGate(Data gateData, Activation activation, Activation instant = Activation.Off) {
        if (!SetPuzzleActivation(gateData, activation, instant, false))
            return;

        bool gatePanel = gateData.layer == Layer.Misc;
        LevelBlock.BlockItem gateObject = gateData.levelBlock.GetBlockItem(gatePanel ? "GatePanelLight" : "GateDoor");
        if (activation != Activation.Alt) {
            bool active = activation == Activation.On;
            if (gatePanel) {
                Panel p = (Panel)gateData.levelBlock.GetBlockItem("Panel").script;
                p.SetPanel(activation, instant);
                Player.playerData.gateTunnels[p.gatePanelIndex] = active;
                Game.StartCoroutine(GatePanelIconDelay(gateData));
            }
            else {
                gateData.blockData.state = (int)activation;
                OpenGate(active, instant);
            }

            gateObject.light.enabled = active;
            if (instant == Activation.Off)
                CheckOffScreenAction(gateData, (ColorIndex)(-1), activation);
        }
        else {
            Game.StartCoroutine(BlinkGateLightDelay());

            IEnumerator BlinkGateLightDelay() {
                gateObject.light.enabled = true;

                yield return new WaitForSeconds(0.2f);

                if (gateData.blockData.state == (int)Activation.Off) {
                    gateObject.light.enabled = false;
                    gateObject.spriteRenderer.sprite = gatePanel ? Assets.gatePanelSprites[0]
                                                                 : Assets.gateLightSprites[0];
                }
            }
        }

        int spriteIndex = gateObject.light.enabled ? 1 : 0;
        gateObject.spriteRenderer.sprite = gatePanel ? Assets.gatePanelSprites[spriteIndex] 
                                                     : Assets.gateLightSprites[spriteIndex];

        IEnumerator GatePanelIconDelay(Data d) {
            FlagGameState(true);

            if (instant == Activation.Off)
                yield return new WaitForSeconds(0.2f);

            LevelBlock.BlockItem iconItem = d.levelBlock.GetBlockItem("GatePanelIcon");

            switch (activation) {
                case Activation.Off:
                    if (instant == Activation.Off)
                        PlayRandomSound(AudioController.buttonOff);

                    iconItem.spriteRenderer.sprite = Assets.panelSprites[0];
                    iconItem.light.enabled = false;
                    break;

                case Activation.On:
                    if (instant == Activation.Off)
                        PlayRandomSound(AudioController.buttonOn);

                    iconItem.spriteRenderer.sprite = Assets.panelSprites[1];
                    iconItem.light.enabled = true;
                    break;
            }

            FlagGameState(false);
        }
    }
    // Match gate position to sound clip
    public static void OpenGate(bool open, Activation instant) {
        foreach (Data d in Level.puzzleData[(int)PuzzleIndex.Gate]) {
            FlagGameState(true);
            Game.StartCoroutine(Game.ieOpenGate(d.levelBlock, open, instant));
        }
    }
    IEnumerator ieOpenGate(LevelBlock levelBlock, bool open, Activation instant) {
        int[] doorHeights = open ? new int[] { 2, 4, 6, 7 } : new int[] { 6, 4, 2, 0 };
        Transform gateDoor = levelBlock.GetBlockItem("GateDoor").blockObject.transform;
        levelBlock.GetBlockItem("UndoOutline").blockObject.transform.position = gateDoor.position;

        if (instant == Activation.Off) {
            PlaySound(open ? AudioController.gateOpen : AudioController.gateClose);

            int step = doorHeights[0] < (int)gateDoor.localPosition.y ? -1 : 1;
            foreach (int i in doorHeights) {
                int currentHeight = (int)gateDoor.localPosition.y;
                float time = 0;
                while (time < 1) {
                    int height = Mathf.RoundToInt(Mathf.Lerp(currentHeight, i, time));
                    gateDoor.localPosition = Vector2.up * height;

                    time += Time.deltaTime * 30;
                    yield return null;
                }
                gateDoor.localPosition = Vector2.up * i;

                yield return new WaitForSeconds(0.3f);
            }
        }
        else
            gateDoor.localPosition = Vector2.up * doorHeights[doorHeights.Length - 1];

        FlagGameState(false);
    }
    
    public static void SetButton(Data buttonData, Activation activation, Activation instant = Activation.Off) {
        int prevState = buttonData.blockData.state;
        if (!SetPuzzleActivation(buttonData, activation, instant))
            return;

        int colorIndex = ConvertColorNameToIndex(buttonData.blockData.blockName);
        LevelBlock.BlockItem buttonItem = buttonData.levelBlock.GetBlockItem("Primary");
        switch (activation) {
            case Activation.Off:
                if (instant == Activation.Off) {
                    PlayRandomSound(AudioController.buttonOff);
                    CheckOffScreenAction(buttonData, (ColorIndex)colorIndex, Activation.Off);
                }

                buttonItem.spriteRenderer.sprite = Assets.buttonSprites[0];
                buttonItem.light.enabled = false;
                if (prevState != (int)Activation.Off)
                    Level.buttonColorActivations[colorIndex]--;
                break;

            case Activation.On :
            case Activation.Alt:
                if (instant == Activation.Off) {
                    PlayRandomSound(AudioController.buttonOn);
                    CheckOffScreenAction(buttonData, (ColorIndex)colorIndex, Activation.On);
                }

                buttonItem.spriteRenderer.sprite = Assets.buttonSprites[1];
                buttonItem.light.enabled = true;
                if (prevState == (int)Activation.Off)
                    Level.buttonColorActivations[colorIndex]++;
                break;
        }
    }
    // Turn off buttons powered by bullets
    public static bool DeactivateBulletButtons() {
        List<Data> buttonDatas = Level.puzzleData[(int)PuzzleIndex.Button];
        if (buttonDatas == null)
            return false;

        List<Data> recallBullets = new List<Data>();
        foreach (Data d in buttonDatas) {
            if (d.blockData.state == (int)Activation.Alt) {
                SetButton(d, Activation.Off);
                recallBullets.Add(d);
            }
        }

        if (recallBullets.Count > 0) {
            CheckLevelButtons();

            FlagGameState(true);
            Game.StartCoroutine(Game.RecallBullets(recallBullets));
            return true;
        }
        return false;
    }
    IEnumerator RecallBullets(List<Data> buttonDatas) {
        (Vector2 pos, GameObject recall)[] recalls = new (Vector2 pos, GameObject recall)[buttonDatas.Count];
        for (int i = 0; i < recalls.Length; i++) {
            recalls[i] = (GetGridPosition(buttonDatas[i].blockData.coordinates), buttonDatas[i].levelBlock.GetBlockItem("ButtonBullet").blockObject);
            recalls[i].recall.SetActive(true);
        }

        float time  = 0;
        float speed = BLOCK_MOVE_SPEED * 0.5f;
        while (time < 1) {
            float curveTime = GetCurve(time);
            Vector2 playerPos = Player.wormEye.data.blockObject.transform.position;
            foreach (var r in recalls)
                r.recall.transform.position = Vector2.Lerp(r.pos, playerPos, curveTime);

            time += Time.deltaTime * speed;
            yield return null;
        }
        foreach (var r in recalls) {
            r.recall.SetActive(false);
            r.recall.transform.position = r.pos;
        }

        FlagGameState(false);
    }

    // Check if buttons are powered by their respective crystals
    public static void CheckLevelButtons(Activation instant = Activation.Off) {
        List<Data> buttonDatas = Level.puzzleData[(int)PuzzleIndex.Button];
        if (buttonDatas == null)
            return;

        foreach (Data d in buttonDatas) {
            int buttonColor = ConvertColorNameToIndex(d.blockData.blockName);

            Data crystalData = Grid.GetData(d.blockData.coordinates, Layer.Block);
            int crystalColor = crystalData == null ? -1 : ConvertColorNameToIndex(crystalData.blockData.blockName);
            if (d.blockData.state == (int)Activation.Alt) {
                // A crystal on a button takes priority over a bullet powered button
                if (buttonColor == crystalColor)
                    SetButton(d, Activation.On, instant);
            }
            else
                SetButton(d, buttonColor == crystalColor ? Activation.On : Activation.Off, instant);
        }

        UpdatePanelLights(instant);
    }

    public static int GetForceState(Coordinates direction) {
        int state = Coordinates.GetFacing(direction);
        if (state == -1) state = 4; // No direction, just float in place
        return state;
    }
    public static void SetForceDirection(Coordinates direction, Activation instant = Activation.Off) {
        if (forceDatas == null)
            return;

        if (forceDirection == direction && instant != Activation.Alt) return;
            forceDirection =  direction;

        int forceState = GetForceState(direction);
        foreach (Data d in forceDatas) {
            d.blockData.state = forceState;
            if (instant == Activation.Off)
                CheckOffScreenAction(d, ColorIndex.Force, (Activation)3);

            List<LevelBlock.BlockItem> crystalActivations = d.levelBlock.GetBlockItems("CrystalActivation");
            if (forceState != 4) {
                foreach (LevelBlock.BlockItem bi in crystalActivations) {
                    bi.animator.SetBool("DirectionActivate", true);
                    ApplyFacing(forceState, bi.blockObject);
                }
            }
            else {
                foreach (LevelBlock.BlockItem bi in crystalActivations) {
                    bi.animator.SetBool("DirectionActivate", false);
                    if (instant == Activation.Off)
                        bi.animator.SetTrigger("Activate");
                }
            }
        }

        if (instant == Activation.Off)
            ApplyForce();
    }

    public static void SetForeground(Data fgData, Activation activation, Activation instant = Activation.Off) {
        if (!SetPuzzleActivation(fgData, activation, instant, false))
            return;

        int state = (int)activation;
        foreach (Coordinates c in fgData.blockData.connectedBlocks)
            Grid.GetData(c, Layer.Fg).blockData.state = state;

        SpriteRenderer sr = fgData.levelBlock.GetBlockItem("Primary").spriteRenderer;
        if (foregroundCoroutines[sr] != null) Game.StopCoroutine(foregroundCoroutines[sr]);
            foregroundCoroutines[sr]  = Game.ShowForeground(sr, activation, instant);
        Game.StartCoroutine(foregroundCoroutines[sr]);
    }
    public static void ToggleForegrounds(Activation activation) {
        List<Data> fgDatas = Level.puzzleData[(int)PuzzleIndex.Fg];
        if (fgDatas == null)
            return;

        foreach (Data d in fgDatas) {
            if (d.blockData.state == (int)Activation.Off)
                continue;

            SpriteRenderer sr = d.levelBlock.GetBlockItem("Primary").spriteRenderer;
            if (foregroundCoroutines[sr] != null) Game.StopCoroutine(foregroundCoroutines[sr]);
                foregroundCoroutines[sr]  = Game.ShowForeground(sr, activation, Activation.Off);
            Game.StartCoroutine(foregroundCoroutines[sr]);
        }
    }
    IEnumerator ShowForeground(SpriteRenderer sr, Activation activation, Activation instant) {
        float fromAlpha = FG_ALPHA_MIN;
        float toAlpha   = 255;
        if (activation == Activation.Off) {
            fromAlpha = 255;
            toAlpha   = FG_ALPHA_MIN;
        }
        Color32 color = sr.color;

        if (instant == Activation.Off) {
            float time = 0;
            while (time < 1) {
                color.a = (byte)Mathf.Lerp(fromAlpha, toAlpha, GetCurve(time));
                sr.color = color;

                time += Time.deltaTime * FG_SHOW_SPEED;
                yield return null;
            }
        }
        color.a = (byte)toAlpha;
        sr.color = color;
    }
    // Check if player is still overlapping any foreground groups
    public static void CheckForegrounds() {
        List<Data> fgDatas = Level.puzzleData[(int)PuzzleIndex.Fg];
        if (fgDatas == null)
            return;

        foreach (Data d in fgDatas) {
            if (d.blockData.state == (int)Activation.Off) {
                bool found = false;
                foreach (Coordinates c in d.blockData.connectedBlocks) {
                    if (Grid.GetData(c, Layer.Player) != null) {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    SetForeground(d, Activation.On);
            }
        }
    }

    // Close locked doors opened by player entering from behind
    public static void CheckTemporaryOpenDoors() {
        List<Data> panelDatas = Level.puzzleData[(int)PuzzleIndex.Panel];
        if (panelDatas == null)
            return;

        foreach (Data d in panelDatas) {
            if (d.blockData.state != (int)Activation.Alt)
                continue;
            
            Data playerData = Grid.GetData(d.blockData.coordinates + Coordinates.FacingDirection[d.blockData.facing], Layer.Player);
            if (playerData == null)
                Game.StartCoroutine(Game.CloseTemporaryOpenDoor((TunnelPanel)d.levelBlock.GetBlockItem("Panel").script));
        }
    }
    IEnumerator CloseTemporaryOpenDoor(TunnelPanel tunnelPanel) {
        FlagGameState(true);

        yield return new WaitWhile(() => Player.wormHead.data.moving);

        tunnelPanel.SetPanel(tunnelPanel.inverted ? Activation.On : Activation.Off);
        FlagGameState(false);
    }

    public static void UpdatePanelLights(Activation instant) {
        List<Data> panelDatas = Level.puzzleData[(int)PuzzleIndex.Panel];
        if (panelDatas == null)
            return;

        foreach (Data d in panelDatas) {
            Panel p = (Panel)d.levelBlock.GetBlockItem("Panel").script;
            p.UpdateLights(Level.buttonColorActivations);
            p.CheckLights(instant);
        }
    }

    public static void UpdatePistonArms() {
        List<Data> panelDatas = Level.puzzleData[(int)PuzzleIndex.Panel];
        if (panelDatas == null)
            return;

        foreach (Data d in panelDatas) {
            if (d.levelBlock.layer == Layer.Piston) {
                PistonPanel pp = (PistonPanel)d.levelBlock.GetBlockItem("Panel").script;
                pp.UpdateArms();
            }
        }
    }
    public static void UpdatePistonsIfBlocked(Activation instant = Activation.Off) {
        List<Data> panelDatas = Level.puzzleData[(int)PuzzleIndex.Panel];
        if (panelDatas == null)
            return;

        foreach (Data d in panelDatas) {
            if (d.levelBlock.layer == Layer.Piston) {
                PistonPanel pp = (PistonPanel)d.levelBlock.GetBlockItem("Panel").script;
                pp.UpdateIfBlocked(instant);
            }
        }
    }

    public static void DestroyBlueCrystal(Data crystalData, bool destroy, Activation instant = Activation.Off) {
        if (crystalData.blockData.destroyed == destroy && instant != Activation.Alt)
            return;
        
        if (crystalData.blockData.destroyed != destroy)
            Grid.DestroyData(crystalData, destroy);

        if (destroy && instant == Activation.Off) {
            PlayRandomSound(AudioController.blueBreak);
            foreach (Coordinates c in crystalData.blockData.connectedBlocks) {
                ParticlePooler.Particle p = Particles.GetParticle("BlueCrystalBreakParticles");
                if (p != null) {
                    ApplyCoordinates(c, p.particleObject);
                    Particles.PlayParticle(p);
                }
            }
            UpdatePistonsIfBlocked();
        }
        
        // Swap normal and broken version
        List<LevelBlock.BlockItem> blockItems = crystalData.levelBlock.GetBlockItems("All");
        for (int i = 0; i < blockItems.Count - 1; i++) {
            blockItems[i].spriteRenderer.enabled = !destroy;
            if (blockItems[i].light != null)
                blockItems[i].light.enabled = !destroy;
        }
        blockItems[blockItems.Count - 1].spriteRenderer.enabled = blockItems[blockItems.Count - 1].light.enabled = destroy;
    }

    public static void DestroyCollectable(Data collectData, bool destroy, Activation instant = Activation.Off) {
        if (collectData.blockData.destroyed == destroy && instant != Activation.Alt) return;
            collectData.blockData.destroyed =  destroy;

        if (instant == Activation.Off) {
            Game.StartCoroutine(DestroyDelay());

            // Wait for player mouth to close over collectable
            IEnumerator DestroyDelay() { 
                yield return null;
                yield return new WaitWhile(() => Player.wormHead.IsCurrentAnimation(Animation.EatClose) && Player.wormHead.playingAnimation != null);
                collectData.blockObject.SetActive(!destroy);
            }
        }
        else
            collectData.blockObject.SetActive(!destroy);

        if (playedSongIndex != -1 && instant != Activation.Alt && collectData.blockData.blockName == "CollectSong")
            Player.playerData.songs[playedSongIndex] = destroy;
    }

    public static void SetDig(Data digData, int state, Activation instant = Activation.Off) {
        if (digData.blockData.state == state && instant != Activation.Alt) return;
            digData.blockData.state =  state;

        Texture2D digSprite = digData.blockData.facing == -1 ? Assets.digOutlineSprites[digData.blockData.state].texture
                                                             : Assets.digHoleSprites   [digData.blockData.state].texture;
        UpdateDigTexture(digData, GetDigTexture(digData), digSprite);
    }
    public static void DestroyDig(Data digData) {
        if (digData.blockData.destroyed)
            return;
        
        FlagGameState(true);

        Player.PlayAnimation(Animation.EyeBite, Animation.HeadBite, Animation.MouthBite, true);

        // Destroy all connected dig blocks
        Texture2D digTexture = digOutlines[digData.levelBlock].sprite.texture;
        foreach (Coordinates c in digData.blockData.connectedBlocks) {
            Data d = Grid.GetData(c, Layer.Dig);
            d.blockData.destroyed = true;
            d.blockData.state = 0;

            PlayRandomSound(AudioController.digBreak);
            CreateDigBreakParticles(d);
            UpdateDigTexture(d, digTexture, Assets.digHoleSprites[0].texture);

            // Reveal adjacent dig blocks
            foreach (Coordinates f in Coordinates.FacingDirection) {
                Data fd = Grid.GetData(c + f, Layer.Dig);
                if (fd == null || fd.blockData.destroyed || fd.blockData.connectedBlocks == digData.blockData.connectedBlocks)
                    continue;

                fd.blockData.state = -1;
            }
        }

        UpdateDigBlocks();
        Game.StartCoroutine(Game.DigShake(digData.blockData.connectedBlocks.Length));
    }
    // Update outline visuals to show walls or breakable paths
    public static void UpdateDigBlocks() {
        List<Data> digDatas = Level.puzzleData[(int)PuzzleIndex.Dig];
        if (digDatas == null)
            return;

        bool[][][] s = Assets.SPRITE_TYPES;
        foreach (Data d in digDatas) {
            if (d.blockData.state != -1)
                continue;

            GetState(d);
        }
        foreach (SpriteRenderer sr in digOutlines.Values)
            sr.sprite.texture.Apply();

        void GetState(Data d) {
            bool[] empty = new bool[4];
            for (int i = 0; i < Coordinates.FacingDirection.Length; i++) {
                Data data = Grid.GetData(d.blockData.coordinates + Coordinates.FacingDirection[i], Layer.Dig);
                if (data != null && data.blockData.destroyed)
                    empty[i] = true;
            }

            int state = 1;
            for (int blockType = 0; blockType < s.Length - 1; blockType++) {
                for (int facingType = 0; facingType < s[blockType].Length; facingType++) {
                    bool match = true;
                    for (int i = 0; i < s[blockType][facingType].Length; i++) {
                        if (empty[i] != s[blockType][facingType][i]) {
                            match = false;
                            break;
                        }
                    }
                    if (match) {
                        SetDig(d, state);
                        return;
                    }
                    state++;
                }
            }
        }
    }
    static void UpdateDigTexture(Data digData, Texture2D t, Texture2D digSprite) {
        Coordinates o = digData.blockData.origin;
        for (int x = 0; x < BLOCK_SIZE; x++) {
            for (int y = 0; y < BLOCK_SIZE; y++) {
                Coordinates n = new Coordinates(o.x + x, o.y + y);
                if (n.x < 0 || n.x >= t.width || n.y < 0 || n.y >= t.height)
                    continue;

                t.SetPixel(n.x, n.y, digSprite.GetPixel(x, y));
            }
        }
    }
    static Texture2D GetDigTexture(Data digData) {
        return digOutlines[digData.levelBlock].sprite.texture;
    }
    IEnumerator ShowDigOutline(SpriteRenderer sr, bool active, Activation instant) {
        float fromAlpha = 0.05f;
        float toAlpha   = 0.75f;
        if (!active) {
            fromAlpha = 0.75f;
            toAlpha   = 0.05f;
        }
        Color color = sr.color;

        if (instant == Activation.Off) {
            float time = 0;
            while (time < 1) {
                color.a = Mathf.Lerp(fromAlpha, toAlpha, GetCurve(time));
                sr.color = color;

                time += Time.deltaTime * 5;
                yield return null;
            }
        }
        color.a = toAlpha;
        sr.color = color;
    }
    IEnumerator DigShake(int size) {
        ShakeCamera(size, Mathf.Clamp(size, 1, 2));

        yield return new WaitForSeconds(0.35f);

        FlagGameState(false);
    }

    #endregion

    #region Particles

    // Continually search for open spots to play dust particles that float in air
    void CreateAirDustParticles() {
        IEnumerator ie = ieCreateAirDustParticles(true);
        dustCoroutines.Add(ie);
        StartCoroutine(ie);
    }
    IEnumerator ieCreateAirDustParticles(bool init) {
        int randomCoord = Random.Range(0, usedAirDustCoords.Length);
        for (int i = 0; i < 30; i++) {
            if (!usedAirDustCoords[randomCoord])
                break;

            randomCoord = Random.Range(0, usedAirDustCoords.Length);
        }
        usedAirDustCoords[randomCoord] = true;

        if (init) {
            if (Random.Range(0, 10) > 0)
                yield return new WaitForSeconds(Random.Range(2f, 5f));
        }
        else
            yield return new WaitForSeconds(Random.Range(5f, 10f));

        ParticlePooler.Particle p = null;
        while (p == null) {
            p = Particles.GetParticle("AirDustParticles");
            if (p == null)
                yield return null;
        }

        ApplyCoordinates(Level.airDusts[randomCoord], p.particleObject);
        Particles.PlayParticle(p);

        usedAirDustCoords[randomCoord] = false;
        IEnumerator ie = ieCreateAirDustParticles(false);
        dustCoroutines.Add(ie);
        StartCoroutine(ie);
    }

    // Continually search for open spots to play dust particles that fall from under ground blocks
    IEnumerator CreateGroundDustParticles(Coordinates[] groundDustCoordinates) {
        bool[] usedCoords = new bool[groundDustCoordinates.Length];
        int cycles = 0;
        while (true) {
            yield return new WaitForSeconds(Random.Range(6.0f, 12.0f));

            int randomCoord = Random.Range(0, usedCoords.Length);
            for (int i = 0; i < 20; i++) {
                if (!usedCoords[randomCoord])
                    break;

                randomCoord = Random.Range(0, usedCoords.Length);
            }
            usedCoords[randomCoord] = true;

            // Same spots can be used again every 4 particles
            if (cycles++ > 3) {
                for (int i = 0; i < usedCoords.Length; i++)
                    usedCoords[i] = false;
            }

            // Don't play if block or player is occupying the space
            Coordinates dustCoord = groundDustCoordinates[randomCoord];
            Data pushData   = Grid.GetData(dustCoord, Layer.Block );
            Data playerData = Grid.GetData(dustCoord, Layer.Player);
            if (playerData != null || (pushData != null && pushData.HasTag(Tag.Push)))
                continue;

            ParticlePooler.Particle p = null;
            while (p == null) {
                p = Particles.GetParticle("GroundDustParticles");
                if (p == null)
                    yield return null;
            }
            
            Audio.PlayGroundDust(dustCoord, Player.wormHead.data.blockData.coordinates);
            ApplyCoordinates(dustCoord, p.particleObject);
            Particles.PlayParticle(p);
        }
    }

    // Create particles when block lands on ground
    // Groups of data that land adjacently to each other create a bigger centered dust cloud
    // instead of individual ones for each data
    public static void CreateLandingParticles(List<List<Data>> landDataList, int fallAmount) {
        if (!enableParticles)
            return;
        
        fallAmount = Mathf.Clamp(fallAmount, 0, 10);
        foreach (List<Data> datas in landDataList) {
            float xAverage = 0;
            foreach (Data d in datas)
                xAverage += d.blockData.coordinates.x;
            xAverage /= datas.Count;

            ParticlePooler.Particle p = Particles.GetParticle("GroundLandingParticles");
            if (p != null) {
                Vector2 pos = GetGridPosition(new Coordinates((int)xAverage, datas[0].blockData.coordinates.y));
                pos.x += (xAverage - (int)xAverage) * GRID_SIZE;
                p.particleObject.transform.position = pos;

                ParticleSystem ps = p.particleSystem;
                var main = ps.main; var shape = ps.shape;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1 + fallAmount * 0.5f);
                shape.scale     = new Vector3(GRID_SIZE * datas.Count, 1, 1);

                Particles.PlayParticle(p, 20 + fallAmount * 5);
            }
        }
    }

    // Play particles for blocks moving across ground
    static void PlayGroundMovingParticles(List<Data> moveList, int xDirection) {
        if (!enableParticles)
            return;
        
        foreach (Data d in moveList) {
            Coordinates groundCoord     = d.blockData.coordinates + Coordinates.Down;
            Coordinates lastGroundCoord = groundCoord; lastGroundCoord.x -= xDirection;
            Data groundData     = Grid.GetData(groundCoord    , Layer.Block );
            Data lastGroundData = Grid.GetData(lastGroundCoord, Layer.Block );
            Data tunnelData     = Grid.GetData(lastGroundCoord, Layer.Tunnel);
            if (tunnelData == null && lastGroundData != null && lastGroundData.blockData.blockName == "Ground" && groundData != null && groundData.blockData.blockName == "Ground") {
                ParticlePooler.Particle p = Particles.GetParticle("GroundMovingParticles");
                ApplyCoordinates(d.blockData.coordinates, p.particleObject);
                p.particleObject.transform.rotation = Quaternion.Euler(new Vector3(-90, 0, xDirection == 1 ? 0 : 180));
                Particles.PlayParticle(p);
            }
        }
    }

    static void CreateCloudParticles(ParticleSystem particleSystem, ParticleSystem backDropParticleSystem, float x, float y, float speed) {
        Vector2 velocity = new Vector2(x, y);
        if (x == 0 && y == 0) velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
        else if      (x == 0) velocity = new Vector2(Random.Range(-0.2f, 0.2f), y);
        else if      (y == 0) velocity = new Vector2(x, Random.Range(-0.2f, 0.2f));

        var emitParams = new ParticleSystem.EmitParams();
        emitParams.position = new Vector3(x - 1, y, 100);
        emitParams.velocity = velocity.normalized * speed;
        particleSystem        .Emit(emitParams, 1);
        backDropParticleSystem.Emit(emitParams, 1);
    }

    public static void CreateDigBreakParticles(Data digData) {
        ParticlePooler.Particle p = Particles.GetParticle("DigBreakParticles");
        ApplyCoordinates(digData.blockData.coordinates, p.particleObject);
        var emitParams = new ParticleSystem.EmitParams();

        int size = 2;
        for (int x = -size; x <= size; x++) {
            for (int y = -size; y <= size; y++) {
                if (Random.Range(0, 3) == 0)
                    continue;

                emitParams.position = new Vector3(x, 0, y);
                emitParams.velocity = emitParams.position.normalized * Random.Range(3f, 5f);
                p.particleSystem.Emit(emitParams, 1);
            }
        }
        Particles.PlayParticle(p);
    }

    #endregion

    #region Audio

    public static void PlaySound(AudioClip clip) {
        Audio.audioSound.PlayOneShot(clip);
    }
    public static void PlayPitchedSound(AudioClip clip, float pitch, bool nonDuplicate = false, bool loudenDuplicates = false) {
        Audio.PlayPitched(clip, pitch, nonDuplicate, loudenDuplicates);
    }
    public static void PlayRandomSound(AudioClip clip, bool nonDuplicate = false, bool loudenDuplicates = false) {
        Audio.PlayRandom(clip, nonDuplicate, loudenDuplicates);
    }

    IEnumerator PlayCollectSongAudio() {
        yield return new WaitUntil(() => levelInitialized);
        while (levelInitialized) {
            yield return new WaitForSeconds(Random.Range(0.5f, 2f));
            Audio.PlaySongNote();
        }
    }

    #endregion

    #region Save/Load

    // Check if save files exist, if not copy default data
    public static void InitData() {
        int startIndex = 1;
#if (UNITY_EDITOR)
        startIndex = 0;
#endif

        char dsc = Path.DirectorySeparatorChar;
        string gridDataPath = Application.persistentDataPath + dsc + "GridData";
        if (!File.Exists(gridDataPath))
            Directory.CreateDirectory(gridDataPath);
        for (int i = startIndex; i <= SAVE_SLOT_AMOUNT; i++) {
            if (!File.Exists(gridDataPath + dsc + i))
                Directory.CreateDirectory(gridDataPath + dsc + i);
        }

        foreach (string s in GetSceneNames()) {
            TextAsset ta = Resources.Load("GridData/" + s) as TextAsset;
            if (ta == null)
                continue;

            for (int j = startIndex; j <= SAVE_SLOT_AMOUNT; j++) {
                string path = gridDataPath + dsc + j + dsc + s + ".data";
                if (!File.Exists(path)) {
                    File.WriteAllBytes(path, ta.bytes);
                }
            }
        }

        string playerDataPath = Application.persistentDataPath + dsc + "PlayerData";
        if (!File.Exists(playerDataPath))
            Directory.CreateDirectory(playerDataPath);
        for (int i = startIndex; i <= SAVE_SLOT_AMOUNT; i++) {
            if (!File.Exists(playerDataPath + dsc + i))
                Directory.CreateDirectory(playerDataPath + dsc + i);
        }

        Object[] maps = Resources.LoadAll("PlayerData", typeof(TextAsset));
        if (maps != null) {
            foreach (TextAsset ta in maps) {
                for (int i = startIndex; i <= SAVE_SLOT_AMOUNT; i++) {
                    string mapPath = playerDataPath + dsc + i + dsc + ta.name + ".data";
                    if (!File.Exists(mapPath))
                        File.WriteAllBytes(mapPath, ta.bytes);
                }
            }
        }
    }
    // Delete save data and refresh
    public static void DeleteData(int slot) {
        DirectoryInfo di = new DirectoryInfo(Application.persistentDataPath);
        foreach (DirectoryInfo dir in di.GetDirectories()) {
            if (dir.Name == "GridData" || dir.Name == "PlayerData") {
                foreach (DirectoryInfo ddir in dir.GetDirectories()) {
                    if (ddir.Name == slot + "")
                        ddir.Delete(true);
                }
            }
        }
        InitData();
    }

    public static void SaveGrid(string fileName, int slot, GridSystem gridSystem, bool trimBounds = false) {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "GridData" + Path.DirectorySeparatorChar + slot + Path.DirectorySeparatorChar + fileName + ".data";

        if (trimBounds)
            gridSystem.TrimBounds();
        gridSystem.SortBlockData();
        GridSystem.GridData gridData = gridSystem.GetGridData();

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(path);
        bf.Serialize(file, gridData);
        file.Close();

#if UNITY_EDITOR
        if (slot != 0)
            return;

        string resourcePath = "Assets/Resources/GridData/" + fileName + ".bytes";

        // Save default level state and delete old ones
        foreach (BlockData bd in gridData.blockDatas) {
            if (bd.blockName == "Ground" || bd.blockName == "Dig")
                continue;
            bd.origin = bd.coordinates;
        }

        bf = new BinaryFormatter();
        file = File.Create(resourcePath);
        bf.Serialize(file, gridData);
        file.Close();
        UnityEditor.AssetDatabase.Refresh();

        for (int i = 1; i <= SAVE_SLOT_AMOUNT; i++) {
            string deletePath = Application.persistentDataPath + Path.DirectorySeparatorChar + "GridData" + Path.DirectorySeparatorChar + i + Path.DirectorySeparatorChar + fileName + ".data";
            if (File.Exists(deletePath))
                File.Delete(deletePath);
        }
        InitData();
#endif
    }
    public static GridSystem.GridData LoadGrid(string fileName, int slot) {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "GridData" + Path.DirectorySeparatorChar + slot + Path.DirectorySeparatorChar + fileName + ".data";

        if (File.Exists(path)) {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            GridSystem.GridData gridData = (GridSystem.GridData)bf.Deserialize(file);
            file.Close();

            return gridData;
        }
        return null;
    }
    // Match gameobjects to stored data and initialize other room data
    public static void LoadBlocks(string fileName, int slot, GridSystem gridSystem) {
        GameObject level = GameObject.FindGameObjectWithTag("Level");
        if (level == null) { Debug.LogError("Level Not Found"); return; }

        LevelData levelData = level.GetComponent<LevelData>();
        if (levelData           == null    ) { Debug.LogError(level.name + " Level Data Missing"                      ); return; }
        if (levelData.levelName != fileName) { Debug.LogError(levelData.levelName + "/" + fileName + " Level Mismatch"); return; }
        Level = levelData;
        Level.parallaxLayers[0] = Game.gameCamera.transform.parent.gameObject;

        forceDatas = null;
        forceDirection = Level.initialForceDirection;
        int forceState = GetForceState(forceDirection);

        Screen.ScreenData[] screenDatas = Grid.GetGridData().screenDatas;
        for (int i = 0; i < screenDatas.Length; i++)
            Level.screens[i].screenData = screenDatas[i];

        floatObjects        .Clear();
        foregroundCoroutines.Clear();
        digOutlines         .Clear();
        resetOutlines       .Clear();
        resetOutlines.Add(tunnelResetItem.spriteRenderer);
        resetOutlines.Add(tunnelResetItem.blockObject.transform.GetChild(0).GetComponent<SpriteRenderer>());

        foreach (ColorObjects.ColorField cf in colorObjects[1].colorFields) {
            cf.spriteRenderers.Clear();
            cf.images         .Clear();
            cf.lights         .Clear();
        }
        if (colorSymbols[1] != null)
            colorSymbols[1].Clear();

        stasisMachineArcs = null;
        switch (levelData.levelName) {
            case "Origin":
                GameObject[] arcs = GameObject.FindGameObjectsWithTag("Environment");
                StasisMachineArc[] sma = new StasisMachineArc[arcs.Length];
                bool moveTriggered = Player.playerData.promptTriggers[5];
                for (int i = 0; i < arcs.Length; i++) {
                    sma[i] = arcs[i].GetComponent<StasisMachineArc>();
                    AddColorObject(ColorIndex.Time, sma[i].spriteRenderer, sma[i].light2d);
                    sma[i].Init(moveTriggered);
                }
                if (!moveTriggered) {
                    InitFloatObject(Game.playerHolder);
                    stasisMachineArcs = sma;
                }
                break;

            case "CollectColorRed"  :
            case "CollectColorGreen":
            case "CollectColorBlue" :
                GameObject flower = GameObject.FindGameObjectWithTag("Environment");
                AddColorObject(ConvertColorNameToIndex(levelData.name), flower.GetComponent<SpriteRenderer>());
                break;
        }

        GameObject[] levelBlockObjects = GameObject.FindGameObjectsWithTag("LevelBlock");
        Dictionary<string, List<LevelBlock>> levelBlocks = new Dictionary<string, List<LevelBlock>>();
        foreach (GameObject go in levelBlockObjects) {
            LevelBlock lb = go.GetComponent<LevelBlock>();
            if (lb.layer == Layer.Dig) {
                digOutlines.Add(lb, lb.GetBlockItem("Primary").spriteRenderer);
                List<LevelBlock.BlockItem> positionItems = lb.GetBlockItems("DigPositions");
                foreach (LevelBlock.BlockItem bi in positionItems) {
                    Data digData = Grid.GetData(GetCoordinates(bi.blockObject.transform.position), Layer.Dig);
                    foreach (Coordinates c in digData.blockData.connectedBlocks) {
                        Data d = Grid.GetData(c, Layer.Dig);
                        d.levelBlock = lb;
                        Level.AddPuzzleData(d, PuzzleIndex.Dig);
                    }
                }

                LevelBlock.BlockItem levelOutline = lb.GetBlockItem("DigLevelOutline");
                if (levelOutline.blockObject != null)
                    levelOutline.spriteRenderer.enabled = false;

                continue;
            }

            string blockKey = GetCoordinates(lb.transform.position).ToString();
            levelBlocks.TryGetValue(blockKey, out List<LevelBlock> blockList);
            if (blockList == null) {
                blockList = new List<LevelBlock>();
                levelBlocks.Add(blockKey, blockList);
            }

            blockList.Add(lb);
        }

        int width  = gridSystem.grid.GetLength(0);
        int height = gridSystem.grid.GetLength(1);
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                foreach (Data d in gridSystem.grid[x, y]) {
                    if (d == null || d.HasTag(Tag.Tile) || d.layer == Layer.Dig || (d.HasTag(Tag.Connect) && !d.blockData.IsPrimary()))
                        continue;

                    switch (d.layer) {
                        case Layer.Block:
                            if (d.blockData.blockName == "Pipe")
                                continue;
                            break;

                        case Layer.Misc  :
                        case Layer.Piston:
                            switch (d.blockData.blockName) {
                                case "Panel"     :
                                case "TunnelDoor":
                                case "PistonArm" :
                                    continue;
                            }
                            break;
                    }

                    Initialize(d);
                }
            }
        }
        foreach (List<Data> datas in gridSystem.destroyedBlocks.Values)
            Initialize(datas[0]);
        
        void Initialize(Data d) {
            List<LevelBlock> blockList = levelBlocks[d.blockData.origin.ToString()];
            foreach (LevelBlock lb in blockList) {
                if (lb.layer != d.layer || lb.blockName != d.blockData.blockName)
                    continue;

                LevelBlock.BlockItem primaryItem = lb.GetBlockItem("Primary" );
                LevelBlock.BlockItem infoItem    = lb.GetBlockItem("InfoItem");
                d.levelBlock  = lb;
                d.blockObject = primaryItem.blockObject;

                if (d.blockData.IsPrimary()) {
                    switch (d.layer) {
                        case Layer.Block:
                            ApplyCoordinates(d.blockData.coordinates, d.blockObject);
                            if (d.connectedData != null) {
                                foreach (Data cd in d.connectedData)
                                    cd.levelBlock = lb;
                            }

                            if (d.HasTag(Tag.Push))
                                InitTimeItems();

                            if (d.HasTag(Tag.Float)) {
                                InitFloatObject(lb.GetBlockItem("CrystalFloatOutline").blockObject);
                                ColorIndex colorIndex = (ColorIndex)ConvertColorNameToIndex(d.blockData.blockName);
                                AddColorObject(colorIndex, primaryItem.spriteRenderer, primaryItem.light);
                                foreach (LevelBlock.BlockItem bi in lb.GetBlockItems("All"))
                                    AddColorObject(colorIndex, bi.spriteRenderer, bi.light);
                                AddColorSymbols(lb);

                                switch (colorIndex) {
                                    case ColorIndex.Blue:
                                        GameObject breakObject = lb.GetBlockItem("BlueCrystalBreak").blockObject;
                                        FloatObject crystalFloat = floatObjects[floatObjects.Count - 1];
                                        InitFloatObject(breakObject);

                                        // Match broken version float to normal float
                                        FloatObject breakFloat = floatObjects[floatObjects.Count - 1];
                                        breakFloat.offset    = crystalFloat.offset;
                                        breakFloat.direction = crystalFloat.direction;
                                        break;

                                    case ColorIndex.Force:
                                        if (d.blockData.state == -1)
                                            d.blockData.state =  forceState;
                                        break;
                                }

                                Level.AddPuzzleData(d, PuzzleIndex.Crystal);
                                break;
                            }

                            Level.AddPuzzleData(d, PuzzleIndex.Rock);
                            break;

                        case Layer.Tunnel:
                            Data doorData = gridSystem.GetData(d.blockData.coordinates, Layer.Misc);
                            doorData.blockObject = lb.GetBlockItem("TunnelDoor").blockObject;
                            doorData.levelBlock  = lb;

                            Data panelData = gridSystem.GetData(d.blockData.connectedBlocks[1], Layer.Misc);
                            InitPanelData(panelData);
                            InitTimeItems();
                            break;

                        case Layer.Piston:
                            InitPanelData(gridSystem.GetData(d.blockData.coordinates, Layer.Misc));
                            InitTimeItems();

                            LevelBlock.BlockItem panelItem = lb.GetBlockItem("Panel");
                            PistonPanel pp = (PistonPanel)panelItem.script;
                            for (int i = 0; i < pp.pistonArms.Length; i++) {
                                pp.pistonArms[i].data = gridSystem.GetData(d.blockData.connectedBlocks[i + 1], Layer.Piston);
                                pp.pistonArms[i].data.levelBlock = lb;
                                Level.AddPuzzleData(pp.pistonArms[i].data, PuzzleIndex.Piston);
                            }
                            Level.AddPuzzleData(d, PuzzleIndex.Piston);
                            if (Level.roomInfo[0] == null)
                                Level.roomInfo[0] = new List<LevelBlock.BlockItem>();
                            Level.roomInfo[0].Add(lb.GetBlockItem("PistonInfo"));

                            if (!Player.playerData.promptTriggers[3]) {
                                 Player.playerData.promptTriggers[3] = true;
                                triggerRoomInfoPrompt = true;
                            }
                            break;

                        case Layer.Fg:
                            foregroundCoroutines.Add(d.levelBlock.GetBlockItem("Primary").spriteRenderer, null);
                            if (d.connectedData != null) {
                                foreach (Data cd in d.connectedData)
                                    cd.levelBlock = lb;
                            }
                            Level.AddPuzzleData(d, PuzzleIndex.Fg);
                            break;
                    }
                }
                else {
                    int colorIndex = -1;
                    switch (d.layer) {
                        case Layer.Misc:
                            if (d.blockData.blockName == "GateSlot") {
                                LevelBlock.BlockItem fragmentItem = lb.GetBlockItem("SlotFragment");
                                AddColorObject(ColorIndex.Fragment, fragmentItem.spriteRenderer, fragmentItem.light);
                                AddColorObject(ColorIndex.Fragment, infoItem    .spriteRenderer);

                                InitTimeItems();
                                Level.AddPuzzleData(d, PuzzleIndex.GateSlot);
                                Level.AddRoomInfo(lb);
                                break;
                            }

                            colorIndex = ConvertColorNameToIndex(d.blockData.blockName);
                            primaryItem                     = lb.GetBlockItem("Primary"     );
                            LevelBlock.BlockItem bulletItem = lb.GetBlockItem("ButtonBullet");
                            AddColorObject(colorIndex, bulletItem .spriteRenderer, bulletItem .light);
                            AddColorObject(colorIndex, primaryItem.spriteRenderer, primaryItem.light);
                            AddColorObject(colorIndex, infoItem   .spriteRenderer);
                            AddColorSymbols(lb);

                            InitTimeItems();
                            Level.AddPuzzleData(d, PuzzleIndex.Button);
                            Level.AddRoomInfo(lb);
                            break;

                        case Layer.Collect:
                            switch (d.blockData.blockName) {
                                case "CollectDig":
                                    break;

                                case "CollectSong"  :
                                case "CollectLength":
                                    InitFloatObject(primaryItem.blockObject, false);
                                    break;

                                default:
                                    InitFloatObject(primaryItem.blockObject, false);
                                    colorIndex = ConvertColorNameToIndex(d.blockData.blockName);
                                    AddColorObject(colorIndex, primaryItem.spriteRenderer, primaryItem.light);
                                    if (colorIndex <= (int)ColorIndex.Force)
                                        AddColorSymbols(lb);
                                    break;
                            }

                            switch (d.blockData.blockName) {
                                case "CollectDig":
                                    break;

                                case "CollectSong"    : Audio.InitPositionalAudio(AudioIndex.Song    , d); Game.StartCoroutine(Game.PlayCollectSongAudio()); break;
                                case "CollectFragment": Audio.InitPositionalAudio(AudioIndex.Fragment, d); break;
                                default               : Audio.InitPositionalAudio(AudioIndex.Collect , d); break;
                            }
                            
                            InitTimeItems();
                            lb.GetBlockItem("ResetOutline").blockObject.transform.position = primaryItem.blockObject.transform.position;
                            Level.AddPuzzleData(d, PuzzleIndex.Collectable);
                            break;

                        case Layer.Block:
                            switch (d.blockData.blockName) {
                                case "Basic":
                                    primaryItem.spriteRenderer.enabled = false;
                                    break;

                                case "Gate":
                                    LevelBlock.BlockItem doorItem = lb.GetBlockItem("GateDoor");
                                    AddColorObject(ColorIndex.Fragment, doorItem.spriteRenderer, doorItem.light);
                                    AddColorObject(ColorIndex.Fragment, infoItem.spriteRenderer);

                                    InitTimeItems();
                                    Level.AddPuzzleData(d, PuzzleIndex.Gate);
                                    Level.AddRoomInfo(lb);

                                    if (!Player.playerData.promptTriggers[0]) {
                                         Player.playerData.promptTriggers[0] = true;
                                        triggerRoomInfoPrompt = true;
                                    }
                                    break;
                            }
                            break;
                    }
                }

                void InitPanelData(Data panelData) {
                    if (panelData == null)
                        return;

                    LevelBlock.BlockItem panelItem = lb.GetBlockItem("Panel");
                    panelData.blockObject = panelItem.blockObject;
                    panelData.levelBlock  = lb;

                    Panel p = (Panel)panelItem.script;
                    p.panelData = panelData;

                    int promptTriggerIndex = 1;
                    if (p.gatePanelIndex == -1) {
                        foreach (Panel.PanelLight pl in p.panelLights)
                            AddColorObject(pl.color, pl.spriteRenderer, pl.light);

                        SpriteRenderer[] children = infoItem.blockObject.GetComponentsInChildren<SpriteRenderer>();
                        foreach (SpriteRenderer sr in children)
                            AddColorObject(ConvertColorNameToIndex(sr.name), sr);
                        AddColorSymbols(lb);

                        Level.AddPuzzleData(panelData, PuzzleIndex.Panel);
                        Level.AddRoomInfo(lb, true);
                    }
                    else {
                        promptTriggerIndex = 2;

                        LevelBlock.BlockItem gateItem = lb.GetBlockItem("GatePanelLight");
                        AddColorObject(ColorIndex.Fragment, gateItem.spriteRenderer, gateItem.light);
                        AddColorObject(ColorIndex.Fragment, infoItem.spriteRenderer);
                        panelData.blockData.state = Player.playerData.gateTunnels[p.gatePanelIndex] ? (int)Activation.On : (int)Activation.Off;

                        Level.AddPuzzleData(panelData, PuzzleIndex.Gate);
                        Level.AddRoomInfo(lb);
                    }
                    
                    if (!Player.playerData.promptTriggers[promptTriggerIndex]) {
                         Player.playerData.promptTriggers[promptTriggerIndex] = true;
                        triggerRoomInfoPrompt = true;
                    }
                }
                void InitTimeItems() {
                    LevelBlock.BlockItem undoItem  = lb.GetBlockItem("UndoOutline" );
                    LevelBlock.BlockItem resetItem = lb.GetBlockItem("ResetOutline");
                    if (undoItem == null || resetItem == null)
                        return;

                    AddColorObject(ColorIndex.Time, undoItem .spriteRenderer);
                    AddColorObject(ColorIndex.Time, resetItem.spriteRenderer);
                    resetOutlines.Add(resetItem.spriteRenderer);
                }
            }
        }

        digMask = Level.groundHolder.GetComponentInChildren<SpriteMask>();
        if (digMask != null)
            digMask.enabled = false;

        GetForceData();
        CycleFloating(true);

        CheckLevelButtons(Activation.Alt);
        Level.RecordResetData ();
        Level.RecordPuzzleData();
        Game.ApplyPuzzleDataRecord(Level.puzzleHistory.Peek(), Level.playerHistory.Peek(), Activation.Alt);
        UpdatePistonsIfBlocked(Activation.Alt);
        
        List<Data> gateList = Level.puzzleData[(int)PuzzleIndex.Gate];
        if (gateList != null) {
            foreach (Data d in gateList) {
                if (d.layer == Layer.Block)
                    d.levelBlock.GetBlockItem("ResetOutline").blockObject.transform.position = d.levelBlock.GetBlockItem("GateDoor").blockObject.transform.position;
            }
        }
    }
    
    public static void SavePlayer(PlayerController playerController) {
        if (playerController == null)
            return;
        
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + currentSave + Path.DirectorySeparatorChar + "Player.data";

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(path);
        bf.Serialize(file, playerController.playerData);
        file.Close();
    }
    public static PlayerController.PlayerData LoadPlayer(int slot) {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + slot + Path.DirectorySeparatorChar + "Player.data";

        if (File.Exists(path)) {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            PlayerController.PlayerData playerData = (PlayerController.PlayerData)bf.Deserialize(file);
            file.Close();

            return playerData;
        }
        return null;
    }

    public static void SaveMap(MapController.MapData mapData, int slot) {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + slot + Path.DirectorySeparatorChar + mapData.mapName + ".data";

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(path);
        bf.Serialize(file, mapData);
        file.Close();

#if UNITY_EDITOR
        if (slot != 0)
            return;

        string resourcePath = "Assets/Resources/PlayerData/" + mapData.mapName + ".bytes";

        // Save default map data and delete old ones
        if (mapData.roomDatas != null) {
            foreach (MapController.RoomData rd in mapData.roomDatas) {
                if (rd.tunnels != null) {
                    foreach (MapController.RoomData.Tunnel t in rd.tunnels)
                        t.visited = false;
                }
            }
        }

        bf = new BinaryFormatter();
        file = File.Create(resourcePath);
        bf.Serialize(file, mapData);
        file.Close();

        for (int i = 1; i <= SAVE_SLOT_AMOUNT; i++) {
            string deletePath = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + i + Path.DirectorySeparatorChar + mapData.mapName + ".data";
            if (File.Exists(deletePath))
                File.Delete(deletePath);
        }
        InitData();
#endif
    }
    public static MapController.MapData LoadMap(string mapName, int slot) {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "PlayerData" + Path.DirectorySeparatorChar + slot + Path.DirectorySeparatorChar + mapName + ".data";

        if (File.Exists(path)) {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            MapController.MapData mapData = (MapController.MapData)bf.Deserialize(file);
            file.Close();
            
            return mapData;
        }
        return new MapController.MapData(mapName);
    }
#if UNITY_EDITOR
    public static List<MapController.MapData> LoadAllMaps() {
        List<MapController.MapData> maps = new List<MapController.MapData>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:sprite", new[] { "Assets/Sprites/Maps" })) {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (path.Split('/').Length > 4)
                continue;

            Sprite s = (Sprite)UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
            maps.Add(LoadMap(s.name, 0));
        }
        return maps;
    }
#endif
    
    #endregion
}