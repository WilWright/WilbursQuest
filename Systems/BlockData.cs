[System.Serializable]
public class BlockData {
    public struct Diff {
        public int facing;
        public int state;
        public bool destroyed;
        public Coordinates coordinates;

        public Diff(BlockData blockData) {
            facing      = blockData.facing;
            state       = blockData.state;
            destroyed   = blockData.destroyed;
            coordinates = blockData.coordinates;
        }

        public bool CompareDiff(Diff diff) {
            return state       == diff.state
                && coordinates == diff.coordinates
                && destroyed   == diff.destroyed
                && facing      == diff.facing; 
        }
    }

    public string blockName;
    public int facing = 0;
    public int state = -1;
    public int spriteState = -1;
    public bool destroyed;
    public Coordinates coordinates;
    public Coordinates origin;
    public Coordinates[] connectedBlocks;

    public BlockData(string blockName, Coordinates coordinates, int facing = 0) {
        this.blockName   = blockName;
        this.coordinates = coordinates;
        this.facing      = facing;
    }
    public BlockData(BlockData blockData) {
        blockName   = blockData.blockName;
        facing      = blockData.facing;
        state       = blockData.state;
        coordinates = blockData.coordinates;
        origin      = blockData.origin;

        if (blockData.connectedBlocks != null) {
            connectedBlocks = new Coordinates[blockData.connectedBlocks.Length];
            blockData.connectedBlocks.CopyTo(connectedBlocks, 0);
        }
    }

    public bool IsPrimary() {
        return connectedBlocks != null && coordinates == connectedBlocks[0];
    }

    public bool CompareDiff(Diff diff) {
        return state       == diff.state 
            && coordinates == diff.coordinates 
            && destroyed   == diff.destroyed 
            && facing      == diff.facing;
    }
    public Diff GetDiff() {
        return new Diff(this);
    }
    public void ApplyDiff(Diff diff) {
        facing      = diff.facing;
        state       = diff.state;
        destroyed   = diff.destroyed;
        coordinates = diff.coordinates;
    }
}