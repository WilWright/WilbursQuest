using System.Collections.Generic;
using UnityEngine;

public class Data {
    public BlockData blockData;
    public GameObject blockObject;
    public LevelBlock levelBlock;
    public List<Data> connectedData;
    public Coordinates moveDirection;
    public bool moving;

    public Tag[] tags;
    public Layer layer;

    public Data(BlockData blockData, GameObject blockObject = null) {
        this.blockData   = blockData;
        this.blockObject = blockObject;

        Assets.AssetInfo ai = Assets.GetAssetInfo(blockData.blockName);
        tags  = ai.tags;
        layer = ai.layer;
    }
    public Data(Data data) {
        blockData   = new BlockData(data.blockData);
        blockObject = data.blockObject;
        levelBlock  = data.levelBlock;

        if (data.blockData.IsPrimary() && data.connectedData != null) {
            connectedData = new List<Data>();
            foreach (Data d in data.connectedData)
                connectedData.Add(new Data(d));
        }

        tags  = data.tags;
        layer = data.layer;
    }
    
    public bool HasTag(params Tag[] tags) {
        if (this.tags == null)
            return false;

        foreach (Tag t in tags) {
            foreach (Tag tt in this.tags) {
                if (t == tt)
                    return true;
            }
        }
        return false;
    }
    public bool HasAllTags(params Tag[] tags) {
        if (this.tags == null || this.tags.Length < tags.Length)
            return false;

        foreach (Tag t in tags) {
            bool found = false;
            foreach (Tag tt in this.tags) {
                if (t == tt) {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }
    
    public void ApplyData(bool applyFacing = false) {
        if (blockObject == null)
            return;

        if (blockData.connectedBlocks == null || layer == Layer.Player || applyFacing)
            GameController.ApplyFacing(blockData.facing, blockObject);
        GameController.ApplyCoordinates(blockData.coordinates, blockObject);
    }

    public void SetMoving(bool moving, Coordinates direction = default) {
        this.moving = moving;
        moveDirection = direction;
        if (connectedData != null) {
            foreach (Data d in connectedData) {
                d.moving = moving;
                d.moveDirection = direction;
            }
        }
    }

    public bool CompareDiff(BlockData.Diff diff) {
        return blockData.CompareDiff(diff);
    }
    public BlockData.Diff GetDiff() {
        return blockData.GetDiff();
    }
    public void ApplyDiff(BlockData.Diff diff) {
        blockData.ApplyDiff(diff);
    }
}