#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering.LWRP;

[ExecuteInEditMode]
public class Generation : MonoBehaviour {
    enum SpriteType { Full, Crystal, Connected, Single }
    public SpriteGeneration spriteGeneration;
    public Sprite[] supportCrystalSprites;
    const int TUNNEL_LENGTH_MIN = 9;
    
    public bool generateGround;
    public bool resetTiling;

    public bool generateGroundShadow;
    public bool generateSupportCrystals;
    public bool generateRoomMap;
    bool abort;

    Dictionary<string, List<Sprite>> sprites;
    Dictionary<string, GameObject> prefabs;

    class SupportSorting : IComparable<SupportSorting> {
        public int distance;
        public List<Data> supports = new List<Data>();

        public SupportSorting(int distance, Data support) {
            this.distance = distance;
            supports.Add(support);
        }

        public int CompareTo(SupportSorting ss) {
            return ss.distance - distance;
        }
    }

    class TunnelConnect {
        public GameObject spriteObject;
        public List<Data> dataList;
        public Coordinates facingDirection;
        public Coordinates connection;

        public TunnelConnect(GameObject spriteObject, List<Data> dataList, Coordinates facingDirection, Coordinates connection) {
            this.spriteObject    = spriteObject;
            this.dataList        = dataList;
            this.facingDirection = facingDirection;
            this.connection      = connection;
        }
    }
    
    class PixelGroup {
        public bool visited;
        public List<Pixel> pixels = new List<Pixel>();

        public class Pixel {
            public Coordinates pixel;
            public Texture2D texture;

            public Pixel(Coordinates pixel, Texture2D texture) {
                this.pixel   = pixel;
                this.texture = texture;
            }
        }
    }

    [ContextMenu("Generate")]
    public void Generate() {
        gameObject.SetActive(false);
        
        GenerateLevel();
        if (abort)
            return;

        if (generateGroundShadow   ) GenerateGroundShadow();
        if (generateRoomMap        ) GenerateRoomMap();
        if (generateSupportCrystals) GenerateSupportCrystals();
    }

