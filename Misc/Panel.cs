using UnityEngine;
using UnityEngine.Experimental.Rendering.LWRP;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class Panel : MonoBehaviour {
    [System.Serializable]
    public class PanelLight {
        public ColorIndex color;
        public bool on;
        public GameObject lightObject;
        public Light2D light;
        public SpriteRenderer spriteRenderer;

        public PanelLight(ColorIndex color, GameObject lightObject, Light2D light, SpriteRenderer spriteRenderer) {
            this.color          = color;
            this.lightObject    = lightObject;
            this.light          = light;
            this.spriteRenderer = spriteRenderer;
        }

        public void Activate(bool activate) {
            on = light.enabled = activate;
            spriteRenderer.sprite = GameController.Assets.panelSprites[activate ? 1 : 0];
        }
    }

    static readonly Vector2[][] BUTTON_POSITIONS = new Vector2[][] {
        new Vector2[] { new Vector2(0, 0)                                                                                   },
        new Vector2[] { new Vector2(0, 1), new Vector2(0, -1)                                                               },
        new Vector2[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 0)                                           },
        new Vector2[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1)                      },
        new Vector2[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2( 0, 0), new Vector2(-1,  1), new Vector2(-1, -1) }
    };
    public static readonly Vector2[] GATE_PANEL_ICON_POSITIONS = new Vector2[] {
        new Vector2( 1,  0),
        new Vector2( 1,  1),
        new Vector2( 0,  1),
        new Vector2(-1,  1),
        new Vector2(-1,  0),
        new Vector2(-1, -1),
        new Vector2( 0, -1),
        new Vector2( 1, -1)
    };

    [HideInInspector]
    public PanelLight[] panelLights;
    public ColorIndex[] lightColors;
    public bool inverted;
    public int gatePanelIndex = -1;
    public Data panelData;

    public void UpdateLights(int[] buttonColorActivations) {
        if (gatePanelIndex != -1)
            return;

        for (int i = 0; i < buttonColorActivations.Length; i++) {
            int count = buttonColorActivations[i];
            ColorIndex colorIndex = (ColorIndex)i;
            foreach (PanelLight pl in panelLights) {
                if (pl.color == colorIndex) {
                    if (count > 0) {
                        pl.Activate(true);
                        count--;
                    }
                    else
                        pl.Activate(false);
                }
            }
        }
    }
    public void CheckLights(Activation instant = Activation.Off) {
        if (gatePanelIndex != -1)
            return;

        if (IsTunnelPanel() && instant != Activation.Off && panelData.blockData.state == (int)Activation.Alt)
            return;

        bool activate = true;
        foreach (PanelLight pl in panelLights) {
            if (!pl.on) {
                activate = false;
                break;
            }
        }

        SetPanel(activate ? Activation.On : Activation.Off, instant);
    }

    public void SetPanel(Activation activation, Activation instant = Activation.Off) {
        if (IsTunnelPanel() && instant == Activation.Off && activation == Activation.Off) {
            // Check if player is blocking door
            Data tunnelData = GameController.Grid.GetData(panelData.blockData.coordinates + Coordinates.FacingDirection[panelData.blockData.facing], Layer.Tunnel);
            if (tunnelData != null && GameController.Grid.GetData(tunnelData.blockData.coordinates, Layer.Player) != null)
                activation = Activation.Alt;
        }

        if (inverted) {
            switch (activation) {
                case Activation.Off: activation = Activation.On;  break;
                case Activation.On : activation = Activation.Off; break;
            }
        }

        if (!GameController.SetPuzzleActivation(panelData, activation, instant))
            return;

        if (instant == Activation.Off && activation != Activation.Alt)
            GameController.CheckOffScreenAction(panelData, (ColorIndex)(-1), activation);

        SetActivation(activation, instant);
    }

    public abstract bool IsTunnelPanel();
    protected abstract void SetActivation(Activation activation, Activation instant);

