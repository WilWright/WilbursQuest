using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;

public enum ControlIndex { SelectShoot, MoveUp, MoveDown, MoveLeft, MoveRight, BackUndo, Reset, Grow, Force, Think }
public class MenuController : MonoBehaviour {
    [System.Serializable]
    public class Menu {
        public GameObject menuObject;
        public Item[] items;
        [HideInInspector] public Item item;
        [HideInInspector] public GameObject itemObject;
        [HideInInspector] public int selection;
        [HideInInspector] public int itemSelection;
    }

    [System.Serializable]
    public class Item {
        public enum ItemType { Button, Slider, Options, CheckBox, Save, ColorBlock, KeyBind }
        public ItemType itemType;
        public GameObject[] titleObjects;
        public GameObject[] itemObjects;
        public Button buttonItem;
        public Slider sliderItem;
        public Options optionsItem;
        public CheckBox checkBoxItem;
        public Save saveItem;
        public ColorBlock colorBlockItem;
        public KeyBind keyBindItem;

        [System.Serializable]
        public class Button {
            public enum ButtonAction { Game, Menu }
            public ButtonAction buttonAction;
        }
        [System.Serializable]
        public class Slider {
            public int current;
            public int max;
            public int min;
            public int dotStep;
            public GameObject dot;
            public GameObject dotHolder;
        }
        [System.Serializable]
        public class Options {
            public int index;
            public List<Sprite> list;
        }
        [System.Serializable]
        public class CheckBox {
            public bool selected;
        }
        [System.Serializable]
        public class Save {
            public int slot;
            public bool deleting;
            public PlayerController.PlayerData playerData;
        }
        [System.Serializable]
        public class ColorBlock {
            public Image[] blocks;
            public Image[][] centers;
            public int[] colorIndices;
            public bool picking;
        }
        [System.Serializable]
        public class KeyBind {
            public Action[] actions;
            public GameObject[] controls;
            public GameObject[] locks;
        }
    }

    [System.Serializable]
    public class ResolutionItem {
        public int width;
        public int height;
        public Sprite sprite;

        public bool CompareRes(Resolution resolution) {
            return width == resolution.width && height == resolution.height;
        }
    }

    [System.Serializable]
    public class InventoryItem {
        [System.Serializable]
        public class LengthItem {
            public Image itemOutline;
            public GameObject itemComplete;
            public GameObject[] itemIncrements;
        }

        public GameObject itemObject;
        public Text number;
        public LengthItem[] lengthItems;
    }

    [System.Serializable]
    public class SettingsData {
        public int lastSave   = 1;
        public int lastDevice = 0;
        public int volume     = 15;
        public int brightness = 19;
        public int nativeRes  = -1;
        public int resolution = -1;
        public int windowMode = 0;
        public int vSync      = 1;

        public bool instantUI     = false;
        public bool screenShake   = true;
        public bool toggleButtons = false;
        public bool cameraSnap    = false;
        public bool colorSymbols  = false;
        public bool dpadPreffered = false;

        public int[] colorIndices = new int[] { 0, 3, 5, 6, 2, 4 };
        public string[] keyboardBinds = new string[] {
            "Up", "Down", "Left", "Right",
            "Z", "A", "X", "S", "D", "C",
            "Esc"
        };

        public SettingsData(SettingsData settingsData = null) {
            if (settingsData == null)
                return;

            lastSave      = settingsData.lastSave;
            lastDevice    = settingsData.lastDevice;
            volume        = settingsData.volume;
            brightness    = settingsData.brightness;
            nativeRes     = settingsData.nativeRes;
            resolution    = settingsData.resolution;
            windowMode    = settingsData.windowMode;
            vSync         = settingsData.vSync;
            instantUI     = settingsData.instantUI;
            screenShake   = settingsData.screenShake;
            toggleButtons = settingsData.toggleButtons;
            cameraSnap    = settingsData.cameraSnap;
            colorSymbols  = settingsData.colorSymbols;
            dpadPreffered = settingsData.dpadPreffered;

            colorIndices = new int[settingsData.colorIndices.Length];
            settingsData.colorIndices.CopyTo(colorIndices, 0);
            keyboardBinds = new string[settingsData.keyboardBinds.Length];
            settingsData.keyboardBinds.CopyTo(keyboardBinds, 0);
        }
        public bool CompareSettings(SettingsData settingsData) {
            if (lastSave      != settingsData.lastSave
             || lastDevice    != settingsData.lastDevice
             || volume        != settingsData.volume
             || brightness    != settingsData.brightness
             || nativeRes     != settingsData.nativeRes
             || resolution    != settingsData.resolution
             || windowMode    != settingsData.windowMode
             || vSync         != settingsData.vSync
             || instantUI     != settingsData.instantUI
             || screenShake   != settingsData.screenShake
             || toggleButtons != settingsData.toggleButtons
             || cameraSnap    != settingsData.cameraSnap
             || colorSymbols  != settingsData.colorSymbols
             || dpadPreffered != settingsData.dpadPreffered) 
            {
                return false;
            }

            for (int i = 0; i < colorIndices.Length; i++) {
                if (colorIndices[i] != settingsData.colorIndices[i])
                    return false;
            }
            for (int i = 0; i < keyboardBinds.Length; i++) {
                if (keyboardBinds[i] != settingsData.keyboardBinds[i])
                    return false;
            }

            return true;
        }
    }

    class HighlightObject {
        public Image[] objects = new Image[4];
        public float highlightTime;
        public IEnumerator coroutine;
    }
    Dictionary<GameObject, HighlightObject> highlightObjects = new Dictionary<GameObject, HighlightObject>();

    [Header("Menu")]
    public GameObject canvas;
    public GameObject menuHolder;
    public GameObject menuMover;
    public GameObject menuAnchor;
    public Menu[] menus;
    public Menu confirmMenu;
    public Menu colorPickerMenu;

    Menu currentMenu;
    Menu tempMenu;
    int currentConfirmation;
    bool activeConfirmation;
    bool activeColorPicker;
    (bool swap, int itemIndex, int prevColorIndex) swapColorBlock = (false, 0, 0);
    List<string> menuHistory = new List<string>();
    IEnumerator moveToMenuCoroutine;
    bool pauseLocked;

    float menuNavigateCooldown;
    float menuNavigateHold;
    const float MENU_NAVIGATE_COOLDOWN_MIN = 0.1f;
    const float MENU_NAVIGATE_COOLDOWN_MAX = 0.3f;

    public SpriteRenderer bgSpriteRenderer;
    float bgTime = 1;
    IEnumerator bgCoroutine;

    [HideInInspector]
    public SettingsData settingsData;
    SettingsData previousSettingsData;

    [Header("UI")]
    public Image brightnessOverlay;
    public GameObject controlInfo;
    public GameObject navigationInfo;
    public GameObject navigationVertInfo;
    public GameObject navigationHoriInfo;
    public GameObject selectInfo;
    public GameObject backInfo;
    public Text toolTip;

    public GameObject inventory;
    public InventoryItem[] inventoryItems;
    public Image[] confirmText;
    public ResolutionItem[] resolutionItems;
    Sprite[] confirmButtons;
    Sprite[] saveButtons;
    Sprite[] volumeIcons;
    Sprite[] brightnessIcons;
    Sprite[] menuDots;
    Sprite[] checkBoxes;
    Color highlightColor;
    Color backgroundColor;
    Color inactiveColor;
    Color32[] gameColors;
    const float PIXEL_SPACING = 1.492f;

