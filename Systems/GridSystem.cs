using System.Collections.Generic;

public class GridSystem {
    [System.Serializable]
    public class GridData {
        public Coordinates size   = new Coordinates(200, 200);
        public Coordinates offset = new Coordinates(100, 100);
        public Screen.ScreenData[] screenDatas;
        public List<Coordinates>[][] supportCrystalCoordinates;
        public BlockData[] blockDatas;

        public GridData(BlockData[] blockDatas = null, Screen.ScreenData[] screenDatas = null, List<Coordinates>[][] supportCrystalCoordinates = null) {
            this.blockDatas  = blockDatas;
            this.screenDatas = screenDatas;
            this.supportCrystalCoordinates = supportCrystalCoordinates;
        }

        public void SetBlockDatas(List<BlockData> list) {
            blockDatas = list.Count > 0 ? list.ToArray() : null;
        }
    }

    // Thresholds for array sizes, optimized for highest layer at each coordinate
    // e.g. Collect is inbetween Tunnel and CollectDestroy, but should always also have space for data to be in CollectDestroy,
    // and if there is no data for BG1 or higher at those coordinates, the array doesn't need to be large enough to store that data
    static readonly int[] LAYER_GROUPS = new int[] {
        (int)Layer.Block,
        (int)Layer.SupportCrystal,
        (int)Layer.Piston,
        (int)Layer.Tunnel,
        (int)Layer.CollectDestroy,
        (int)Layer.Bg2,
        (int)Layer.Fg,
        (int)Layer.Dig
    };

    public Data[,][] grid;
    public Dictionary<Coordinates[], List<Data>> destroyedBlocks;
    GridData gridData;

    public GridSystem(GridData gridData = null, bool fullGrid = false) {
        if (gridData == null || fullGrid)
            gridData = new GridData(gridData?.blockDatas, gridData?.screenDatas, gridData?.supportCrystalCoordinates);

        this.gridData = gridData;
        grid = new Data[gridData.size.x, gridData.size.y][];
        destroyedBlocks = new Dictionary<Coordinates[], List<Data>>();
        List<Data> destroyData = new List<Data>();

        if (fullGrid) {
            int length = LAYER_GROUPS[LAYER_GROUPS.Length - 1] + 1;
            for (int x = 0; x < gridData.size.x; x++) {
                for (int y = 0; y < gridData.size.y; y++)
                    grid[x, y] = new Data[length];
            }

            if (gridData.blockDatas != null) {
                List<Data> connectList = new List<Data>();
                foreach (BlockData bd in gridData.blockDatas) {
                    Data d = new Data(bd);
                    AddData(d);

                    if (bd.IsPrimary())
                        connectList.Add(d);
                }
                ConnectData(connectList);
            }

            return;
        }

        if (gridData.blockDatas != null) {
            List<Data> connectList = new List<Data>();
            int minLength = LAYER_GROUPS[0] + 1;
            for (int i = 0; i < gridData.blockDatas.Length; i++) {
                BlockData bd = gridData.blockDatas[i];
                Data d = new Data(bd);
                Coordinates c = Convert(bd.coordinates);

                if (bd.destroyed && d.layer == Layer.Block) {
                    if (bd.IsPrimary()) connectList.Add(d);
                    else                destroyData.Add(d);
                    continue;
                }
                else {
                    if (bd.IsPrimary())
                        connectList.Add(d);
                }

                // Get length array should be according to the layer groups,
                // blockdatas are organized from highest to lowest layer as chunks for each grid coordinates
                int index  = (int)d.layer;
                int length = minLength;
                for (int layer = LAYER_GROUPS.Length - 1; layer >= 0; layer--) {
                    if (index > LAYER_GROUPS[layer]) {
                        length = LAYER_GROUPS[layer + 1] + 1;
                        break;
                    }
                }

                Data[] datas = new Data[length];
                datas[index] = d;
                
                // Continue through the chunk of layers from highest to lowest to set the rest of the array
                while (++i < gridData.blockDatas.Length && gridData.blockDatas[i].coordinates == bd.coordinates) {
                    Data nd = new Data(gridData.blockDatas[i]);
                    datas[(int)nd.layer] = nd;
                    if (nd.blockData.IsPrimary())
                        connectList.Add(nd);
                }
                i--;

                grid[c.x, c.y] = datas;
            }
            
            for (int x = 0; x < gridData.size.x; x++) {
                for (int y = 0; y < gridData.size.y; y++) {
                    if (grid[x, y] == null)
                        grid[x, y] = new Data[minLength];
                }
            }

            ConnectData(connectList);
        }

        void ConnectData(List<Data> connectList) {
            foreach (Data d in connectList) {
                if (d.blockData.connectedBlocks.Length > 1)
                    d.connectedData = new List<Data>();

                if (d.blockData.destroyed && d.layer == Layer.Block) {
                    List<Data> datas = new List<Data>() { d };
                    for (int i = 1; i < d.blockData.connectedBlocks.Length; i++) {
                        foreach (Data dd in destroyData) {
                            if (dd.blockData.connectedBlocks == d.blockData.connectedBlocks && dd.blockData.coordinates == d.blockData.connectedBlocks[i]) {
                                datas.Add(dd);
                                destroyData.Remove(dd);
                                d.connectedData.Add(dd);
                                break;
                            }
                        }
                    }
                    destroyedBlocks.Add(d.blockData.connectedBlocks, datas);
                }
                else {
                    if (d.connectedData == null)
                        continue;

                    for (int i = 1; i < d.blockData.connectedBlocks.Length; i++)
                        d.connectedData.Add(GetData(d.blockData.connectedBlocks[i], d.layer));
                }
            }
        }
    }

