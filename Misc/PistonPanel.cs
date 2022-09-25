using System.Collections;
using UnityEngine;

public class PistonPanel : Panel {
    [System.Serializable]
    public class PistonArm {
        public GameObject[] arms;
        public Data data;

        public PistonArm(GameObject armA, GameObject armB) {
            arms = new GameObject[] { armA, armB };
        }
    }

    public int facing = -1;
    public int length = -1;

    [HideInInspector]
    public PistonArm[] pistonArms;
    const float EXTEND_WAIT = 0.3f;
    IEnumerator setPistonCoroutine;
    
    static readonly int[][] EXTENSION_POSITIONS = new int[][] {
        new int[] { 3, 4 },
        new int[] { 3, 3 },
        new int[] { 3, 2 },
        new int[] { 3, 1 },
        new int[] { 3, 0 },
        new int[] { 2, 0 },
        new int[] { 1, 0 },
        new int[] { 0, 0 }
    };

    public override bool IsTunnelPanel() {
        return false;
    }

    public void UpdateArms() {
        foreach (PistonArm pa in pistonArms) {
            int[] extension = pa.data.blockData.state == (int)Activation.On ? EXTENSION_POSITIONS[0] : EXTENSION_POSITIONS[EXTENSION_POSITIONS.Length - 1];
            for (int i = 0; i < pa.arms.Length; i++)
                pa.arms[i].transform.localPosition = Vector3.right * extension[i];
        }
    }

    public void UpdateIfBlocked(Activation instant = Activation.Off) {
        if (setPistonCoroutine != null)
            return;

        if (instant == Activation.Off) {
            foreach (PistonArm pa in pistonArms) {
                if (pa.data.blockData.state != panelData.blockData.state) {
                    SetActivation((Activation)panelData.blockData.state);
                    return;
                }
            }
        }
        else {
            foreach (PistonArm pa in pistonArms)
                pa.data.blockData.state = panelData.blockData.state;
            UpdateArms();
        }
    }