    [ContextMenu("Generate Level")]
    public void GenerateLevel() {
        if (EditorApplication.isPlaying)
            return;

        GameObject level = GameObject.FindGameObjectWithTag("Level");
        if (level == null) {
            Debug.LogError("Level Not Found");
            abort = true;
            return;
        }
        
        sprites = Assets.GetTileSprites();
        prefabs = Assets.GetPrefabs();

        string levelName = level.name;
        LevelData oldLevelData = level.GetComponent<LevelData>();
        GridSystem.GridData checkGridData = GameController.LoadGrid(levelName, 0);
        if (checkGridData == null && oldLevelData.levelName != "Default") {
            Debug.LogError("GridSystem Not Found");
            abort = true;
            return;
        }

        GameController.generatingBlocks = true;
        GridSystem gridSystem = new GridSystem(checkGridData, true);
        GridSystem.GridData gridData = gridSystem.GetGridData();
        gridData.supportCrystalCoordinates = null;

        Vector2 iconPos = GameController.GetGridPosition(GameController.GetCoordinates(oldLevelData.playerIcon.transform.position));
        Coordinates initialForceDirection = oldLevelData.initialForceDirection;

        LevelBlock[] levelBlocks = level.GetComponentsInChildren<LevelBlock>();
        List<PanelCopy> panelCopies = new List<PanelCopy>();
        List<Coordinates> presetSlots = new List<Coordinates>();
        List<Coordinates> rockReskinCoords = new List<Coordinates>();
        List<int> rockReskinHeadTypes = new List<int>();
        foreach (LevelBlock lb in levelBlocks) {
            if (lb.layer == Layer.Tunnel || lb.layer == Layer.Piston) {
                // Copy exisiting panels to regenerate
                LevelBlock.BlockItem panelItem = lb.GetBlockItem("Panel");
                if (panelItem == null)
                    continue;

                Panel p = (Panel)panelItem.script;
                if ((p.lightColors != null && p.lightColors.Length > 0) || p.gatePanelIndex != -1) {
                    GameObject copy = new GameObject("PanelCopy");
                    copy.tag = "EditorOnly";
                    copy.transform.position = p.transform.position;

                    PanelCopy pc = copy.AddComponent<PanelCopy>();
                    if (p.gatePanelIndex == -1) {
                        pc.lightColors = new ColorIndex[p.lightColors.Length];
                        p.lightColors.CopyTo(pc.lightColors, 0);
                        pc.inverted = p.inverted;
                    }
                    else
                        pc.gatePanelIndex = p.gatePanelIndex;

                    panelCopies.Add(pc);
                }
            }
            else {
                switch (lb.blockName) {
                    case "Rock":
                        ReskinAsRobot rar = lb.GetComponent<ReskinAsRobot>();
                        if (rar != null) {
                            rockReskinCoords.Add(GameController.GetCoordinates(lb.transform.position));
                            rockReskinHeadTypes.Add(rar.headType);
                        }
                        break;

                    case "GateSlot":
                        LevelBlock.BlockItem fragmentItem = lb.GetBlockItem("SlotFragment");
                        if (fragmentItem != null && fragmentItem.blockObject.activeSelf)
                            presetSlots.Add(GameController.GetCoordinates(lb.transform.position));
                        break;
                }
            }
        }

        DestroyImmediate(level);
        level = CreatePrefab("LevelTemplate");
        level.name = levelName;
        LevelData levelData = level.GetComponent<LevelData>();
        levelData.levelName = levelName;
        levelData.playerIcon.GetComponent<EditorSetup>().levelEditor = gameObject;
        levelData.playerIcon.transform.position = iconPos;
        levelData.initialForceDirection = initialForceDirection;

        GameObject[] screenObjects = GameObject.FindGameObjectsWithTag("Screen");
        List<Screen> screens = new List<Screen>();
        foreach (GameObject go in screenObjects)
            screens.Add(go.GetComponent<Screen>());

        // Compare old screens with new screens, if matching leave alone, else destroy old screens and create new screens
        Screen.ScreenData[] screenDatas = gridData.screenDatas;
        levelData.screenBounds = new int[4];
        if (screenDatas != null) {
            List<Screen> levelScreens = new List<Screen>();
            screenDatas[0].bounds.CopyTo(levelData.screenBounds, 0);
            GameObject screenHolder = screenObjects[0].transform.parent.gameObject;

            foreach (Screen s in screens) {
                bool found = false;
                foreach (Screen.ScreenData sd in screenDatas) {
                    if (s.screenData.CompareData(sd)) {
                        levelScreens.Add(s);
                        found = true;
                        break;
                    }
                }
                if (!found)
                    DestroyImmediate(s.gameObject);
            }
            foreach (Screen.ScreenData sd in screenDatas) {
                bool found = false;
                foreach (Screen s in screens) {
                    if (s == null)
                        continue;

                    if (sd.CompareData(s.screenData)) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    Screen screen = CreatePrefab("Screen").GetComponent<Screen>();
                    screen.transform.SetParent(screenHolder.transform);
                    screen.SetPosition(sd.coordinates);
                    screen.SetSize(sd.size);
                    levelScreens.Add(screen);
                }

                levelData.screenBounds[0] = Mathf.Max(sd.bounds[0], levelData.screenBounds[0]);
                levelData.screenBounds[1] = Mathf.Min(sd.bounds[1], levelData.screenBounds[1]);
                levelData.screenBounds[2] = Mathf.Min(sd.bounds[2], levelData.screenBounds[2]);
                levelData.screenBounds[3] = Mathf.Max(sd.bounds[3], levelData.screenBounds[3]);
            }

            levelData.screens = levelScreens.ToArray();
            gridSystem.SetScreens(levelData.screens);
        }

        Coordinates center = new Coordinates((levelData.screenBounds[3] + levelData.screenBounds[2]) / 2, (levelData.screenBounds[0] + levelData.screenBounds[1]) / 2);
        GameObject bg = GameObject.FindGameObjectWithTag("BG");
        bg.transform.position = GameController.GetGridPosition(center);
        bg.GetComponent<SpriteRenderer>().size = new Vector2((levelData.screenBounds[3] - levelData.screenBounds[2] + 10) * GameController.GRID_SIZE, 
                                                             (levelData.screenBounds[0] - levelData.screenBounds[1] + 10) * GameController.GRID_SIZE);

        // Fill ground blocks from screen bounds inwards, until hitting existing ground
        List<Coordinates> fillSpaces = new List<Coordinates>();
        if (generateGround) {
            // For each direction, fill from one perpendicular bound across to the other, moving in the direction until hitting parallel bound,
            // e.g. Starting from North Bound, fill from West to East and move one step South until hitting South Bound
            int[] b = levelData.screenBounds;
            int[][] ranges = new int[][] {
                new int[] { b[2], b[3], b[1] },
                new int[] { b[2], b[3], b[0] },
                new int[] { b[1], b[0], b[3] },
                new int[] { b[1], b[0], b[2] }
            };
            List<Data> addedGround = new List<Data>();
            for (int i = 0; i < b.Length; i++) {
                Coordinates direction = -Coordinates.CompassDirection[i];

                for (int j = ranges[i][0] - 1; j <= ranges[i][1] + 1; j++) {
                    int padding = (i == 0 || i == 3) ? 1 : -1;
                    Coordinates nextCoord = new Coordinates(b[i] + padding, j);
                    int end = nextCoord.x;
                    if (direction.x == 0) {
                        nextCoord = new Coordinates(j, b[i] + padding);
                        end = nextCoord.y;
                    }

                    Data nextData = gridSystem.GetData(nextCoord, Layer.Block);
                    while (nextData == null && end != ranges[i][2]) {
                        if (fillSpaces.Contains(nextCoord)) {
                            nextCoord += direction;
                            end = direction.x == 0 ? nextCoord.y : nextCoord.x;
                            nextData = gridSystem.GetData(nextCoord, Layer.Block);
                            continue;
                        }

                        Data groundData = new Data(new BlockData("Ground", nextCoord));
                        addedGround.Add(groundData);
                        fillSpaces.Add(nextCoord);

                        nextCoord += direction;
                        end = direction.x == 0 ? nextCoord.y : nextCoord.x;
                        nextData = gridSystem.GetData(nextCoord, Layer.Block);
                    }
                }
            }

            foreach (Data d in addedGround)
                gridSystem.AddData(d);

            // Check for tunnels that are too short for max player length and extend ground blocks from the ends
            foreach (Coordinates c in gridSystem.GetCoordinates()) {
                Data tunnelData = gridSystem.GetData(c, Layer.Tunnel);
                if (tunnelData == null || !tunnelData.blockData.IsPrimary() || tunnelData.blockData.connectedBlocks.Length >= TUNNEL_LENGTH_MIN)
                    continue;

                Data endData = gridSystem.GetData(tunnelData.blockData.connectedBlocks[tunnelData.blockData.connectedBlocks.Length - 1], Layer.Tunnel);
                Coordinates direction = -Coordinates.FacingDirection[endData.blockData.facing];
                Coordinates nextCoord = endData.blockData.coordinates + direction;
                if (!gridSystem.WithinBounds(nextCoord)) {
                    Debug.LogError("Tunnel Bounds Reached: " + tunnelData.blockData.connectedBlocks.Length + " | " + endData.blockData.coordinates);
                    continue;
                }
                if (gridSystem.GetData(nextCoord, Layer.Tunnel) != null)
                    continue;

                Coordinates currentCoord = nextCoord;
                for (int i = 0; i < TUNNEL_LENGTH_MIN - tunnelData.blockData.connectedBlocks.Length; i++) {
                    if (!gridSystem.WithinBounds(currentCoord)) {
                        Debug.LogError("Tunnel Bounds Reached: " + tunnelData.blockData.connectedBlocks.Length + " | " + endData.blockData.coordinates);
                        break;
                    }

                    if (gridSystem.GetData(currentCoord, Layer.Block) == null) {
                        Data groundData = new Data(new BlockData("Ground", currentCoord));
                        gridSystem.AddData(groundData);
                    }

                    currentCoord += direction;
                }
            }
        }

        // Check empty spaces for elligible air dust spawn positions
        List<Coordinates> airDusts = new List<Coordinates>();
        if (levelData.screenBounds != null && levelData.screenBounds.Length > 0) {
            foreach (Coordinates c in gridSystem.GetCoordinates()) {
                if (!levelData.WithinScreenBounds(c))
                    continue;

                Data[] datas = gridSystem.GetData(c, Layer.Block, Layer.Tunnel);
                foreach (Data d in datas) {
                    if (d == null)
                        continue;

                    switch (d.layer) {
                        case Layer.Block:
                            if (d.HasTag(Tag.Push))
                                continue;

                            switch (d.blockData.blockName) {
                                case "Gate":
                                case "Pipe":
                                    continue;
                            }
                            break;
                    }

                    datas = null;
                    break;
                }
                if (datas != null)
                    airDusts.Add(c);
            }
        }
        levelData.airDusts = airDusts.ToArray();

        List<Data> digDatas = new List<Data>();
        List<Data>[] dataGroups = new List<Data>[Enum.GetNames(typeof(SpriteType)).Length];
        for (int i = 0; i < dataGroups.Length; i++)
            dataGroups[i] = new List<Data>();

        // Categorize blocks for different tiling methods
        List<Coordinates> groundDusts = new List<Coordinates>();
        foreach (Coordinates c in gridSystem.GetCoordinates()) {
            foreach (Data d in gridSystem.GetDatas(c)) {
                if (d == null)
                    continue;

                if (d.HasTag(Tag.Tile)) {
                    InitBlock(SpriteType.Full);

                    // Check blocks overlapping and under ground blocks for elligible ground dust spawn positions
                    if (d.blockData.blockName == "Ground") {
                        if (levelData.screenBounds == null || levelData.screenBounds.Length == 0 || !levelData.WithinScreenBounds(d.blockData.coordinates))
                            continue;

                        Data[] datas = gridSystem.GetData(d.blockData.coordinates + Coordinates.Down, Layer.Block, Layer.Piston, Layer.Tunnel, Layer.Dig);
                        foreach (Data data in datas) {
                            if (data != null && !data.HasTag(Tag.Push)) {
                                datas = null;
                                break;
                            }
                        }
                        if (datas == null)
                            continue;

                        datas = gridSystem.GetData(d.blockData.coordinates, Layer.Piston, Layer.Tunnel, Layer.Dig);
                        foreach (Data data in datas) {
                            if (data != null) {
                                datas = null;
                                break;
                            }
                        }
                        if (datas == null)
                            continue;

                        groundDusts.Add(d.blockData.coordinates + Coordinates.Down);
                    }
                }
                else {
                    if (d.HasTag(Tag.Connect)) {
                        if (d.blockData.IsPrimary()) {
                            if (!d.HasTag(Tag.Float)) {
                                if (d.layer != Layer.Dig)
                                    InitBlock(SpriteType.Connected);
                            }
                            else
                                InitBlock(SpriteType.Crystal);
                        }
                        if (d.layer == Layer.Dig) {
                            if (d.blockData.facing == -1) gridSystem.RemoveData(d);
                            else                          digDatas.Add(d);
                        }
                        if (d.layer == Layer.Fg)
                            d.blockData.state = (int)Activation.On;
                    }
                    else {
                        switch (d.blockData.blockName) {
                            case "TunnelDoor"    :
                            case "PistonArm"     :
                            case "Panel"         :
                            case "SupportCrystal":
                                gridSystem.RemoveData(d);
                                continue;

                            default:
                                InitBlock(SpriteType.Single);
                                continue;
                        }
                    }
                }

                void InitBlock(SpriteType spriteType) {
                    dataGroups[(int)spriteType].Add(d);
                    if (spriteType == SpriteType.Single)
                        return;

                    if (resetTiling || (fillSpaces.Count > 0 && d.blockData.blockName == "Ground"))
                        d.blockData.spriteState = -1;
                    GameController.ApplyTiling(d, gridSystem);
                }
            }
        }
        levelData.groundDusts = groundDusts.ToArray();
        
        LevelBlock.BlockItem groundItem = null;
        List<Data> groundDatas = null;
        List<Data> basicDatas = new List<Data>();
        List<TunnelConnect> tunnels = new List<TunnelConnect>();
        GameObject outlineTemp = new GameObject();
        SpriteRenderer timeOutline = outlineTemp.AddComponent<SpriteRenderer>();
        for (int i = 0; i < dataGroups.Length; i++) {
            if (dataGroups[i].Count > 0) {
                switch ((SpriteType)i) {
                    case SpriteType.Full:
                        Dictionary<string, List<Data>> fullGroups = new Dictionary<string, List<Data>>();
                        foreach (Data d in dataGroups[i]) {
                            fullGroups.TryGetValue(d.blockData.blockName, out List<Data> group);
                            if (group == null) {
                                group = new List<Data>();
                                fullGroups.Add(d.blockData.blockName, group);
                            }

                            group.Add(d);
                        }
                        foreach (var kvp in fullGroups) {
                            LevelBlock levelBlock = CreateLevelBlock(kvp.Key, false, kvp.Key == "Ground");
                            GameObject block = levelBlock.GetBlockItem("Primary").blockObject;

                            spriteGeneration.GenerateSprite(kvp.Value, gridSystem, block);
                            List<Data> tempDatas = new List<Data>();
                            if (kvp.Key == "Ground") {
                                groundDatas = kvp.Value;
                                groundItem  = levelBlock.GetBlockItem("Primary");

                                // Include empty spaces for shadow generation texture origins
                                foreach (Coordinates c in airDusts) {
                                    Data tempData = new Data(new BlockData("Ground", c));
                                    groundDatas.Add(tempData);
                                      tempDatas.Add(tempData);
                                }
                            }

                            SetTextureOrigins(kvp.Value, GameController.BLOCK_SIZE);

                            if (kvp.Key == "Ground") {
                                // Color pixels used for shadow generation
                                Texture2D t = groundItem.spriteRenderer.sprite.texture;
                                foreach (Data d in tempDatas) {
                                    Coordinates o = d.blockData.origin;
                                    Color32 color = new Color32(255, 0, 0, 2);
                                    for (int x = 0; x < GameController.BLOCK_SIZE; x++) {
                                        for (int y = 0; y < GameController.BLOCK_SIZE; y++)
                                            t.SetPixel(o.x + x, o.y + y, color);
                                    }
                                    groundDatas.Remove(d);
                                }
                                t.Apply();
                            }

                            switch (kvp.Key) {
                                case "Ground"   : block.transform.SetParent(levelData.groundHolder     .transform); break;
                                case "Support"  : block.transform.SetParent(levelData.parallaxLayers[1].transform); break;
                                case "GroundBG1": block.transform.SetParent(levelData.groundHolder     .transform); break;
                                case "GroundBG2": block.transform.SetParent(levelData.parallaxLayers[2].transform); break;
                            }
                        }
                        break;

                    case SpriteType.Connected:
                        foreach (Data d in dataGroups[i]) {
                            List<Data> dataList = new List<Data>();
                            foreach (Coordinates c in d.blockData.connectedBlocks)
                                dataList.Add(gridSystem.GetData(c, d.layer));

                            LevelBlock levelBlock = null;
                            LevelBlock.BlockItem primaryItem = null;
                            switch (d.layer) {
                                case Layer.Block:
                                    levelBlock  = CreateLevelBlock(d.blockData.blockName, true, true);
                                    primaryItem = levelBlock.GetBlockItem("Primary");
                                    GameController.ApplyCoordinates(d.blockData.coordinates, primaryItem.blockObject);

                                    if (d.blockData.blockName == "Pipe") {
                                        BuildSprite(dataList, sprites["Pipe"], primaryItem.spriteRenderer.gameObject);
                                        primaryItem.blockObject.transform.SetParent(levelData.otherHolder.transform);
                                        break;
                                    }
                                    
                                    spriteGeneration.GenerateSprite(dataList, gridSystem, null, timeOutline, primaryItem.spriteRenderer.gameObject);
                                    CreateTimeOutlines(d, timeOutline.sprite, levelBlock, levelData, CreatePrefab("UndoOutline"));

                                    primaryItem.blockObject.transform.SetParent(levelData.rockHolder.transform);

                                    for (int j = 0; j < rockReskinCoords.Count; j++) {
                                        if (rockReskinCoords[j] != d.blockData.coordinates)
                                            continue;

                                        ReskinAsRobot rar = levelBlock.gameObject.AddComponent<ReskinAsRobot>();
                                        rar.headType = rockReskinHeadTypes[j];
                                        rar.Reskin(gridSystem);
                                    }
                                    break;

                                case Layer.Tunnel:
                                    levelBlock  = CreateLevelBlock(d.blockData.blockName, true, true);
                                    primaryItem = levelBlock.GetBlockItem("Primary");
                                    GameController.ApplyCoordinates(d.blockData.coordinates, primaryItem.blockObject);
                                    
                                    BlockData tail = gridSystem.GetData(d.blockData.connectedBlocks[d.blockData.connectedBlocks.Length - 1], Layer.Tunnel).blockData;
                                    Coordinates facingDirection = Coordinates.FacingDirection[tail.facing];
                                    tunnels.Add(new TunnelConnect(primaryItem.spriteRenderer.gameObject, dataList, facingDirection, tail.coordinates - facingDirection));

                                    GameObject tunnelDoor = CreatePrefab("TunnelDoor");
                                    Data doorData = new Data(new BlockData("TunnelDoor", d.blockData.coordinates, d.blockData.facing), tunnelDoor);
                                    doorData.ApplyData();
                                    gridSystem.AddData(doorData);
                                    levelBlock.AddBlockItem(new LevelBlock.BlockItem(tunnelDoor, tunnelDoor.GetComponent<SpriteRenderer>()));

                                    CreateInfo(levelBlock);
                                    primaryItem.blockObject.transform.SetParent(levelData.tunnelHolder.transform);

                                    Data groundData = gridSystem.GetData(d.blockData.coordinates, Layer.Block);
                                    if (groundData == null)
                                        break;
                                    
                                    // Clear ground texture pixels that are overlapping tunnel entrance to prevent worm skeleton showing on sprite mask when in dig mode
                                    Texture2D   t = groundItem.spriteRenderer.sprite.texture;
                                    Coordinates o = groundData.blockData.origin;
                                    Color32 color = new Color32(0, 0, 0, 1);
                                    for (int x = 0; x < GameController.BLOCK_SIZE; x++) {
                                        for (int y = 0; y < GameController.BLOCK_SIZE; y++)
                                            t.SetPixel(o.x + x, o.y + y, color);
                                    }
                                    t.Apply();
                                    break;

                                case Layer.Fg:
                                    levelBlock  = CreateLevelBlock(d.blockData.blockName, true, true);
                                    primaryItem = levelBlock.GetBlockItem("Primary");
                                    GameController.ApplyCoordinates(d.blockData.coordinates, primaryItem.blockObject);
                                    spriteGeneration.GenerateSprite(dataList, gridSystem, primaryItem.spriteRenderer.gameObject);
                                    primaryItem.blockObject.transform.SetParent(levelData.parallaxLayers[4].transform);
                                    break;
                            }
                        }
                        break;

                    case SpriteType.Crystal:
                        foreach (Data d in dataGroups[i]) {
                            LevelBlock levelBlock = CreateLevelBlock(d.blockData.blockName, true, true);
                            LevelBlock.BlockItem primaryItem = levelBlock.GetBlockItem("Primary");

                            GameObject glowOutline  = CreatePrefab("CrystalGlowOutline" );
                            GameObject floatOutline = CreatePrefab("CrystalFloatOutline");
                            LevelBlock.BlockItem glowItem  = levelBlock.AddBlockItem(new LevelBlock.BlockItem(glowOutline , glowOutline .GetComponent<SpriteRenderer>(), glowOutline.GetComponent<Light2D>()));
                            LevelBlock.BlockItem floatItem = levelBlock.AddBlockItem(new LevelBlock.BlockItem(floatOutline, floatOutline.GetComponent<SpriteRenderer>()));
                            GameController.ApplyCoordinates(d.blockData.coordinates, primaryItem.blockObject);

                            List<Data> dataList = new List<Data>();
                            foreach (Coordinates c in d.blockData.connectedBlocks) {
                                GameObject crystalActivation = CreatePrefab("CrystalActivation");
                                GameController.ApplyCoordinates(c, crystalActivation);
                                levelBlock.AddBlockItem(new LevelBlock.BlockItem(crystalActivation, crystalActivation.GetComponent<SpriteRenderer>(), null, crystalActivation.GetComponent<Animator>()));

                                dataList.Add(gridSystem.GetData(c, d.layer));
                            }

                            spriteGeneration.GenerateSprite(dataList, gridSystem, glowItem.spriteRenderer, timeOutline, primaryItem.spriteRenderer.gameObject);
                            floatItem.spriteRenderer.sprite = glowItem.spriteRenderer.sprite;
                            glowOutline.transform.localPosition = floatOutline.transform.localPosition = primaryItem.spriteRenderer.transform.localPosition;

                            if (d.blockData.blockName == "BlueCrystal") {
                                GameObject bcb = CreatePrefab("BlueCrystalBreak");
                                LevelBlock.BlockItem breakItem = levelBlock.AddBlockItem(new LevelBlock.BlockItem(bcb, bcb.GetComponent<SpriteRenderer>(), bcb.GetComponent<Light2D>()));

                                // Create broken version of crystal based on generated texture
                                Texture2D t = primaryItem.spriteRenderer.sprite.texture;
                                Texture2D texture = new Texture2D(t.width, t.height);
                                texture.filterMode = FilterMode.Point;
                                for (int x = 0; x < t.width; x++) {
                                    for (int y = 0; y < t.height; y++)
                                        texture.SetPixel(x, y, Color.clear);
                                }
                                int pixelCount = 0;
                                for (int x = 0; x < t.width; x++) {
                                    for (int y = 0; y < t.height; y++) {
                                        if (++pixelCount < 3 || t.GetPixel(x, y).a == 0 || UnityEngine.Random.Range(0, 3) > 0)
                                            continue;

                                        Coordinates c = new Coordinates(x, y);
                                        bool skip = false;
                                        foreach (Coordinates f in Coordinates.FacingDirection) {
                                            Coordinates n = c + f;
                                            if (!WithinBounds(n))
                                                continue;

                                            if (texture.GetPixel(n.x, n.y).a > 0) {
                                                skip = true;
                                                break;
                                            }
                                        }
                                        if (skip)
                                            continue;

                                        foreach (Coordinates dd in Coordinates.DiagonalDirection) {
                                            Coordinates n = c + dd;
                                            if (!WithinBounds(n))
                                                continue;

                                            if (texture.GetPixel(n.x, n.y).a > 0 && UnityEngine.Random.Range(0, 3) > 0) {
                                                skip = true;
                                                break;
                                            }
                                        }
                                        if (skip)
                                            continue;
                                        
                                        texture.SetPixel(c.x, c.y, t.GetPixel(c.x, c.y));
                                        pixelCount = 0;
                                    }
                                }
                                texture.Apply();
                                breakItem.spriteRenderer.sprite = Sprite.Create(texture, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 1);
                                breakItem.blockObject.transform.localPosition = primaryItem.spriteRenderer.transform.localPosition;

                                bool WithinBounds(Coordinates c) {
                                    return c.x >= 0 && c.x < t.width 
                                        && c.y >= 0 && c.y < t.height;
                                }

                                breakItem.ApplySpriteLight();
                                breakItem.spriteRenderer.enabled = breakItem.light.enabled = false;
                            }
                            glowItem.ApplySpriteLight();

                            GameObject colorSymbol = CreatePrefab("ColorSymbol_Block");
                            colorSymbol.transform.SetParent(primaryItem.blockObject.transform);
                            Coordinates[] cb = d.blockData.connectedBlocks;
                            GameController.ApplyCoordinates(cb[Mathf.Min(cb.Length - 1, cb.Length / 2 + cb.Length % 2)], colorSymbol);
                            AddColorSymbols(levelBlock, colorSymbol);

                            string symbolType = null;
                            switch ((ColorIndex)GameController.ConvertColorNameToIndex(d.blockData.blockName)) {
                                case ColorIndex.Red  : symbolType = "triangle"; break;
                                case ColorIndex.Green: symbolType = "square";   break;
                                case ColorIndex.Blue : symbolType = "circle";   break;
                                case ColorIndex.Force: symbolType = "cross";    break;
                            }
                            levelBlock.GetColorSymbolItems()[0].spriteRenderer.sprite = Assets.GetSprite("UI/Game/colorsymbol_" + symbolType);

                            CreateTimeOutlines(d, timeOutline.sprite, levelBlock, levelData, CreatePrefab("UndoOutline"));
                            primaryItem.blockObject.transform.SetParent(levelData.crystalHolder.transform);
                        }
                        break;

                    case SpriteType.Single:
                        Sprite timeOutlineSprite = Assets.GetSprite("BlockMisc/block_outline_undo_misc");
                        foreach (Data d in dataGroups[i]) {
                            LevelBlock levelBlock = null;
                            LevelBlock.BlockItem primaryItem = null;
                            bool createTimeOutlines = false;

                            switch (d.blockData.blockName) {
                                case "Gate":
                                    levelBlock  = CreateLevelBlock(d.blockData.blockName);
                                    primaryItem = levelBlock.GetBlockItem("Primary");
                                    primaryItem.blockObject.transform.SetParent(levelData.otherHolder.transform);
                                    d.blockData.state = (int)Activation.Off;

                                    Light2D gateLight = primaryItem.blockObject.GetComponentInChildren<Light2D>();
                                    levelBlock.AddBlockItem(new LevelBlock.BlockItem(gateLight.transform.parent.gameObject, gateLight.GetComponent<SpriteRenderer>(), gateLight));

                                    GameObject undoOutline = CreatePrefab("UndoOutline");
                                    SpriteRenderer sr = undoOutline.GetComponent<SpriteRenderer>();
                                    sr.sortingLayerName = "Tunnel"; sr.sortingOrder = 1;
                                    CreateTimeOutlines(d, Assets.GetSprite("Blocks/block_outline_undo_gate"), levelBlock, levelData, undoOutline);
                                    levelBlock.GetBlockItem("ResetOutline").blockObject.SetActive(false);

                                    CreateInfo(levelBlock);
                                    break;

                                case "GateSlot":
                                    levelBlock  = CreateLevelBlock(d.blockData.blockName);
                                    primaryItem = levelBlock.GetBlockItem("Primary");
                                    primaryItem.blockObject.transform.SetParent(levelData.miscHolder.transform);
                                    createTimeOutlines = true;

                                    GameObject fragment = CreatePrefab("CollectFragment");
                                    LevelBlock.BlockItem fragmentItem = levelBlock.AddBlockItem(new LevelBlock.BlockItem(fragment, fragment.GetComponent<SpriteRenderer>(), fragment.GetComponent<Light2D>()), true);
                                    bool found = false;
                                    foreach (Coordinates c in presetSlots) {
                                        if (d.blockData.coordinates == c) {
                                            found = true;
                                            break;
                                        }
                                    }
                                    fragmentItem.blockObject.SetActive(found);
                                    d.blockData.state = found ? (int)Activation.On : (int)Activation.Off;

                                    CreateInfo(levelBlock);
                                    break;

                                case "Piston":
                                    levelBlock  = CreateLevelBlock(d.blockData.blockName, false, true);
                                    primaryItem = levelBlock.GetBlockItem("Primary");
                                    primaryItem.blockObject.transform.SetParent(levelData.otherHolder.transform);

                                    GameObject pistonHead = CreatePrefab("PistonHead");
                                    LevelBlock.BlockItem pistonHeadItem = levelBlock.AddBlockItem(new LevelBlock.BlockItem(pistonHead, pistonHead.GetComponent<SpriteRenderer>()), true);
                                    break;

                                case "Basic":
                                    levelBlock  = CreateLevelBlock(d.blockData.blockName, false, true);
                                    primaryItem = levelBlock.GetBlockItem("Primary");
                                    primaryItem.blockObject.transform.SetParent(levelData.otherHolder.transform);
                                    basicDatas.Add(d);
                                    break;

                                // Collectables and Buttons
                                default:
                                    levelBlock  = CreateLevelBlock(d.blockData.blockName, false, true, true);
                                    primaryItem = levelBlock.GetBlockItem("Primary");
                                    createTimeOutlines = true;

                                    if (d.layer == Layer.Collect) {
                                        primaryItem.blockObject.transform.SetParent(levelData.collectHolder.transform);
                                        if (GameController.ConvertColorNameToIndex(d.blockData.blockName) <= (int)ColorIndex.Force)
                                            AddColorSymbols(levelBlock, primaryItem.blockObject);

                                        switch (d.blockData.blockName) {
                                            case "CollectSong":
                                                CreateTimeOutlines(d, Assets.GetSprite("Collectables/block_outline_undo_musicnote"), levelBlock, levelData, CreatePrefab("UndoOutline"));
                                                createTimeOutlines = false;
                                                break;

                                            case "CollectLength"  :
                                            case "CollectFragment":
                                                CreateTimeOutlines(d, Assets.GetSprite("Collectables/block_outline_undo_collect_small"), levelBlock, levelData, CreatePrefab("UndoOutline"));
                                                createTimeOutlines = false;
                                                break;
                                        }
                                    }
                                    else {
                                        d.blockData.state = (int)Activation.Off;
                                        primaryItem.blockObject.transform.SetParent(levelData.miscHolder.transform);
                                        CreateInfo(levelBlock);
                                        AddColorSymbols(levelBlock, primaryItem.blockObject);

                                        GameObject buttonBullet = CreatePrefab("ButtonBullet");
                                        buttonBullet.transform.SetParent(primaryItem.blockObject.transform);
                                        buttonBullet.transform.localPosition = Vector2.zero;
                                        levelBlock.AddBlockItem(new LevelBlock.BlockItem(buttonBullet, buttonBullet.GetComponent<SpriteRenderer>(), buttonBullet.GetComponent<Light2D>()));
                                    }
                                    break;

                            }
                            GameController.ApplyCoordinates(d.blockData.coordinates, primaryItem.blockObject);
                            if (createTimeOutlines)
                                CreateTimeOutlines(d, timeOutlineSprite, levelBlock, levelData, CreatePrefab("UndoOutline"));
                        }
                        break;
                }
            }
        }
        DestroyImmediate(outlineTemp);

        // If tunnels are connected to each other create one sprite using both
        while (tunnels.Count > 0) {
            TunnelConnect tcA = tunnels[0];
            foreach (TunnelConnect tcB in tunnels) {
                if (tcA.connection == tcB.dataList[tcB.dataList.Count - 1].blockData.coordinates && tcA.facingDirection == -tcB.facingDirection) {
                    tunnels.Remove(tcB);
                    tcB.spriteObject.SetActive(false);
                    foreach (Data d in tcB.dataList)
                        tcA.dataList.Add(d);
                    break;
                }
            }

            tunnels.Remove(tcA);
            spriteGeneration.GenerateSprite(tcA.dataList, gridSystem, tcA.spriteObject);
        }

        // Create a sprite for each connected dig group
        if (digDatas.Count > 0) {
            SpriteMask sm = groundItem.blockObject.AddComponent<SpriteMask>();
            sm.sprite = groundItem.spriteRenderer.sprite;
            sm.isCustomRangeActive = true;
            sm.frontSortingLayerID = sm.backSortingLayerID = SortingLayer.NameToID("Player");
            sm.frontSortingOrder = 3; sm.backSortingOrder = -100;

            List<List<Data>> digGroups = new List<List<Data>>();
            bool[] visited = new bool[digDatas.Count];
            int index = 0;
            while (index < visited.Length) {
                if (visited[index]) {
                    index++;
                    continue;
                }

                List<Data> group = new List<Data>();
                Check(digDatas[index].blockData.coordinates);
                digGroups.Add(group);

                void Check(Coordinates c) {
                    if (!Add(c))
                        return;

                    foreach (Coordinates f in Coordinates.FacingDirection) {
                        Coordinates n = c + f;
                        if (gridSystem.GetData(n, Layer.Dig) != null)
                            Check(n);
                    }
                }
                bool Add(Coordinates c) {
                    for (int i = index; i < digDatas.Count; i++) {
                        if (c == digDatas[i].blockData.coordinates) {
                            if (visited[i])
                                return false;

                            group.Add(digDatas[i]);
                            visited[i] = true;
                            return true;
                        }
                    }
                    return false;
                }
            }

            foreach (List<Data> group in digGroups) {
                LevelBlock levelBlock = CreateLevelBlock("Dig", true, true);
                LevelBlock.BlockItem primaryItem = levelBlock.GetBlockItem("Primary");
                primaryItem.blockObject.transform.SetParent(levelData.groundHolder.transform);

                GameObject outline = Instantiate(primaryItem.spriteRenderer.gameObject);
                outline.name = "DigLevelOutline"; outline.tag = "EditorOnly";
                LevelBlock.BlockItem outlineItem = levelBlock.AddBlockItem(new LevelBlock.BlockItem(outline, outline.GetComponent<SpriteRenderer>()));
                outlineItem.spriteRenderer.sortingOrder = 1;

                BuildSprite(group, sprites["Dig"], primaryItem.spriteRenderer.gameObject);
                BuildSprite(group, sprites["Dig"], outline);
                primaryItem.blockObject.transform.position = primaryItem.spriteRenderer.transform.position;
                primaryItem.spriteRenderer.transform.localPosition = outlineItem.spriteRenderer.transform.localPosition = Vector2.zero;

                Texture2D texture = primaryItem.spriteRenderer.sprite.texture;
                for (int y = 0; y < texture.height; y++) {
                    for (int x = 0; x < texture.width; x++)
                        texture.SetPixel(x, y, Color.clear);
                }
                texture.Apply();

                SetTextureOrigins(group, GameController.BLOCK_SIZE);

                GameObject positions = new GameObject("Positions");
                positions.transform.SetParent(primaryItem.blockObject.transform);
                positions.transform.localPosition = Vector2.zero;

                // Create data surrounding dig blocks as walls inside of the ground
                List<Data> wallDatas = new List<Data>();
                foreach (Data d in group) {
                    d.blockData.origin += 1;
                    d.blockData.state   = 0;

                    Data[][] nearData = GameController.GetNearData(d.blockData.coordinates, gridSystem);
                    foreach (Data[] datas in nearData) {
                        Data groundData = datas[(int)Layer.Block];
                        if (groundData == null || groundData.blockData.blockName != "Ground" || gridSystem.GetData(groundData.blockData.coordinates, Layer.Dig) != null)
                            continue;

                        Data wallData = new Data(new BlockData("Dig", groundData.blockData.coordinates, -1));
                        wallData.blockData.origin = d.blockData.origin + (wallData.blockData.coordinates - d.blockData.coordinates) * GameController.BLOCK_SIZE;
                        wallData.blockData.state = 0;
                        gridSystem.AddData(wallData);
                        wallDatas.Add(wallData);
                    }

                    if (d.blockData.IsPrimary())
                        AddPos(d.blockData.coordinates);
                }

                Coordinates[] connectedBlocks = new Coordinates[wallDatas.Count];
                for (int i = 0; i < wallDatas.Count; i++) {
                    connectedBlocks[i] = wallDatas[i].blockData.coordinates;
                    wallDatas[i].blockData.connectedBlocks = connectedBlocks;
                }
                AddPos(connectedBlocks[0]);

                void AddPos (Coordinates c) {
                    GameObject pos = new GameObject(c + "");
                    GameController.ApplyCoordinates(c, pos);
                    pos.transform.SetParent(positions.transform);
                    levelBlock.AddBlockItem(new LevelBlock.BlockItem(pos));
                }
            }
        }

        // Create a template from all basic blocks in the room for drawing over to create custom room assets
        if (basicDatas.Count > 0) {
            foreach (Data d in basicDatas)
                d.blockData.spriteState = 0;

            GameObject spriteTemp = new GameObject();
            SpriteRenderer sr = spriteTemp.AddComponent<SpriteRenderer>();
            BuildSprite(basicDatas, new List<Sprite> { Assets.GetSprite("Blocks/block_basic") }, spriteTemp);
            Assets.SaveSprite(sr.sprite.texture, "Assets/Sprites/BasicTemplates/" + levelName);
            DestroyImmediate(spriteTemp);
        }

        foreach (PanelCopy pc in panelCopies)
            pc.Copy(gridSystem);
        
        GameController.SaveGrid(levelName, 0, gridSystem, true);
        Debug.LogError("Saved " + levelName);
        Debug.LogError("Done Generating");
        GameController.generatingBlocks = false;
    }
    