    static readonly string[] CONTROL_NAMES = new string[] { "Move (Up)", "Move (Down)", "Move (Left)", "Move (Right)", "Select/Shoot", "Grow", "Back/Undo", "Reset", "Force", "Think" };
    static readonly Dictionary<string, string> TOOL_TIPS = new Dictionary<string, string>() {
        // Main menu
        { "Play"          , "Play the game"                                                 },
        { "Settings"      , "Change settings"                                               },
        { "Quit"          , "Quit the game"                                                 },

        // Play menu
        { "Continue"      , "Continue save "                                                },
        { "NewGame"       , "Start a new game in save "                                     },
        { "Delete"        , "Delete save "                                                  },

        // Settings menu
        { "AudioVideo"    , "Change audio/video settings"                                   },
        { "Game"          , "Change game/accessibility settings"                            },
        { "Controls"      , "Rebind controls (keyboard only)\nand see controls information" },

        // Audio/Video menu
        { "VSync"         , "Syncs game to monitor refresh rate"                            },

        // Game menu
        { "Instant UI"    , "Instantly displays certain UI elements such as map/grid"       },
        { "Screenshake"   , "Shakes screen when rocks fall"                                 },
        { "Toggle Buttons", "The primary buttons in (hold + move) actions are togglable"    },
        { "Camera Snap"   , "Instantly changes screens\nand disables drift when moving"     },
        { "Color Symbols" , "Puzzle elements are also identified\nwith unique symbols"      },

        // Controls menu
        { "Move (Up)"     , "Move up\n(press/hold)"                                         },
        { "Move (Down)"   , "Move down\n(press/hold)"                                       },
        { "Move (Left)"   , "Move left\n(press/hold)"                                       },
        { "Move (Right)"  , "Move right\n(press/hold)"                                      },
        { "Select/Shoot"  , "Fire a projectile\n(press/hold)"                               },
        { "Grow"          , "Grow/shrink\n(hold + move)"                                    },
        { "Back/Undo"     , "Undo last action\n(press/hold)"                                },
        { "Reset"         , "Reset the room\n(hold)"                                        },
        { "Force"         , "Switch force crystal direction\n(hold + move)"                 },
        { "Think"         , "Display information\n(hold + move)"                            },
        { "Locked"        , "Find ability to unlock"                                        },

        // Confirmation menu
        { "Check"         , "Confirm "                                                      },
        { "X"             , "Cancel "                                                       }
    };