    protected override void SetActivation(Activation activation, Activation instant = Activation.Off) {
        if (instant != Activation.Off)
            return;

        GameController.FlagGameState(true);

        if (setPistonCoroutine != null) {
            StopCoroutine(setPistonCoroutine);
            GameController.FlagGameState(false);
        }
        setPistonCoroutine = SetPiston(activation);
        StartCoroutine(setPistonCoroutine);
    }
    IEnumerator SetPiston(Activation activation) {
        switch (activation) {
            case Activation.Off:
                for (int i = pistonArms.Length - 1; i >= 0; i--) {
                    Data pistonData = pistonArms[i].data;
                    int state = (int)activation;
                    if (pistonData.blockData.state == state) continue;
                        pistonData.blockData.state =  state;

                    GameController.CheckOffScreenAction(pistonData, (ColorIndex)(-1), activation);

                    // If piston is detracting a block that falls, manually pull it to avoid constant collision sounds from ApplyGravity()
                    if (Coordinates.FacingDirection[facing] == -GameController.gravityDirection) {
                        Data d = GameController.Grid.GetData(pistonData.blockData.coordinates - GameController.gravityDirection, Layer.Block);
                        if (d != null && !d.HasTag(Tag.Float) && d.HasTag(Tag.Push)) {
                            bool tryMove = true;
                            foreach (Coordinates c in d.blockData.connectedBlocks) {
                                Data blockData = GameController.Grid.GetData(c + GameController.gravityDirection, Layer.Block);
                                if (blockData != null) {
                                    Coordinates[] cb = blockData.blockData.connectedBlocks;
                                    if (cb != null && cb == d.blockData.connectedBlocks)
                                        continue;

                                    tryMove = false;
                                    break;
                                }
                            }
                            if (tryMove)
                                GameController.MoveBlock(GameController.Grid.GetData(d.blockData.connectedBlocks[0], d.layer), GameController.gravityDirection, MoveType.Block);
                        }
                    }

                    yield return null;
                    GameController.PlayPitchedSound(AudioController.pistonRetract, 1 + i * 0.1f, true);
                    GameController.ApplyGravity();

                    float time = 0;
                    while (time < 1) {
                        int moveIndex = Mathf.RoundToInt(Mathf.Lerp(0, GameController.BLOCK_SIZE, time));
                        for (int j = 0; j < pistonArms[i].arms.Length; j++)
                            pistonArms[i].arms[j].transform.localPosition = Vector3.right * EXTENSION_POSITIONS[moveIndex][j];

                        time += Time.deltaTime * GameController.BLOCK_MOVE_SPEED;
                        yield return null;
                    }
                    for (int j = 0; j < pistonArms[i].arms.Length; j++)
                        pistonArms[i].arms[j].transform.localPosition = Vector3.right * EXTENSION_POSITIONS[GameController.BLOCK_SIZE][j];

                    yield return new WaitForSeconds(EXTEND_WAIT);
                }
                break;

            case Activation.On:
                for (int i = 0; i < pistonArms.Length; i++) {
                    Data pistonData = pistonArms[i].data;
                    int state = (int)activation;
                    if (pistonData.blockData.state == state)
                        continue;

                    yield return null;

                    // Push blocks until blocked
                    Data moveData = null;
                    Data[] datas = GameController.Grid.GetData(pistonArms[i].data.blockData.coordinates, Layer.Block, Layer.Player, Layer.Piston);
                    bool stop = false;
                    foreach (Data d in datas) {
                        if (d == null)
                            continue;

                        if (!d.moving && GameController.MoveBlock(d, Coordinates.FacingDirection[facing], MoveType.Block))
                            moveData = d;
                        else
                            stop = true;
                        
                        break;
                    }
                    if (stop)
                        break;

                    pistonData.blockData.state = state;
                    GameController.PlayPitchedSound(AudioController.pistonExtend, AudioController.GetRandomPitch(1) + i * 0.1f, true);
                    GameController.CheckOffScreenAction(pistonData, (ColorIndex)(-1), activation);

                    float time = 0;
                    while (time < 1) {
                        int moveIndex = Mathf.RoundToInt(Mathf.Lerp(GameController.BLOCK_SIZE, 0, time));
                        for (int j = 0; j < pistonArms[i].arms.Length; j++)
                            pistonArms[i].arms[j].transform.localPosition = Vector3.right * EXTENSION_POSITIONS[moveIndex][j];

                        time += Time.deltaTime * GameController.BLOCK_MOVE_SPEED;
                        yield return null;
                    }
                    for (int j = 0; j < pistonArms[i].arms.Length; j++)
                        pistonArms[i].arms[j].transform.localPosition = Vector3.right * EXTENSION_POSITIONS[0][j];

                    if (moveData != null)
                        GameController.ApplyGravity();

                    yield return new WaitForSeconds(EXTEND_WAIT);
                }
                break;
        }

        setPistonCoroutine = null;
        GameController.FlagGameState(false);
    }

#if UNITY_EDITOR
    protected override void Generate(GridSystem gridSystem, bool saveGridSystem, Data panelData) {
        Coordinates pistonCoord = GameController.GetCoordinates(transform.position);
        Data oldPistonData = gridSystem.GetData(pistonCoord, Layer.Piston);
        
        if (oldPistonData != null) {
            if (oldPistonData.blockData.connectedBlocks != null) {
                if (facing == -1) facing = oldPistonData.blockData.facing;
                if (length == -1) length = oldPistonData.blockData.connectedBlocks.Length - 1;

                foreach (Coordinates c in oldPistonData.blockData.connectedBlocks) {
                    Data cd = gridSystem.GetData(c, Layer.Piston);
                    if (cd != null)
                        gridSystem.RemoveData(cd);
                }
            }
            gridSystem.RemoveData(oldPistonData);
        }

        LevelBlock levelBlock = panelData.levelBlock;
        LevelBlock.BlockItem pistonItem = levelBlock.GetBlockItem("Primary");
        Data pistonData = new Data(new BlockData("Piston", pistonCoord, facing), pistonItem.blockObject);
        pistonData.blockData.state = (int)Activation.On;
        gridSystem.AddData(pistonData);

        SpriteRenderer pistonSR = pistonItem.spriteRenderer;
        pistonSR.sortingOrder += length * 2;

        LevelBlock.BlockItem headItem = levelBlock.GetBlockItem("PistonHead");
        headItem.spriteRenderer.sortingOrder = pistonSR.sortingOrder - 1;
        headItem.blockObject.transform.SetParent(pistonItem.blockObject.transform);
        headItem.blockObject.transform.localPosition = headItem.blockObject.transform.localEulerAngles = Vector3.zero;

        Transform[] children = GetComponentsInChildren<Transform>();
        foreach (Transform t in children) {
            if (t != null && t.name == "PistonArm")
                DestroyImmediate(t.gameObject);
        }
        pistonArms = null;

        if (length > 0) {
            GameObject pistonArm = Assets.GetPrefab("PistonArm");
            pistonArms = new PistonArm[length];
            Coordinates[] connectedBlocks = new Coordinates[length + 1];
            connectedBlocks[0] = pistonData.blockData.coordinates;
            Coordinates facingDirection = Coordinates.FacingDirection[facing];
            for (int i = 0; i < pistonArms.Length; i++) {
                Coordinates nextCoord = pistonCoord + facingDirection * (i + 1);
                if (gridSystem.GetData(nextCoord, Layer.Piston) != null) {
                    Debug.LogError("Overlapping Pistons: " + nextCoord);
                    return;
                }

                GameObject armA = Instantiate(pistonArm, pistonItem.blockObject.transform); armA.name = "PistonArm";
                armA.GetComponent<SpriteRenderer>().sortingOrder = headItem.spriteRenderer.sortingOrder - (((i + 1) * 2) - 1);

                GameObject armB = Instantiate(armA, armA.transform); armB.name = "PistonArm";
                SpriteRenderer sr = armB.GetComponent<SpriteRenderer>();
                sr.sprite = pistonSR.sprite;
                sr.sortingOrder--;

                pistonArms[i] = new PistonArm(armA, armB);

                Data d = new Data(new BlockData("PistonArm", nextCoord, facing));
                d.blockData.state = inverted ? (int)Activation.On : (int)Activation.Off;
                connectedBlocks[i + 1] = d.blockData.coordinates;
                d.blockData.connectedBlocks = connectedBlocks;
                gridSystem.AddData(d);
            }
            pistonData.blockData.connectedBlocks = connectedBlocks;

            headItem.blockObject.transform.SetParent(pistonArms[pistonArms.Length - 1].arms[1].transform);
            if (pistonArms.Length > 1) {
                for (int i = pistonArms.Length - 1; i > 0; i--)
                    pistonArms[i].arms[0].transform.SetParent(pistonArms[i - 1].arms[1].transform);
            }
            if (inverted) {
                foreach (PistonArm pa in pistonArms) {
                    for (int i = 0; i < pa.arms.Length; i++)
                        pa.arms[i].transform.localPosition = Vector3.right * EXTENSION_POSITIONS[0][i];
                }
            }
        }

        if (panelData.blockObject != null) {
            panelData.blockObject.transform.localEulerAngles = Vector3.zero;
            levelBlock.GetBlockItem("Panel").spriteRenderer.sortingOrder = pistonSR.sortingOrder + 1;
            foreach (PanelLight pl in panelLights) {
                SpriteRenderer[] srs = pl.lightObject.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer sr in srs)
                    sr.sortingOrder = pistonSR.sortingOrder + (sr.name.Contains("ColorSymbol") ? 3 : 2);
            }

            foreach (LevelBlock.BlockItem bi in levelBlock.GetBlockItems("Time")) {
                bi.spriteRenderer.sortingLayerName = "Tunnel"; bi.spriteRenderer.sortingOrder = pistonSR.sortingOrder + 2;
                bi.spriteRenderer.gameObject.transform.localPosition = Vector3.zero;
                bi.blockObject.transform.position = panelData.blockObject.transform.position;
            }
        }

        Generation.CreateInfo(levelBlock);
        Generation.AddColorSymbols(levelBlock, pistonItem.blockObject);
        GameController.ApplyFacing(facing, pistonItem                         .blockObject);
        GameController.ApplyFacing(facing, levelBlock.GetBlockItem("InfoItem").blockObject);
        levelBlock.GetBlockItem("PistonInfo").blockObject.transform.localEulerAngles = Vector3.zero;

        if (saveGridSystem)
            GameController.SaveGrid(GameObject.FindGameObjectWithTag("Level").name, 0, gridSystem, true);

        Debug.LogError("Generated PistonPanel");
    }
#endif
}
