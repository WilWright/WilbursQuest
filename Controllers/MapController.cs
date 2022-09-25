using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapController : MonoBehaviour {
    [System.Serializable]
    public class MapData {
        public string mapName;
        public RoomData[] roomDatas;
        public Coordinates[] colorCoordinates;
        public byte[] map;

        public MapData(string mapName) {
            this.mapName = mapName;
        }

#if UNITY_EDITOR
        // Connect tunnels by linking adjacent tunnel ends dictated by placement on map
        public void ConnectTunnels() {
            Debug.LogError("");
            Debug.LogError("Connecting: " + mapName);
            if (roomDatas == null) {
                Debug.LogError("No Room Data");
                return;
            }

            Dictionary<string, RoomData> rooms = new Dictionary<string, RoomData>();
            List<RoomData.Tunnel> tunnels = new List<RoomData.Tunnel>();
            foreach (RoomData rd in roomDatas) {
                if (rd.tunnels == null) {
                    Debug.LogError(rd.roomName + ": No Tunnels");
                    continue;
                }

                rooms.Add(rd.roomName, rd);
                foreach (RoomData.Tunnel t in rd.tunnels) {
                    t.connectedTunnel = null;
                    tunnels.Add(t);
                }
            }

            for (int i = 0; i < tunnels.Count; i++) {
                if (tunnels[i].connectedTunnel != null)
                    continue;

                RoomData rd = rooms[tunnels[i].roomName];
                Coordinates c = tunnels[i].connectedBlocks[tunnels[i].connectedBlocks.Length - 1] + rd.mapOffset + rd.localOffset;
                int gatePanelIndex = tunnels[i].gatePanelIndex;
                bool found = false;
                if (tunnels[i].local) {
                    foreach (RoomData.Tunnel t in rd.tunnels) {
                        if (t == tunnels[i])
                            continue;

                        Coordinates n = t.connectedBlocks[t.connectedBlocks.Length - 1] + rd.mapOffset + rd.localOffset;
                        foreach (Coordinates f in Coordinates.FacingDirection) {
                            if (c + f != n)
                                continue;

                            tunnels[i].connectedTunnel = t;
                            t         .connectedTunnel = tunnels[i];
                            found = true;
                            break;
                        }
                        if (found)
                            break;
                    }
                }
                else {
                    for (int j = i + 1; j < tunnels.Count; j++) {
                        if (tunnels[j].connectedTunnel != null)
                            continue;

                        if (tunnels[i].gatePanelIndex != -1) {
                            if (tunnels[i].gatePanelIndex == tunnels[j].gatePanelIndex) {
                                tunnels[i].connectedTunnel = tunnels[j];
                                tunnels[j].connectedTunnel = tunnels[i];
                                found = true;
                                break;
                            }
                            continue;
                        }

                        RoomData nRD = rooms[tunnels[j].roomName];
                        Coordinates n = tunnels[j].connectedBlocks[tunnels[j].connectedBlocks.Length - 1] + nRD.mapOffset + nRD.localOffset;
                        foreach (Coordinates f in Coordinates.FacingDirection) {
                            if (c + f != n)
                                continue;

                            tunnels[i].connectedTunnel = tunnels[j];
                            tunnels[j].connectedTunnel = tunnels[i];
                            found = true;
                            break;
                        }
                        if (found)
                            break;
                    }
                }
                if (!found)
                    Debug.LogError(tunnels[i].roomName + ", " + c + ": No Match Found");
            }
        }

        public void GenerateMapSprite() {
            Debug.LogError("Generating: " + mapName);
            if (roomDatas == null) {
                Debug.LogError("No Room Data");
                return;
            }

            Coordinates size = Coordinates.Zero;
            foreach (RoomData rd in roomDatas) {
                if (rd.tunnels == null)
                    continue;

                Sprite s = Assets.GetSprite("Maps/Rooms/" + rd.roomName);
                if (s == null)
                    continue;

                size = new Coordinates(Mathf.Max(size.x, rd.mapOffset.x + s.texture.width), Mathf.Max(size.y, rd.mapOffset.y + s.texture.height));
            }

            Texture2D mapTexture = new Texture2D(size.x, size.y);
            for (int x = 0; x < mapTexture.width; x++) {
                for (int y = 0; y < mapTexture.height; y++)
                    mapTexture.SetPixel(x, y, Color.clear);
            }

            foreach (RoomData rd in roomDatas) {
                if (rd.tunnels == null)
                    continue;

                Sprite s = Assets.GetSprite("Maps/Rooms/" + rd.roomName);
                if (s == null)
                    continue;

                Texture2D t = s.texture;
                for (int x = 0; x < t.width; x++) {
                    for (int y = 0; y < t.height; y++) {
                        Color32 color = t.GetPixel(x, y);
                        if (color.a == 0 || color.a == 1)
                            continue;

                        if ((Color)color == Assets.MAP_COLORS["TunnelEnd"])
                            color = Assets.MAP_COLORS["Tunnel"];

                        mapTexture.SetPixel(x + rd.mapOffset.x, y + rd.mapOffset.y, color);
                    }
                }
            }
            mapTexture.Apply();

            GameController.SaveMap(this, 0);
            Assets.SaveSprite(mapTexture, "Assets/Sprites/Maps/" + mapName);
        }
#endif
    }

    [System.Serializable]
    public class RoomData {
        [System.Serializable]
        public class Tunnel {
            public string roomName;
            public Tunnel connectedTunnel;
            public Coordinates[] connectedBlocks;
            public Coordinates[][] mapOrder;
            public bool visited;
            public bool local; // Local tunnels are two tunnels connected by their ends in the same room instead of a tunnel leading to another room
            public int gatePanelIndex = -1;
        }

        public string roomName;
        public Coordinates mapOffset;
        public Coordinates localOffset;
        public Tunnel[] tunnels;
        public List<Coordinates>[] colorPixels;
        public List<Coordinates>[] buttonColorPixels;

        public RoomData(string roomName) {
            this.roomName = roomName;
        }

        public Tunnel GetTunnel(Data tunnelData) { return GetTunnel(tunnelData.blockData.connectedBlocks[0]); }
        public Tunnel GetTunnel(Coordinates c) {
            if (tunnels == null)
                return null;

            foreach (Tunnel t in tunnels) {
                if (t.connectedBlocks[0] == c)
                    return t;
            }
            return null;
        }
    }

    class MapInfo {
        public float cameraSize;
        public Color bgColor = Color.white;
        public Color mapColor = Color.white;
        public Coordinates[] mapPixels;
        public Coordinates[] revealPixelsA;
        public Coordinates[] revealPixelsB;

        public MapInfo(Coordinates[] mapPixels) {
            this.mapPixels = mapPixels;
        }
    }
    class TunnelOrder {
        public RoomData.Tunnel tunnel;
        public int index;

        public TunnelOrder(RoomData.Tunnel tunnel) {
            this.tunnel = tunnel;
        }
    }
    class BlinkPixel {
        public Color32 pixelColor;
        public Color32 mapColor;
        public Coordinates coordinates;

        public BlinkPixel(Color32 pixelColor, Color32 mapColor, Coordinates coordinates) {
            this.pixelColor  = pixelColor;
            this.  mapColor  = mapColor;
            this.coordinates = coordinates;
        }
    }

    [System.Serializable]
    public class MapArrow {
        public SpriteRenderer[] spriteRenderers;
        public bool active;
        public IEnumerator enableCoroutine;
    }

    [HideInInspector] public MapData mapData;
    [HideInInspector] public RoomData currentRoom;
    public GameObject mapUI;
    public GameObject mapHolder;
    public GameObject mapAnchor;
    public GameObject mapButtons;
    public MapArrow[] mapArrows;
    Color32[][] mapArrowColors;
    public GameObject borderHolder;
    public GameObject[] mapBorders;
    public Sprite mapMaster;
    Sprite mapSprite;
    public SpriteRenderer mapSpriteRenderer;
    public SpriteRenderer mapMasterSpriteRenderer;
    public SpriteRenderer bgSpriteRenderer;
    public SpriteRenderer gridSpriteRenderer;

    Texture2D roomTexture;
    Texture2D roomTemplate;
    Dictionary<string, RoomData> roomDatas;
    Coordinates originOffset;
    static readonly Vector3 MAX_SCALE = new Vector3(7f, 7f, 1f);
    static readonly Vector3 MIN_SCALE = new Vector3(0.01f, 0.01f, 1f);
    static readonly Color32 REVEAL_A_COLOR = new Color32(255, 255, 255, 50);
    static readonly Color32 REVEAL_B_COLOR = new Color32(255, 255, 255, 255);
    static readonly Color32 BG_COLOR = new Color32(0, 0, 0, 0);
    const float MAP_ARROW_ENABLE_SPEED = 5;
    const float MAP_GRID_ALPHA_MAX = 0.4f;

    Camera cam;
    float cameraSize;
    const float CAMERA_SIZE_DEFAULT = 360;
    const float CAMERA_SIZE_MIN = 120;
    const float CAMERA_SIZE_MAX = 600;
    const int CAMERA_ZOOM_FRAMES = 20;
    const float CAMERA_ZOOM_SPEED = 350;
    
    MapInfo[] mapInfo;
    Coordinates[][] roomShuffle;
    Coordinates[][] mapPath;
    bool[,] visibleRoomPixels;
    const int UNVISITED_TUNNEL_LENGTH = 7;
    Vector3 startPos;
    Vector3 releasePos;
    bool showMap;
    bool finishedShowing;
    int mapIndex;
    int unlockIndex;
    int releaseIndex;
    float mapTime;
    float displayTime;
    float currentMapSpeed;
    const float MOVE_SPEED = 1.35f;
    const float SHOW_SPEED = 1;
    const float HIDE_SPEED = 3;
    const float DISPLAY_TIME_MIN = 1;
    const float DISPLAY_TIME_MAX = 5;

    void Update() {
        if (!mapUI.activeSelf || GameController.paused)
            return;

        if (!showMap || mapIndex < unlockIndex + CAMERA_ZOOM_FRAMES)
            return;

        if (!mapButtons.activeSelf)
            mapButtons.SetActive(true);
        
        if (InputController.Get(Action.Shoot)) ZoomMap(-1);
        if (InputController.Get(Action.Undo )) ZoomMap( 1);
        void ZoomMap(int direction) {
            cam.orthographicSize = cameraSize = Mathf.Clamp(cameraSize + Time.deltaTime * CAMERA_ZOOM_SPEED * direction, CAMERA_SIZE_MIN, CAMERA_SIZE_MAX);
            Color gridColor = Color.white;
            gridColor.a = Mathf.Lerp(0, MAP_GRID_ALPHA_MAX, Mathf.InverseLerp(CAMERA_SIZE_DEFAULT, CAMERA_SIZE_MIN, cameraSize));
            gridSpriteRenderer.color = gridColor;
        }

        Vector3 moveDirection = Vector3.zero;

        bool moveUp = mapBorders[0].transform.position.y > mapHolder.transform.position.y;
        if (moveUp && InputController.Get(Action.Up)) moveDirection += Vector3.up;
        EnableMapArrow(mapArrows[0], moveUp);

        bool moveDown = mapBorders[1].transform.position.y < mapHolder.transform.position.y;
        if (moveDown && InputController.Get(Action.Down)) moveDirection += Vector3.down;
        EnableMapArrow(mapArrows[1], moveDown);

        bool moveLeft = mapBorders[0].transform.position.x < mapHolder.transform.position.x;
        if (moveLeft && InputController.Get(Action.Left)) moveDirection += Vector3.left;
        EnableMapArrow(mapArrows[2], moveLeft);

        bool moveRight = mapBorders[1].transform.position.x > mapHolder.transform.position.x;
        if (moveRight && InputController.Get(Action.Right)) moveDirection += Vector3.right;
        EnableMapArrow(mapArrows[3], moveRight);
        
        mapAnchor.transform.position -= moveDirection.normalized * Time.deltaTime * MOVE_SPEED * cameraSize;

        void EnableMapArrow(MapArrow mapArrow, bool active) {
            if (mapArrow.active == active) return;
                mapArrow.active =  active;

            if (mapArrow.enableCoroutine != null) StopCoroutine(mapArrow.enableCoroutine);
                mapArrow.enableCoroutine  = ieEnableMapArrow();
            StartCoroutine(mapArrow.enableCoroutine);

            IEnumerator ieEnableMapArrow() {
                int fromIndex = 0;
                int toIndex   = 1;
                if (active) {
                    fromIndex = 1;
                    toIndex   = 0;
                }

                float time = 0;
                while (time < 1) {
                    float curveTime = GameController.GetCurve(time);
                    for (int i = 0; i < mapArrow.spriteRenderers.Length; i++)
                        mapArrow.spriteRenderers[i].color = Color32.Lerp(mapArrowColors[i][fromIndex], mapArrowColors[i][toIndex], curveTime);

                    time += Time.deltaTime * MAP_ARROW_ENABLE_SPEED;
                    yield return null;
                }
                for (int i = 0; i < mapArrow.spriteRenderers.Length; i++)
                    mapArrow.spriteRenderers[i].color = mapArrowColors[i][toIndex];

                mapArrow.enableCoroutine = null;
            }
        }
    }

    public void Init() {
        mapData = GameController.LoadMap(mapMaster.name, GameController.currentSave);
        if (mapData.roomDatas == null)
            return;

        roomDatas = new Dictionary<string, RoomData>();
        foreach (RoomData rd in mapData.roomDatas)
            roomDatas.Add(rd.roomName, rd);
        
        Texture2D masterTexture = new Texture2D(mapMaster.texture.width, mapMaster.texture.height);
        Texture2D mapTexture    = new Texture2D(mapMaster.texture.width, mapMaster.texture.height);

        originOffset = -new Coordinates(mapTexture.width / 2 + mapTexture.width % 2, mapTexture.height / 2 + mapTexture.height % 2) + 1;
        borderHolder.transform.localPosition = GameController.GetVector(originOffset);

        mapArrowColors = new Color32[2][];
        for (int i = 0; i < mapArrowColors.Length; i++) {
            mapArrowColors[i] = new Color32[2];
            mapArrowColors[i][0] = mapArrows[0].spriteRenderers[i].color;
            mapArrowColors[i][1] = Color32.Lerp(mapArrowColors[i][0], Color.black, 0.65f);
        }

        string startRoom = GameController.startRoom;
        if (startRoom.Contains(":")) startRoom = startRoom.Split(':')[0];
        RoomData startRD = GetRoomData(startRoom);
        if (startRD != null) {
            foreach (GameObject go in mapBorders)
                go.transform.localPosition = GameController.GetVector(startRD.mapOffset + startRD.localOffset);
        }

        if (mapData.map == null) {
            for (int x = 0; x < mapTexture.width; x++) {
                for (int y = 0; y < mapTexture.height; y++) {
                    masterTexture.SetPixel(x, y, BG_COLOR);
                    mapTexture   .SetPixel(x, y, BG_COLOR);
                }
            }
        }
        else {
            masterTexture.LoadImage(mapData.map);

            for (int x = 0; x < mapTexture.width; x++) {
                for (int y = 0; y < mapTexture.height; y++) {
                    if (masterTexture.GetPixel(x, y).a > 0)
                        UpdateBorders(new Coordinates(x, y));

                    mapTexture.SetPixel(x, y, BG_COLOR);
                }
            }
        }

        masterTexture.filterMode = mapTexture.filterMode = FilterMode.Point;
        masterTexture.Apply(); mapTexture.Apply();
        mapMaster = Sprite.Create(masterTexture, new Rect(0, 0, mapTexture.width, mapTexture.height), new Vector2(0.5f, 0.5f), 1);
        mapSprite = Sprite.Create(mapTexture   , new Rect(0, 0, mapTexture.width, mapTexture.height), new Vector2(0.5f, 0.5f), 1);
        mapMasterSpriteRenderer.sprite = mapMasterSpriteRenderer.GetComponent<SpriteMask>().sprite = mapMaster;
        mapSpriteRenderer      .sprite = mapSpriteRenderer      .GetComponent<SpriteMask>().sprite = mapSprite;
        mapMaster.name = mapData.mapName;
        bgSpriteRenderer.size = gridSpriteRenderer.size = new Vector2(mapTexture.width + 500, mapTexture.height + 500);

        cam = GameController.Game.gameCamera;
        UpdateMapUI();
    }
    public void InitRoom(string room) {
        currentRoom = GetRoomData(room);
        if (currentRoom == null)
            return;

        Coordinates roomCoordinates = currentRoom.mapOffset + currentRoom.localOffset;
        for (int i = 0; i < mapData.colorCoordinates.Length; i++)
            GameController.Audio.SetSupportCrystalVolume(i, GameController.GetVector(roomCoordinates - mapData.colorCoordinates[i]).sqrMagnitude);
        GameController.Audio.UpdatePositionalAudio(GameController.Player.wormHead.data.blockData.coordinates);

        roomTexture  = GameController.Level.roomMap.texture;
        roomTemplate = new Texture2D(roomTexture.width, roomTexture.height);
        for (int x = 0; x < roomTexture.width; x++) {
            for (int y = 0; y < roomTexture.height; y++) {
                roomTemplate.SetPixel(x, y, roomTexture.GetPixel(x, y));
                roomTexture .SetPixel(x, y, BG_COLOR);
            }
        }
        roomTexture.filterMode = roomTemplate.filterMode = FilterMode.Point;

        GridSystem.GridData gridData = GameController.Grid.GetGridData();
        visibleRoomPixels = new bool[gridData.size.x, gridData.size.y];
        foreach (Screen s in GameController.Level.screens) UpdateVisiblePixels(s, false);
        foreach (RoomData.Tunnel t in currentRoom.tunnels) UpdateMapTunnel    (t, false);

        UpdateRoomShuffle();
        CalculateMapPath();
        UpdateMapInfo();
    }

    public RoomData GetRoomData(string roomName) {
        roomDatas.TryGetValue(roomName, out RoomData roomData);
        return roomData;
    }

    public void CalculateMapPath() {
        List<TunnelOrder> tunnelOrders     = new List<TunnelOrder>();
        List<TunnelOrder> gateTunnelOrders = new List<TunnelOrder>();
        foreach (RoomData.Tunnel t in currentRoom.tunnels) {
            RoomData.Tunnel next = t.connectedTunnel;
            if (next != null && next.visited && next.roomName != currentRoom.roomName) {
                if (next.gatePanelIndex != -1) gateTunnelOrders.Add(new TunnelOrder(next));
                else                           tunnelOrders    .Add(new TunnelOrder(next));
            }
        }

        bool[,] visited = new bool[mapMaster.texture.width, mapMaster.texture.height];
        for (int x = 0; x < roomTemplate.width; x++) {
            for (int y = 0; y < roomTemplate.height; y++) {
                if (!visibleRoomPixels[x, y])
                    continue;

                Coordinates o = new Coordinates(x, y) + currentRoom.mapOffset;
                visited[o.x, o.y] = true;
            }
        }

        Dictionary<int, List<Coordinates>> paths = new Dictionary<int, List<Coordinates>>();
        int currentIndex = 0;

        CalculatePath();
        // If any rooms from gate tunnels weren't reached by the initial path, add them after
        foreach (TunnelOrder to in gateTunnelOrders)
            tunnelOrders.Add(to);
        CalculatePath();

        void CalculatePath() {
            bool done = false;
            while (!done && tunnelOrders.Count > 0) {
                done = true;
                List<TunnelOrder> nextTunnels = new List<TunnelOrder>();
                foreach (TunnelOrder to in tunnelOrders) {
                    Coordinates[][] order = to.tunnel.mapOrder;
                    if (order == null)
                        continue;

                    foreach (Coordinates c in order[to.index]) {
                        if (c.x < 0) { // Indicator for tunnel index and triggering next room
                            RoomData.Tunnel next = GetRoomData(to.tunnel.roomName).tunnels[-c.x - 1].connectedTunnel;
                            if (next != null && next.visited)
                                nextTunnels.Add(new TunnelOrder(next));
                            continue;
                        }
                        // Path is done when no new pixels are reached
                        if (AddPath(currentIndex, c))
                            done = false;
                    }
                    if (++to.index < order.Length)
                        nextTunnels.Add(to);
                }

                tunnelOrders = nextTunnels;
                currentIndex++;
            }
        }

        bool AddPath(int index, Coordinates c) {
            if (visited[c.x, c.y]) return false;
                visited[c.x, c.y] = true;

            paths.TryGetValue(index, out List<Coordinates> list);
            if (list == null) paths.Add(index, new List<Coordinates>() { c });
            else              list.Add(c);

            return true;
        }

        mapPath = new Coordinates[paths.Count][];
        for (int i = 0; i < mapPath.Length; i++)
            mapPath[i] = paths[i].ToArray();
    }

    void UpdateRoomShuffle() {
        ApplyLevelState(false);
        
        List<Coordinates> shuffle = new List<Coordinates>();
        for (int x = 0; x < roomTexture.width; x++) {
            for (int y = 0; y < roomTexture.height; y++) {
                if (roomTexture.GetPixel(x, y).a > 0)
                    shuffle.Add(new Coordinates(x, y) + currentRoom.mapOffset);
            }
        }

        // Add dig pixels even if they haven't been revealed so we don't have to reshuffle everytime a dig block is broken
        List<Data> digDatas = GameController.Level.puzzleData[(int)PuzzleIndex.Dig];
        if (digDatas != null) {
            foreach (Data d in digDatas)
                shuffle.Add(Offset(d.blockData.coordinates));
        }

        // Add gate panel icon pixels that extend past ground outline
        foreach (RoomData.Tunnel t in currentRoom.tunnels) {
            if (t.gatePanelIndex == -1)
                continue;
            
            Coordinates o = Offset(t.connectedBlocks[1]);
            for (int x = -2; x <= 2; x++) {
                for (int y = -2; y <= 2; y++)
                    shuffle.Add(o + new Coordinates(x, y));
            }
        }

        if (shuffle.Count > 1) {
            for (int i = 0; i < shuffle.Count; i++) {
                int r = i;
                while (r == i)
                    r = Random.Range(0, shuffle.Count);

                Coordinates c = shuffle[i];
                shuffle[i] = shuffle[r];
                shuffle[r] = c;
            }
        }

        int groupFrames = 15;
        int groupStep   = shuffle.Count / groupFrames;
        List<List<Coordinates>> groups = new List<List<Coordinates>>();
        List<Coordinates> group = new List<Coordinates>();
        foreach (Coordinates c in shuffle) {
            group.Add(c);

            if (group.Count >= groupStep) {
                groups.Add(group);
                group = new List<Coordinates>();
            }
        }
        if (group.Count > 0)
            groups.Add(group);
        
        roomShuffle = new Coordinates[groups.Count][];
        for (int i = 0; i < roomShuffle.Length; i++)
            roomShuffle[i] = groups[i].ToArray();
    }
    // Get path reveal from each tunnel entrance so full map path can use any combination of room connections
    void UpdateRoomPaths() {
        GridSystem.GridData gridData = GameController.Grid.GetGridData();
        bool[,] empties = new bool[gridData.size.x, gridData.size.y];
        foreach (Coordinates c in GameController.Level.airDusts) {
            Coordinates l = c + currentRoom.localOffset;
            empties[l.x, l.y] = true;
        }
        
        foreach (RoomData.Tunnel t in currentRoom.tunnels) {
            if (t.local || !t.visited)
                continue;

            List<List<Coordinates>> mapOrder = new List<List<Coordinates>>();
            List<Coordinates> check = new List<Coordinates>();
            bool[,] visited = new bool[empties.GetLength(0), empties.GetLength(1)];

            if (t.gatePanelIndex == -1) {
                check.Add(t.connectedBlocks[0]);
                for (int j = t.connectedBlocks.Length - 1; j > 0; j--) {
                    Coordinates l = t.connectedBlocks[j] + currentRoom.localOffset;
                    mapOrder.Add(new List<Coordinates> { l + currentRoom.mapOffset });
                    visited[l.x, l.y] = true;
                }
                foreach (Coordinates a in Coordinates.AllDirection) {
                    Coordinates n = check[0] + a;
                    Coordinates l = n + currentRoom.localOffset;
                    if (empties[l.x, l.y])
                        check.Add(n);
                }
            }
            else {
                // Expand path reveal outward on gate panel icon
                Coordinates center = t.connectedBlocks[1];
                List<Coordinates> nextCheck = new List<Coordinates>();
                for (int i = 0; i <= 2; i++) {
                    List<Coordinates> nextMap = new List<Coordinates>();
                    for (int x = -i; x <= i; x++) {
                        for (int y = -i; y <= i; y++) {
                            Coordinates n = center + new Coordinates(x, y);
                            Coordinates l = n + currentRoom.localOffset;
                            if (visited[l.x, l.y]) continue;
                                visited[l.x, l.y] = true;

                            nextCheck.Add(n);
                            nextMap.Add(l + currentRoom.mapOffset);
                        }
                    }
                    mapOrder.Add(nextMap);
                }

                foreach (Coordinates c in t.connectedBlocks) {
                    Coordinates l = c + currentRoom.localOffset;
                    visited[l.x, l.y] = true;
                }

                foreach (Coordinates c in nextCheck) {
                    foreach (Coordinates f in Coordinates.FacingDirection) {
                        Coordinates n = c + f;
                        Coordinates l = n + currentRoom.localOffset;
                        if (empties[l.x, l.y] && !visited[l.x, l.y] && !check.Contains(n))
                            check.Add(n);
                    }
                }
            }

            while (check.Count > 0) {
                List<Coordinates> nextCheck = new List<Coordinates>();
                List<Coordinates> nextMap   = new List<Coordinates>();

                foreach (Coordinates c in check) {
                    if (!GameController.Grid.WithinBounds(c))
                        continue;

                    Coordinates l = c + currentRoom.localOffset;
                    if (visited[l.x, l.y]) continue;
                        visited[l.x, l.y] = true;

                    nextMap.Add(l + currentRoom.mapOffset);
                    if (!empties[l.x, l.y]) {
                        Data[] datas = GameController.Grid.GetData(c, Layer.Tunnel, Layer.Dig);
                        foreach (Data d in datas) {
                            if (CheckData(d))
                                break;
                        }

                        bool CheckData(Data d) {
                            if (d == null)
                                return false;

                            switch (d.layer) {
                                case Layer.Tunnel:
                                    int tunnelIndex    = -1;
                                    int gatePanelIndex = -1;
                                    bool local = false;
                                    bool tunnelVisited = false;
                                    for (int i = 0; i < currentRoom.tunnels.Length; i++) {
                                        if (d.blockData.connectedBlocks[0] != currentRoom.tunnels[i].connectedBlocks[0])
                                            continue;

                                        tunnelVisited  = currentRoom.tunnels[i].visited;
                                        gatePanelIndex = currentRoom.tunnels[i].gatePanelIndex;

                                        if (!tunnelVisited && gatePanelIndex == -1) {
                                            // Make sure path travels only through tunnel and not adjacent blocks
                                            int tunnelLength = Mathf.Min(UNVISITED_TUNNEL_LENGTH, d.blockData.connectedBlocks.Length) - 1;
                                            for (int j = 0; j < d.blockData.connectedBlocks.Length; j++) {
                                                if (c == d.blockData.connectedBlocks[j] && j >= tunnelLength)
                                                    return true;
                                            }
                                        }

                                        tunnelIndex = i;
                                        local = currentRoom.tunnels[i].local;
                                        break;
                                    }

                                    if (local) {
                                        if (d.blockData.IsPrimary()) {
                                            foreach (Coordinates a in Coordinates.AllDirection) {
                                                Coordinates n = l + a;
                                                if (empties[n.x, n.y])
                                                    nextCheck.Add(c + a);
                                            }
                                        }

                                        // Find connected tunnel
                                        foreach (Coordinates f in Coordinates.FacingDirection) {
                                            Coordinates n = c + f;
                                            if (!GameController.Grid.WithinBounds(n))
                                                continue;

                                            Data tunnelData = GameController.Grid.GetData(n, Layer.Tunnel);
                                            if (tunnelData != null) {
                                                if (tunnelData.blockData.connectedBlocks == d.blockData.connectedBlocks
                                                 || Coordinates.FacingDirection[tunnelData.blockData.facing] == -Coordinates.FacingDirection[d.blockData.facing])
                                                    nextCheck.Add(n);
                                            }
                                        }
                                    }
                                    else {
                                        if (gatePanelIndex != -1) {
                                            if (!d.blockData.IsPrimary())
                                                break;

                                            // Reveal gate panel icon in direction of tunnel entrance
                                            Coordinates mapDirection  = -Coordinates.FacingDirection[d.blockData.facing];
                                            Coordinates fillDirection = d.blockData.facing % 2 == 0 ? Coordinates.Up : Coordinates.Right;
                                            Coordinates[] panelCoords = new Coordinates[5];

                                            int index = 0;
                                            for (int i = -2; i <= 2; i++)
                                                panelCoords[index++] = d.blockData.coordinates + fillDirection * i;
                                            for (int i = -1; i <= 3; i++) {
                                                foreach (Coordinates p in panelCoords) {
                                                    Coordinates n = p + mapDirection * i;
                                                    Coordinates nl = n + currentRoom.localOffset;
                                                    if (visited[nl.x, nl.y]) continue;
                                                        visited[nl.x, nl.y] = true;

                                                    nextMap.Add(nl + currentRoom.mapOffset);

                                                    foreach (Coordinates f in Coordinates.FacingDirection) {
                                                        Coordinates nf = n + f;
                                                        Coordinates nfl = nf + currentRoom.localOffset;
                                                        if (visibleRoomPixels[nfl.x, nfl.y])
                                                            nextCheck.Add(nf);
                                                    }
                                                }
                                            }

                                            if (tunnelVisited)
                                                nextMap.Add(-(Coordinates.Zero + tunnelIndex + 1)); // Indicator for tunnel index and triggering next room
                                            break;
                                        }

                                        if (c != d.blockData.connectedBlocks[d.blockData.connectedBlocks.Length - 1]) {
                                            // Find next block of tunnel
                                            foreach (Coordinates f in Coordinates.FacingDirection) {
                                                Coordinates n = c + f;
                                                if (!GameController.Grid.WithinBounds(n))
                                                    continue;

                                                Data tunnelData = GameController.Grid.GetData(n, Layer.Tunnel);
                                                if (tunnelData != null) {
                                                    if (tunnelData.blockData.connectedBlocks == d.blockData.connectedBlocks)
                                                        nextCheck.Add(n);
                                                }
                                            }
                                        }
                                        else {
                                            if (tunnelVisited)
                                                nextMap.Add(-(Coordinates.Zero + tunnelIndex + 1)); // Indicator for tunnel index and triggering next room
                                        }
                                    }
                                    break;

                                case Layer.Dig:
                                    if (!visibleRoomPixels[l.x, l.y] || !d.blockData.destroyed)
                                        return true;

                                    if (IsEdge(d.blockData.coordinates)) {
                                        foreach (Coordinates a in Coordinates.AllDirection)
                                            nextCheck.Add(c + a);
                                    }
                                    else {
                                        foreach (Coordinates f in Coordinates.FacingDirection)
                                            nextCheck.Add(c + f);
                                    }
                                    break;
                            }

                            return true;
                        }
                    }
                    else {
                        if (!visibleRoomPixels[l.x, l.y])
                            continue;

                        foreach (Coordinates f in Coordinates.FacingDirection) {
                            Coordinates n = l + f;
                            if (roomTexture.GetPixel(n.x, n.y).a > 0)
                                nextCheck.Add(c + f);
                        }
                        if (Random.Range(0, 5) == 0) {
                            foreach (Coordinates d in Coordinates.DiagonalDirection) {
                                Coordinates n = l + d;
                                if (empties[n.x, n.y] && roomTexture.GetPixel(n.x, n.y).a > 0)
                                    nextCheck.Add(c + d);
                            }
                        }
                    }
                }

                check = nextCheck;
                if (nextMap.Count > 0)
                    mapOrder.Add(nextMap);
            }

            // Areas of the room that have been revealed but not reached by the reveal path get shuffled and added after,
            // some reasons for this being sections of a room separated by walls with tunnels that connect to different rooms,
            // or the sections are connected by dig blocks that haven't been broken yet
            List<Coordinates> shuffle = new List<Coordinates>();
            for (int x = 0; x < roomTemplate.width; x++) {
                for (int y = 0; y < roomTemplate.height; y++) {
                    if (roomTemplate.GetPixel(x, y).a == 0 || visited[x, y] || !visibleRoomPixels[x, y])
                        continue;

                    shuffle.Add(new Coordinates(x, y) + currentRoom.mapOffset);
                }
            }

            List<Data> digDatas = GameController.Level.puzzleData[(int)PuzzleIndex.Dig];
            if (digDatas != null) {
                foreach (Data d in digDatas) {
                    if (!d.blockData.destroyed && d.blockData.state == 0)
                        continue;

                    Coordinates l = d.blockData.coordinates + currentRoom.localOffset;
                    if (!visited[l.x, l.y] && visibleRoomPixels[l.x, l.y] && roomTemplate.GetPixel(l.x, l.y).a == 0)
                        shuffle.Add(l + currentRoom.mapOffset);
                }
            }
            
            foreach (RoomData.Tunnel tunnel in currentRoom.tunnels) {
                if (!tunnel.local)
                    continue;

                Coordinates l = tunnel.connectedBlocks[0] + currentRoom.localOffset;
                if (visited[l.x, l.y] || !visibleRoomPixels[l.x, l.y])
                    continue;
                
                if (tunnel.visited) {
                    for (int i = 0; i < tunnel.connectedBlocks.Length; i++)
                        AddShuffle(tunnel.connectedBlocks[i]);
                }
                else {
                    for (int i = 0; i < tunnel.connectedBlocks.Length; i++) {
                        if (i >= UNVISITED_TUNNEL_LENGTH)
                            break;

                        AddShuffle(tunnel.connectedBlocks[i]);
                    }
                }

                void AddShuffle(Coordinates c) {
                    Coordinates v = c + currentRoom.localOffset;
                    visited[v.x, v.y] = true;
                    shuffle.Add(v + currentRoom.mapOffset);
                }
            }

            if (shuffle.Count > 1) {
                for (int i = 0; i < shuffle.Count; i++) {
                    int r = i;
                    while (r == i)
                        r = Random.Range(0, shuffle.Count);

                    Coordinates c = shuffle[i];
                    shuffle[i] = shuffle[r];
                    shuffle[r] = c;
                }
            }
            
            int groupFrames = 5;
            int groupStep   = shuffle.Count / groupFrames;
            List<Coordinates> group = new List<Coordinates>();
            Dictionary<int, List<RoomData.Tunnel>> tunnels = new Dictionary<int, List<RoomData.Tunnel>>();
            Color tunnelColor = Assets.MAP_COLORS["Tunnel"];
            foreach (Coordinates c in shuffle) {
                // Tunnels that have been revealed but not reached by the reveal path should still propogate along the tunnel
                // instead of being randomly shuffled
                Coordinates l = c - currentRoom.mapOffset;
                if (roomTexture.GetPixel(l.x, l.y) == tunnelColor && !visited[l.x, l.y]) {
                    RoomData.Tunnel tunnel = currentRoom.GetTunnel(l - currentRoom.localOffset);
                    int index = mapOrder.Count - 1;
                    tunnels.TryGetValue(index, out List<RoomData.Tunnel> list);
                    if (list == null) tunnels.Add(index, new List<RoomData.Tunnel>() { tunnel });
                    else              list   .Add(tunnel);
                }

                group.Add(c);

                if (group.Count >= groupStep) {
                    mapOrder.Add(group);
                    group = new List<Coordinates>();
                }
            }
            if (group.Count > 0)
                mapOrder.Add(group);

            // Propogate the path through unreached tunnels as if they weren't shuffled
            Dictionary<int, List<Coordinates>> tunnelPaths = new Dictionary<int, List<Coordinates>>();
            foreach (var kvp in tunnels) {
                List<RoomData.Tunnel> tunnelList = kvp.Value;
                foreach (RoomData.Tunnel tunnel in tunnelList) {
                    if (tunnel.visited) {
                        for (int i = 0; i < tunnel.connectedBlocks.Length; i++)
                            AddTunnelPath(kvp.Key + i, tunnel.connectedBlocks[i]);

                        for (int i = 0; i < currentRoom.tunnels.Length; i++) {
                            if (tunnel.connectedBlocks == currentRoom.tunnels[i].connectedBlocks)
                                AddTunnelPath(kvp.Key + tunnel.connectedBlocks.Length, -(Coordinates.Zero + i + 1), true);
                        }
                    }
                    else {
                        for (int i = 0; i < tunnel.connectedBlocks.Length; i++) {
                            if (i >= UNVISITED_TUNNEL_LENGTH)
                                break;

                            AddTunnelPath(kvp.Key + i, tunnel.connectedBlocks[i]);
                        }
                    }
                }

                void AddTunnelPath(int index, Coordinates c, bool nextTunnel = false) {
                    if (!nextTunnel)
                        c = Offset(c);

                    tunnelPaths.TryGetValue(index, out List<Coordinates> list);
                    if (list == null) tunnelPaths.Add(index, new List<Coordinates>() { c });
                    else              list.Add(c);
                }
            }

            // Insert tunnel paths at the point their shuffled entrances are revealed
            foreach (var kvp in tunnelPaths) {
                if (kvp.Key >= mapOrder.Count) {
                    for (int i = mapOrder.Count; i <= kvp.Key; i++)
                        mapOrder.Add(new List<Coordinates>());
                }
                foreach (Coordinates c in kvp.Value)
                    mapOrder[kvp.Key].Add(c);
            }

            t.mapOrder = new Coordinates[mapOrder.Count][];
            for (int i = 0; i < t.mapOrder.Length; i++)
                t.mapOrder[i] = mapOrder[i].ToArray();
        }
    }
    void UpdateMapInfo() {
        List<MapInfo> infoList = new List<MapInfo>();
        foreach (Coordinates[] mapPixels in roomShuffle) infoList.Add(new MapInfo(mapPixels));
        foreach (Coordinates[] mapPixels in mapPath    ) infoList.Add(new MapInfo(mapPixels));

        unlockIndex = roomShuffle.Length;

        for (int i = unlockIndex; i < infoList.Count; i++) {
            int a = i + 1;
            int b = i + 2;
            if (a < infoList.Count) infoList[i].revealPixelsA = infoList[a].mapPixels;
            if (b < infoList.Count) infoList[i].revealPixelsB = infoList[b].mapPixels;
        }

        int zoomStart = unlockIndex + 1;
        for (int i = unlockIndex; i <= zoomStart + CAMERA_ZOOM_FRAMES; i++) {
            if (i >= infoList.Count)
                infoList.Add(new MapInfo(null));
        }

        // Fade in map and background
        int mapColorEnd = unlockIndex / 2;
        Color fromColor = Color.white; fromColor.a = 0;
        Color toColor   = Color.white;
        for (int i = 0; i <= mapColorEnd; i++) {
            Color color = Color.Lerp(fromColor, toColor, (float)i / mapColorEnd);
            infoList[i].mapColor = infoList[i + mapColorEnd].bgColor = color;
            infoList[i].bgColor  = fromColor;
        }

        mapInfo = infoList.ToArray();
        displayTime = Mathf.Clamp(Mathf.Sqrt(mapInfo.Length) / 8, DISPLAY_TIME_MIN, DISPLAY_TIME_MAX);
    }

    public void EnableMap(bool enable) {
        if (mapUI.activeSelf == enable)
            return;

        if (enable) {
            ApplyLevelState(false);
            finishedShowing = false;
            currentMapSpeed = SHOW_SPEED;
            mapHolder.transform.position = GameController.currentScreen.transform.position;
            Vector3 offset = GameController.GetVector(originOffset + currentRoom.mapOffset + currentRoom.localOffset + GameController.GetCoordinates(GameController.currentScreen.transform.position));
            mapAnchor.transform.localPosition = startPos = -offset;

            cameraSize = cam.orthographicSize;
            int zoomStart = unlockIndex + 1;
            int zoomEnd   = zoomStart + CAMERA_ZOOM_FRAMES;
            for (int i = 0;       i <= unlockIndex;        i++) mapInfo[i            ].cameraSize = cameraSize;
            for (int i = 0;       i <= CAMERA_ZOOM_FRAMES; i++) mapInfo[i + zoomStart].cameraSize = Mathf.Lerp(cameraSize, CAMERA_SIZE_DEFAULT, GameController.GetCurve((float)i / CAMERA_ZOOM_FRAMES));
            for (int i = zoomEnd; i <  mapInfo.Length;     i++) mapInfo[i            ].cameraSize = CAMERA_SIZE_DEFAULT;
        }

        GameController.canPause = !enable;
        mapUI.SetActive(enable);
    }
    void ReleaseMap() {
        currentMapSpeed = HIDE_SPEED;
        if (mapIndex >= mapInfo.Length - 1)
            finishedShowing = true;

        releasePos   = mapAnchor.transform.localPosition;
        releaseIndex = mapIndex - (unlockIndex + CAMERA_ZOOM_FRAMES);

        Color gridColor = Color.white; gridColor.a = 0;
        gridSpriteRenderer.color = gridColor;
        mapButtons.SetActive(false);

        Clear(mapInfo[mapIndex].mapPixels    );
        Clear(mapInfo[mapIndex].revealPixelsA);
        Clear(mapInfo[mapIndex].revealPixelsB);

        void Clear(Coordinates[] pixels) {
            if (pixels != null) {
                foreach (Coordinates c in pixels)
                    ColorMap(c, BG_COLOR);
            }
        }
    }

    public void ShowMap(bool show) {
        if (GameController.instantUI) ShowMapInstant (show);
        else                          ShowMapAnimated(show);
    }
    void ShowMapInstant(bool show) {
        showMap = show;
        EnableMap(show);

        if (show && mapIndex == 0) {
            mapIndex = mapInfo.Length - 1;
            UpdateMapWithMapInfo(mapInfo[mapIndex]);
            return;
        }
        if (!show && mapIndex == mapInfo.Length - 1) {
            mapIndex = 0;
            Color gridColor = Color.white; gridColor.a = 0;
            gridSpriteRenderer.color = gridColor;
            mapButtons.SetActive(false);
            UpdateMapWithMapInfo(mapInfo[mapIndex]);
            return;
        }
    }
    void ShowMapAnimated(bool show) {
        if (showMap && !show)
            ReleaseMap();

        showMap = show;
        
        if (show) {
            EnableMap(true);
            if (!finishedShowing)
                currentMapSpeed = SHOW_SPEED;

            mapTime += Time.deltaTime * currentMapSpeed;
            if (mapTime > displayTime)
                mapTime = displayTime;
        }
        else {
            mapTime -= Time.deltaTime * currentMapSpeed;
            if (mapTime <= 0) {
                mapTime  = 0;
                EnableMap(false);
            }

            // Automatically move map back to initial position
            float posIndex = mapIndex - (unlockIndex + CAMERA_ZOOM_FRAMES);
            mapAnchor.transform.localPosition = releaseIndex > 0 ? Vector3.Lerp(startPos, releasePos, GameController.GetCurve(posIndex / releaseIndex)) : startPos;
        }
        
        int nextIndex = Mathf.RoundToInt(Mathf.Lerp(0, mapInfo.Length, mapTime / displayTime));
        if (nextIndex == mapInfo.Length)
            return;

        MapInfo mi = mapInfo[mapIndex];
        if (show) {
            while (mapIndex <= nextIndex) {
                ColorMapPixels(mi.mapPixels);
                ColorPixels(mi.revealPixelsA, REVEAL_A_COLOR);
                ColorPixels(mi.revealPixelsB, REVEAL_B_COLOR);

                if (++mapIndex >= mapInfo.Length) {
                    mapIndex = mapInfo.Length - 1;
                    break;
                }
                mi = mapInfo[mapIndex];
            }
        }
        else {
            while (mapIndex >= nextIndex) {
                ColorPixels(mi.mapPixels, BG_COLOR);

                if (--mapIndex < 0) {
                    mapIndex = 0;
                    break;
                }
                mi = mapInfo[mapIndex];
            }
        }

        void ColorMapPixels(Coordinates[] pixels) {
            if (pixels == null)
                return;

            foreach (Coordinates c in pixels)
                ColorMap(c, mapMaster.texture.GetPixel(c.x, c.y));
        }
        void ColorPixels(Coordinates[] pixels, Color32 color) {
            if (pixels == null)
                return;

            foreach (Coordinates c in pixels)
                ColorMap(c, color);
        }

        UpdateMapWithMapInfo(mi);
        mapSprite.texture.Apply();
    }
    void UpdateMapWithMapInfo(MapInfo mapInfo) {
        bgSpriteRenderer .color = mapInfo.bgColor;
        mapSpriteRenderer.color = mapInfo.mapColor;
        cam.orthographicSize = cameraSize = mapInfo.cameraSize;
    }

    public void ApplyLevelState(bool exitState) {
        for (int x = 0; x < roomTexture.width; x++) {
            for (int y = 0; y < roomTexture.height; y++) {
                Color32 color = roomTexture.GetPixel(x, y);
                if (color.a == 0)
                    continue;

                Coordinates o = new Coordinates(x, y) + currentRoom.mapOffset;
                ColorMapMaster(o, color);
            }
        }

        Color32 emptyColor = Assets.MAP_COLORS["Empty"];
        for (int i = 0; i < GameController.Level.puzzleData.Length; i++) {
            if (GameController.Level.puzzleData[i] == null)
                continue;

            PuzzleIndex puzzleIndex = (PuzzleIndex)i;
            foreach (Data d in GameController.Level.puzzleData[i]) {
                switch (puzzleIndex) {
                    case PuzzleIndex.Rock:
                        Color32 rockColor = Assets.MAP_COLORS["Rock"];
                        foreach (Coordinates c in d.blockData.connectedBlocks)
                            TryColorMap(c, rockColor);
                        break;

                    case PuzzleIndex.Crystal:
                        if (d.blockData.destroyed)
                            continue;

                        Color32 crystalColor = GameController.GetGameColor(GameController.ConvertColorNameToIndex(d.blockData.blockName));
                        foreach (Coordinates c in d.blockData.connectedBlocks)
                            TryColorMap(c, crystalColor);
                        break;

                    case PuzzleIndex.Button:
                        if (!DrawButton(d))
                            break;

                        Color32 buttonColor = GameController.GetGameColor(GameController.ConvertColorNameToIndex(d.blockData.blockName)); buttonColor.a = Assets.MAP_BUTTON_ALPHA;
                        TryColorMap(d.blockData.coordinates, buttonColor);
                        break;

                    case PuzzleIndex.Piston:
                        if (d.blockData.state == (int)Activation.On)
                            TryColorMap(d.blockData.coordinates, Assets.MAP_COLORS["Piston"]);
                        break;

                    case PuzzleIndex.Dig:
                        if (d.blockData.destroyed)
                            TryColorMap(d.blockData.coordinates, Assets.MAP_COLORS["Dig"]);
                        else {
                            if (d.blockData.state == 0) {
                                if (d.blockData.facing != -1 && GameController.Player.playerData.abilities[(int)AbilityIndex.Dig] && IsEdge(d.blockData.coordinates))
                                    TryColorMap(d.blockData.coordinates, Assets.MAP_COLORS["DigAlt"]);
                            }
                            else
                                TryColorMap(d.blockData.coordinates, Assets.MAP_COLORS[d.blockData.facing == -1 ? "Ground" : "DigAlt"]);
                        }
                        break;

                    case PuzzleIndex.Gate:
                        if (!IsVisible(d.blockData.coordinates))
                            break;

                        Color32 colorA = Assets.MAP_COLORS["Gate"];
                        Color32 colorB = GameController.GetGameColor(ColorIndex.Fragment);
                        if (d.blockData.state == (int)Activation.On) {
                            Color32 temp = colorA;
                            colorA = colorB;
                            colorB = temp;
                        }

                        Coordinates o = Offset(d.blockData.coordinates);
                        if (d.layer == Layer.Misc) {
                            Panel p = (Panel)d.levelBlock.GetBlockItem("Panel").script;
                            Coordinates gatePanelIconCoord = GameController.GetCoordinates(Panel.GATE_PANEL_ICON_POSITIONS[p.gatePanelIndex], false);
                            for (int x = -2; x <= 2; x++) {
                                for (int y = -2; y <= 2; y++) {
                                    Coordinates n = new Coordinates(x, y);
                                    if (Mathf.Abs(x) != 2 && Mathf.Abs(y) != 2) ColorMapMaster(o + n, Assets.MAP_COLORS[n == gatePanelIconCoord ? "GatePanelIcon" : "GatePanel"]);
                                    else                                        ColorMapMaster(o + n, colorA);
                                }
                            }
                            UpdateMapGateTunnelHub(true);
                        }
                        else {
                            ColorMapMaster(o, colorB);
                            foreach (Coordinates a in Coordinates.AllDirection)
                                ColorMapMaster(o + a, colorA);
                        }
                        break;

                    case PuzzleIndex.GateSlot:
                        TryColorMap(d.blockData.coordinates, d.blockData.state == (int)Activation.On ? GameController.GetGameColor(ColorIndex.Fragment) : Assets.MAP_COLORS["Gate"]);
                        break;

                    case PuzzleIndex.Collectable:
                        if (d.blockData.destroyed)
                            continue;

                        int colorIndex = GameController.ConvertColorNameToIndex(d.blockData.blockName);
                        switch (d.blockData.blockName) {
                            case "CollectSong":
                                TryColorMap(d.blockData.coordinates, Assets.MAP_COLORS["Song"]);
                                break;

                            case "CollectLength":
                            case "CollectDig"   :
                                TryColorMap(d.blockData.coordinates, Assets.MAP_COLORS[colorIndex + ""]);
                                break;

                            default:
                                TryColorMap(d.blockData.coordinates, GameController.GetGameColor(colorIndex));
                                break;
                        }
                        break;
                }

                void TryColorMap(Coordinates c, Color32 color) {
                    if (IsVisible(c))
                        ColorMapMaster(Offset(c), color);
                }
            }
        }

        // Check for locked tunnel doors
        foreach (RoomData.Tunnel t in currentRoom.tunnels) {
            if (t.gatePanelIndex != -1 || !IsVisible(t.connectedBlocks[0]))
                continue;

            Data panelData = GameController.Grid.GetData(t.connectedBlocks[1], Layer.Misc);
            ColorMapMaster(Offset(t.connectedBlocks[0]), Assets.MAP_COLORS[(panelData == null || panelData.blockData.state != (int)Activation.Off) ? "Tunnel" : "TunnelAlt"]);
        }

        if (exitState) {
            UpdateRoomPaths();
            UpdateColorPixels();
            UpdateMapGateTunnelHub(false);
            CleanUp();
        }
        else
            BlinkPlayer(GameController.instantUI ? mapMaster.texture : mapSprite.texture);

        mapMaster.texture.Apply();
    }

    void BlinkPlayer(Texture2D texture) {
        // Don't display player pixels that are inside a tunnel
        List<BlinkPixel> playerPixels = new List<BlinkPixel>();
        bool skipTunnel = false;
        foreach (PlayerController.Worm w in GameController.Player.worm) {
            if (!skipTunnel) {
                foreach (RoomData.Tunnel t in currentRoom.tunnels) {
                    if (!t.local) {
                        foreach (Coordinates c in t.connectedBlocks) {
                            if (w.data.blockData.coordinates == c) {
                                skipTunnel = true;
                                break;
                            }
                        }
                    }
                    if (skipTunnel)
                        break;
                }
            }

            if (GameController.Grid.GetData(w.data.blockData.coordinates, Layer.Tunnel) != null) {
                if (skipTunnel) break;
                else            continue;
            }

            Coordinates o = Offset(w.data.blockData.coordinates);
            Color color = w.spriteRenderer.color;
            playerPixels.Add(new BlinkPixel(color, mapMaster.texture.GetPixel(o.x, o.y), o));
            ColorMapMaster(o, color);
        }
        StartCoroutine(BlinkPixels(playerPixels, texture));
    }
    IEnumerator BlinkPixels(List<BlinkPixel> blinkPixels, Texture2D texture) {
        yield return null;

        bool blink = true;
        while (mapUI.activeSelf) {
            if (blink) { foreach (BlinkPixel bp in blinkPixels) ColorBlinkPixel(bp.coordinates, bp.pixelColor); }
            else       { foreach (BlinkPixel bp in blinkPixels) ColorBlinkPixel(bp.coordinates, bp.mapColor  ); }
            texture.Apply();

            float time = 0;
            while (time < 0.5f) {
                if (!mapUI.activeSelf)
                    yield break;

                time += Time.deltaTime;
                yield return null;
            }
            blink = !blink;
        }

        void ColorBlinkPixel(Coordinates c, Color32 color) {
            if (texture.GetPixel(c.x, c.y).a > 0)
                texture.SetPixel(c.x, c.y, color);
        }
    }

    Coordinates Offset(Coordinates c) {
        return currentRoom.mapOffset + currentRoom.localOffset + c;
    }

    void ColorMap(Coordinates c, Color color) {
        mapSprite.texture.SetPixel(c.x, c.y, color);
    }
    void ColorMapMaster(Coordinates c, Color color) {
        mapMaster.texture.SetPixel(c.x, c.y, color);
    }

    void UpdateBorders(Coordinates c) {
        Vector2 posA = mapBorders[0].transform.localPosition;
        if (c.y > posA.y) posA.y = c.y;
        if (c.x < posA.x) posA.x = c.x;
        mapBorders[0].transform.localPosition = posA;

        Vector2 posB = mapBorders[1].transform.localPosition;
        if (c.y < posB.y) posB.y = c.y;
        if (c.x > posB.x) posB.x = c.x;
        mapBorders[1].transform.localPosition = posB;
    }
    public void UpdateVisiblePixels(Screen screen, bool updateRoom) {
        if (!screen.screenData.visited || roomTemplate == null)
            return;

        int[] b = screen.screenData.bounds;
        for (int x = b[2]; x <= b[3]; x++) {
            for (int y = b[1]; y <= b[0]; y++) {
                Coordinates l = new Coordinates(x, y) + currentRoom.localOffset;
                Color32 color = roomTemplate.GetPixel(l.x, l.y);
                if (color.a > 0) {
                    roomTexture.SetPixel(l.x, l.y, color);
                    UpdateBorders(l + currentRoom.mapOffset);
                }
                visibleRoomPixels[l.x, l.y] = true;
            }
        }

        Color emptyColor = Assets.MAP_COLORS["Empty"];
        Color edgeColor  = Assets.MAP_COLORS["Edge" ];
        for (int x = b[2]; x <= b[3]; x++) {
            CheckEdge(new Coordinates(x, b[0]));
            CheckEdge(new Coordinates(x, b[1]));
        }
        for (int y = b[1]; y <= b[0]; y++) {
            CheckEdge(new Coordinates(b[2], y));
            CheckEdge(new Coordinates(b[3], y));
        }

        void CheckEdge(Coordinates c) {
            Coordinates l = c + currentRoom.localOffset;
            if (roomTexture.GetPixel(l.x, l.y) != emptyColor)
                return;

            List<Coordinates> edges = new List<Coordinates>();
            foreach (Coordinates f in Coordinates.FacingDirection) {
                Coordinates n = l + f;
                if (visibleRoomPixels[n.x, n.y])
                    continue;

                edges.Add(n);
            }

            if (edges.Count == 0)
                return;

            if (Mathf.Abs(c.x) % 2 == Mathf.Abs(c.y) % 2) {
                foreach (Coordinates e in edges) {
                    roomTexture.SetPixel(e.x, e.y, edgeColor);
                    visibleRoomPixels[e.x, e.y] = true;
                }
            }
            else
                roomTexture.SetPixel(l.x, l.y, edgeColor);
        }

        foreach (RoomData.Tunnel t in currentRoom.tunnels) {
            if (!t.visited)
                UpdateMapTunnel(t, false);
        }

        if (updateRoom) {
            UpdateRoomShuffle();
            UpdateMapInfo();
        }
    }
    bool IsVisible(Coordinates c) {
        Coordinates l = c + currentRoom.localOffset;
        return visibleRoomPixels[l.x, l.y];
    }
    bool IsEdge(Coordinates c) {
        foreach (Coordinates f in Coordinates.FacingDirection) {
            Coordinates n = c + f;
            if (!GameController.Grid.WithinBounds(n))
                return false;

            Data d = GameController.Grid.GetData(n, Layer.Block);
            if (d == null)
                return true;
        }
        return false;
    }

    bool DrawButton(Data buttonData) {
        Data blockData = GameController.Grid.GetData(buttonData.blockData.coordinates, Layer.Block);
        if (blockData != null && blockData.HasTag(Tag.Push))
            return false;

        Data pistonData = GameController.Grid.GetData(buttonData.blockData.coordinates, Layer.Piston);
        if (pistonData != null && pistonData.blockData.state == (int)Activation.On)
            return false;

        return true;
    }

    // Show full tunnel if visited, else show a shortened faded version
    public void UpdateMapTunnel(RoomData.Tunnel tunnel, bool updateRoom) {
        if (tunnel.gatePanelIndex != -1)
            return;

        Color32 tunnelColor = Assets.MAP_COLORS["Tunnel"];
        if (tunnel.visited) {
            foreach (Coordinates c in tunnel.connectedBlocks) {
                Coordinates l = c + currentRoom.localOffset;
                roomTexture.SetPixel(l.x, l.y, tunnelColor);
                visibleRoomPixels[l.x, l.y] = true;
            }
        }
        else {
            if (!IsVisible(tunnel.connectedBlocks[0]))
                return;

            int tunnelIndex = 0;
            Color32 endColor = tunnelColor; endColor.a = 0;
            foreach (Coordinates c in tunnel.connectedBlocks) {
                Coordinates l = c + currentRoom.localOffset;
                if (tunnelIndex < UNVISITED_TUNNEL_LENGTH) {
                    roomTexture.SetPixel(l.x, l.y, Color32.Lerp(tunnelColor, endColor, (float)tunnelIndex++ / UNVISITED_TUNNEL_LENGTH));
                    visibleRoomPixels[l.x, l.y] = true;
                }
                else
                    break;
            }
        }

        if (updateRoom) {
            UpdateRoomShuffle();
            UpdateMapInfo();
        }
    }
    // Since the visuals for gate panels in the hub can change without being in that room
    // they have to be updated when their pair is (de)activated
    void UpdateMapGateTunnelHub(bool drawOnly) {
        int gatePanelIndex = -1;
        foreach (RoomData.Tunnel t in currentRoom.tunnels) {
            if (t.gatePanelIndex != -1) {
                gatePanelIndex = t.gatePanelIndex;
                break;
            }
        }
        if (gatePanelIndex == -1)
            return;

        RoomData hub = GetRoomData("GateTunnelHub");
        if (currentRoom == hub)
            return;

        int fragmentIndex = (int)ColorIndex.Fragment;
        Coordinates hubPanel = Coordinates.Zero;
        foreach (RoomData.Tunnel t in hub.tunnels) {
            if (t.gatePanelIndex == gatePanelIndex)
                hubPanel = t.connectedBlocks[1];
        }
        if (GameController.Player.playerData.gateTunnels[gatePanelIndex]) {
            if (!drawOnly) {
                if (hub.colorPixels == null)
                    hub.colorPixels = new List<Coordinates>[GameController.COLLECTABLE_TYPES];
                if (hub.colorPixels[fragmentIndex] == null)
                    hub.colorPixels[fragmentIndex] = new List<Coordinates>();
            }

            Color32 fragmentColor = GameController.GetGameColor(ColorIndex.Fragment);
            for (int x = -2; x <= 2; x++) {
                for (int y = -2; y <= 2; y++) {
                    if (Mathf.Abs(x) == 2 || Mathf.Abs(y) == 2) {
                        Coordinates o = HubOffset(hubPanel + new Coordinates(x, y));
                        ColorMapMaster(o, fragmentColor);

                        if (!drawOnly && !hub.colorPixels[fragmentIndex].Contains(o))
                            hub.colorPixels[fragmentIndex].Add(o);
                    }
                }
            }
        }
        else {
            if (!drawOnly && (hub.colorPixels == null || hub.colorPixels[fragmentIndex] == null))
                return;

            for (int x = -2; x <= 2; x++) {
                for (int y = -2; y <= 2; y++) {
                    if (Mathf.Abs(x) == 2 || Mathf.Abs(y) == 2) {
                        Coordinates o = HubOffset(hubPanel + new Coordinates(x, y));
                        ColorMapMaster(o, Assets.MAP_COLORS["Gate"]);

                        if (!drawOnly)
                            hub.colorPixels[fragmentIndex].Remove(o);
                    }
                }
            }

            if (!drawOnly && hub.colorPixels[fragmentIndex].Count == 0)
                hub.colorPixels[fragmentIndex] = null;
        }

        Coordinates HubOffset(Coordinates c) {
            return c + hub.localOffset + hub.mapOffset;
        }
    }
    void UpdateColorPixels() {
        currentRoom.colorPixels       = new List<Coordinates>[GameController.COLLECTABLE_TYPES ];
        currentRoom.buttonColorPixels = new List<Coordinates>[GameController.CRYSTAL_COLORS + 1];
        bool setNull       = true;
        bool buttonSetNull = true;

        for (int i = 0; i < GameController.Level.puzzleData.Length; i++) {
            if (GameController.Level.puzzleData[i] == null)
                continue;

            PuzzleIndex puzzleIndex = (PuzzleIndex)i;
            int colorIndex = -1;
            switch (puzzleIndex) {
                case PuzzleIndex.Gate    :
                case PuzzleIndex.GateSlot: colorIndex = (int)ColorIndex.Fragment; break;
                case PuzzleIndex.Dig     : colorIndex = (int)ColorIndex.Dig;      break;
            }

            foreach (Data d in GameController.Level.puzzleData[i]) {
                switch (puzzleIndex) {
                    case PuzzleIndex.Crystal:
                        if (d.blockData.destroyed)
                            continue;

                        colorIndex = GameController.ConvertColorNameToIndex(d.blockData.blockName);
                        foreach (Coordinates c in d.blockData.connectedBlocks)
                            AddPixel(colorIndex, c);
                        break;

                    case PuzzleIndex.Button:
                        if (!DrawButton(d))
                            break;

                        colorIndex = GameController.ConvertColorNameToIndex(d.blockData.blockName);
                        AddButtonPixel(colorIndex, d.blockData.coordinates);
                        break;

                    case PuzzleIndex.Dig:
                        if (!d.blockData.destroyed && d.blockData.facing != -1 && IsEdge(d.blockData.coordinates))
                            AddPixel(colorIndex, d.blockData.coordinates);
                        break;

                    case PuzzleIndex.Gate:
                        if (d.blockData.state == (int)Activation.On) {
                            if (d.layer == Layer.Misc) {
                                for (int x = -2; x <= 2; x++) {
                                    for (int y = -2; y <= 2; y++) {
                                        if (Mathf.Abs(x) == 2 || Mathf.Abs(y) == 2)
                                            AddPixel(colorIndex, d.blockData.coordinates + new Coordinates(x, y));
                                    }
                                }
                            }
                            else {
                                foreach (Coordinates a in Coordinates.AllDirection)
                                    AddPixel(colorIndex, d.blockData.coordinates + a);
                            }
                        }
                        else {
                            if (d.layer == Layer.Block)
                                AddPixel(colorIndex, d.blockData.coordinates);
                        }
                        break;

                    case PuzzleIndex.GateSlot:
                        if (d.blockData.state == (int)Activation.On)
                            AddPixel(colorIndex, d.blockData.coordinates);
                        break;

                    case PuzzleIndex.Collectable:
                        if (d.blockData.destroyed)
                            continue;

                        AddPixel(GameController.ConvertColorNameToIndex(d.blockData.blockName), d.blockData.coordinates);
                        break;
                }
            }
        }

        if (setNull      ) currentRoom.colorPixels       = null;
        if (buttonSetNull) currentRoom.buttonColorPixels = null;

        void AddPixel(int index, Coordinates c) {
            setNull = false;

            if (currentRoom.colorPixels[index] == null)
                currentRoom.colorPixels[index] = new List<Coordinates>();
            currentRoom.colorPixels[index].Add(Offset(c));
        }
        void AddButtonPixel(int index, Coordinates c) {
            buttonSetNull = false;

            if (currentRoom.buttonColorPixels[index] == null)
                currentRoom.buttonColorPixels[index] = new List<Coordinates>();
            currentRoom.buttonColorPixels[index].Add(Offset(c));
        }
    }

    public void RevealMapDigEntrances() {
        int index = (int)ColorIndex.Dig;
        Color digColor = Assets.MAP_COLORS["DigAlt"];
        foreach (RoomData rd in mapData.roomDatas) {
            if (rd.colorPixels == null || rd.colorPixels[index] == null)
                continue;

            foreach (Coordinates c in rd.colorPixels[index])
                mapMaster.texture.SetPixel(c.x, c.y, digColor);
        }
    }

    public void UpdateMapUI() {
        mapMasterSpriteRenderer.gameObject.SetActive( GameController.instantUI);
        mapSpriteRenderer      .gameObject.SetActive(!GameController.instantUI);
    }

    public void CleanUp() {
        currentRoom = null;
        roomTexture = roomTemplate = null;
        roomShuffle = mapPath = null;
        visibleRoomPixels = null;
    }
    public void SaveMap() {
        ApplyLevelState(true);
        mapData.map = mapMaster.texture.EncodeToPNG();
        GameController.SaveMap(mapData, GameController.currentSave);
    }
}