#if UNITY_EDITOR
    [ContextMenu("Generate Panel")]
    void GenerateNewPanel() { GeneratePanel(null); }
    public void GeneratePanel(GridSystem gridSystem) {
        string levelName = GameObject.FindGameObjectWithTag("Level").name;
        LevelBlock levelBlock = transform.parent.GetComponent<LevelBlock>();
        if (levelBlock == null) levelBlock = GetComponent<LevelBlock>();
        bool saveGridSystem = false;

        if (gridSystem == null) {
            gridSystem = new GridSystem(GameController.LoadGrid(levelName, 0), true);
            saveGridSystem = true;
        }

        if (levelBlock.layer == Layer.Piston) {
            LevelBlock.BlockItem pistonInfoItem = levelBlock.GetBlockItem("PistonInfo");
            if (pistonInfoItem != null && pistonInfoItem.blockObject != null) {
                DestroyImmediate(pistonInfoItem.blockObject);
                levelBlock.RemoveBlockItem(pistonInfoItem);
            }
        }

        LevelBlock.BlockItem panelItem = levelBlock.GetBlockItem("Panel");
        if (panelItem != null && panelItem.blockObject != null) {
            Data oldPanelData = gridSystem.GetData(GameController.GetCoordinates(panelItem.blockObject.transform.position), Layer.Misc);
            if (oldPanelData != null && oldPanelData.blockData.blockName == "Panel")
                gridSystem.RemoveData(oldPanelData);

            LevelBlock.BlockItem gatePanelItem = levelBlock.GetBlockItem("GatePanelIcon");
            if (gatePanelItem != null)
                levelBlock.RemoveBlockItem(gatePanelItem);

            DestroyImmediate(panelItem.blockObject);
            levelBlock.RemoveBlockItem(panelItem);
            levelBlock.RemoveInfoItem();
            levelBlock.RemoveTimeItems();
            levelBlock.RemoveColorSymbolItems();
        }

        if (lightColors == null || lightColors.Length == 0) {
            lightColors = null;
            panelLights = null;

            if (gatePanelIndex == -1) {
                Data nullData = new Data(new BlockData("Panel", Coordinates.Zero), null);
                nullData.levelBlock = levelBlock;

                Generate(gridSystem, saveGridSystem, nullData);
                return;
            }
        }

        GameObject panel = PrefabUtility.InstantiatePrefab(Assets.GetPrefab(gatePanelIndex == -1 ? "Panel" : "GatePanel")) as GameObject;
        panelItem = levelBlock.AddBlockItem(new LevelBlock.BlockItem(panel, panel.GetComponent<SpriteRenderer>()), true);
        panelItem.script = this;
        if (inverted)
            panelItem.spriteRenderer.sprite = Assets.GetSprite("BlockMisc/block_panel_inverted");

        if (gatePanelIndex == -1) {
            panelLights = new PanelLight[lightColors.Length];
            for (int i = 0; i < lightColors.Length; i++) {
                GameObject panelLight = PrefabUtility.InstantiatePrefab(Assets.GetPrefab(lightColors[i].ToString() + "Panel")) as GameObject;
                panelLight.transform.SetParent(panel.transform);
                panelLight.transform.localPosition = BUTTON_POSITIONS[lightColors.Length - 1][i];
                panelLights[i] = new PanelLight(lightColors[i], panelLight, panelLight.GetComponent<Light2D>(), panelLight.GetComponent<SpriteRenderer>());
            }
        }
        else {
            Light2D[] lights = panel.GetComponentsInChildren<Light2D>();
            LevelBlock.BlockItem gateItem = null;
            LevelBlock.BlockItem iconItem = null;
            foreach (Light2D l in lights) {
                if (l.name.Contains("Icon")) {
                    iconItem = new LevelBlock.BlockItem(l.gameObject, l.GetComponent<SpriteRenderer>(), l);
                    l.gameObject.transform.localPosition = GATE_PANEL_ICON_POSITIONS[gatePanelIndex];
                }
                else
                    gateItem = new LevelBlock.BlockItem(l.gameObject, l.GetComponent<SpriteRenderer>(), l);
            }
            levelBlock.AddBlockItem(iconItem);
            levelBlock.AddBlockItem(gateItem);
        }
        
        Data panelData = new Data(new BlockData("Panel", GameController.GetCoordinates(panel.transform.position)), panel);
        panelData.levelBlock = levelBlock;
        panelData.blockData.state = (int)Activation.Off;
        panelData.ApplyData();
        gridSystem.AddData(panelData);

        string spriteName = "BlockMisc/block_outline_undo_" + (gatePanelIndex == -1 ? "misc" : "gatepanel") + (inverted ? "_inverted" : "");
        Generation.CreateTimeOutlines(panelData, Assets.GetSprite(spriteName), levelBlock, GameObject.FindGameObjectWithTag("Level").GetComponent<LevelData>());
        if (levelBlock.layer != Layer.Piston) {
            Generation.CreateInfo(levelBlock);
            Generation.AddColorSymbols(levelBlock, levelBlock.GetBlockItem("Primary").blockObject);
        }
        Generate(gridSystem, saveGridSystem, panelData);
    }
    protected abstract void Generate(GridSystem gridSystem, bool saveGridSystem, Data panelData);
#endif
}