    public static void BuildSprite(List<Data> dataList, List<Sprite> sprites, params GameObject[] blocks) {
        // Get bounds of all blocks
        Coordinates c0 = dataList[0].blockData.coordinates;
        int[] bounds = new int[] { c0.x, c0.x, c0.y, c0.y }; // [Largest X, Smallest X, Largest Y, Smallest Y]
        for (int i = 1; i < dataList.Count; i++) {
            Coordinates c = dataList[i].blockData.coordinates;
            if      (c.x > bounds[0]) bounds[0] = c.x;
            else if (c.x < bounds[1]) bounds[1] = c.x;
            if      (c.y > bounds[2]) bounds[2] = c.y;
            else if (c.y < bounds[3]) bounds[3] = c.y;
        }

        int spriteSize = sprites[0].texture.width;
        Texture2D texture = new Texture2D((Mathf.Abs(bounds[0] - bounds[1]) + 1) * GameController.BLOCK_SIZE + spriteSize - GameController.BLOCK_SIZE,
                                          (Mathf.Abs(bounds[2] - bounds[3]) + 1) * GameController.BLOCK_SIZE + spriteSize - GameController.BLOCK_SIZE);
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < texture.height; y++) {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, Color.clear);
        }

        // Build new sprite from block pieces
        int b = spriteSize - 1;
        foreach (Data d in dataList) {
            Coordinates c = d.blockData.coordinates;
            Coordinates gridCoord = (c - new Coordinates(bounds[1], bounds[3])) * GameController.BLOCK_SIZE;
            int spriteIndex = d.blockData.spriteState < sprites.Count ? d.blockData.spriteState : d.blockData.spriteState % Assets.SPRITE_TYPES.Length;
            Texture2D spriteTexture = sprites[spriteIndex].texture;

            for (int y = 0; y < spriteTexture.height; y++) {
                for (int x = 0; x < spriteTexture.width; x++) {
                    Coordinates rotatedCoord = new Coordinates(x, y);
                    switch (d.blockData.facing) {
                        case 1: rotatedCoord = new Coordinates(-y + b,  x    ); break;
                        case 2: rotatedCoord = new Coordinates(-x + b, -y + b); break;
                        case 3: rotatedCoord = new Coordinates( y    , -x + b); break;
                    }

                    Coordinates textureCoord = gridCoord + rotatedCoord;
                    Color color = spriteTexture.GetPixel(x, y);
                    if (color.a > 0)
                        texture.SetPixel(textureCoord.x, textureCoord.y, color);
                }
            }
        }
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1);
        Vector3 center = new Vector3(Mathf.Lerp(bounds[1], bounds[0], 0.5f), Mathf.Lerp(bounds[3], bounds[2], 0.5f)) * GameController.GRID_SIZE;

        if (blocks != null) {
            foreach (GameObject go in blocks) {
                go.GetComponent<SpriteRenderer>().sprite = sprite;
                go.transform.position = center;
            }
        }
    }

    // Use origin for texture coordinates by getting offset from bottom left corner of all blockdata
    void SetTextureOrigins(List<Data> datas, int size) {
        Coordinates corner = datas[0].blockData.coordinates;
        foreach (Data d in datas)
            corner = new Coordinates(Mathf.Min(corner.x, d.blockData.coordinates.x), Mathf.Min(corner.y, d.blockData.coordinates.y));
        foreach (Data d in datas)
            d.blockData.origin = (d.blockData.coordinates - corner) * size;
    }

    GameObject CreatePrefab(string assetName) {
        return PrefabUtility.InstantiatePrefab(prefabs[assetName]) as GameObject;
    }
    LevelBlock CreateLevelBlock(string blockName, bool addHolder = false, bool addSpriteRenderer = false, bool addLight = false) {
        GameObject block = CreatePrefab(blockName);
        block.name = blockName;

        GameObject primaryBlock = block;
        if (addHolder) {
            primaryBlock = new GameObject(blockName);
            block.transform.SetParent(primaryBlock.transform);
        }
        primaryBlock.tag = "LevelBlock";
        
        LevelBlock levelBlock = primaryBlock.AddComponent<LevelBlock>();
        levelBlock.blockName = blockName;
        levelBlock.layer = Assets.ASSET_INFO[blockName].layer;
        LevelBlock.BlockItem primaryItem = new LevelBlock.BlockItem(primaryBlock, addSpriteRenderer ? block.GetComponent<SpriteRenderer>() : null, 
                                                                                  addLight          ? block.GetComponent<Light2D>()        : null);
        levelBlock.AddBlockItem(primaryItem);

        return levelBlock;
    }
    public static void CreateTimeOutlines(Data data, Sprite outlineSprite, LevelBlock levelBlock, LevelData levelData, GameObject undoOutline = null) {
        LevelBlock.BlockItem primaryItem = levelBlock.GetBlockItem("Primary");
        GameObject undoHolder = new GameObject("UndoHolder");
        undoHolder.transform.SetParent(primaryItem.blockObject.transform);
        undoHolder.transform.localPosition = Vector3.zero;
        undoHolder.SetActive(false);
        
        if (undoOutline == null)
            undoOutline = PrefabUtility.InstantiatePrefab(Assets.GetPrefab("UndoOutline")) as GameObject;
        undoOutline.name = data.blockData.blockName + "UndoOutline";
        LevelBlock.BlockItem undoItem = new LevelBlock.BlockItem(undoHolder, undoOutline.GetComponent<SpriteRenderer>());
        undoItem.spriteRenderer.sprite = outlineSprite;
        undoOutline.transform.SetParent(undoHolder.transform);
        undoOutline.transform.localPosition = Vector3.zero;
        if (primaryItem.spriteRenderer != null)
            undoOutline.transform.position = primaryItem.spriteRenderer.gameObject.transform.position;

        GameObject resetOutline = Instantiate(undoOutline);
        resetOutline.name = data.blockData.blockName + "ResetOutline";
        LevelBlock.BlockItem resetItem = new LevelBlock.BlockItem(resetOutline, resetOutline.GetComponent<SpriteRenderer>());
        resetOutline.transform.SetParent(levelData.resetHolder.transform);
        resetOutline.transform.position = undoOutline.transform.position;
        resetOutline.SetActive(false);

        levelBlock.AddTimeItems(undoItem, resetItem);
    }
    public static void CreateInfo(LevelBlock lb, Generation generation = null) {
        GameObject copyObject = null;
        string onSprite = null;
        bool invertedPanel = false;
        bool altSort = false;

        switch (lb.blockName) {
            case "Gate":
                copyObject = lb.GetBlockItem("GateDoor").blockObject;
                onSprite   = "Blocks/gate_on";
                break;

            case "Piston":
            case "Tunnel":
                LevelBlock.BlockItem panelItem = lb.GetBlockItem("Panel");
                if (panelItem == null)
                    return;

                copyObject = panelItem.blockObject;
                onSprite   = "BlockMisc/panel_on";
                Panel p = (Panel)panelItem.script;
                invertedPanel = p.inverted;
                break;

            default:
                copyObject = lb.GetBlockItem("Primary").blockObject;
                if (lb.blockName != "GateSlot") onSprite = "BlockMisc/button_on";
                else                            altSort  = true;
                break;
        }

        LevelBlock.BlockItem primaryItem = lb.GetBlockItem("Primary");
        GameObject infoObject = Instantiate(copyObject, lb.blockName == "Gate" ? lb.GetBlockItem("GateDoor").blockObject.transform
                                                                               : primaryItem                .blockObject.transform);
        DestroyImmediate(infoObject.GetComponent<LevelBlock>());
        infoObject.name = lb.blockName + "Info";
        if (lb.layer != Layer.Tunnel)
            infoObject.transform.localPosition = Vector3.zero;

        Light2D[] lights = infoObject.GetComponentsInChildren<Light2D>(true);
        SpriteRenderer colorSR = null;
        Sprite s = Assets.GetSprite(onSprite);
        foreach (Light2D l in lights) {
            bool gatePanel = l.name == "GatePanelLight";
            SpriteRenderer sr = l.GetComponent<SpriteRenderer>();
            if (onSprite != null)
                sr.sprite = gatePanel ? Assets.GetSprite("BlockMisc/gate_panel_on") : s;
            DestroyImmediate(l);

            if (lights.Length == 1 || gatePanel)
                colorSR = sr;
        }

        string infoName = "Panel";
        if (lb.layer == Layer.Tunnel) {
            LevelBlock.BlockItem panelItem = lb.GetBlockItem("Panel");
            if (panelItem != null && panelItem.script != null) {
                TunnelPanel tp = (TunnelPanel)panelItem.script;
                if (tp.gatePanelIndex != -1)
                    infoName = "GatePanel";
            }
        }
        else {
            if (lb.name == "Gate")
                infoName = "Gate";
        }
        infoName += "InfoOutline";

        GameObject infoOutline = CreatePrefab(infoName);
        infoOutline.transform.SetParent(infoObject.transform);
        infoOutline.transform.localPosition = Vector3.zero;
        LevelBlock.BlockItem infoItem = new LevelBlock.BlockItem(infoOutline, infoOutline.GetComponent<SpriteRenderer>());
        if (invertedPanel) infoItem.spriteRenderer.sprite       = Assets.GetSprite("BlockMisc/info_outline_square");
        if (altSort      ) infoItem.spriteRenderer.sortingOrder = -11; // -11 signifies alternate fading effect when showing room info
        lb.AddInfoItem(new LevelBlock.BlockItem(infoObject, colorSR), infoItem);

        SpriteRenderer[] srs = infoObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in srs) {
            sr.sortingLayerName = "Screen";
            sr.material = lb.GetBlockItem("InfoOutline").spriteRenderer.sharedMaterial;
        }

        infoObject.SetActive(false);

        if (lb.blockName == "Piston") {
            LevelBlock.BlockItem panelItem = lb.GetBlockItem("Panel");
            if (panelItem != null) {
                PistonPanel pp = (PistonPanel)panelItem.script;
                LevelBlock.BlockItem pistonInfoItem = lb.AddBlockItem(new LevelBlock.BlockItem(new GameObject("PistonInfo")), true);
                pistonInfoItem.blockObject.SetActive(false);

                for (int i = 0; i < pp.length; i++) {
                    GameObject info = CreatePrefab(i < pp.length - 1 ? "PistonInfo" : "PistonHeadInfo");
                    info.transform.SetParent(pistonInfoItem.blockObject.transform);
                    info.transform.localEulerAngles = Vector3.zero;
                    info.transform.localPosition    = Vector3.right * GameController.GRID_SIZE * (i + 1);
                }
            }
        }

        GameObject CreatePrefab(string assetName) {
            if (generation != null)
                return generation.CreatePrefab(assetName);

            return PrefabUtility.InstantiatePrefab(Assets.GetPrefab(assetName)) as GameObject;
        }
    }
    public static void AddColorSymbols(LevelBlock levelBlock, params GameObject[] parents) {
        List<LevelBlock.BlockItem> colorSymbolItems = new List<LevelBlock.BlockItem>();
        foreach (GameObject go in parents) {
            SpriteRenderer[] srs = go.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in srs) {
                if (sr.name.Contains("ColorSymbol"))
                    colorSymbolItems.Add(new LevelBlock.BlockItem(sr.gameObject, sr));
            }
        }
        levelBlock.AddColorSymbolItems(colorSymbolItems.ToArray());
    }

    [ContextMenu("Generate Ground Shadow")]
    void GenerateGroundShadow() {
        if (EditorApplication.isPlaying)
            return;

        float shadowStep = 0.075f;
        LevelBlock[] levelBlocks = FindObjectsOfType<LevelBlock>();
        Texture2D ground = null;
        Coordinates groundPos = default;
        Coordinates groundCenter = default;
        PixelGroup[,] pixelGrid = null;
        foreach (LevelBlock lb in levelBlocks) {
            if (lb.blockName == "Ground") {
                ground = lb.GetBlockItem("Primary").spriteRenderer.sprite.texture;
                pixelGrid = new PixelGroup[ground.width, ground.height];
                for (int y = 0; y < ground.height; y++) {
                    for (int x = 0; x < ground.width; x++) {
                        pixelGrid[x, y] = new PixelGroup();
                        pixelGrid[x, y].pixels.Add(new PixelGroup.Pixel(new Coordinates(x, y), ground));
                    }
                }

                groundPos    = new Coordinates(Mathf.RoundToInt(lb.transform.position.x), Mathf.RoundToInt(lb.transform.position.y));
                groundCenter = new Coordinates(ground.width / 2 + ground.width % 2, ground.height / 2 + ground.height % 2);
                break;
            }
        }

        List<Texture2D> otherTextures = new List<Texture2D>();
        foreach (LevelBlock lb in levelBlocks) {
            if (lb.blockName == "Tunnel" || lb.blockName == "GroundFG") {
                SpriteRenderer sr = lb.GetBlockItem("Primary").spriteRenderer;
                Texture2D t = sr.sprite.texture;
                otherTextures.Add(t);

                Coordinates pos    = new Coordinates(Mathf.RoundToInt(sr.transform.position.x), Mathf.RoundToInt(sr.transform.position.y));
                Coordinates center = new Coordinates(t.width / 2 + t.width % 2, t.height / 2 + t.height % 2);
                for (int y = 0; y < t.height; y++) {
                    for (int x = 0; x < t.width; x++) {
                        if (t.GetPixel(x, y).a == 0)
                            continue;

                        Coordinates c = new Coordinates(x, y);
                        Coordinates offset = c - center + (groundCenter + (pos - groundPos));
                        if (WithinBounds(offset))
                            pixelGrid[offset.x, offset.y].pixels.Add(new PixelGroup.Pixel(c, t));
                    }
                }
            }
        }
        
        List<Coordinates> coordinates = new List<Coordinates>();
        int s = GameController.BLOCK_SIZE;
        for (int y = s; y < ground.height - s; y++) {
            for (int x = s; x < ground.width - s; x++) {
                Color32 color = ground.GetPixel(x, y);
                if (pixelGrid[x, y].visited || color.a != 2)
                    continue;

                ground.SetPixel(x, y, Color.clear);
                for (int py = -s; py <= s; py++) {
                    for (int px = -s; px <= s; px++) {
                        Coordinates p = new Coordinates(x + px, y + py);
                        if ((Mathf.Abs(px) == s && Mathf.Abs(py) == s) || !WithinBounds(p) || pixelGrid[p.x, p.y].visited)
                            continue;

                        coordinates.Add(new Coordinates(p.x, p.y));
                        pixelGrid[p.x, p.y].visited = true;
                    }
                }
            }
        }

        int steps = 0;
        while (coordinates.Count > 0) {
            List<Coordinates> check = new List<Coordinates>();
            foreach (Coordinates c in coordinates) {
                foreach (PixelGroup.Pixel p in pixelGrid[c.x, c.y].pixels) {
                    int step = p.texture == ground ? steps : steps - 5;
                    if (step < 0)
                        continue;

                    if (step > 0 && UnityEngine.Random.Range(0, 3) == 0)
                        step -= 1;

                    p.texture.SetPixel(p.pixel.x, p.pixel.y, Color.Lerp(p.texture.GetPixel(p.pixel.x, p.pixel.y), Color.black, Mathf.Pow(step * shadowStep, 1.5f)));
                }

                foreach (Coordinates d in Coordinates.CompassDirection) {
                    Coordinates next = c + d;
                    if (!WithinBounds(next) || pixelGrid[next.x, next.y].visited || ground.GetPixel(next.x, next.y).a == 0)
                        continue;

                    check.Add(next);
                    pixelGrid[next.x, next.y].visited = true;
                }
            }
            coordinates = check;
            steps++;
        }
        ground.Apply();
        foreach (Texture2D t in otherTextures)
            t.Apply();

        bool WithinBounds(Coordinates c) {
            return c.x >= 0 && c.x < ground.width
                && c.y >= 0 && c.y < ground.height;
        }

        Debug.LogError("Generated Shadows");
    }
    
    [ContextMenu("Generate Support Crystals")]
    void GenerateSupportCrystals() {
        if (EditorApplication.isPlaying)
            return;
        
        GameObject level = GameObject.FindWithTag("Level");
        MapController.MapData mapData = GameController.LoadMap("WorldMap", 0);
        MapController.RoomData roomData = null;
        if (mapData.roomDatas != null) {
            foreach (MapController.RoomData rd in mapData.roomDatas) {
                if (rd.roomName == level.name) {
                    roomData = rd;
                    break;
                }
            }
        }
        if (roomData == null) {
            Debug.LogError(level.name + " Not Included In WorldMap");
            return;
        }

        LevelBlock[] levelBlocks = level.GetComponentsInChildren<LevelBlock>();
        GameObject holder = null;
        foreach (LevelBlock lb in levelBlocks) {
            if (lb.layer != Layer.Support)
                continue;

            holder = lb.gameObject;
            break;
        }
        if (holder == null) {
            Debug.LogError("Missing Supports");
            return;
        }
        Transform[] children = holder.GetComponentsInChildren<Transform>();
        foreach (Transform t in children) {
            if (t.gameObject != holder)
                DestroyImmediate(t.gameObject);
        }

        GridSystem gridSystem = new GridSystem(GameController.LoadGrid(level.name, 0));
        GridSystem.GridData gridData = gridSystem.GetGridData();
        int maxDistance = 400;

        // Check if support data is overlapping blocks unelligible for crystal placement
        List<Data> supportDatas = new List<Data>();
        foreach (Coordinates c in gridSystem.GetCoordinates()) {
            Data crystalData = gridSystem.GetData(c, Layer.SupportCrystal);
            if (crystalData != null)
                gridSystem.RemoveData(crystalData);

            Data supportData = gridSystem.GetData(c, Layer.Support);
            if (supportData != null) {
                Data[] datas = gridSystem.GetData(c, Layer.Block, Layer.Misc, Layer.Tunnel, Layer.Bg1);
                foreach (Data d in datas) {
                    if (d == null)
                        continue;

                    if (d.layer == Layer.Block && d.blockData.blockName != "Ground")
                        continue;

                    datas = null;
                    break;
                }
                if (datas != null) 
                    supportDatas.Add(supportData);
            }
        }

        prefabs = Assets.GetPrefabs();

        // Get distance to each color collectable and sort so that glow effects flow towards them
        Vector2[] colorVectors = new Vector2[GameController.CRYSTAL_COLORS];
        Vector2[] roomVectors  = new Vector2[GameController.CRYSTAL_COLORS];
        List<SupportSorting>[] sortedSupports = new List<SupportSorting>[colorVectors.Length];
        for (int i = 0; i < colorVectors.Length; i++) {
            colorVectors[i] = GameController.GetVector(mapData.colorCoordinates[i]);
            roomVectors [i] = colorVectors[i] - GameController.GetVector(roomData.mapOffset + roomData.localOffset);

            sortedSupports[i] = new List<SupportSorting>();
            foreach (Data d in supportDatas) {
                Vector2 supportPos = GameController.GetVector(d.blockData.coordinates + roomData.mapOffset + roomData.localOffset);
                int distance = Mathf.RoundToInt(Vector2.Distance(colorVectors[i], supportPos));
                bool found = false;
                foreach (SupportSorting ss in sortedSupports[i]) {
                    if (ss.distance == distance) {
                        ss.supports.Add(d);
                        found = true;
                        break;
                    }
                }
                if (!found)
                    sortedSupports[i].Add(new SupportSorting(distance, d));
            }
            sortedSupports[i].Sort();
        }

        // Closest color collectable gets priority for more crystals if unequal grouping
        float closestColor = roomVectors[0].magnitude;
        int[] supportSections = new int[roomVectors.Length];
        for (int i = 0; i < roomVectors.Length; i++) {
            if (roomVectors[i].magnitude < closestColor)
                closestColor = roomVectors[i].magnitude;

            supportSections[i] = supportDatas.Count / roomVectors.Length;
        }
        int remainder = supportDatas.Count % roomVectors.Length;
        if (remainder > 0) {
            for (int i = 0; i < roomVectors.Length; i++) {
                if (closestColor == roomVectors[i].magnitude) {
                    supportSections[i] += remainder;
                    break;
                }
            }
        }
        
        Color32[] colors = new Color32[] { new Color32(255, 37, 45, 255), new Color32(63, 255, 49, 255), new Color32(49, 143, 255, 255) };
        List<int> availableSpots = new List<int>();
        for (int i = 0; i < supportDatas.Count; i++)
            availableSpots.Add(i);
        
        // Create crystals based on available spaces and distance to color collectable
        // The closer a room is to each color collectable the bigger and more numerous the crystals are
        int crystalSizes = supportCrystalSprites.Length;
        int crystalVariations = 1;
        for (int i = 0; i < supportSections.Length; i++) {
            float percentage = (maxDistance - roomVectors[i].magnitude) / maxDistance;
            int sectionSpots = (int)Mathf.Lerp(0, supportSections[i], percentage);
            int maxSize      = (int)(percentage * 100) / (100 / crystalSizes);

            for (int j = 0; j < sectionSpots; j++) {
                int randomSize = UnityEngine.Random.Range(0, maxSize);
                int blockIndex = randomSize + crystalSizes * UnityEngine.Random.Range(0, crystalVariations);
                LevelBlock.BlockItem primaryItem = CreateLevelBlock("SupportCrystal", false, true, true).GetBlockItem("Primary");
                GameObject crystal = primaryItem.blockObject;
                crystal.transform.SetParent(holder.transform);

                SpriteRenderer sr = primaryItem.spriteRenderer;
                sr.sprite = supportCrystalSprites[blockIndex];
                primaryItem.ApplySpriteLight();
                sr.color = colors[i];

                int randomIndex = UnityEngine.Random.Range(0, availableSpots.Count);
                int spot = availableSpots[randomIndex];

                // Check adjacent crystals of selected spot to prevent the same colors and sizes being next to each other
                for (int retry = 0; retry < 5; retry++) {
                    Data[][] nearData = GameController.GetNearData(supportDatas[spot].blockData.coordinates, gridSystem);
                    foreach (Data[] datas in nearData) {
                        foreach (Data d in datas) {
                            if (d != null && d.layer == Layer.SupportCrystal) {
                                SpriteRenderer dsr = d.blockObject.GetComponent<SpriteRenderer>();
                                if (dsr.color == sr.color || d.blockData.state % crystalSizes == blockIndex % crystalSizes) {
                                    randomIndex = UnityEngine.Random.Range(0, availableSpots.Count);
                                    spot = availableSpots[randomIndex];
                                    break;
                                }
                            }
                        }
                    }
                }
                availableSpots.RemoveAt(randomIndex);

                int facing = UnityEngine.Random.Range(0, 4);
                Data crystalData = new Data(new BlockData("SupportCrystal", supportDatas[spot].blockData.coordinates, facing), crystal);
                crystalData.blockData.connectedBlocks = new Coordinates[] { new Coordinates(0, i) };
                crystalData.blockData.state = blockIndex;
                crystalData.ApplyData(true);
                gridSystem.AddData(crystalData);

                int offset = randomSize == 0 ? 2 : 1;
                crystal.transform.position += new Vector3(UnityEngine.Random.Range(-offset, offset + 1), UnityEngine.Random.Range(-offset, offset + 1), 0);
            }
        }

        // Group crystals of each color that are the same distance together
        List<Coordinates>[][] scc = new List<Coordinates>[colorVectors.Length][];
        for (int i = 0; i < scc.Length; i++) {
            int amount = sortedSupports[i][0].distance - sortedSupports[i][sortedSupports[i].Count - 1].distance + 1;
            scc[i] = new List<Coordinates>[amount];
            foreach (SupportSorting ss in sortedSupports[i]) {
                foreach (Data d in ss.supports) {
                    Data crystalData = gridSystem.GetData(d.blockData.coordinates, Layer.SupportCrystal);
                    if (crystalData != null && crystalData.blockData.connectedBlocks != null && crystalData.blockData.connectedBlocks[0].y == i) {
                        int index = sortedSupports[i][0].distance - ss.distance;
                        if (scc[i][index] == null)
                            scc[i][index] = new List<Coordinates>();

                        crystalData.blockData.connectedBlocks = null;
                        scc[i][index].Add(crystalData.blockData.coordinates);
                    }
                }
            }
        }
        gridSystem.GetGridData().supportCrystalCoordinates = scc;
        GameController.SaveGrid(level.name, 0, gridSystem);

        Debug.LogError("Generated Support Crystals");
    }

    [ContextMenu("Generate Room Map")]
    void GenerateRoomMap() {
        if (EditorApplication.isPlaying)
            return;

        GameObject level = GameObject.FindGameObjectWithTag("Level");
        if (level == null) {
            Debug.LogError("Level Not Found");
            return;
        }
        
        GridSystem gridSystem = new GridSystem(GameController.LoadGrid(level.name, 0));
        if (gridSystem == null) {
            Debug.LogError("GridSystem Not Found");
            return;
        }

        LevelData levelData = level.GetComponent<LevelData>();
        GridSystem.GridData gridData = gridSystem.GetGridData();

        Texture2D editorMap = new Texture2D(gridData.size.x, gridData.size.y);
        Texture2D roomMap   = new Texture2D(gridData.size.x, gridData.size.y);
        editorMap.filterMode = roomMap.filterMode = FilterMode.Point;
        for (int x = 0; x < roomMap.width; x++) {
            for (int y = 0; y < roomMap.height; y++) {
                editorMap.SetPixel(x, y, Color.clear);
                roomMap  .SetPixel(x, y, Color.clear);
            }
        }
        
        // Draw screen borders
        foreach (Screen.ScreenData sd in gridData.screenDatas) {
            for (int y = sd.bounds[1]; y <= sd.bounds[0]; y++) {
                for (int x = sd.bounds[2]; x <= sd.bounds[3]; x++) {
                    if (x > sd.bounds[2] && x < sd.bounds[3] && y > sd.bounds[1] && y < sd.bounds[0])
                        continue;

                    Coordinates o = Offset(new Coordinates(x, y));
                    editorMap.SetPixel(o.x, o.y, Assets.MAP_COLORS["Screen"]);
                }
            }
        }

        foreach (Coordinates c in levelData.airDusts)
            SetPixels(c, Assets.MAP_COLORS["Empty"]);

        List<Data>[] datas = new List<Data>[(int)Layer.None];
        foreach (Coordinates c in levelData.airDusts) {
            Data checkData = gridSystem.GetData(c, Layer.Block);
            if (checkData == null || checkData.blockData.blockName != "Pipe")
                AddNearBlocks(c);
        }
        foreach (Coordinates c in gridSystem.GetCoordinates()) {
            Data checkNearData = gridSystem.GetData(c, Layer.Block);
            if (checkNearData != null) {
                switch (checkNearData.blockData.blockName) {
                    case "Basic":
                        AddNearBlocks(c);
                        break;

                    case "Pipe":
                        SetPixels(c, Assets.MAP_COLORS["Pipe"]);

                        Data buttonData = gridSystem.GetData(c, Layer.Misc);
                        if (buttonData != null)
                            datas[(int)Layer.Misc].Add(buttonData);
                        break;
                }
            }

            Data digData = gridSystem.GetData(c, Layer.Dig);
            if (digData != null)
                datas[(int)Layer.Dig].Add(digData);
        }
        void AddNearBlocks(Coordinates c) {
            foreach (Coordinates f in Coordinates.FacingDirection) {
                Coordinates n = c + f;

                foreach (Data d in gridSystem.GetDatas(n)) {
                    if (d != null) {
                        int index = (int)d.layer;
                        if (datas[index] == null)
                            datas[index] = new List<Data>();
                        if (!datas[index].Contains(d))
                            datas[index].Add(d);
                    }
                }
            }
        }

        Layer[] layerOrder = new Layer[] { Layer.Block, Layer.Dig, Layer.Tunnel, Layer.Collect, Layer.Misc, Layer.Piston };
        List<MapController.RoomData.Tunnel> tunnels = new List<MapController.RoomData.Tunnel>();
        List<Data> gateDatas = new List<Data>();
        foreach (Layer l in layerOrder) {
            if (datas[(int)l] == null)
                continue;

            foreach (Data d in datas[(int)l]) {
                Coordinates c = d.blockData.coordinates;
                int colorIndex = -1;

                switch (l) {
                    case Layer.Block:
                        if (d.HasTag(Tag.Push)) {
                            colorIndex = GameController.ConvertColorNameToIndex(d.blockData.blockName);
                            SetPixel(editorMap, c, Assets.MAP_COLORS[colorIndex == -1 ? "Rock" : colorIndex + ""]);
                        }
                        else {
                            switch (d.blockData.blockName) {
                                case "Basic" :
                                case "Ground":
                                    SetPixels(c, Assets.MAP_COLORS["Ground"]);
                                    break;

                                case "Gate":
                                    gateDatas.Add(d);
                                    break;
                            }
                        }
                        break;

                    case Layer.Dig:
                        SetPixel(editorMap, c, Assets.MAP_COLORS[d.blockData.facing == -1 ? "Ground" : "Dig"]);
                        break;

                    case Layer.Tunnel:
                        if (!d.blockData.IsPrimary())
                            continue;

                        MapController.RoomData.Tunnel tunnel = new MapController.RoomData.Tunnel();
                        tunnel.roomName = level.name;
                        tunnel.connectedBlocks = d.blockData.connectedBlocks;
                        foreach (Coordinates f in Coordinates.FacingDirection) {
                            Coordinates n = tunnel.connectedBlocks[tunnel.connectedBlocks.Length - 1] + f;
                            if (!gridSystem.WithinBounds(n))
                                continue;

                            Data tunnelData = gridSystem.GetData(n, Layer.Tunnel);
                            if (tunnelData == null)
                                continue;

                            if (tunnelData.blockData.coordinates == tunnelData.blockData.connectedBlocks[tunnelData.blockData.connectedBlocks.Length - 1]) {
                                tunnel.local = true;
                                break;
                            }
                        }

                        tunnels.Add(tunnel);

                        if (d.blockData.state != -1) {
                            Coordinates center = d.blockData.connectedBlocks[1];
                            for (int x = -2; x <= 2; x++) {
                                for (int y = -2; y <= 2; y++) {
                                    Coordinates n = new Coordinates(x, y);
                                    string mapColor = "Gate";
                                    if (Mathf.Abs(x) != 2 && Mathf.Abs(y) != 2)
                                        mapColor = GameController.GetVector(n) == Panel.GATE_PANEL_ICON_POSITIONS[d.blockData.state] ? "GatePanelIcon" : "GatePanel";
                                    SetPixel(editorMap, center + n, Assets.MAP_COLORS[mapColor]);
                                }
                            }

                            tunnel.gatePanelIndex = d.blockData.state;
                            break;
                        }

                        foreach (Coordinates cb in tunnel.connectedBlocks)
                            SetPixel(editorMap, cb, Assets.MAP_COLORS["Tunnel"]);
                        if (gridSystem.GetData(tunnel.connectedBlocks[1], Layer.Misc) != null)
                            SetPixel(editorMap, c, Assets.MAP_COLORS["TunnelAlt"]);

                        if (!tunnel.local)
                            SetPixel(editorMap, tunnel.connectedBlocks[tunnel.connectedBlocks.Length - 1], Assets.MAP_COLORS["TunnelEnd"]);
                        break;

                    case Layer.Collect:
                        SetPixel(editorMap, c, Assets.MAP_COLORS[d.blockData.blockName == "CollectSong" ? "Song" : GameController.ConvertColorNameToIndex(d.blockData.blockName) + ""]);
                        break;

                    case Layer.Misc:
                        if (d.blockData.blockName == "GateSlot") {
                            SetPixel(editorMap, c, Assets.MAP_COLORS[d.blockData.state == (int)Activation.On ? ((int)ColorIndex.Fragment + "") : "Gate"]);
                            break;
                        }

                        Data blockData = gridSystem.GetData(d.blockData.coordinates, Layer.Block);
                        if (blockData != null && blockData.HasTag(Tag.Push))
                            break;

                        Data pistonData = gridSystem.GetData(d.blockData.coordinates, Layer.Piston);
                        if (pistonData != null && pistonData.blockData.state == (int)Activation.On)
                            break;

                        colorIndex = GameController.ConvertColorNameToIndex(d.blockData.blockName);
                        if (colorIndex != -1) {
                            Color32 color = Assets.MAP_COLORS[colorIndex + ""]; color.a = Assets.MAP_BUTTON_ALPHA;
                            SetPixel(editorMap, c, color);
                        }
                        break;

                    case Layer.Piston:
                        if (d.blockData.IsPrimary()) {
                            foreach (Coordinates cb in d.blockData.connectedBlocks) {
                                if (gridSystem.GetData(cb, Layer.Piston).blockData.state == (int)Activation.On)
                                    SetPixel(editorMap, cb, Assets.MAP_COLORS["Piston"]);
                            }
                        }
                        break;
                }
            }
        }

        foreach (Data d in gateDatas) {
            SetPixel(editorMap, d.blockData.coordinates, Assets.MAP_COLORS[(int)ColorIndex.Fragment + ""]);
            foreach (Coordinates a in Coordinates.AllDirection)
                SetPixel(editorMap, d.blockData.coordinates + a, Assets.MAP_COLORS["Gate"]);
        }

        editorMap.Apply();
        roomMap  .Apply();

        void SetPixels(Coordinates c, Color32 color) {
            SetPixel(editorMap, c, color);
            SetPixel(roomMap  , c, color);
        }
        void SetPixel(Texture2D texture, Coordinates c, Color32 color) {
            Coordinates o = Offset(c);
            texture.SetPixel(o.x, o.y, color);
        }
        Coordinates Offset(Coordinates c) {
            return c + gridData.offset;
        }

        MapController.RoomData roomData = new MapController.RoomData(levelData.levelName);
        roomData.localOffset = gridData.offset;
        roomData.tunnels = tunnels.ToArray();

        MapController.MapData defaultMap = GameController.LoadMap("Default", 0);
        List<MapController.RoomData> newRoomDatas = new List<MapController.RoomData>();
        if (defaultMap.roomDatas != null) {
            foreach (MapController.RoomData rd in defaultMap.roomDatas) {
                if (rd.roomName != roomData.roomName)
                    newRoomDatas.Add(rd);
            }
        }
        newRoomDatas.Add(roomData);
        defaultMap.roomDatas = newRoomDatas.ToArray();
        GameController.SaveMap(defaultMap, 0);
        
        string path = "Maps/Rooms/" + levelData.levelName;
        Assets.SaveSprite(editorMap, "Assets/Sprites/" + path);
        
        SerializedObject so = new SerializedObject(levelData);
        so.FindProperty("roomMap").objectReferenceValue = Sprite.Create(roomMap, new Rect(0, 0, roomMap.width, roomMap.height), new Vector2(0.5f, 0.5f), 1);
        so.ApplyModifiedProperties();

        Debug.LogError("Generated Room Map: " + levelData.levelName);
    }
    
    //[ContextMenu("Generate Dig Sprites")]
    void GenerateDigSprites() {
        string path   = "Assets/Sprites/Blocks/Dig";
        string folder = "DigSprites";
        if (AssetDatabase.IsValidFolder(path + "/" + folder))
            AssetDatabase.DeleteAsset(path + "/" + folder);
        AssetDatabase.CreateFolder(path, folder);

        Sprite emptySprite = Assets.GetSprite("Blocks/Dig/dig_blank");
        Sprite[] outlineSprites = new Sprite[4];
        Sprite[] holeSprites    = new Sprite[4];
        for (int i = 0; i < outlineSprites.Length; i++) outlineSprites[i] = GetSprite("dig_outline", i);
        for (int i = 0; i < holeSprites   .Length; i++) holeSprites   [i] = GetSprite("dig_hole"   , i);

        Sprite GetSprite(string name, int index) {
            return Assets.GetSprite("Blocks/Dig/" + name + "_" + index);
        }

        CreateSprite(outlineSprites, "dig_outline");
        CreateSprite(holeSprites   , "dig_hole"   );

        // Combine outline types to create variations for each sprite type
        void CreateSprite(Sprite[] sprites, string name) {
            int spriteIndex = 0;

            for (int type = 0; type < Assets.SPRITE_TYPES.Length - 1; type++) {
                for (int facing = 0; facing < Assets.SPRITE_TYPES[type].Length; facing++) {
                    Texture2D texture = new Texture2D(GameController.BLOCK_SIZE, GameController.BLOCK_SIZE);
                    for (int y = 0; y < texture.width; y++) {
                        for (int x = 0; x < texture.height; x++)
                            texture.SetPixel(x, y, Color.clear);
                    }

                    for (int i = 0; i < Assets.SPRITE_TYPES[type][facing].Length; i++) {
                        if (Assets.SPRITE_TYPES[type][facing][i])
                            AddPixels(sprites[i].texture);
                    }

                    void AddPixels(Texture2D t) {
                        for (int y = 0; y < texture.width; y++) {
                            for (int x = 0; x < texture.height; x++) {
                                Color c = t.GetPixel(x, y);
                                if (c.a == 0)
                                    continue;

                                texture.SetPixel(x, y, c);
                            }
                        }
                    }

                    texture.Apply();
                    byte[] png = texture.EncodeToPNG();
                    File.WriteAllBytes(path  + "/" + folder  + "/" + name + "_" + spriteIndex + ".png", png);
                    spriteIndex++;
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.LogError("Generated Dig Sprites");
    }
}
#endif