    Coordinates Convert(Coordinates coordinates) {
        return coordinates + gridData.offset;
    }
    public bool WithinBounds(Coordinates coordinates) {
        Coordinates c = Convert(coordinates);
        return c.x >= 0 && c.x < gridData.size.x 
            && c.y >= 0 && c.y < gridData.size.y;
    }

    public GridData GetGridData() {
        return gridData;
    }

    public Data[] GetDatas(Coordinates coordinates) {
        Coordinates c = Convert(coordinates);
        return grid[c.x, c.y];
    }
    public List<Coordinates> GetCoordinates() {
        List<Coordinates> coordinates = new List<Coordinates>();
        for (int x = 0; x < gridData.size.x; x++) {
            for (int y = 0; y < gridData.size.y; y++)
                coordinates.Add(new Coordinates(x, y) - gridData.offset);
        }

        return coordinates;
    }

    public Data GetData(Coordinates coordinates, Layer layer) {
        Data[] datas = GetDatas(coordinates);
           int index = (int)layer;

        return index < datas.Length ? datas[index] : null;
    }
    public Data[] GetData(Coordinates coordinates, params Layer[] layers) {
        Data[] newDatas = new Data[layers.Length];
        Data[] datas = GetDatas(coordinates);
        for (int i = 0; i < newDatas.Length; i++) {
            int index = (int)layers[i];
            if (index < datas.Length)
                newDatas[i] = datas[index];
        }

        return newDatas;
    }
    public void AddData(Data data) {
        Data[] datas = GetDatas(data.blockData.coordinates);
        int index = (int)data.layer;

        if (datas[index] == null)
            datas[index] = data;
    }
    public void RemoveData(Data data) {
        Data[] datas = GetDatas(data.blockData.coordinates);
        int index = (int)data.layer;

        if (datas[index] == data)
            datas[index] = null;
    }

    public void MoveData(Coordinates direction, Data data, bool instant) {
        RemoveData(data);
        data.blockData.coordinates += direction;
        AddData(data);
        if (instant) data.ApplyData();
    }
    public void SetData(Coordinates coordinates, Data data, bool instant) {
        RemoveData(data);
        data.blockData.coordinates = coordinates;
        AddData(data);
        if (instant) data.ApplyData();
    }
    public void DestroyData(Data data, bool destroy) {
        if (destroy) {
            List<Data> datas = new List<Data>();
            foreach (Coordinates cb in data.blockData.connectedBlocks) {
                Data d = GetData(cb, data.layer);
                RemoveData(d);
                d.blockData.destroyed = true;
                datas.Add(d);
            }
            destroyedBlocks.Add(data.blockData.connectedBlocks, datas);
        }
        else {
            foreach (Data d in destroyedBlocks[data.blockData.connectedBlocks]) {
                AddData(d);
                d.blockData.destroyed = false;
            }
            destroyedBlocks.Remove(data.blockData.connectedBlocks);
        }
    }

    public void SortBlockData() {
        List<BlockData> sortedList = new List<BlockData>();
        foreach (Data[] datas in grid) {
            for (int i = datas.Length - 1; i > 0; i--) {
                if (datas[i] != null)
                    sortedList.Add(datas[i].blockData);
            }
        }
        foreach (List<Data> datas in destroyedBlocks.Values) {
            if (datas != null) {
                foreach (Data d in datas)
                    sortedList.Add(d.blockData);
            }
        }
        gridData.SetBlockDatas(sortedList);
    }
    public void TrimBounds() {
        int[] bounds = new int[] { -100, 99, 99, -100 };
        foreach (Data[] datas in grid) {
            foreach (Data d in datas) {
                if (d == null)
                    continue;

                Coordinates c = d.blockData.coordinates;
                if      (c.y > bounds[0]) bounds[0] = c.y;
                else if (c.y < bounds[1]) bounds[1] = c.y;
                if      (c.x < bounds[2]) bounds[2] = c.x;
                else if (c.x > bounds[3]) bounds[3] = c.x;

                break;
            }
        }

        gridData.size   = new Coordinates(System.Math.Abs(bounds[2] - bounds[3]) + 1, System.Math.Abs(bounds[0] - bounds[1]) + 1);
        gridData.offset = new Coordinates(-bounds[2], -bounds[1]);
    }
    public void SetScreens(Screen[] screens) {
        gridData.screenDatas = new Screen.ScreenData[screens.Length];
        for (int i = 0; i < screens.Length; i++)
            gridData.screenDatas[i] = screens[i].screenData;
    }
}
