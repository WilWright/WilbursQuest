using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.LWRP;

public class LevelBlock : MonoBehaviour {
    [System.Serializable]
    public class BlockItem {
        public GameObject blockObject;
        public SpriteRenderer spriteRenderer;
        public Light2D light;
        public Animator animator;
        public ParticleSystem particles;
        public MonoBehaviour script;
         
        public BlockItem(GameObject     blockObject          , 
                         SpriteRenderer spriteRenderer = null, 
                         Light2D        light          = null, 
                         Animator       animator       = null, 
                         ParticleSystem particles      = null,
                         MonoBehaviour  script         = null) 
        {
            this.blockObject    = blockObject;
            this.spriteRenderer = spriteRenderer;
            this.light          = light;
            this.animator       = animator;
            this.particles      = particles;
            this.script         = script;
        }

        public void ApplySpriteLight() {
            if (light != null && light.lightType == Light2D.LightType.Sprite && spriteRenderer != null)
                light.lightCookieSprite = spriteRenderer.sprite;
        }
    }

    public static readonly Dictionary<string, int> ITEM_INDECES = new Dictionary<string, int>() {
        { "Primary"            ,  0 },

        { "CrystalGlowOutline" ,  1 },
        { "CrystalFloatOutline",  2 },
        { "BlueCrystalBreak"   , -9 },
        { "ButtonBullet"       ,  1 },

        { "TunnelDoor"         ,  1 },
        { "TunnelResetUnder"   ,  1 },
        { "PistonHead"         ,  1 },
        { "PistonInfo"         ,  3 },
        { "Panel"              ,  2 },
        { "GatePanelIcon"      ,  3 },
        { "GatePanelLight"     ,  4 },

        { "GateDoor"           ,  1 },
        { "SlotFragment"       ,  1 },

        { "DigLevelOutline"    ,  1 },

        { "InfoItem"           , -1 },
        { "InfoOutline"        , -2 },
        { "UndoOutline"        , -3 },
        { "ResetOutline"       , -4 },
        { "ColorSymbol"        , -5 },
        { "PanelColorSymbol"   , -6 }
    };

    public string blockName;
    public Layer layer;
    [SerializeField] List<BlockItem> blockItems;
    [SerializeField] BlockItem[] infoItem;
    [SerializeField] BlockItem[] timeItems;
    [SerializeField] BlockItem[] colorSymbolItems;

    public BlockItem GetBlockItem(string itemName) {
        int index = ITEM_INDECES[itemName];

        if (index < 0) {
            switch (index) {
                case -1: return HasLength(infoItem ) ? infoItem [0] : null;
                case -2: return HasLength(infoItem ) ? infoItem [1] : null;
                case -3: return HasLength(timeItems) ? timeItems[0] : null;
                case -4: return HasLength(timeItems) ? timeItems[1] : null;
                case -9: return blockItems[blockItems.Count - 1];
                default: return null;
            }
        }
        return blockItems.Count > index ? blockItems[index] : null;

        bool HasLength(BlockItem[] itemArray) { return itemArray != null && itemArray.Length > 0; }
    }
    public List<BlockItem> GetBlockItems(string itemName) {
        List<BlockItem> itemList = new List<BlockItem>();

        switch (itemName) {
            case "All":
                itemList = blockItems;
                break;

            case "CrystalActivation":
                int count = blockItems.Count;
                if (blockName == "BlueCrystal")
                    count--;

                for (int i = 3; i < count; i++)
                    itemList.Add(blockItems[i]);
                break;

            case "DigPositions":
                for (int i = 2; i < blockItems.Count; i++)
                    itemList.Add(blockItems[i]);
                break;

            case "Info":
                itemList.Add(GetBlockItem("InfoItem"   ));
                itemList.Add(GetBlockItem("InfoOutline"));
                break;

            case "Time":
                itemList.Add(GetBlockItem( "UndoOutline"));
                itemList.Add(GetBlockItem("ResetOutline"));
                break;

            default:
                itemList = null;
                break;
        }

        return itemList;
    }
    public BlockItem AddBlockItem(BlockItem blockItem, bool center = false) {
        if (blockItems == null)
            blockItems = new List<BlockItem>();
        blockItems.Add(blockItem);

        if (blockItem.blockObject.transform.parent == null) {
            blockItem.blockObject.transform.SetParent(blockItems[0].blockObject.transform);
            if (center)
                blockItem.blockObject.transform.localPosition = Vector3.zero;
        }

        return blockItem;
    }
    public void RemoveBlockItem(BlockItem blockItem) {
        blockItems.Remove(blockItem);
    }

    public void AddInfoItem(BlockItem infoItem, BlockItem infoOutline) {
        this.infoItem = new BlockItem[] { infoItem, infoOutline};
    }
    public void RemoveInfoItem() {
        if (infoItem == null)
            return;

        foreach (BlockItem bi in infoItem) {
            if (bi != null && bi.blockObject != null)
                DestroyImmediate(bi.blockObject);
        }
        infoItem = null;
    }

    public void AddTimeItems(BlockItem undoOutline, BlockItem resetOutline) {
        timeItems = new BlockItem[] { undoOutline, resetOutline };
    }
    public void RemoveTimeItems() {
        if (timeItems == null)
            return;

        foreach (BlockItem bi in timeItems) {
            if (bi != null && bi.blockObject != null)
                DestroyImmediate(bi.blockObject);
        }
        timeItems = null;
    }

    public void SetColorSymbols(bool active) {
        foreach (BlockItem bi in colorSymbolItems)
            bi.spriteRenderer.enabled = active;
    }
    public BlockItem[] GetColorSymbolItems() {
        return colorSymbolItems;
    }
    public void AddColorSymbolItems(params BlockItem[] colorSymbols) {
        colorSymbolItems = colorSymbols.Length == 0 ? null : colorSymbols;
    }
    public void RemoveColorSymbolItems() {
        if (colorSymbolItems == null)
            return;

        foreach (BlockItem bi in colorSymbolItems) {
            if (bi != null && bi.blockObject != null)
                DestroyImmediate(bi.blockObject);
        }
        colorSymbolItems = null;
    }
}
