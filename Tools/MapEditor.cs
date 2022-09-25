#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public class MapEditor : MonoBehaviour {
    public GameObject mapHolder;
    public bool showMapInfo = true;

    void Awake() {
        if (GameController.initialized)
            return;

        SpriteRenderer[] mapSprites = GetMapSprites();
        if (mapSprites == null)
            return;

        string levelName = GameObject.FindGameObjectWithTag("Level").name;
        foreach (SpriteRenderer sr in mapSprites) {
            if (sr.transform.parent.name == levelName)
                sr.enabled = false;
        }
    }

    [ContextMenu("Generate")]
    public void Generate() {
        GameController.InitData();
        MapController.MapData defaultMap = GameController.LoadMap("Default", 0);
        if (defaultMap.roomDatas == null) {
            Debug.LogError("Default Rooms Missing");
            return;
        }

        List<string> reactivate = new List<string>();
        if (mapHolder != null) {
            SpriteRenderer[] mapSprites = GetMapSprites();
            if (mapSprites != null) {
                foreach (SpriteRenderer sr in mapSprites) {
                    GameObject roomObject = sr.transform.parent.gameObject;
                    string mapName = roomObject.transform.parent.name;
                    if (mapName == "Default")
                        continue;

                    if (!reactivate.Contains(mapName) && roomObject.transform.parent.gameObject.activeSelf)
                         reactivate.Add(mapName);
                }
            }
            DestroyImmediate(mapHolder);
        }
        mapHolder = new GameObject("Maps");
        mapHolder.transform.SetParent(transform);

        Dictionary<string, GameObject> defaultRooms = new Dictionary<string, GameObject>();
        GameObject defaultHolder = new GameObject("Default");
        defaultHolder.transform.SetParent(mapHolder.transform);
        defaultHolder.SetActive(false);

        foreach (MapController.RoomData rd in defaultMap.roomDatas) {
            Sprite s = Assets.GetSprite("Maps/Rooms/" + rd.roomName);
            if (s == null)
                continue;

            GameObject roomObject = new GameObject(rd.roomName);
            roomObject.tag = "Map";
            roomObject.transform.SetParent(mapHolder.transform);
            roomObject.SetActive(false);

            GameObject roomMap = new GameObject("Map");
            roomMap.transform.SetParent(roomObject.transform);

            Texture2D t = new Texture2D(s.texture.width, s.texture.height);
            for (int x = 0; x < t.width; x++) {
                for (int y = 0; y < t.height; y++)
                    t.SetPixel(x, y, s.texture.GetPixel(x, y));
            }
            t.filterMode = FilterMode.Point;
            t.Apply();
            roomMap.AddComponent<SpriteRenderer>().sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 1);
            roomMap.transform.localPosition = new Vector2(t.width / 2.0f - 0.5f, t.height / 2.0f - 0.5f);

            defaultRooms.Add(rd.roomName, roomObject);
        }

        GameObject[] sortedRooms = new GameObject[defaultRooms.Count];
        defaultRooms.Values.CopyTo(sortedRooms, 0);
        Array.Sort(sortedRooms, (a, b) => a.name.CompareTo(b.name));
        foreach (GameObject go in sortedRooms)
            go.transform.SetParent(defaultHolder.transform);
        defaultHolder.transform.localScale = new Vector3(GameController.GRID_SIZE, GameController.GRID_SIZE, 1);

        string levelName = GameObject.FindGameObjectWithTag("Level").name;
        List<MapController.MapData> mapDatas = GameController.LoadAllMaps();
        foreach (MapController.MapData md in mapDatas) {
            GameObject mapObject = Instantiate(defaultHolder, mapHolder.transform);
            mapObject.name = md.mapName;
            Vector3 mapOffset = Vector3.zero;

            if (md.roomDatas != null) {
                Transform[] children = mapObject.GetComponentsInChildren<Transform>(true);

                foreach (MapController.RoomData rd in md.roomDatas) {
                    if (!defaultRooms.ContainsKey(rd.roomName)) {
                        Debug.LogError("Missing Default: " + rd.roomName);
                        continue;
                    }

                    foreach (Transform t in children) {
                        if (!t.CompareTag("Map"))
                            continue;

                        if (t.name == rd.roomName) {
                            t.gameObject.SetActive(true);
                            t.localPosition = GameController.GetVector(rd.mapOffset);
                            if (rd.roomName == levelName)
                                mapOffset = -(t.localPosition + (Vector3)GameController.GetVector(rd.localOffset));
                            break;
                        }
                    }
                }
            }

            mapObject.transform.position += mapOffset * GameController.GRID_SIZE;
            if (reactivate.Contains(mapObject.name))
                mapObject.SetActive(true);
        }
        UpdateMapInfo();

        Debug.LogError("Generated Maps");
    }

    [ContextMenu("Save")]
    public void Save() {
        MapController.MapData defaultMap = GameController.LoadMap("Default", 0);
        if (defaultMap.roomDatas == null) {
            Debug.LogError("Default Rooms Missing");
            return;
        }
        
        if (mapHolder == null)
            return;

        Dictionary<string, List<GameObject>> maps = new Dictionary<string, List<GameObject>>();
        SpriteRenderer[] mapSprites = GetMapSprites();
        if (mapSprites != null) {
            foreach (SpriteRenderer sr in mapSprites) {
                GameObject roomObject = sr.transform.parent.gameObject;
                string mapName = roomObject.transform.parent.name;
                if (mapName == "Default")
                    continue;

                if (!maps.ContainsKey(mapName))
                    maps.Add(mapName, new List<GameObject>());

                if (roomObject.activeSelf)
                    maps[mapName].Add(roomObject);
            }
        }

        foreach (string key in maps.Keys) {
            MapController.MapData md = GameController.LoadMap(key, 0);

            if (maps[key].Count > 0) {
                GameObject mapObject = null;
                Vector2 corner = maps[key][0].transform.position;
                foreach (GameObject go in maps[key]) {
                    if (mapObject == null)
                        mapObject = go.transform.parent.gameObject;
                    go.transform.SetParent(null);
                    corner.x = Mathf.Min(corner.x, go.transform.position.x);
                    corner.y = Mathf.Min(corner.y, go.transform.position.y);
                }
                mapObject.transform.position = corner;
                foreach (GameObject go in maps[key])
                    go.transform.SetParent(mapObject.transform);

                md.colorCoordinates = new Coordinates[GameController.CRYSTAL_COLORS];
                List<MapController.RoomData> newRoomDatas = new List<MapController.RoomData>();
                foreach (GameObject go in maps[key]) {
                    MapController.RoomData roomData = new MapController.RoomData(go.name);
                    roomData.mapOffset = new Coordinates(Mathf.RoundToInt(go.transform.localPosition.x), Mathf.RoundToInt(go.transform.localPosition.y));
                    foreach (MapController.RoomData drd in defaultMap.roomDatas) {
                        if (drd.roomName == roomData.roomName) {
                            roomData.localOffset = drd.localOffset;
                            roomData.tunnels     = drd.tunnels;
                            break;
                        }
                    }
                    if (roomData.roomName.Contains("CollectColor"))
                        md.colorCoordinates[GameController.ConvertColorNameToIndex(roomData.roomName)] = roomData.mapOffset + roomData.localOffset;

                    newRoomDatas.Add(roomData);
                }

                md.roomDatas = newRoomDatas.ToArray();
                md.ConnectTunnels();
                md.GenerateMapSprite();
            }
            else
                md.roomDatas = null;

            GameController.SaveMap(md, 0);
        }

        Debug.LogError("Saved Maps");

        Generate();
    }

    [ContextMenu("Snap Maps")]
    public void SnapMaps() {
        SpriteRenderer[] mapSprites = GetMapSprites();
        if (mapSprites == null)
            return;

        foreach (SpriteRenderer sr in mapSprites) {
            GameObject go = sr.transform.parent.gameObject;
            Vector2 pos = go.transform.localPosition;
            go.transform.localPosition = new Vector2(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
        }
    }

    [ContextMenu("Toggle Layer Order")]
    public void ToggleLayerOrder() {
        SpriteRenderer[] mapSprites = GetMapSprites();
        if (mapSprites == null)
            return;

        string layer = null;
        foreach (SpriteRenderer sr in mapSprites) {
            if (layer == null)
                layer = sr.sortingLayerName == "Default" ? "BG" : "Default";
            sr.sortingLayerName = layer;
        }
    }

    [ContextMenu("Update Map Info")]
    public void UpdateMapInfo() {
        SpriteRenderer[] mapSprites = GetMapSprites();
        if (mapSprites == null)
            return;

        byte alpha = (byte)(showMapInfo ? 254 : 1);
        Color32 tunnelColor = Assets.MAP_COLORS[showMapInfo ? "TunnelEnd" : "Tunnel"]; tunnelColor.a = 253;
        Color   tunnelEnd   = Assets.MAP_COLORS["TunnelEnd"];
        foreach (SpriteRenderer sr in mapSprites) {
            if (sr.sprite == null)
                continue;

            Texture2D t = sr.sprite.texture;
            for (int x = 0; x < t.width; x++) {
                for (int y = 0; y < t.height; y++) {
                    Color32 c = t.GetPixel(x, y);
                    if      (c   == tunnelEnd || c.a == 253) c   = tunnelColor;
                    else if (c.a == 1         || c.a == 254) c.a = alpha;
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
        }
    }

    SpriteRenderer[] GetMapSprites() {
        return mapHolder == null ? null : mapHolder.GetComponentsInChildren<SpriteRenderer>(true);
    }
}
#endif