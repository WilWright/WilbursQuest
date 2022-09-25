using System.Collections;
using UnityEngine;

public class TunnelPanel : Panel {
    public LevelBlock.BlockItem doorItem;
    public Sprite[] doorSprites;
    IEnumerator doorCoroutine;
    const float DOOR_SPEED = 10;
    float doorTime;

    public override bool IsTunnelPanel() {
        return true;
    }

    public bool IsOpen() {
        return panelData == null || panelData.blockData.state != (int)Activation.Off;
    }

    protected override void SetActivation(Activation activation, Activation instant) {
        bool open = activation != Activation.Off;
        if (doorItem.spriteRenderer.sprite == doorSprites[open ? 0 : GameController.BLOCK_SIZE])
            return;

        GameController.FlagGameState(true);
        if (doorCoroutine != null) {
            StopCoroutine(doorCoroutine);
            GameController.FlagGameState(false);
        }
        doorCoroutine = SetDoor(open, instant);
        StartCoroutine(doorCoroutine);
    }
    IEnumerator SetDoor(bool open, Activation instant) {
        if (instant == Activation.Off) {
            GameController.PlayRandomSound(open ? AudioController.doorOpen : AudioController.doorClose);

            int doorIndex = Mathf.RoundToInt(Mathf.Lerp(0, GameController.BLOCK_SIZE, doorTime));
            if (open) {
                while (doorTime > 0) {
                    int index = Mathf.RoundToInt(Mathf.Lerp(0, GameController.BLOCK_SIZE, GameController.GetCurve(doorTime)));
                    if (index <= doorIndex - 1) {
                        doorIndex = index;
                        doorItem.spriteRenderer.sprite = doorSprites[doorIndex];
                    }

                    doorTime -= Time.deltaTime * DOOR_SPEED;
                    yield return null;
                }
                doorIndex = 0;
            }
            else {
                while (doorTime < 1) {
                    int index = Mathf.RoundToInt(Mathf.Lerp(0, GameController.BLOCK_SIZE, GameController.GetCurve(doorTime)));
                    if (index >= doorIndex + 1) {
                        doorIndex = index;
                        doorItem.spriteRenderer.sprite = doorSprites[doorIndex];
                    }

                    doorTime += Time.deltaTime * DOOR_SPEED;
                    yield return null;
                }
                doorIndex = GameController.BLOCK_SIZE;
            }
            doorItem.spriteRenderer.sprite = doorSprites[doorIndex];
        }
        else {
            doorTime = open ? 0 : 1;
            doorItem.spriteRenderer.sprite = doorSprites[open ? 0 : GameController.BLOCK_SIZE];
        }

        doorCoroutine = null;
        GameController.FlagGameState(false);
    }

#if UNITY_EDITOR
    protected override void Generate(GridSystem gridSystem, bool saveGridSystem, Data panelData) {
        LevelBlock levelBlock = panelData.levelBlock;
        doorItem = levelBlock.GetBlockItem("TunnelDoor");
        Data doorData = gridSystem.GetData(GameController.GetCoordinates(levelBlock.GetBlockItem("Primary").blockObject.transform.position), Layer.Misc);
        doorData.blockData.state = lightColors == null && gatePanelIndex == -1 ? (int)Activation.Alt : (int)Activation.Off;
        if (doorData.blockData.facing == 2 || doorData.blockData.facing == 3)
            doorItem.spriteRenderer.flipY = true;

        SpriteRenderer underDoor = doorItem.blockObject.transform.GetChild(0).GetComponent<SpriteRenderer>();
        if (doorData.blockData.state != (int)Activation.Alt) {
            doorItem.spriteRenderer.sprite = doorSprites[inverted ? 0 : doorSprites.Length - 1];
            underDoor              .sprite = Assets.GetSprite("Blocks/block_tunneldoor1_under");
        }
        else {
            doorItem.spriteRenderer.sprite = null;
            underDoor              .sprite = Assets.GetSprite("Blocks/block_tunneldoor0_under");
        }

        if (panelData.blockObject != null) {
            panelData.blockData.facing = doorData.blockData.facing;
            gridSystem.MoveData(-Coordinates.FacingDirection[panelData.blockData.facing], panelData, true);
            panelData.blockData.origin = panelData.blockData.coordinates;

            LevelBlock.BlockItem infoItem = levelBlock.GetBlockItem("InfoItem");
            GameController.ApplyData(panelData.blockData, infoItem.blockObject);
            foreach (LevelBlock.BlockItem bi in levelBlock.GetBlockItems("Time")) {
                bi.spriteRenderer.sortingLayerName = "Tunnel"; bi.spriteRenderer.sortingOrder = 2;
                bi.spriteRenderer.gameObject.transform.localPosition = Vector3.zero;
                bi.blockObject.transform.position = panelData.blockObject.transform.position;
            }

            gridSystem.GetData(doorData.blockData.coordinates, Layer.Tunnel).blockData.state = gatePanelIndex;
            if (gatePanelIndex != -1)
                levelBlock.GetBlockItem("Panel").blockObject.transform.eulerAngles = infoItem.blockObject.transform.eulerAngles = Vector3.zero;
        }

        if (saveGridSystem)
            GameController.SaveGrid(GameObject.FindGameObjectWithTag("Level").name, 0, gridSystem, true);

        Debug.LogError("Generated TunnelPanel");
    }
#endif
}