    public void Init() {
        confirmButtons  = GameController.Assets.confirmButtons;
        saveButtons     = GameController.Assets.saveButtons;
        volumeIcons     = GameController.Assets.volumeIcons;
        brightnessIcons = GameController.Assets.brightnessIcons;
        menuDots        = GameController.Assets.menuDots;
        checkBoxes      = GameController.Assets.checkBoxes;
        highlightColor  = GameController.Assets.highlightColor;
        backgroundColor = GameController.Assets.backgroundColor;
        inactiveColor   = GameController.Assets.inactiveColor;
        gameColors      = GameController.Assets.gameColors;

        settingsData = LoadSettings();
        previousSettingsData = new SettingsData(settingsData);

        PlayerController.PlayerData[] saves = new PlayerController.PlayerData[3];
        pauseLocked = true;
        for (int i = 0; i < saves.Length; i++) {
            saves[i] = GameController.LoadPlayer(i + 1);

            // Prevent unpausing from main menu if first time playing game to force player to pick a save
            if (saves[i] != null)
                pauseLocked = false;
        }
        
        GameController.startRoom = null;
        if (saves[settingsData.lastSave - 1] == null) {
            // If previous save is missing go to next valid save
            for (int i = 0; i < saves.Length; i++) {
                if (saves[i] != null) {
                    GameController.currentSave = settingsData.lastSave = i + 1;
                    GameController.startRoom   = saves[i].currentRoom;
                    break;
                }
            }
        }
        else {
            GameController.currentSave = settingsData.lastSave;
            GameController.startRoom   = saves[settingsData.lastSave - 1].currentRoom;
        }

        if (GameController.startRoom == null) {
            // Valid save still wasn't chosen, so prepare to load new save 1 for main menu
            GameController.currentSave = 1;
            GameController.startRoom   = GameController.originRoom;
        }

        // Init menu items
        foreach (Menu m in menus) {
            foreach (Item item in m.items) {
                switch (item.itemType) {
                    case Item.ItemType.Slider:
                        Item.Slider s = item.sliderItem;
                        int range = s.max - s.min;
                        int amount = range / s.dotStep;

                        item.itemObjects = new GameObject[amount];
                        item.itemObjects[0] = s.dot;

                        for (int i = 0; i < item.itemObjects.Length; i++) {
                            if (item.itemObjects[i] == null) item.itemObjects[i] = Instantiate(s.dot, s.dotHolder.transform);
                            item.itemObjects[i].transform.localPosition = Vector3.right * i * PIXEL_SPACING * 8;
                        }

                        switch (item.titleObjects[0].name) {
                            case "Volume"    : s.current = settingsData.volume;     break;
                            case "Brightness": s.current = settingsData.brightness; break;
                        }
                        break;

                    case Item.ItemType.Options:
                        switch (item.titleObjects[0].name) {
                            case "Resolution":
                                Resolution[] resolutions = UnityEngine.Screen.resolutions;
                                for (int i = 0; i < resolutionItems.Length; i++) {
                                    foreach (Resolution r in resolutions) {
                                        if (resolutionItems[i].CompareRes(r)) {
                                            item.optionsItem.list.Add(resolutionItems[i].sprite);
                                            break;
                                        }
                                    }
                                }

                                if (settingsData.resolution == -1) {
                                    int nativeWidth  = Display.main.systemWidth;
                                    int nativeHeight = Display.main.systemHeight;

                                    // Check if native res matches a valid game res
                                    bool found = false;
                                    for (int i = 0; i < item.optionsItem.list.Count; i++) {
                                        foreach (ResolutionItem ri in resolutionItems) {
                                            if (ri.sprite == item.optionsItem.list[i]) {
                                                if (ri.width == nativeWidth && ri.height == nativeHeight) {
                                                    item.optionsItem.index = settingsData.resolution = settingsData.nativeRes = i;
                                                    found = true;
                                                }
                                            }
                                        }
                                    }
                                    if (!found) {
                                        // Choose next best fit for native res
                                        for (int i = 1; i < item.optionsItem.list.Count; i++) {
                                            ResolutionItem ri = GetResolutionItem(item.optionsItem.list[i]);
                                            if (ri.sprite == item.optionsItem.list[i]) {
                                                if (ri.width > nativeWidth || ri.height > nativeHeight) {
                                                    ResolutionItem ri2 = GetResolutionItem(item.optionsItem.list[i - 1]);
                                                    item.optionsItem.index = settingsData.resolution = settingsData.nativeRes = i - 1;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                    item.optionsItem.index = settingsData.resolution;

                                UpdateResolution(GetResolutionItem(item.optionsItem.list[item.optionsItem.index]), true);
                                break;

                            case "Window Mode":
                                item.optionsItem.index = settingsData.windowMode;
                                break;
                        }
                        break;

                    case Item.ItemType.ColorBlock:
                        item.colorBlockItem.colorIndices = new int[item.colorBlockItem.blocks.Length];
                        item.itemObjects = new GameObject[item.colorBlockItem.blocks.Length];
                        for (int i = 0; i < settingsData.colorIndices.Length; i++) {
                            int colorIndex = settingsData.colorIndices[i];
                            item.itemObjects[i] = item.colorBlockItem.blocks[i].transform.parent.gameObject;
                            item.colorBlockItem.blocks[i].color = GameController.Assets.gameColors[colorIndex];
                            item.colorBlockItem.colorIndices[i] = colorIndex;
                        }
                        break;

                    case Item.ItemType.CheckBox:
                        switch (item.titleObjects[0].name) {
                            case "VSync":
                                item.checkBoxItem.selected = settingsData.vSync == 1;
                                QualitySettings.vSyncCount = settingsData.vSync;
                                break;

                            case "Toggle Buttons":
                                item.checkBoxItem.selected = GameController.toggleButtons = settingsData.toggleButtons;
                                GameController.Input.SetToggleButtonUI(settingsData.toggleButtons);
                                break;

                            case "Instant UI"   : item.checkBoxItem.selected = GameController.instantUI          = settingsData.instantUI;    break;
                            case "Screenshake"  : item.checkBoxItem.selected = GameController.enableScreenshake  = settingsData.screenShake;  break;
                            case "Camera Snap"  : item.checkBoxItem.selected = GameController.enableCameraSnap   = settingsData.cameraSnap;   break;
                            case "Color Symbols": item.checkBoxItem.selected = GameController.enableColorSymbols = settingsData.colorSymbols; break;
                        }    
                        break;
                }
            }
        }

        // Init color picker menu
        for (int i = 0; i < 2; i++) {
            colorPickerMenu.items[i].colorBlockItem.colorIndices = new int[4];
            colorPickerMenu.items[i].itemObjects = new GameObject[colorPickerMenu.items[i].colorBlockItem.blocks.Length];
            for (int j = 0; j < colorPickerMenu.items[i].itemObjects.Length; j++) {
                int colorIndex = i == 0 ? j : j + 4;
                colorPickerMenu.items[i].itemObjects[j] = colorPickerMenu.items[i].colorBlockItem.blocks[j].transform.parent.gameObject;
                colorPickerMenu.items[i].colorBlockItem.blocks[j].color = gameColors[colorIndex];
                colorPickerMenu.items[i].colorBlockItem.colorIndices[j] = colorIndex;
            }
            colorPickerMenu.items[i].colorBlockItem.blocks = null;
        }

        confirmMenu    .menuObject.SetActive(true);
        colorPickerMenu.menuObject.SetActive(true);

        // Create sprite layers for menu button animations
        GameObject[] highlights = GameObject.FindGameObjectsWithTag("HighlightUI");
        foreach (GameObject go in highlights) {
            Transform child = null;
            bool color = false;
            if (go.transform.childCount > 0) {
                child = go.transform.GetChild(0);
                child.SetParent(null, false);
                color = child.name == "ColorBlockCenter";
            }

            highlightObjects.Add(go, new HighlightObject());
            Image[] hos = highlightObjects[go].objects;
            hos[0] = go.GetComponent<Image>();
            hos[0].color = backgroundColor;
            for (int i = 1; i < hos.Length; i++) {
                hos[i] = Instantiate(go).GetComponent<Image>();
                if (i > 1) hos[i].gameObject.SetActive(false);
                if (color) Instantiate(child.gameObject, hos[i].transform);
            }
            if (color) {
                Destroy(child.gameObject);
                child = null;
            }

            for (int i = 1; i < hos.Length; i++) {
                hos[i].transform.SetParent(hos[i - 1].transform);
                hos[i].transform.localPosition = Vector3.left * PIXEL_SPACING;
                hos[i].transform.localScale = Vector3.one;
            }
            hos[1].color = inactiveColor;
            if (child != null) child.SetParent(go.transform, false);
        }

        confirmMenu    .menuObject.SetActive(false);
        colorPickerMenu.menuObject.SetActive(false);

        foreach (Menu m in menus) {
            switch (m.menuObject.name) {
                case "Play":
                    for (int i = 0; i < m.items.Length; i++)
                        SetSaveUI(m.items[i], saves[i], i + 1);
                    SetCurrentSave(settingsData.lastSave);
                    break;
            }
            foreach (Item item in m.items) {
                switch (item.itemType) {
                    case Item.ItemType.Slider:
                        UpdateSliderUI(item, item.sliderItem.current);
                        break;

                    case Item.ItemType.Options:
                        SwapHighlightSprites(item.itemObjects[0], item.optionsItem.list[item.optionsItem.index]);

                        if (item.titleObjects[0].name == "Window Mode")
                            SetWindowMode(settingsData.windowMode);
                        break;

                    case Item.ItemType.ColorBlock:
                        InitColorBlocks(item);

                        for (int i = 0; i < settingsData.colorIndices.Length; i++) {
                            int colorIndex = settingsData.colorIndices[i];
                            if (colorIndex > 3) item.itemObjects[i].name = colorPickerMenu.items[1].itemObjects[colorIndex - 4].name;
                            else                item.itemObjects[i].name = colorPickerMenu.items[0].itemObjects[colorIndex    ].name;
                        }
                        break;

                    case Item.ItemType.CheckBox:
                        SwapHighlightSprites(item.itemObjects[0], checkBoxes[item.checkBoxItem.selected ? 1 : 0]);
                        break;
                }
            }
        }
        foreach (Item i in colorPickerMenu.items)
            InitColorBlocks(i);

        UpdateControlLocks(saves[settingsData.lastSave - 1]);
    }

    void Update() {
        if (!pauseLocked && InputController.Get(Action.Menu, PressType.Down) && InputController.Get(Action.Grow, PressType.None)) {
            menuNavigateHold = 0;
            if (activeConfirmation) { ActivateConfirmation(false, currentConfirmation); return; }
            if (activeColorPicker ) { ActivateColorPicker (false                     ); return; }
            
            Item saveItem = GetSaveItem(GameController.currentSave);
            saveItem.saveItem.playerData = GameController.Player.playerData;
            if (!GameController.paused) {
                if (currentMenu.item == saveItem)
                    UpdateInventoryUI(GameController.Player.playerData);
            }
            else
                SaveSettings(settingsData);

            GameController.Pause(!GameController.paused);
            return;
        }

        if (!GameController.paused)
            return;

        if (menuNavigateHold <= 0 || menuNavigateCooldown <= 0) {
            menuNavigateCooldown = Mathf.Lerp(MENU_NAVIGATE_COOLDOWN_MAX, MENU_NAVIGATE_COOLDOWN_MIN, GameController.GetCurve(menuNavigateHold));

            if      (InputController.Get(Action.Up   )) SelectItem    ( 1);
            else if (InputController.Get(Action.Down )) SelectItem    (-1);
            else if (InputController.Get(Action.Left )) SelectItemList(-1);
            else if (InputController.Get(Action.Right)) SelectItemList( 1);
        }
        else
            menuNavigateCooldown -= Time.deltaTime;

        if (InputController.Get(Action.Up   , PressType.Up)
         || InputController.Get(Action.Down , PressType.Up)
         || InputController.Get(Action.Left , PressType.Up)
         || InputController.Get(Action.Right, PressType.Up))
        {
            menuNavigateHold = 0;
        }
        else {
            if (InputController.Get(Action.Up   )
             || InputController.Get(Action.Down )
             || InputController.Get(Action.Left )
             || InputController.Get(Action.Right)) 
            {
                menuNavigateHold += Time.deltaTime;
            }
        }

        if (InputController.Get(Action.Shoot, PressType.Down)) {
            if (currentMenu.item.itemObjects == null || currentMenu.item.itemObjects.Length == 0)
                return;

            GameController.PlayRandomSound(AudioController.menuSelect);
            menuNavigateHold = 0;

            switch (currentMenu.item.itemType) {
                case Item.ItemType.Button:
                    switch (currentMenu.item.buttonItem.buttonAction) {
                        case Item.Button.ButtonAction.Game:
                            switch (currentMenu.itemObject.name) {
                                case "Quit" : ActivateConfirmation(true , 0                  ); return;
                                case "Check": ActivateConfirmation(true , currentConfirmation); return;
                                case "X"    : ActivateConfirmation(false, currentConfirmation); return;
                            }
                            return;

                        case Item.Button.ButtonAction.Menu:
                            GoToMenu(currentMenu.itemObject.name, false);
                            return;
                    }
                    return;

                case Item.ItemType.CheckBox:
                    bool selected = !currentMenu.item.checkBoxItem.selected;
                    currentMenu.item.checkBoxItem.selected = selected;
                    SwapHighlightSprites(currentMenu.itemObject, checkBoxes[currentMenu.item.checkBoxItem.selected ? 1 : 0]);
                    UpdateToolTip();

                    switch (currentMenu.item.titleObjects[0].name) {
                        case "Instant UI" :
                            settingsData.instantUI = GameController.instantUI = selected;
                            GameController.Player.Map.UpdateMapUI();
                            return;

                        case "Toggle Buttons":
                            settingsData.toggleButtons = GameController.toggleButtons = selected;
                            GameController.Input.SetToggleButtonUI(selected);
                            return;

                        case "Color Symbols":
                            settingsData.colorSymbols = GameController.enableColorSymbols = selected;
                            GameController.UpdateColorSymbols();
                            return;

                        case "Screenshake": settingsData.screenShake = GameController.enableScreenshake = selected;   return;
                        case "Camera Snap": settingsData.cameraSnap  = GameController.enableCameraSnap  = selected;   return;
                        case "VSync"      : settingsData.vSync       = QualitySettings.vSyncCount = selected ? 1 : 0; return;
                    }
                    return;

                case Item.ItemType.Save:
                    if (currentMenu.item.saveItem.deleting) ActivateConfirmation(true, 1);
                    else                                    PlaySave(currentMenu.item.saveItem.slot);
                    return;

                case Item.ItemType.ColorBlock:
                    if (activeColorPicker) {
                        int colorIndex = colorPickerMenu.item.colorBlockItem.colorIndices[colorPickerMenu.itemSelection];

                        if (swapColorBlock.swap) {
                            tempMenu.item.colorBlockItem.colorIndices[swapColorBlock.itemIndex] = swapColorBlock.prevColorIndex;
                            foreach (Image im in tempMenu.item.colorBlockItem.centers[swapColorBlock.itemIndex])
                                im.color = gameColors[swapColorBlock.prevColorIndex];

                            if (swapColorBlock.prevColorIndex > 3) tempMenu.item.itemObjects[swapColorBlock.itemIndex].name = colorPickerMenu.items[1].itemObjects[swapColorBlock.prevColorIndex - 4].name;
                            else                                   tempMenu.item.itemObjects[swapColorBlock.itemIndex].name = colorPickerMenu.items[0].itemObjects[swapColorBlock.prevColorIndex    ].name;

                            GameController.SetGameColor(swapColorBlock.itemIndex, gameColors[swapColorBlock.prevColorIndex]);
                            swapColorBlock.swap = false;
                        }

                        tempMenu.item.colorBlockItem.colorIndices[tempMenu.itemSelection] = colorIndex;
                        settingsData                .colorIndices[tempMenu.itemSelection] = colorIndex;
                        foreach (Image im in tempMenu.item.colorBlockItem.centers[tempMenu.itemSelection])
                            im.color = gameColors[colorIndex];

                        if (colorIndex > 3) tempMenu.item.itemObjects[tempMenu.itemSelection].name = colorPickerMenu.items[1].itemObjects[colorIndex - 4].name;
                        else                tempMenu.item.itemObjects[tempMenu.itemSelection].name = colorPickerMenu.items[0].itemObjects[colorIndex    ].name;

                        GameController.SetGameColor(tempMenu.itemSelection, gameColors[colorIndex]);
                        ActivateColorPicker(false);
                    }
                    else
                        ActivateColorPicker(true);
                    return;

                case Item.ItemType.KeyBind:
                    if (InputController.usingGamepad)
                        return;

                    Item.KeyBind kb = currentMenu.item.keyBindItem;
                    bool[] done = new bool[1];
                    InputController.RebindAction(kb.actions[currentMenu.itemSelection], done);
                    StartCoroutine(Flash(kb.controls[currentMenu.itemSelection], done));
                    return;
            }
        }

        if (InputController.Get(Action.Undo, PressType.Down)) {
            if (activeColorPicker) {
                menuNavigateHold = 0;
                ActivateColorPicker(false);
                return;
            }
            if (!activeConfirmation) {
                menuNavigateHold = 0;

                if (currentMenu != menus[0]) {
                    Highlight(currentMenu.itemObject, false);
                    HighlightTitles(currentMenu, false);

                    menuHistory.RemoveAt(menuHistory.Count - 1);
                    GoToMenu(menuHistory[menuHistory.Count - 1], true);
                }
            }
            else {
                menuNavigateHold = 0;
                ActivateConfirmation(false, currentConfirmation);
            }

            UpdateControlInfo();
            UpdateToolTip();
        }
    }

    public void InitMenu() { GoToMenu(menus[0].menuObject.name, false); }
    void GoToMenu(string menu, bool back) {
        bool found = false;
        foreach (Menu m in menus) {
            if (m.menuObject.name == menu) {
                currentMenu = m;
                found = true;
                break;
            }
        }
        if (!found) {
            Debug.LogError(menu + " not found.");
            return;
        }

        if (!back) {
            menuHistory.Add(menu);
            currentMenu.selection = currentMenu.itemSelection = 0;
        }
        else
            GameController.PlayRandomSound(AudioController.menuBack);

        if (currentMenu.items != null && currentMenu.items.Length > 0) {
            currentMenu.item = currentMenu.items[currentMenu.selection];
            if (currentMenu.item.itemType == Item.ItemType.Save)
                UpdateInventoryUI(currentMenu.item.saveItem.playerData);
        }
        if (currentMenu.item.itemObjects != null && currentMenu.item.itemObjects.Length > 0) {
            if (currentMenu.item.itemType == Item.ItemType.Slider)
                currentMenu.itemSelection = currentMenu.item.sliderItem.current;
            currentMenu.itemObject = currentMenu.item.itemObjects[currentMenu.itemSelection];
        }

        UpdateControlInfo();
        UpdateToolTip();
        Highlight(currentMenu.itemObject, true);
        HighlightTitles(currentMenu, true);

        if (moveToMenuCoroutine != null) {
            StopCoroutine(moveToMenuCoroutine);
            menuMover.transform.localPosition = Vector2.zero;
            menuHolder.transform.SetParent(menuAnchor.transform);
            controlInfo.SetActive(true);
        }
        moveToMenuCoroutine = MoveToMenu(currentMenu.menuObject);
        StartCoroutine(moveToMenuCoroutine);
    }
    IEnumerator MoveToMenu(GameObject menu) {
        menuMover.transform.position = menu.transform.position;
        menuHolder.transform.SetParent(menuMover.transform);
        controlInfo.SetActive(false);

        Vector2 fromPos = menuMover.transform.localPosition;
        Vector2 toPos   = Vector2.zero;

        float time = 0;
        while (time < 1) {
            menuMover.transform.localPosition = Vector2.Lerp(fromPos, toPos, GameController.GetCurve(time));
            time += Time.deltaTime * 3.5f;
            yield return null;
        }
        menuMover.transform.localPosition = toPos;
        menuHolder.transform.SetParent(menuAnchor.transform);
        controlInfo.SetActive(true);
    }

    void SelectItem(int direction) {
        if (currentMenu.items == null || currentMenu.items.Length < 2)
            return;

        GameController.PlayRandomSound(AudioController.menuMove);

        Highlight(currentMenu.itemObject, false);
        HighlightTitles(currentMenu, false);

        int nextSelection = currentMenu.selection - direction;
        currentMenu.selection = nextSelection < 0 || nextSelection >= currentMenu.items.Length 
                              ? nextSelection + direction * currentMenu.items.Length : nextSelection;

        currentMenu.item = currentMenu.items[currentMenu.selection];
        if (currentMenu.item.itemType != Item.ItemType.Slider) {
            if (!activeColorPicker && currentMenu.item.itemType != Item.ItemType.KeyBind)
                currentMenu.itemSelection = 0;
        }
        else
            currentMenu.itemSelection = currentMenu.item.sliderItem.current;

        currentMenu.itemObject = currentMenu.item.itemObjects[currentMenu.itemSelection];

        switch (currentMenu.item.itemType) {
            case Item.ItemType.Save:
                UpdateInventoryUI(currentMenu.item.saveItem.playerData);
                currentMenu.item.saveItem.deleting = currentMenu.itemSelection == 1;
                if (!currentMenu.item.itemObjects[1].activeSelf && currentMenu.item.saveItem.deleting)
                    SelectItemList(-1);
                break;
        }

        UpdateControlInfo();
        UpdateToolTip();
        Highlight(currentMenu.itemObject, true);
        HighlightTitles(currentMenu, true);
    }
    void SelectItemList(int direction) {
        if (currentMenu.items == null
         || currentMenu.items.Length == 0
         || currentMenu.item.itemObjects == null
         || (currentMenu.item.itemObjects.Length < 2 && currentMenu.item.itemType != Item.ItemType.Options)) 
        {
            return;
        }

        switch (currentMenu.item.itemType) {
            case Item.ItemType.Slider:
                SwapHighlightSprites(currentMenu.item.itemObjects[currentMenu.itemSelection], menuDots[0]);
                break;

            case Item.ItemType.KeyBind:
                HighlightTitles(currentMenu, false);
                break;
        }
        Highlight(currentMenu.itemObject, false);
        
        int nextSelection = currentMenu.itemSelection + direction;
        currentMenu.itemSelection = nextSelection < 0 || nextSelection >= currentMenu.item.itemObjects.Length 
                                  ? nextSelection - direction * currentMenu.item.itemObjects.Length : nextSelection;

        currentMenu.itemObject = currentMenu.item.itemObjects[currentMenu.itemSelection];
        
        if (currentMenu.item.itemType != Item.ItemType.Save || currentMenu.item.itemObjects[1].activeSelf)
            GameController.PlayRandomSound(AudioController.menuMove);
        
        switch (currentMenu.item.itemType) {
            case Item.ItemType.Save:
                if (!currentMenu.itemObject.activeSelf && currentMenu.itemSelection == 1) SelectItemList(-1);
                else                                                                      currentMenu.item.saveItem.deleting = currentMenu.itemSelection == 1;
                break;

            case Item.ItemType.Slider : UpdateSliderUI(currentMenu.item, currentMenu.itemSelection); break;
            case Item.ItemType.Options: UpdateOptionUI(currentMenu.item, direction);                 break;
            case Item.ItemType.KeyBind: HighlightTitles(currentMenu, true);                          break;
        }

        UpdateToolTip();
        Highlight(currentMenu.itemObject, true);
    }

    void ActivateConfirmation(bool activate, int type) {
        currentConfirmation = type;

        if (activeConfirmation) {
            GameController.PlayRandomSound(AudioController.menuBack);
            confirmMenu.menuObject.SetActive(false);
            currentMenu = tempMenu;
            activeConfirmation = false;
            Highlight(confirmMenu.item.itemObjects[1], false);

            if (!activate) {
                UpdateControlInfo();
                UpdateToolTip();
                return;
            }

            switch (type) {
                case 0:
                    SaveSettings(settingsData);
                    Application.Quit();
                    return;

                case 1:
                    GameController.DeleteData(currentMenu.item.saveItem.slot);
                    SetSaveUI(currentMenu.item, null, currentMenu.item.saveItem.slot);
                    UpdateInventoryUI(currentMenu.item.saveItem.playerData);
                    currentMenu.item.saveItem.deleting = false;
                    SelectItemList(-1);
                    if (currentMenu.item.saveItem.slot == GameController.currentSave)
                        StartCoroutine(SwitchSave(GameController.currentSave));
                    break;
            }
        }
        else {
            if (!activate)
                return;

            foreach (Image i in confirmText)
                i.sprite = confirmButtons[type];
            tempMenu = currentMenu;
            currentMenu = confirmMenu;
            confirmMenu.selection = confirmMenu.itemSelection = 0;
            confirmMenu.item = confirmMenu.items[0];
            confirmMenu.itemObject = confirmMenu.item.itemObjects[0];
            Highlight(confirmMenu.itemObject, true);
            confirmMenu.menuObject.SetActive(true);
            activeConfirmation = true;
        }
        
        UpdateControlInfo();
        UpdateToolTip();
    }
    void ActivateColorPicker(bool activate) {
        if (activeColorPicker) {
            GameController.PlayRandomSound(AudioController.menuBack);
            colorPickerMenu.menuObject.SetActive(false);
            currentMenu = tempMenu;
            activeColorPicker = false;
            Highlight(colorPickerMenu.item.itemObjects[colorPickerMenu.itemSelection], false);
        }
        else {
            if (!activate)
                return;

            tempMenu = currentMenu;
            currentMenu = colorPickerMenu;
            int colorIndex = tempMenu.item.colorBlockItem.colorIndices[tempMenu.itemSelection];
            colorPickerMenu.selection = colorIndex < 4 ? 0 : 1;
            colorPickerMenu.itemSelection = colorIndex < 4 ? colorIndex : colorIndex - 4;
            colorPickerMenu.item = colorPickerMenu.items[colorPickerMenu.selection];
            colorPickerMenu.itemObject = colorPickerMenu.item.itemObjects[colorPickerMenu.itemSelection];
            Highlight(colorPickerMenu.itemObject, true);
            colorPickerMenu.menuObject.SetActive(true);
            activeColorPicker = true;
        }

        UpdateControlInfo();
        UpdateToolTip();
    }

    public void UpdateControlInfo() {
        if (currentMenu.item.itemType == Item.ItemType.Save)
            navigationHoriInfo.SetActive(currentMenu.item.itemObjects[1].activeSelf);
        else
            navigationHoriInfo.SetActive(currentMenu.item.itemObjects.Length > 1 || currentMenu.item.optionsItem.list.Count > 0);

        navigationVertInfo.SetActive(currentMenu.items.Length > 1);
        navigationInfo.SetActive(navigationVertInfo.activeSelf || navigationHoriInfo.activeSelf);
        backInfo.SetActive(menuHistory.Count > 1);
        
        switch (currentMenu.item.itemType) {
            case Item.ItemType.Button    :
            case Item.ItemType.CheckBox  :
            case Item.ItemType.ColorBlock:
            case Item.ItemType.Save      : selectInfo.SetActive(true);                          break;
            case Item.ItemType.KeyBind   : selectInfo.SetActive(!InputController.usingGamepad); break;
            case Item.ItemType.Options   :
            case Item.ItemType.Slider    : selectInfo.SetActive(false);                         break;
        }
    }
    public void UpdateToolTip() {
        if (currentMenu == null)
            return;

        string tip = null;

        switch (currentMenu.item.itemType) {
            case Item.ItemType.Button:
                tip = TOOL_TIPS[currentMenu.itemObject.name];

                if (activeConfirmation) {
                    switch (currentConfirmation) {
                        case 0: tip += "quit";   break;
                        case 1: tip += "delete"; break;
                    }
                }
                break;

            case Item.ItemType.CheckBox:
                tip = "turn " + currentMenu.item.titleObjects[0].name.ToLower() + (currentMenu.item.checkBoxItem.selected ? " off" : " on");
                tip += "\n\n" + TOOL_TIPS[currentMenu.item.titleObjects[0].name];
                break;

            case Item.ItemType.ColorBlock:
                string objectType = null;
                Menu initialMenu = activeColorPicker ? tempMenu : currentMenu;
                switch (initialMenu.itemSelection) {
                    case 0:
                    case 1:
                    case 2: 
                    case 3: objectType = " puzzle elements"; break;
                    case 4: objectType = " time elements";   break;
                    case 5: objectType = " gate elements";   break;
                }
                tip = "change color of " + initialMenu.itemObject.name + objectType;

                if (activeColorPicker) {
                    tip += " to " + colorPickerMenu.itemObject.name;
                    CheckColorSwap();

                    if (swapColorBlock.swap) {
                        tip += "\n\nthe color " + colorPickerMenu.itemObject.name + " is in use";
                        tip += "\nit will swap with " + tempMenu.itemObject.name;
                    }
                    else {
                        if (tempMenu       .item.colorBlockItem.colorIndices[tempMenu       .itemSelection]
                         == colorPickerMenu.item.colorBlockItem.colorIndices[colorPickerMenu.itemSelection]) 
                        {
                            tip = "keep color of " + tempMenu.itemObject.name + objectType;
                        }
                    }
                }
                break;

            case Item.ItemType.KeyBind:
                tip = "";
                if (!InputController.usingGamepad)
                    tip += "rebind ";

                bool locked = currentMenu.item.keyBindItem.locks[currentMenu.itemSelection].activeSelf;
                tip += locked ? "locked ability" : currentMenu.itemObject.name.ToLower();
                
                for (int i = 0; i < CONTROL_NAMES.Length; i++) {
                    if (CONTROL_NAMES[i] == currentMenu.itemObject.name) {
                        tip += "\n" + InputController.actionInfo[i].description;
                        break;
                    }
                }

                tip += " for " + InputController.currentControls.device;
                tip += "\n\n" + TOOL_TIPS[locked ? "Locked" : currentMenu.itemObject.name];
                break;

            case Item.ItemType.Options:
                tip = "change " + currentMenu.item.titleObjects[0].name.ToLower();
                break;

            case Item.ItemType.Save:
                if (!currentMenu.item.itemObjects[1].activeSelf) tip = TOOL_TIPS["NewGame"] + (currentMenu.selection + 1);
                else                                             tip = TOOL_TIPS[currentMenu.itemSelection == 0 ? "Continue" : "Delete"] + (currentMenu.selection + 1);
                break;

            case Item.ItemType.Slider:
                string name = currentMenu.item.titleObjects[0].name;
                tip = "change " + name + " level";
                if (name == "Brightness")
                    tip += "\n\nGlow effects cannot be seen\nwith lower than max brightness";
                break;
        }

        toolTip.text = tip;
    }

    void HighlightTitles(Menu menu, bool active) {
        if (menu.item.titleObjects != null && menu.item.titleObjects.Length > 0) {
            if (menu.item.itemType == Item.ItemType.KeyBind) {
                if (menu.item.titleObjects.Length > menu.itemSelection)
                    Highlight(menu.item.titleObjects[menu.itemSelection], active);
            }
            else {
                foreach (GameObject go in menu.item.titleObjects)
                    Highlight(go, active);
            }
        }

        if (menu.item.itemType == Item.ItemType.KeyBind) {
            GameObject controlLock = menu.item.keyBindItem.locks[menu.itemSelection];
            if (controlLock.activeSelf)
                Highlight(controlLock, active);
        }
    }
    void Highlight(GameObject highlightObject, bool active) {
        highlightObjects.TryGetValue(highlightObject, out HighlightObject ho);
        if (ho == null)
            return;

        if (ho.coroutine != null) StopCoroutine(ho.coroutine);
            ho.coroutine  = ieHighlight(ho, active);
        StartCoroutine(ho.coroutine);
    }
    IEnumerator ieHighlight(HighlightObject highlightObject, bool active) {
        int fromIndex = 2;
        int toIndex   = highlightObject.objects.Length - 1;
        
        float speed = 10f;
        if (active) {
            while (highlightObject.highlightTime < 1) {
                int index = GameController.GetCurveIndex(fromIndex, toIndex, highlightObject.highlightTime);
                highlightObject.objects[index - 1].color = backgroundColor;
                highlightObject.objects[index    ].color = highlightColor;
                highlightObject.objects[index].gameObject.SetActive(true);

                highlightObject.highlightTime += Time.deltaTime * speed;
                yield return null;
            }
            highlightObject.highlightTime = 1;
            highlightObject.objects[toIndex - 1].color = backgroundColor;
            highlightObject.objects[toIndex    ].color = highlightColor;
            highlightObject.objects[toIndex].gameObject.SetActive(true);
        }
        else {
            while (highlightObject.highlightTime > 0) {
                int index = GameController.GetCurveIndex(fromIndex, toIndex, highlightObject.highlightTime);
                highlightObject.objects[index - 1].color = inactiveColor;
                highlightObject.objects[index].gameObject.SetActive(false);

                highlightObject.highlightTime -= Time.deltaTime * speed;
                yield return null;
            }
            highlightObject.highlightTime = 0;
            highlightObject.objects[fromIndex - 1].color = inactiveColor;
            highlightObject.objects[fromIndex].gameObject.SetActive(false);
        }
    }
    void SwapHighlightSprites(GameObject go, Sprite sprite) {
        foreach (Image im in highlightObjects[go].objects)
            im.sprite = sprite;
    }

    IEnumerator Flash(GameObject go, bool[] done) {
        bool active = false;
        while (!done[0]) {
            go.SetActive(active);
            active = !active;
            yield return new WaitForSeconds(0.25f);
        }
        go.SetActive(true);
    }

    Item GetSaveItem(int slot) {
        return menus[3].items[slot - 1];
    }
    void SetSaveUI(Item item, PlayerController.PlayerData playerData, int slot) {
        bool active = playerData != null;
        item.saveItem.playerData = playerData;
        item.itemObjects[1].SetActive(active);
        SwapHighlightSprites(item.itemObjects[0], active ? saveButtons[slot - 1] : saveButtons[saveButtons.Length - 1]);
    }
    void SetCurrentSave(int slot, bool init = false) {
        for (int i = 0; i < GameController.SAVE_SLOT_AMOUNT; i++) {
            Item saveItem = GetSaveItem(i + 1);
            bool selectedSlot = i == slot - 1;
            if (selectedSlot) {
                if (!init) {
                    if (!saveItem.itemObjects[1].activeSelf)
                        continue;
                }
                else
                    saveItem.itemObjects[1].SetActive(true);
            }
            else {
                if (!saveItem.itemObjects[1].activeSelf)
                    continue;
            }

            SwapHighlightSprites(saveItem.itemObjects[0], saveButtons[selectedSlot ? slot - 1 : i + 3]);
        }
    }
    public void InitInventoryColors() {
        AddColorObject(ColorIndex.Red     , InventoryIndex.Red     );
        AddColorObject(ColorIndex.Green   , InventoryIndex.Green   );
        AddColorObject(ColorIndex.Blue    , InventoryIndex.Blue    );
        AddColorObject(ColorIndex.Time    , InventoryIndex.Undo    );
        AddColorObject(ColorIndex.Time    , InventoryIndex.Reset   );
        AddColorObject(ColorIndex.Force   , InventoryIndex.Force   );
        AddColorObject(ColorIndex.Fragment, InventoryIndex.Fragment);

        void AddColorObject(ColorIndex colorIndex, InventoryIndex inventoryIndex) {
            GameController.AddColorObject(colorIndex, null, null, inventoryItems[(int)inventoryIndex].itemObject.GetComponent<Image>(), false);
        }
    }
    public void UpdateInventoryUI(PlayerController.PlayerData playerData) {
        if (playerData == null) {
            inventory.SetActive(false);
            return;
        }
        else
            inventory.SetActive(true);

        EnableInventoryItem(InventoryIndex.Undo    , playerData.abilities[(int)AbilityIndex.Undo ]);
        EnableInventoryItem(InventoryIndex.Reset   , playerData.abilities[(int)AbilityIndex.Reset]);
        EnableInventoryItem(InventoryIndex.Force   , playerData.abilities[(int)AbilityIndex.Force]);
        EnableInventoryItem(InventoryIndex.Fragment, playerData.fragments > 0);
        for (int i = 0; i < playerData.colors.Length; i++)
            EnableInventoryItem((InventoryIndex)i, playerData.colors[i]);

        UpdateLengthInventory();
        UpdateInventoryCount(InventoryIndex.Fragment);

        void EnableInventoryItem(InventoryIndex inventoryIndex, bool enable) {
            inventoryItems[(int)inventoryIndex].itemObject.SetActive(enable);
        }
        void UpdateInventoryCount(InventoryIndex inventoryIndex) {
            int count = -1;
            switch (inventoryIndex) {
                case InventoryIndex.Fragment:
                    count = playerData.fragments;
                    break;
            }

            EnableInventoryItem(inventoryIndex, count > 0);
            inventoryItems[(int)inventoryIndex].number.text = count + "";
        }
        void UpdateLengthInventory() {
            InventoryItem.LengthItem[] lengthItems = inventoryItems[(int)InventoryIndex.Grow].lengthItems;
            foreach (InventoryItem.LengthItem li in lengthItems) {
                li.itemComplete.SetActive(false);
                li.itemOutline.sprite = GameController.Assets.lengthItemOutlines[0];
            }

            int lengthIndex = 0;
            for (int i = 0; i < playerData.lengthIncrements; i++) {
                lengthItems[lengthIndex].itemIncrements[i % PlayerController.LENGTH_INCREMENT_AMOUNT].SetActive(true);

                if ((i + 1) % PlayerController.LENGTH_INCREMENT_AMOUNT == 0) {
                    lengthItems[lengthIndex].itemComplete.SetActive(true);
                    lengthItems[lengthIndex].itemOutline.sprite = GameController.Assets.lengthItemOutlines[1];
                    lengthIndex++;
                }
            }
        }
    }
    void UpdateSliderUI(Item item, int selection) {
        Sprite[] sprites = null;
        item.sliderItem.current = selection;
        int value = item.sliderItem.min + 1 + selection * item.sliderItem.dotStep;

        switch (item.titleObjects[0].name) {
            case "Volume":
                settingsData.volume = selection;
                GameController.Audio.mixer.SetFloat("Volume", Mathf.Log10(value / 100.0f) * 20);
                sprites = volumeIcons;
                break;

            case "Brightness":
                if (brightnessOverlay != null)
                    brightnessOverlay.color = new Color(0, 0, 0, 1 - value / 100.0f);
                sprites = brightnessIcons;
                UnityEngine.Screen.brightness = value / 100.0f;
                break;
        }

        int iconIndex = Mathf.CeilToInt(item.sliderItem.current * 1.0f * (sprites.Length - 1) / (item.itemObjects.Length - 1));
        SwapHighlightSprites(item.titleObjects[0], sprites[iconIndex]);
        SwapHighlightSprites(item.itemObjects[item.sliderItem.current], menuDots[1]);
    }
    void UpdateOptionUI(Item item, int direction) {
        int nextIndex = item.optionsItem.index + direction;
        item.optionsItem.index = nextIndex < 0 || nextIndex >= item.optionsItem.list.Count
                               ? nextIndex - direction * item.optionsItem.list.Count : nextIndex;

        switch (item.titleObjects[0].name) {
            case "Resolution":
                settingsData.resolution = item.optionsItem.index;
                UpdateResolution(GetResolutionItem(item.optionsItem.list[item.optionsItem.index]), false);
                break;

            case "Window Mode":
                settingsData.windowMode = item.optionsItem.index;
                SetWindowMode(settingsData.windowMode);
                break;
        }

        SwapHighlightSprites(item.itemObjects[0], item.optionsItem.list[item.optionsItem.index]);
    }

    void SetWindowMode(int windowMode) {
        switch (windowMode) {
            case 0: UnityEngine.Screen.fullScreen = true;                                break;
            case 1: UnityEngine.Screen.fullScreenMode = FullScreenMode.Windowed;         break;
            case 2: UnityEngine.Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
        }
    }

    void UpdateResolution(ResolutionItem ri, bool init) {
        UnityEngine.Screen.SetResolution(ri.width, ri.height, UnityEngine.Screen.fullScreenMode);
        StartCoroutine(UpdateCameraSize(ri));
    }
    ResolutionItem GetResolutionItem(Sprite sprite) {
        foreach (ResolutionItem ri in resolutionItems) {
            if (sprite == ri.sprite)
                return ri;
        }
        return null;
    }
    IEnumerator UpdateCameraSize(ResolutionItem ri) {
        Resolution r = UnityEngine.Screen.currentResolution;
        yield return new WaitUntil(() => r.width == ri.width && r.height == ri.height);
        yield return new WaitWhile(() => GameController.currentScreen == null);
        GameController.Game.gameCamera.orthographicSize = GameController.GetScreenOrthographicSize(GameController.currentScreen);
    }

    void InitColorBlocks(Item item) {
        item.colorBlockItem.centers = new Image[item.itemObjects.Length][];
        for (int i = 0; i < item.itemObjects.Length; i++) {
            Image[] images = item.itemObjects[i].GetComponentsInChildren<Image>(true);
            List<Image> centers = new List<Image>();
            foreach (Image im in images) {
                if (im.name.Contains("Center"))
                    centers.Add(im);
            }
            item.colorBlockItem.centers[i] = centers.ToArray();
        }
    }
    void CheckColorSwap() {
        int prevColorIndex = tempMenu       .item.colorBlockItem.colorIndices[tempMenu.itemSelection];
        int colorIndex     = colorPickerMenu.item.colorBlockItem.colorIndices[colorPickerMenu.itemSelection];

        for (int i = 0; i < tempMenu.item.colorBlockItem.colorIndices.Length; i++) {
            if (tempMenu.item.colorBlockItem.colorIndices[i] == colorIndex && i != tempMenu.itemSelection) {
                swapColorBlock = (true, i, prevColorIndex);
                return;
            }
        }
        swapColorBlock = (false, 0, 0);
    }
    int GetColorIndex(Color32 color) {
        for (int i = 0; i < gameColors.Length; i++) {
            if (CompareColors(color, gameColors[i]))
                return i;
        }
        return -1;
    }
    bool CompareColors(Color32 a, Color32 b) {
        return a.r == b.r 
            && a.g == b.g 
            && a.b == b.b 
            && a.a == b.a;
    }

    public void UpdateControlLocks(PlayerController.PlayerData playerData) {
        ControlIndex[] indeces = new ControlIndex[] {
            ControlIndex.SelectShoot,
            ControlIndex.BackUndo   ,
            ControlIndex.Reset      ,
            ControlIndex.Grow       ,
            ControlIndex.Force      ,
            ControlIndex.Think
        };
        bool[] locks = null;

        PlayerController.PlayerData pd = playerData;
        if (pd != null) {
            locks = new bool[] {
                pd.abilities[(int)AbilityIndex.Shoot],
                pd.abilities[(int)AbilityIndex.Undo ],
                pd.abilities[(int)AbilityIndex.Reset],
                pd.abilities[(int)AbilityIndex.Grow ],
                pd.abilities[(int)AbilityIndex.Force],
                pd.abilities[(int)AbilityIndex.Think]
            };
        }
        else
            locks = new bool[indeces.Length];

        for (int i = 0; i < indeces.Length; i++)
            UnlockControl(indeces[i], locks[i]);
    }
    public void UnlockControl(ControlIndex controlIndex, bool unlock) {
        int objectIndex = 0;
        int itemIndex   = (int)controlIndex;
        if (itemIndex > 4) {
            objectIndex = 1;
            itemIndex  -= 5;
        }

        menus[6].items[itemIndex].itemObjects      [objectIndex].SetActive( unlock);
        menus[6].items[itemIndex].keyBindItem.locks[objectIndex].SetActive(!unlock);

        UpdateToolTip();
    }

    public void ShowMenu(bool enable) {
        if (bgCoroutine != null) StopCoroutine(bgCoroutine);
            bgCoroutine  = ieShowMenu(enable);
        StartCoroutine(bgCoroutine);
    }
    IEnumerator ieShowMenu(bool enable) {
        int fromIndex = 0;
        int toIndex   = GameController.Assets.menuBGSprites.Length - 1;
        
        float speed = 5;
        if (enable) {
            while (bgTime < 1) {
                bgSpriteRenderer.sprite = GameController.Assets.menuBGSprites[GameController.GetCurveIndex(fromIndex, toIndex, bgTime)];

                bgTime += Time.deltaTime * speed;
                yield return null;
            }
            bgTime = 1;
            bgSpriteRenderer.sprite = GameController.Assets.menuBGSprites[toIndex];

            canvas.gameObject.SetActive(true);
        }
        else {
            canvas.gameObject.SetActive(false);

            while (bgTime > 0) {
                bgSpriteRenderer.sprite = GameController.Assets.menuBGSprites[GameController.GetCurveIndex(fromIndex, toIndex, bgTime)];

                bgTime -= Time.deltaTime * speed;
                yield return null;
            }
            bgTime = 0;
            bgSpriteRenderer.sprite = GameController.Assets.menuBGSprites[fromIndex];
        }
    }

    void PlaySave(int slot) {
        pauseLocked = false;
        if (slot == GameController.currentSave) {
            // Unpause if already playing selected save
            SetCurrentSave(slot, true);
            SaveSettings(settingsData);
            GameController.Pause(false);
            return;
        }
        StartCoroutine(SwitchSave(slot));
    }
    IEnumerator SwitchSave(int slot) {
        GameController.FlagGameState(true);

        yield return new WaitUntil(() => GameController.levelInitialized);

        GameController.Pause(false);

        yield return new WaitUntil(() => GameController.transitionState == Activation.Off);
        yield return new WaitUntil(() => GameController.      fadeState == Activation.Off);

        GameController.SetFade(true);

        yield return new WaitUntil(() => GameController.fadeState == Activation.On);
        yield return new WaitUntil(() => GameController.gameStateFlags == 2);

        GameController.ResetCloud();

        GameController.initialStart = true;
        GameController.currentSave = settingsData.lastSave = slot;

        PlayerController.PlayerData playerData = GameController.LoadPlayer(slot);
        GameController.startRoom = GameController.originRoom;
        if (playerData != null && playerData.currentRoom != null && playerData.currentRoom.Length > 0)
            GameController.startRoom = playerData.currentRoom;

        GameController.Player.playerData = playerData;
        if (GameController.Player.playerData == null)
            GameController.Player.playerData = new PlayerController.PlayerData();
        SetSaveUI(currentMenu.item, GameController.Player.playerData, slot);
        SetCurrentSave(slot);
        GameController.UpdateAbilityInfo();
        GameController.Player.Map.CleanUp();
        GameController.Player.Map.Init();

        GameController.LoadRoom(GameController.startRoom);

        yield return new WaitUntil(() => GameController.levelInitialized);
        yield return new WaitUntil(() => GameController.Player.wormEye  .IsCurrentAnimation(Animation.Blink     ));
        yield return new WaitUntil(() => GameController.Player.wormMouth.IsCurrentAnimation(Animation.MouthSleep));
        yield return new WaitForSeconds(0.5f);

        GameController.SetFade(false);

        yield return new WaitUntil(() => GameController.fadeState == Activation.Off);

        GameController.FlagGameState(false);
    }

    SettingsData LoadSettings() {
        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "Settings.data";

        if (File.Exists(path)) {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(path, FileMode.Open);
            SettingsData settingsData = (SettingsData)bf.Deserialize(file);
            file.Close();

            return settingsData;
        }
        return new SettingsData();
    }
    void SaveSettings(SettingsData settingsData) {
        if (previousSettingsData.CompareSettings(settingsData)) return;
            previousSettingsData = new SettingsData(settingsData);

        string path = Application.persistentDataPath + Path.DirectorySeparatorChar + "Settings.data";

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(path);
        bf.Serialize(file, settingsData);
        file.Close();
    }
}
