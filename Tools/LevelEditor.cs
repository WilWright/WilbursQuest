#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class LevelEditor : MonoBehaviour {
    public GameObject cursorBlock;
    public GameObject cursor;
    public GameObject screenAsset;
    public GameObject UI;
    public Text blockText;
    public Text hotBarText;
    public Text screenText;
    public Text controlsText;   
    public SpriteGeneration spriteGeneration;
    static readonly Coordinates bounds = new Coordinates(-100, 99);
    
    string levelName;
    GameObject tempLevel;
    GridSystem gridSystem;
    GameObject currentBlock;
    Layer currentLayer;
    Coordinates mouseCoords;
    Data connectTo;
    int hotBarIndex;
    bool allowInput = true;
    bool gridEnabled;

    Screen currentScreen;
    Screen selectedScreen;
    int screenIndex;
    bool showScreens = true;
    bool screenMode;
    List<Screen> screens = new List<Screen>();

    float cameraSize;
    Vector3 cameraPos;
    bool cameraSnap;
    const float CAMERA_MOVE_SPEED = 100;
    const float CAMERA_ZOOM_SPEED = 1000;

    GameObject moveBlocks;
    List<Data> moveData;
    List<Data> connectedDatas = new List<Data>();
    Sprite[][] spriteTiles;
    Dictionary<string, GameObject> prefabs;
    Dictionary<string, Color> prefabColors;
    UnityEngine.InputSystem.Controls.KeyControl[] digitKeys;
    List<SpriteRenderer>[] groupDisplays;
    bool[] showGroupDisplays;
    bool showAllGroups;

    Keyboard keyB;
    Mouse mouse;

    public bool dontSave;

    static readonly string[][] HOT_BAR_ORDER = new string[][] {
        new string[] { "Ground", "Dig", "Support", "GroundBG1", "GroundBG2", "GroundFG", "Screen" },
        new string[] { "Rock", "Tunnel", "Piston", "Pipe", "Gate", "GateSlot", "Basic" },
        new string[] { "RedCrystal", "GreenCrystal", "BlueCrystal", "ForceCrystal" },
        new string[] { "RedButton" , "GreenButton" , "BlueButton" , "ForceButton"  },
        new string[] { "CollectRed", "CollectGreen", "CollectBlue", "CollectForce", "CollectTime", "CollectFragment", "CollectLength", "CollectDig", "CollectSong" }
    };
    static readonly string[] GROUP_DISPLAY_ORDER = new string[] { "Ground", "Dig", "Support", "GroundBG1", "GroundBG2", "GroundFG" };

    void Start() {
        if (GameController.initialized) {
            gameObject.SetActive(false);
            return;
        }

        keyB  = InputSystem.GetDevice<Keyboard>();
        mouse = InputSystem.GetDevice<Mouse   >();
        GameController.InitData();
        Cursor.visible = false;

        digitKeys = new UnityEngine.InputSystem.Controls.KeyControl[] {
            keyB.digit1Key,
            keyB.digit2Key,
            keyB.digit3Key,
            keyB.digit4Key,
            keyB.digit5Key,
            keyB.digit6Key,
            keyB.digit7Key,
            keyB.digit8Key,
            keyB.digit9Key
        };
        groupDisplays = new List<SpriteRenderer>[digitKeys.Length];
        showGroupDisplays = new bool[digitKeys.Length];
        for (int i = 0; i < showGroupDisplays.Length; i++)
            showGroupDisplays[i] = true;
        
        GameObject level = GameObject.FindGameObjectWithTag("Level");
        if (level == null) {
            Debug.LogError("Level Not Found");
            return;
        }

        level.SetActive(false);
        levelName = level.name;
        
        gridSystem = new GridSystem(GameController.LoadGrid(levelName, 0), true);
        InitBlocks(gridSystem);

        GameObject[] screenObjects = GameObject.FindGameObjectsWithTag("Screen");
        foreach (GameObject go in screenObjects)
            screens.Add(go.GetComponent<Screen>());

        SetBlock("Ground");
        SelectHotBar(0);
        ToggleInput();
    }
    private void OnDisable() {
        if (!GameController.initialized && !dontSave && !GameController.disableEditors) {
            gridSystem.SetScreens(screens.ToArray());
            gridSystem.TrimBounds();
            GameController.SaveGrid(levelName, 0, gridSystem);
            AssetDatabase.Refresh();
            Debug.LogError("Generate Level");
        }
        else
            dontSave = false;
    }

    void Update() {
        if (GameController.initialized) {
            gameObject.SetActive(false);
            return;
        }

        if (keyB.tabKey.wasPressedThisFrame)
            ToggleInput();
        if (!allowInput)
            return;

        if (keyB.jKey.wasPressedThisFrame)
            controlsText.gameObject.SetActive(!controlsText.gameObject.activeSelf);
        if (keyB.hKey.wasPressedThisFrame)
            UI.SetActive(!UI.activeSelf);

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseCoords = GameController.GetCoordinates(mousePos);
        GameController.ApplyCoordinates(mouseCoords, cursorBlock);
        cursor.transform.position = mousePos;

        if (keyB.wKey.isPressed) MoveCamera(Vector3.up   );
        if (keyB.sKey.isPressed) MoveCamera(Vector3.down );
        if (keyB.aKey.isPressed) MoveCamera(Vector3.left );
        if (keyB.dKey.isPressed) MoveCamera(Vector3.right);
        void MoveCamera(Vector3 direction) { Camera.main.transform.position += direction * CAMERA_MOVE_SPEED * Time.deltaTime; }

        if      (mouse.scroll.ReadValue().y < 0) ZoomCamera(false);
        else if (mouse.scroll.ReadValue().y > 0) ZoomCamera(true );
        void ZoomCamera(bool zoom) { Camera.main.orthographicSize += CAMERA_ZOOM_SPEED * (zoom ? -1 : 1) * Time.deltaTime; }

        if      (keyB.qKey.wasPressedThisFrame) SelectHotBar(-1);
        else if (keyB.eKey.wasPressedThisFrame) SelectHotBar( 1);

        for (int i = 0; i < digitKeys.Length; i++) {
            if (digitKeys[i].wasPressedThisFrame) {
                if (keyB.leftShiftKey.isPressed) {
                    ToggleGroupDisplay(i);
                    return;
                }
                else {
                    if (i < HOT_BAR_ORDER[hotBarIndex].Length) {
                        SetBlock(HOT_BAR_ORDER[hotBarIndex][i]);
                        return;
                    }
                }
            }
        }

        if (keyB.leftCtrlKey.wasPressedThisFrame)
            ToggleAllGroupDisplays();

        if (keyB.gKey.wasPressedThisFrame) gridEnabled = !gridEnabled;
        if (gridEnabled)                   DisplayGrid();

        if (keyB.upArrowKey  .wasPressedThisFrame) SnapCamera(true );
        if (keyB.downArrowKey.wasPressedThisFrame) SnapCamera(false);

        if (keyB.leftArrowKey .wasPressedThisFrame) SwitchScreen(-1);
        if (keyB.rightArrowKey.wasPressedThisFrame) SwitchScreen( 1);

        if (keyB.rightCtrlKey.wasPressedThisFrame) {
            showScreens = !showScreens;
            foreach (Screen s in screens)
                s.gameObject.SetActive(showScreens);
        }

        if (screenMode) {
            if (mouse.leftButton .wasPressedThisFrame) PlaceScreen();
            if (mouse.rightButton.wasPressedThisFrame) SetSelectedScreen();
            if (selectedScreen != null) {
                if (mouse.rightButton.isPressed           ) MoveScreen(true );
                if (mouse.rightButton.wasReleasedThisFrame) MoveScreen(false);
            }

            if (mouse.middleButton.wasPressedThisFrame || keyB.spaceKey.wasPressedThisFrame)
                DeleteScreen();

            if (keyB.tKey.wasPressedThisFrame) currentScreen.SetSize(currentScreen.screenData.size - 1);
            if (keyB.yKey.wasPressedThisFrame) currentScreen.SetSize(currentScreen.screenData.size + 1);

            return;
        }

        if (mouse.leftButton.isPressed           ) PlaceBlock();
        if (mouse.leftButton.wasReleasedThisFrame) ConnectBlocks();

        if (mouse.rightButton.wasPressedThisFrame ) PrepareMoveBlock();
        if (mouse.rightButton.wasReleasedThisFrame) TryMoveBlock();
        if (mouse.rightButton.isPressed) {
            if (moveData != null && WithinBounds(mouseCoords))
                GameController.ApplyCoordinates(mouseCoords, moveBlocks);
        }

        if (mouse.middleButton.isPressed || keyB.spaceKey.isPressed)
            DeleteBlock();

        if (keyB.leftShiftKey.isPressed) {
            if (keyB.tKey.wasPressedThisFrame) ChangeTiling(gridSystem.GetData(mouseCoords, currentLayer), -1);
            if (keyB.yKey.wasPressedThisFrame) ChangeTiling(gridSystem.GetData(mouseCoords, currentLayer),  1);
        }
        else {
            if (keyB.tKey.wasPressedThisFrame) ChangeFacing(gridSystem.GetData(mouseCoords, currentLayer), -1);
            if (keyB.yKey.wasPressedThisFrame) ChangeFacing(gridSystem.GetData(mouseCoords, currentLayer),  1);
        }
    }

    void ToggleInput() {
        allowInput = !cursorBlock.activeSelf;
        cursorBlock.SetActive(allowInput);
        Cursor.visible = !allowInput;
    }

    void SetBlock(string blockName) {
        blockText.text = blockName;
        screenMode = blockName == "Screen";
        if (screenMode)
            return;

        currentBlock = prefabs[blockName];
        currentLayer = Assets.GetAssetInfo(blockName).layer;
    }

    void SelectHotBar(int direction) {
        hotBarIndex += direction;
        if (hotBarIndex < 0)                     hotBarIndex = HOT_BAR_ORDER.Length - 1;
        if (hotBarIndex >= HOT_BAR_ORDER.Length) hotBarIndex = 0;

        hotBarText.text = "";
        for (int i = 0; i < HOT_BAR_ORDER[hotBarIndex].Length; i++) {
            string blockName    = HOT_BAR_ORDER[hotBarIndex][i];
            string groupDisplay = showGroupDisplays[GROUP_DISPLAY_ORDER.Length] ? (GROUP_DISPLAY_ORDER.Length + 1 + "") : ("(" + (GROUP_DISPLAY_ORDER.Length + 1) + ")");

            for (int j = 0; j < GROUP_DISPLAY_ORDER.Length; j++) {
                if (blockName == GROUP_DISPLAY_ORDER[j]) {
                    groupDisplay = showGroupDisplays[j] ? (j + 1 + "") : ("(" + (j + 1) + ")");
                    break;
                }
            }

            hotBarText.text += "(" + (i + 1) + "-" + groupDisplay + ") " + blockName + "\n";
        }
    }

    void ToggleGroupDisplay(int groupIndex) {
        showGroupDisplays[groupIndex] = !showGroupDisplays[groupIndex];
        foreach (SpriteRenderer sr in groupDisplays[groupIndex]) {
            if (sr != null)
                sr.enabled = showGroupDisplays[groupIndex];
        }
        SelectHotBar(0);
    }
    void ToggleAllGroupDisplays() {
        showAllGroups = !showAllGroups;
        if (showAllGroups) {
            foreach (List<SpriteRenderer> list in groupDisplays) {
                foreach (SpriteRenderer sr in list) {
                    if (sr != null)
                        sr.enabled = true;
                }
            }
        }
        else {
            for (int i = 0; i < groupDisplays.Length; i++) {
                foreach (SpriteRenderer sr in groupDisplays[i]) {
                    if (sr != null)
                        sr.enabled = showGroupDisplays[i];
                }
            }
        }
        SelectHotBar(0);
    }
    void AddToGroupDisplay(Data data) {
        SpriteRenderer spriteRenderer = data.blockObject.GetComponent<SpriteRenderer>();
        for (int i = 0; i < GROUP_DISPLAY_ORDER.Length; i++) {
            if (data.blockData.blockName == GROUP_DISPLAY_ORDER[i]) {
                groupDisplays[i].Add(spriteRenderer);
                return;
            }
        }
        
        if (data.layer == Layer.Tunnel && data.blockData.IsPrimary())
            return;

        SpriteRenderer[] srs = data.blockObject.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in srs)
            groupDisplays[GROUP_DISPLAY_ORDER.Length].Add(sr);
    }

    void DisplayGrid() {
        float g  = GameController.GRID_SIZE;
        float hg = g / 2;
        Vector2 p = GameController.GetGridPosition(bounds);

        for (int i = bounds.x; i < bounds.y; i++) {
            float pos = i * g - hg;
            Debug.DrawLine(new Vector2(pos, p.x + hg - g),
                           new Vector2(pos, p.y + hg    ), Color.grey);
            Debug.DrawLine(new Vector2(p.x - hg    , pos),
                           new Vector2(p.y - hg + g, pos), Color.grey);
        }
    }

    void PlaceScreen() {
        currentScreen = Instantiate(screenAsset, tempLevel.transform).GetComponent<Screen>();
        screens.Add(currentScreen);
        screenIndex = screens.Count - 1;
        screenText.text = "Screen - " + screenIndex;

        currentScreen.SetPosition(mouseCoords);
        currentScreen.SetSize(9);
    }
    void SetSelectedScreen() {
        for (int i = 0; i < screens.Count; i++) {
            if (screens[i].screenData.coordinates == mouseCoords) {
                selectedScreen = screens[i];
                SwitchScreen(i - screenIndex);
                break;
            }
        }
    }
    void SnapCamera(bool snap) {
        if (cameraSnap == snap) return;
            cameraSnap =  snap;

        if (cameraSnap) {
            cameraSize = Camera.main.orthographicSize;
            cameraPos  = Camera.main.transform.position;
            SetScreen(screens[screenIndex]);
        }
        else {
            Camera.main.orthographicSize   = cameraSize;
            Camera.main.transform.position = cameraPos;
        }
    }
    void SwitchScreen(int direction) {
        screenIndex += direction;
        if (screenIndex < 0)              screenIndex = screens.Count - 1;
        if (screenIndex >= screens.Count) screenIndex = 0;

        screenText.text = "Screen - " + screenIndex;
        if (cameraSnap)
            SetScreen(screens[screenIndex]);
    }
    void MoveScreen(bool moving) {
        if (moving) {
            if (WithinBounds(mouseCoords))
                selectedScreen.SetPosition(mouseCoords);
        }
        else {
            selectedScreen.SetPosition(mouseCoords);
            selectedScreen.SetSize(selectedScreen.screenData.size);
            currentScreen = selectedScreen;
            selectedScreen = null;
        }
    }
    void DeleteScreen() {
        if (screens.Count < 1)
            return;

        foreach (Screen s in screens) {
            if (s.screenData.coordinates == mouseCoords) {
                screens.Remove(s);
                screenIndex = screens.Count - 1;
                screenText.text = "Screen - " + screenIndex;
                currentScreen = screens[screenIndex];
                Destroy(s.gameObject);
                break;
            }
        }
    }

    void PlaceBlock() {
        if (!WithinBounds(mouseCoords))
            return;
        
        Data d = gridSystem.GetData(mouseCoords, currentLayer);
        if (d == null) {
            BlockData bd = new BlockData(currentBlock.name, mouseCoords);
            GameObject block = Instantiate(currentBlock, tempLevel.transform);
            Data newData = new Data(bd, block);
            newData.ApplyData();
            gridSystem.AddData(newData);
            AddToGroupDisplay(newData);

            if (!newData.HasTag(Tag.Connect)) {
                if (newData.HasTag(Tag.Tile)) {
                    TileNearBlocks(newData);
                    ApplyTiling(newData);
                }
            }
            else
                connectedDatas.Add(newData);
        }
        else {
            if (connectTo == null && connectedDatas.Count == 0 && d.blockData.connectedBlocks != null)
                connectTo = d;
        }
    }
    void DeleteBlock() {
        if (!WithinBounds(mouseCoords))
            return;

        Data d = gridSystem.GetData(mouseCoords, currentLayer);
        if (d != null && !d.HasTag(Tag.Player)) {
            if (d.HasTag(Tag.Connect)) {
                if (d.blockData.connectedBlocks != null) {
                    foreach (Coordinates c in d.blockData.connectedBlocks) {
                        Data cd = gridSystem.GetData(c, currentLayer);
                        if (currentLayer == Layer.Tunnel) {
                            Data td = gridSystem.GetData(c, Layer.Misc);
                            if (td != null)
                                gridSystem.RemoveData(td);
                        }
                        Destroy(cd.blockObject);
                        gridSystem.RemoveData(cd);
                    }
                }
            }
            else {
                if (currentLayer == Layer.Piston) {
                    if (d.blockData.connectedBlocks != null) {
                        foreach (Coordinates c in d.blockData.connectedBlocks) {
                            Data cd = gridSystem.GetData(c, currentLayer);
                            Destroy(cd.blockObject);
                            gridSystem.RemoveData(cd);
                        }
                    }
                }
            }

            if (d.blockObject != null)
                Destroy(d.blockObject);

            gridSystem.RemoveData(d);
            if (d.HasTag(Tag.Tile))
                TileNearBlocks(d);
        }
    }
    void PrepareMoveBlock() {
        if (!WithinBounds(mouseCoords) || moveData != null)
            return;

        Data d = gridSystem.GetData(mouseCoords, currentLayer);
        if (d != null) {
            moveBlocks = new GameObject("MoveBlocks");
            moveBlocks.transform.position = GameController.GetGridPosition(mouseCoords);
            Coordinates[] cb = d.blockData.connectedBlocks;
            moveData = new List<Data> { d };
            GameObject testPrefab = Assets.GetPrefab("Test");
            if (cb != null) {
                foreach (Coordinates c in cb) {
                    Data cd = gridSystem.GetData(c, d.layer);
                    GameObject go = Instantiate(testPrefab);
                    GameController.ApplyCoordinates(cd.blockData.coordinates, go);
                    go.transform.SetParent(moveBlocks.transform);
                    if (c != d.blockData.coordinates)
                        moveData.Add(cd);
                }
            }
            else {
                GameObject go = Instantiate(testPrefab);
                GameController.ApplyCoordinates(moveData[0].blockData.coordinates, go);
                go.transform.SetParent(moveBlocks.transform);
            }
        }
    }
    void TryMoveBlock() {
        if (!WithinBounds(mouseCoords) || moveData == null) {
            FinishMove();
            return;
        }
        
        Data placeData = gridSystem.GetData(mouseCoords, moveData[0].layer);
        if (placeData != null) {
            bool same = false;
            if (moveData[0].blockData.connectedBlocks != null) {
                foreach (Coordinates c in moveData[0].blockData.connectedBlocks) {
                    if (placeData.blockData.coordinates == c) {
                        same = true;
                        break;
                    }
                }
            }
            if (!same) {
                FinishMove();
                return;
            }
        }

        Coordinates[] mdc = moveData[0].blockData.connectedBlocks;
        if (mdc != null) {
            Coordinates[] offsets = new Coordinates[mdc.Length];
            Coordinates offset = mouseCoords - moveData[0].blockData.coordinates;
            for (int i = 0; i < mdc.Length; i++) {
                offsets[i] = mdc[i] + offset;

                if (!WithinBounds(offsets[i])) {
                    FinishMove();
                    return;
                }

                Data d = gridSystem.GetData(offsets[i], moveData[0].layer);
                if (d != null) {
                    bool same = false;
                    foreach (Coordinates c in mdc) {
                        if (c == d.blockData.coordinates) {
                            same = true;
                            break;
                        }
                    }
                    if (!same) {
                        FinishMove();
                        return;
                    }
                }
            }

            Layer l = moveData[0].layer;
            moveData.Clear();
            for (int i = 0; i < mdc.Length; i++) {
                moveData.Add(gridSystem.GetData(mdc[i], l));
                gridSystem.RemoveData(moveData[i]);
                moveData[i].blockData.coordinates = mdc[i] = offsets[i];
            }
            foreach (Data d in moveData) {
                gridSystem.AddData(d);
                d.ApplyData();
            }
        }
        else {
            Data prevData = new Data(moveData[0]);
            gridSystem.SetData(mouseCoords, moveData[0], true);
            if (moveData[0].HasTag(Tag.Tile)) {
                TileNearBlocks(prevData);
                TileNearBlocks(moveData[0]);
                moveData[0].blockData.spriteState = -1;
                ApplyTiling(moveData[0]);
            }
        }

        FinishMove();

        void FinishMove() {
            moveData = null;
            Destroy(moveBlocks);
        }
    }

    void ConnectBlocks() {
        if (connectedDatas.Count == 0) {
            connectTo = null;
            return;
        }

        List<Data> connectToDatas = new List<Data>();
        if (connectTo != null) {
            foreach (Coordinates c in connectTo.blockData.connectedBlocks) {
                Data d = gridSystem.GetData(c, connectTo.layer);
                d.blockData.spriteState = -1;
                d.blockData.connectedBlocks = null;
                connectToDatas.Add(d);
            }
        }
        foreach (Data d in connectedDatas)
            connectToDatas.Add(d);

        Coordinates[] cBlocks = new Coordinates[connectToDatas.Count];
        for (int i = 0; i < cBlocks.Length; i++)
            cBlocks[i] = connectToDatas[i].blockData.coordinates;
        foreach (Data d in connectToDatas) {
            d.blockData.connectedBlocks = cBlocks;
            ApplyTiling(d);
        }

        connectTo = null;
        connectedDatas.Clear();
    }
    void TileNearBlocks(Data data) {
        Data[][] nearData = GameController.GetNearData(data.blockData.coordinates, gridSystem);
        foreach (Data[] datas in nearData) {
            foreach (Data d in datas) {
                if (d != null && d.blockData.blockName == data.blockData.blockName) {
                    d.blockData.spriteState = -1;
                    ApplyTiling(d);
                }
            }
        }
    }
    void ChangeFacing(Data data, int direction) {
        if (data == null)
            return;

        data.blockData.facing += direction;
        if (data.blockData.facing < 0)                                   data.blockData.facing = Coordinates.FacingDirection.Length - 1;
        if (data.blockData.facing >= Coordinates.FacingDirection.Length) data.blockData.facing = 0;

        data.ApplyData(true);
    }
    void ChangeTiling(Data data, int direction) {
        if (data == null)
            return;

        data.blockData.spriteState += direction;
        if (data.blockData.spriteState < 0)                           data.blockData.spriteState = Assets.SPRITE_TYPES.Length - 1;
        if (data.blockData.spriteState >= Assets.SPRITE_TYPES.Length) data.blockData.spriteState = 0;

        ApplyTiling(data);
    }
    void ApplyTiling(Data data) {
        if (data.layer == Layer.Tunnel && data.blockData.connectedBlocks.Length < 2)
            return;
        
        GameController.ApplyTiling(data, gridSystem);
        int tileIndex = 0;
        switch (data.blockData.blockName) {
            case "GroundBG1":
            case "GroundBG2":
            case "Support"  : tileIndex = 1; break; 
            case "Dig"      : tileIndex = 2; break;
            case "Pipe"     : tileIndex = 3; break;
        }
        SpriteRenderer sr = data.blockObject.GetComponent<SpriteRenderer>();
        sr.sprite = spriteTiles[tileIndex][data.blockData.spriteState];

        SpriteGeneration.SpriteInfo si = spriteGeneration.GetSpriteInfo(data.blockData.blockName);
        if (si != null) {
            Color color = prefabColors[data.blockData.blockName];
            if (color == Color.white) sr.color = si.color;
            else                      sr.color = Color.Lerp(color, si.color, 0.45f);
        }
        data.ApplyData(true);
    }

    void SetScreen(Screen screen) {
        Camera.main.transform.position = new Vector3(screen.transform.position.x, screen.transform.position.y, -1);
        Camera.main.orthographicSize   = GameController.GetScreenOrthographicSize(screen);
    }

    bool WithinBounds(Coordinates coordinates) {
        return coordinates.x <= bounds.y 
            && coordinates.x >= bounds.x 
            && coordinates.y <= bounds.y 
            && coordinates.y >= bounds.x;
    }

    void InitBlocks(GridSystem gridSystem) {
        tempLevel = new GameObject("Level");
        for (int i = 0; i < groupDisplays.Length; i++) {
            groupDisplays[i] = new List<SpriteRenderer>();
            showGroupDisplays[i] = true;
        }

        spriteTiles = new Sprite[4][];
        for (int i = 0; i < spriteTiles   .Length; i++) spriteTiles   [i] = new Sprite[Assets.SPRITE_TYPES.Length];
        for (int i = 0; i < spriteTiles[0].Length; i++) spriteTiles[0][i] = Assets.GetSprite("Blocks/Tiling/Generation/Tiles/Generic/spritetile_generic_" + i);
        for (int i = 0; i < spriteTiles[1].Length; i++) spriteTiles[1][i] = Assets.GetSprite("Blocks/Tiling/Generation/Tiles/GenericLarge/spritetile_generic_large_" + i);
        for (int i = 0; i < spriteTiles[2].Length; i++) spriteTiles[2][i] = Assets.GetSprite("Blocks/Tiling/Dig/block_dig_" + i);
        for (int i = 0; i < spriteTiles[3].Length; i++) spriteTiles[3][i] = Assets.GetSprite("Blocks/Tiling/Pipe/block_pipe_" + i);
        
        prefabs = Assets.GetPrefabs();
        prefabColors = new Dictionary<string, Color>();
        foreach (var kvp in prefabs) {
            SpriteRenderer sr = kvp.Value.GetComponent<SpriteRenderer>();
            if (sr != null)
                prefabColors.Add(kvp.Key, sr.color);
        }

        if (gridSystem.GetGridData().blockDatas == null) {
            gridEnabled = true;
            return;
        }

        foreach (Data[] datas in gridSystem.grid) {
            foreach (Data d in datas) {
                if (d == null)
                    continue;

                if (d.layer == Layer.Dig && d.blockData.facing == -1) {
                    gridSystem.RemoveData(d);
                    continue;
                }

                switch (d.blockData.blockName) {
                    case "TunnelDoor"    :
                    case "SupportCrystal":
                    case "Panel"         :
                        continue;
                }
                
                GameObject newBlock = Instantiate(prefabs[d.blockData.blockName]);
                newBlock.name = d.blockData.blockName;
                d.blockObject = newBlock;
                d.ApplyData();
                newBlock.transform.parent = tempLevel.transform;

                if (d.HasTag(Tag.Connect, Tag.Tile))
                    ApplyTiling(d);

                AddToGroupDisplay(d);
            }
        }
    }
}
#endif