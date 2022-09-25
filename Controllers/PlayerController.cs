using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.LWRP;
using UnityEngine.UI;

public enum AbilityIndex { Shoot, Grow, Undo, Reset, Dig, Force, Think }
public enum InventoryIndex { Red, Green, Blue, Undo, Reset, Force, Dig, Grow, Fragment }
public enum ThinkIndex { Map, Grid, Inventory, RoomInfo, None }
public enum Animation { EyeDefault  , EyeBite  , EyeNod , EyeSleep, Blink, Fall, Roll, Shake, Think, Sing, // Eye
                        HeadDefault , HeadBite , HeadNod,           EatOpen, EatClose, Look,               // Head
                        MouthDefault, MouthBite,          MouthSleep,                                      // Mouth
                        Corner, Grow,                                                                      // Body
                        None
}

public class PlayerController : MonoBehaviour {
    [System.Serializable]
    public class PlayerData {
        public struct Diff {
            public bool headFlipped;
            public bool ignoreGravity;
            public bool digging;
            public int fragments;
            public int lengthIncrements;
            public bool[] colors;
            public bool[] abilities;

            public Diff(PlayerData playerData) {
                headFlipped      = playerData.headFlipped;
                ignoreGravity    = playerData.ignoreGravity;
                digging          = playerData.digging;
                fragments        = playerData.fragments;
                lengthIncrements = playerData.lengthIncrements;
                colors    = new bool[playerData.colors   .Length];
                abilities = new bool[playerData.abilities.Length];
                playerData.colors   .CopyTo(colors   , 0);
                playerData.abilities.CopyTo(abilities, 0);
            }

            public int GetMaxLength() {
                return 3 + lengthIncrements / 3;
            }
        }

        public string currentRoom;
        public int fragments;
        public int length = 3;
        public int lengthIncrements;
        public bool headFlipped;
        public bool ignoreGravity;
        public bool digging;
        public bool[] colors         = new bool[3]; // [Red, Green, Blue]
        public bool[] abilities      = new bool[7]; // [Shoot, Grow, Undo, Reset, Dig, Force, Think]
        public bool[] thinkOptions   = new bool[4]; // [Map, Grid, Inventory, Info]
        public bool[] promptTriggers = new bool[6]; // [Gate, Panel, GatePanel, Piston, Fragment, Move]
        public bool[] gateTunnels    = new bool[8];
        public bool[] activatedDreams;
        public bool[] songs;
        public int currentSongIndex;
        public List<string> popUps = new List<string>();

        public PlayerData() {
            songs = new bool[7];
            for (int i = 0; i < 1; i++)
                songs[i] = true;
            gateTunnels[4] = true;
        }

        public int GetMaxLength() {
            return 3 + lengthIncrements / 3;
        }

        public Diff GetDiff() {
            return new Diff(this);
        }
        public void ApplyDiff(Diff diff) {
            headFlipped      = diff.headFlipped;
            ignoreGravity    = diff.ignoreGravity;
            digging          = diff.digging;
            fragments        = diff.fragments;
            lengthIncrements = diff.lengthIncrements;
            for (int i = 0; i < colors   .Length; i++) colors   [i] = diff.colors   [i];
            for (int i = 0; i < abilities.Length; i++) abilities[i] = diff.abilities[i];
        }
    }
    public PlayerData playerData;

    public class Worm {
        public Data data;
        public Light2D light;
        public SpriteRenderer spriteRenderer;
        public SpriteRenderer skeleton;
        public AnimationData currentAnimation;
        public IEnumerator playingAnimation;

        public Worm(GameObject wormObject, Light2D light, SpriteRenderer spriteRenderer) {
            this.light          = light;
            this.spriteRenderer = spriteRenderer;
            skeleton = wormObject.transform.GetChild(0).GetComponent<SpriteRenderer>();
            data = new Data(new BlockData("Player", Coordinates.Zero), wormObject);
        }

        public void SetFrame(int frame) {
            if (frame == -1)
                frame = currentAnimation.frames[0].sprites.Length - 1;

            spriteRenderer.sprite = currentAnimation.frames[0].sprites[frame];
            skeleton      .sprite = currentAnimation.frames[1].sprites[frame];
            if (light != null)
                light.lightCookieSprite = spriteRenderer.sprite;
        }

        public bool IsCurrentAnimation(Animation animation) {
            return currentAnimation != null && currentAnimation.animation == animation;
        }
    }
    Queue<Worm> wormQueue = new Queue<Worm>();

    [System.Serializable]
    public class ThinkOption {
        public GameObject optionObject;
        public GameObject optionSymbol;
        public SpriteRenderer spriteRenderer;
        public Vector2 startPosition;
    }

    [System.Serializable]
    public class InventoryItem {
        [System.Serializable]
        public class LengthItem {
            public Image itemOutline;
            public GameObject itemComplete;
            public GameObject[] itemIncrements;
        }

        public GameObject itemHolder;
        public GameObject itemObject;
        public LengthItem[] lengthItems;
        public Text number;
        public IEnumerator displayCoroutine;
        [HideInInspector] public Activation activationState;
        [HideInInspector] public int startIndex;
        [HideInInspector] public int endIndex;
        [HideInInspector] public List<Vector2> movePositions;
    }

    [System.Serializable]
    public class AnimationData {
        [System.Serializable]
        public class Frames {
            public Sprite[] sprites;
        }

        public Animation animation;
        public Frames[] frames;
        public float duration = 0.1f;
        public bool loop;
    }

    // Stores patterns and outline sprites for different player shape variations
    // After the player moves its shape is matched against the tree, if it doesn't exist create a new sprite
    // Optimized to use the same sprite when the player is facing a different direction but has the same rotated shape,
    // and when the player shape is flipped/mirrored
    class PatternTree {
        public class FacingPattern {
            public int facing;
            public Vector2 offset;
            public List<FacingPattern> children;
            public Sprite sprite;

            public FacingPattern(int facing) {
                this.facing = facing;
            }
            public void SetPattern(PlayerController pc) {
                pc.undoSpriteRenderer.sprite = sprite;
                pc.undoSpriteRenderer.transform.localPosition = offset;
            }
        }

        public bool flipped;
        List <FacingPattern> patternSizes   = new List <FacingPattern>();
        Stack<FacingPattern> patternHistory = new Stack<FacingPattern>();

        FacingPattern GetPattern(List<Worm> worm) {
            int sizeIndex = Mathf.Max(worm.Count - 3, 0);
            while (patternSizes.Count <= sizeIndex)
                patternSizes.Add(new FacingPattern(0));

            FacingPattern current = patternSizes[sizeIndex];
            int[] pattern = CreatePattern(worm);
            for (int i = 0; i < pattern.Length; i++) {
                FacingPattern next = null;
                if (current.children != null) {
                    foreach (FacingPattern fp in current.children) {
                        if (fp.facing == pattern[i]) {
                            next = fp;
                            break;
                        }
                    }
                }
                else
                    current.children = new List<FacingPattern>();

                if (next == null) {
                    next = new FacingPattern(pattern[i]);
                    current.children.Add(next);
                }

                current = next;
            }

            if (current.sprite == null)
                GameController.Player.BuildUndoOutline(pattern, current);

            return current;
        }
        public void SetPattern(PlayerController pc) {
            GetPattern(pc.worm).SetPattern(pc);
        }
        public void UpdatePattern(PlayerController pc) {
            GetPattern(pc.worm);
        }

        int[] CreatePattern(List<Worm> worm) {
            int[] pattern = new int[worm.Count];
            int facingOffset = worm[0].data.blockData.facing;
            bool checkFlip = true;
            bool flip = false;
            for (int i = 2; i < worm.Count; i++) {
                pattern[i] = worm[i].data.blockData.facing - facingOffset;
                if (pattern[i] < 0)
                    pattern[i] += 4;
                
                if (checkFlip) {
                    switch (pattern[i]) {
                        case 1: checkFlip = false; flip = true; break;
                        case 3: checkFlip = false;              break;
                    }
                }
            }

            if (flip) {
                for (int i = 0; i < pattern.Length; i++) {
                    switch (pattern[i]) {
                        case 1: pattern[i] = 3; break;
                        case 3: pattern[i] = 1; break;
                    }
                }
            }

            flipped = flip;
            return pattern;
        }
    }
    PatternTree undoTree = new PatternTree();
    
    [Header("Game")]
    public MapController Map;
    [HideInInspector] public bool devFlying;
    [HideInInspector] public bool devNoClip;

    [Header("Worm")]
    GameObject wormHolder;
    public Worm wormEye;
    public Worm wormHead;
    public Worm wormMouth;
    public List<Worm> worm;
    public const int WORM_BLOCK_AMOUNT = 15;
    public const int LENGTH_INCREMENT_AMOUNT = 3;
    [HideInInspector] public GameObject headTrigger;
    [HideInInspector] public bool promptMovementOverride;
    [HideInInspector] public bool holdingMove;
    [HideInInspector] public bool headBump;
    [HideInInspector] public float moveCooldown;
    const float MOVE_COOLDOWN_MAX = 0.18f;
    Action currentHeldAction = (Action)(-1);
    ParticleSystem[] bubblePops;
    Color32[] gradientColors;
    List<IEnumerator> gradientCoroutines = new List<IEnumerator>();
    const float COLOR_CHANGE_SPEED = 5;

    [Header("Songs")]
    public Song[] songs;
    Song currentSong;
    int currentNoteIndex = -1;
    int currentNoteColorIndex;
    float singHoldTime;
    const float SONG_CHANGE_TIME = 1f;
    [HideInInspector]
    public bool singing;
    public Material[] musicNoteMaterials;
    ParticleSystem musicNote;

    [Header("Animation")]
    public AnimationData[] animationData;
    bool canShoot = true;
    bool shaking;
    bool forceMoving;
    bool thinking;
    bool sleeping;
    float sleepTime;
    float blinkTime;
    float resetLookCooldown;
    const float RESET_LOOK_COOLDOWN_MAX = 0.25f;

    [Header("Undo / Reset")]
    GameObject undoHolder;
    GameObject undoOutlineHolder;
    public SpriteRenderer undoSpriteRenderer;
    public Sprite[] undoSprites;
    bool undoing;
    const float RESET_TIME = 100;
    const float RESET_TIME_SPEED = 45;
    [HideInInspector]
    public float currentResetTime;

    [Header("Think UI")]
    public GameObject thinkUI;
    public GameObject thinkCenter;
    public ThinkOption[] thinkOptions = new ThinkOption[4];
    Sprite[] thinkSprites;
    ThinkIndex selectedThink = ThinkIndex.None;
    bool thinkingOption;
    float thinkTime;
    const float THINK_SPEED = 4.5f;

    [Header("Grid / RoomInfo")]
    public GameObject grid;
    public SpriteRenderer gridSprite;
    Sprite[] gridSprites;
    float gridTime;
    const float GRID_SPEED = 0.8f;
    bool showRoomInfo;
    bool showingRoomInfo;

    [Header("Inventory")]
    public InventoryItem[] inventoryItems;
    [HideInInspector]
    public float inventoryTime;
    [HideInInspector]
    public int inventoryMoveTotal;
    bool disableManualInventoryDisplay;
    const int INVENTORY_DELAY = 5;
    const int INVENTORY_BOUNCE_STEPS = 5;
    const int INVENTORY_MOVE_STEPS = 35;
    const float INVENTORY_SPEED = 2.25f;
    const float INVENTORY_PIXEL_SPACING = 14.18f;

    public void Init() {
        thinkSprites = GameController.Assets.thinkShowSprites;
        blinkTime = Random.Range(3, 10);

        playerData = GameController.LoadPlayer(GameController.currentSave);
        if (playerData == null)
            playerData = new PlayerData();

        GameObject headAsset = GameController.Assets.wormHead;
        GameObject bodyAsset = GameController.Assets.wormBody;
        wormHolder = new GameObject("WormBlocks");
        wormHolder.transform.SetParent(GameController.Game.playerObjectsHolder.transform);
        wormHolder.SetActive(false);

        GameObject eyeObject = Instantiate(GameController.Assets.wormEye, GameController.Game.playerObjectsHolder.transform);
        wormEye = new Worm(eyeObject, eyeObject.GetComponent<Light2D>(), eyeObject.GetComponent<SpriteRenderer>());
        wormEye.light.enabled = false;
        wormEye.spriteRenderer.color = GameController.Assets.eyeColor;
        Transform[] children = wormEye.data.blockObject.GetComponentsInChildren<Transform>();
        foreach (Transform t in children) {
            if (t.name == "BlipAnchor") {
                GameController.blipAnchor = t.gameObject;
                break;
            }
        }

        GameObject groundParticlesHolder = new GameObject("GroundParticlesHolder");
        groundParticlesHolder.transform.SetParent(GameController.Game.playerObjectsHolder.transform);

        // Create all worm blocks to be used during runtime
        gradientColors = new Color32[WORM_BLOCK_AMOUNT];
        Color32 skeletonColor = GameController.Assets.infoOutlineColor; skeletonColor.a = 255;
        for (int i = 0; i < WORM_BLOCK_AMOUNT; i++) {
            GameObject w = Instantiate(i == 0 ? headAsset : bodyAsset, wormHolder.transform);
            w.name = "Worm";
            Worm worm = new Worm(w, w.GetComponent<Light2D>(), w.GetComponent<SpriteRenderer>());
            wormQueue.Enqueue(worm);
            worm.spriteRenderer.sortingOrder = -i;
            worm.skeleton      .sortingOrder = -(i + 1);
            worm.spriteRenderer.color = gradientColors[i] = Color32.Lerp(GameController.Assets.wormColor, Color.black, (0.5f / WORM_BLOCK_AMOUNT) * i);
            worm.skeleton      .color = skeletonColor;
            if (i > 0)
                SetAnimation(worm, Animation.Grow, -1);
        }
        wormHead = wormQueue.Dequeue();

        wormEye.data.blockObject.transform.SetParent(wormHead.data.blockObject.transform);
        wormEye.data.blockObject.transform.localPosition = Vector3.zero;

        GameObject mouthAsset = Instantiate(GameController.Assets.wormMouth, wormHead.data.blockObject.transform);
        wormMouth = new Worm(mouthAsset, null, mouthAsset.GetComponent<SpriteRenderer>());
        bubblePops = wormMouth.data.blockObject.GetComponentsInChildren<ParticleSystem>();

        musicNote = wormHead.data.blockObject.GetComponent<ParticleSystem>();
        currentSong = songs[playerData.currentSongIndex];
        currentNoteColorIndex = Random.Range(0, GameController.Assets.gameColors.Length);

        headTrigger = Instantiate(GameController.Assets.headTrigger);
        headTrigger                                           .transform.SetParent(GameController.Game.playerObjectsHolder.transform);
        headTrigger.GetComponent<HeadTrigger>().parallaxFollow.transform.SetParent(GameController.Game.playerObjectsHolder.transform);
        thinkUI.transform.SetParent(headTrigger.transform);
        thinkUI.transform.localPosition = Vector3.zero;
        gridSprites = GameController.Assets.gridSprites;
        grid.transform.SetParent(headTrigger.transform);

        worm = new List<Worm>() { wormHead };
        for (int i = 0; i < playerData.length - 1; i++)
            worm.Add(wormQueue.Dequeue());
        UpdateConnections();

        undoOutlineHolder = new GameObject("UndoOutlineHolder");
        undoOutlineHolder .transform.SetParent(GameController.Game.playerObjectsHolder.transform);
        undoSpriteRenderer.transform.SetParent(undoOutlineHolder.transform);
        GameController.AddColorObject(ColorIndex.Time, undoSpriteRenderer, null, null, false);
        undoTree.UpdatePattern(this);

        InitInventory();
        foreach (ThinkOption to in thinkOptions)
            to.startPosition = to.optionObject.transform.localPosition;

        Map.Init();
    }
    
    void Update() {
        if (!GameController.levelInitialized)
            return;

        if (blinkTime <= 0) {
            if (!GameController.shooting && !GameController.resetting && !sleeping && !singing && wormEye.playingAnimation == null && !wormEye.IsCurrentAnimation(Animation.EyeBite))
                PlayAnimation(Animation.Blink);
            blinkTime = Random.Range(3, 10);
        }
        else
            blinkTime -= Time.deltaTime;

        if (resetLookCooldown <= 0) {
            if (!wormHead.IsCurrentAnimation(Animation.Look) && wormEye.data.blockObject.transform.localPosition != Vector3.zero)
                LookForward(false);
        }
        else
            resetLookCooldown -= Time.deltaTime;

        if (sleepTime > 0) sleepTime -= Time.deltaTime;
        else               Sleep(false);
        
        if (moveCooldown > 0) moveCooldown -= Time.deltaTime;
        else                  moveCooldown  = 0;
        
        if (GameController.paused) {
            currentHeldAction = (Action)(-1);
            return;
        }

        if (GameController.shooting && InputController.Get(Action.Shoot, PressType.Down))
            GameController.cancelBullets = true;
        
        if (!GameController.resolvedGameState)
            return;

        if (!GameController.resetting) {
            // Think abilities
            if (playerData.abilities[(int)AbilityIndex.Think] && InputController.Get(Action.Think) && Activate(Action.Think)) {
                WakeUp();

                if (thinkTime >= 1 || GameController.instantUI) {
                    if (!thinkOptions[0].optionSymbol.activeSelf) {
                        for (int i = 0; i < thinkOptions.Length; i++)
                            thinkOptions[i].optionSymbol.SetActive(playerData.thinkOptions[i]);
                    }
                    thinkTime = 1;
                }
                else
                    thinkTime += Time.deltaTime * THINK_SPEED;

                if (selectedThink == ThinkIndex.None) {
                    if (!thinking) {
                        PlayAnimation(Animation.Think);
                        thinkCenter.SetActive(true);
                        for (int i = 0; i < thinkOptions.Length; i++) {
                            thinkOptions[i].optionObject.transform.localPosition = thinkOptions[i].startPosition;
                            thinkOptions[i].optionObject.SetActive(playerData.thinkOptions[i]);
                        }
                        thinking = true;
                    }
                    
                    if (InputController.Get(Action.Up   , PressType.Down)) selectedThink = ThinkIndex.Map;
                    if (InputController.Get(Action.Down , PressType.Down)) selectedThink = ThinkIndex.Grid;
                    if (InputController.Get(Action.Left , PressType.Down)) selectedThink = ThinkIndex.Inventory;
                    if (InputController.Get(Action.Right, PressType.Down)) selectedThink = ThinkIndex.RoomInfo;

                    if (selectedThink != ThinkIndex.None) {
                        if (playerData.thinkOptions[(int)selectedThink]) {
                            switch (selectedThink) {
                                case ThinkIndex.Inventory:
                                    if (disableManualInventoryDisplay)
                                        selectedThink = ThinkIndex.None;
                                    break;

                                case ThinkIndex.RoomInfo:
                                    if (GameController.Level.roomInfo[0] == null && GameController.Level.roomInfo[1] == null)
                                        selectedThink = ThinkIndex.None;
                                    break;
                            }
                        }
                        else
                            selectedThink = ThinkIndex.None;

                        return;
                    }
                }
                else {
                    if (!thinkingOption) {
                        for (int i = 0; i < thinkOptions.Length; i++) {
                            if ((ThinkIndex)i != selectedThink)
                                thinkOptions[i].optionObject.SetActive(false);
                        }
                        thinkCenter.SetActive(false);

                        switch (selectedThink) {
                            case ThinkIndex.Grid:
                                GameController.ApplyFacing(Random.Range(0, 4), grid);
                                grid.SetActive(true);
                                break;

                            case ThinkIndex.Inventory:
                                foreach (InventoryItem ii in inventoryItems) {
                                    if (ii.displayCoroutine != null) {
                                        StopCoroutine(ii.displayCoroutine);
                                        ii.activationState = Activation.Off;
                                    }
                                }
                                break;
                        }

                        thinkSprites = GameController.Assets.thinkHideSprites;
                        ThinkOption to = thinkOptions[(int)selectedThink];
                        to.spriteRenderer.sprite = thinkSprites[thinkSprites.Length - 1];
                        to.optionObject.transform.localPosition = Vector3.zero;
                        thinkingOption = true;
                    }

                    switch (selectedThink) {
                        case ThinkIndex.Map:
                            Map.ShowMap(true);
                            break;

                        case ThinkIndex.Grid:
                            if (gridTime < 1 && !GameController.instantUI) gridTime += Time.deltaTime * GRID_SPEED * (gridTime + 1);
                            else                                           gridTime  = 1;
                            break;

                        case ThinkIndex.Inventory:
                            if (inventoryTime < 1 && !GameController.instantUI) inventoryTime += Time.deltaTime * INVENTORY_SPEED;
                            else                                                inventoryTime  = 1;
                            break;

                        case ThinkIndex.RoomInfo:
                            if (!showingRoomInfo)
                                ShowRoomInfo(true);
                            break;
                    }
                }
            }
            else {
                if (!thinkingOption) {
                    if (InputController.Get(Action.Think, PressType.Up) || (selectedThink != ThinkIndex.None && thinkOptions[(int)selectedThink].optionSymbol.activeSelf)) {
                        foreach (ThinkOption to in thinkOptions)
                            to.optionSymbol.SetActive(false);

                        thinkTime = 1;
                        Deactivate(Action.Think);
                    }

                    if (thinkTime > 0 && !GameController.instantUI) thinkTime -= Time.deltaTime * THINK_SPEED;
                    else                                            thinkTime  = 0;

                    if (thinkTime == 0 && (thinkCenter.activeSelf || selectedThink != ThinkIndex.None)) {
                        ResetAnimation();
                        thinkCenter.SetActive(false);
                        foreach (ThinkOption to in thinkOptions)
                            to.optionObject.SetActive(false);

                        thinkSprites = GameController.Assets.thinkShowSprites;
                        selectedThink = ThinkIndex.None;
                        thinking = false;
                        grid.SetActive(false);
                    }
                }
                else {
                    switch (selectedThink) {
                        case ThinkIndex.Map:
                            Map.ShowMap(false);
                            if (!Map.mapUI.activeSelf)
                                thinkingOption = false;
                            break;

                        case ThinkIndex.Grid:
                            if (gridTime <= 0 || GameController.instantUI) {
                                gridTime = 0;
                                thinkingOption = false;
                            }
                            else
                                gridTime -= Time.deltaTime * GRID_SPEED * (gridTime + 1);
                            break;

                        case ThinkIndex.Inventory:
                            if (inventoryTime <= 0 || GameController.instantUI) {
                                if (GameController.instantUI)
                                    SetInventoryPositions(0);

                                inventoryTime = 0;
                                thinkingOption = false;
                            }
                            else
                                inventoryTime -= Time.deltaTime * INVENTORY_SPEED;
                            break;

                        case ThinkIndex.RoomInfo:
                            if (showRoomInfo) {
                                ShowRoomInfo(false);
                                thinkingOption = false;
                            }
                            break;
                    }
                }
            }
        }

        if (thinking) {
            if (thinkingOption) {
                switch (selectedThink) {
                    case ThinkIndex.Grid     : gridSprite.sprite = gridSprites[GameController.GetCurveIndex(0, gridSprites.Length - 1, gridTime     )]; break;
                    case ThinkIndex.Inventory: SetInventoryPositions          (GameController.GetCurveIndex(0, inventoryMoveTotal - 1, inventoryTime)); break;
                }
            }
            else {
                int thinkIndex = GameController.GetCurveIndex(0, thinkSprites.Length - 1, thinkTime);
                for (int i = 0; i < thinkOptions.Length; i++) {
                    if (playerData.thinkOptions[i])
                        thinkOptions[i].spriteRenderer.sprite = thinkSprites[thinkIndex];
                }
            }
            return;
        }

        if (GameController.resetting) {
            if (InputController.Get(Action.Reset)) {
                if (currentResetTime < RESET_TIME) {
                    GameController.SetResetMeterLength(currentResetTime);
                    currentResetTime += Time.deltaTime * RESET_TIME_SPEED;
                }
                else {
                    GameController.ResetRoom();
                    return;
                }
            }
            else {
                if (InputController.Get(Action.Reset, PressType.Up)) {
                    GameController.ResolveReset();
                    return;
                }
            }
            return;
        }

        if (playerData.abilities[(int)AbilityIndex.Force]) {
            if (InputController.Get(Action.Force) && Activate(Action.Force)) {
                WakeUp();

                if (!forceMoving) {
                    SetForceDirection(Coordinates.Zero);
                    Look(false);
                    SetEyeColor(GameController.GetGameColor(ColorIndex.Force));
                    forceMoving = true;
                    return;
                }

                if (InputController.Get(Action.Up   )) { SetForceDirection(Coordinates.Up   ); return; }
                if (InputController.Get(Action.Down )) { SetForceDirection(Coordinates.Down ); return; }
                if (InputController.Get(Action.Left )) { SetForceDirection(Coordinates.Left ); return; }
                if (InputController.Get(Action.Right)) { SetForceDirection(Coordinates.Right); return; }
                return;
            }
            if (forceMoving && InputController.Get(Action.Force, PressType.None)) {
                ResetEyeColor();
                Look(false);
                Deactivate(Action.Force);
                forceMoving = false;
                return;
            }
        }

        if (playerData.abilities[(int)AbilityIndex.Grow] && InputController.Get(Action.Grow) && Activate(Action.Grow)) {
            if (moveCooldown <= 0) {
                if (!shaking) {
                    PlayAnimation(Animation.Shake, Animation.HeadDefault);
                    GameController.PlayRandomSound(AudioController.playerShake);
                    GameController.ShowLengthMeter(true);
                    shaking = true;
                }
                else {
                    if (wormEye.playingAnimation == null || wormEye.IsCurrentAnimation(Animation.Blink))
                        PlayAnimation(Animation.Shake, Animation.HeadDefault);
                }
                WakeUp();
                
                if      (InputController.Get(Action.Up   , PressType.Down)) GrowPlayer(Coordinates.Up   );
                else if (InputController.Get(Action.Down , PressType.Down)) GrowPlayer(Coordinates.Down );
                else if (InputController.Get(Action.Left , PressType.Down)) GrowPlayer(Coordinates.Left );
                else if (InputController.Get(Action.Right, PressType.Down)) GrowPlayer(Coordinates.Right);
            }
        }
        else {
            if (shaking) {
                ResetAnimation();
                GameController.ShowLengthMeter(false);
                Deactivate(Action.Grow);
                shaking = false;
            }

            if (moveCooldown <= 0) {
                if (!promptMovementOverride) {
                    if (InputController.Get(Action.Up   )) { MovePlayer(Coordinates.Up   ); return; }
                    if (InputController.Get(Action.Down )) { MovePlayer(Coordinates.Down ); return; }
                    if (InputController.Get(Action.Left )) { MovePlayer(Coordinates.Left ); return; }
                    if (InputController.Get(Action.Right)) { MovePlayer(Coordinates.Right); return; }
                    Sing(false);
                    holdingMove = false;
                }
                
                if (playerData.abilities[(int)AbilityIndex.Shoot] && canShoot && InputController.Get(Action.Shoot)) {
                    WakeUp();
                    undoTree.UpdatePattern(this);
                    GameController.StartAction();
                    if (GameController.Grid.GetData(wormHead.data.blockData.coordinates, Layer.Dig) != null) {
                        GameController.FlagGameState(true);
                        GameController.MoveBlocked(MoveType.Player);
                    }
                    else
                        GameController.Shoot(wormHead.data.blockData.coordinates, wormHead.data.blockData.facing);
                }
            }
            
            if (playerData.abilities[(int)AbilityIndex.Reset] && InputController.Get(Action.Reset)) {
                WakeUp();
                GameController.UpdateActiveResetOutlines();
                GameController.ShowResetMeter(true);
                GameController.resetting = true;
                ResetAnimation();
                GameController.FadeResetOutlines();
                ResetEyes(true);
            }
            
            if (playerData.abilities[(int)AbilityIndex.Undo]) {
                if (InputController.Get(Action.Undo)) {
                    GameController.Undo(true);
                    undoing = true;
                }
                else {
                    if (undoing) {
                        GameController.Undo(false);
                        GameController.Audio.UpdatePositionalAudio(wormHead.data.blockData.coordinates);
                        undoing = false;
                    }
                }
            }
        }

        bool Activate(Action action) {
            if (currentHeldAction == action || currentHeldAction == (Action)(-1)) {
                currentHeldAction = action;
                return true;
            }
            return false;
        }
        void Deactivate(Action action) {
            if (currentHeldAction == action)
                currentHeldAction = (Action)(-1);
        }
    }
    
    public void MovePlayer(Coordinates direction, bool tunnelMove = false) {
        GameController.FlagGameState(true);
        WakeUp();
        resetLookCooldown = RESET_LOOK_COOLDOWN_MAX;

        // Pressed backwards
        if (!tunnelMove && direction == -Coordinates.FacingDirection[wormHead.data.blockData.facing]) {
            Coordinates tunnelCoord = wormHead.data.blockData.connectedBlocks[1];
            Data tunnelData = GameController.Grid.GetData(tunnelCoord, Layer.Tunnel);
            if (tunnelData != null) {
                // Move backwards into tunnel
                tunnelCoord = tunnelData.blockData.connectedBlocks[0];
                if (tunnelData.blockData.coordinates == tunnelData.blockData.connectedBlocks[1])
                    tunnelData = GameController.Grid.GetData(tunnelCoord, Layer.Tunnel);
                if (tunnelData.blockData.coordinates == tunnelCoord) {
                    GameController.StartAction();
                    GameController.EnterTunnel(Map.currentRoom.GetTunnel(tunnelData), tunnelData, true);
                }
            }
            else
                Sing(true);

            GameController.FlagGameState(false);
            return;
        }

        moveCooldown = MOVE_COOLDOWN_MAX;
        Coordinates nextCoord = wormHead.data.blockData.coordinates + direction;
        if (!GameController.Grid.WithinBounds(nextCoord + direction)) {
            GameController.PlayRandomSound(AudioController.playerBlocked);
            GameController.FlagGameState(false);
            return;
        }

        if (!tunnelMove) {
            GameController.StartAction();

            // Check if entering tunnel
            Data data = GameController.Grid.GetData(nextCoord, Layer.Tunnel);
            if (data != null && data.blockData.IsPrimary()) {
                Data doorData = GameController.Grid.GetData(nextCoord, Layer.Misc);
                if (doorData != null) {
                    LevelBlock.BlockItem panelItem = doorData.levelBlock.GetBlockItem("Panel");
                    if (panelItem != null) {
                        TunnelPanel tp = (TunnelPanel)panelItem.script;
                        if (!tp.IsOpen()) {
                            GameController.MoveBlocked(MoveType.Player);
                            return;
                        }
                    }
                }

                // Check if moving into hole of tunnel
                Coordinates backDirection = -Coordinates.FacingDirection[data.blockData.facing];
                if (direction == backDirection) {
                    GameController.EnterTunnel(Map.currentRoom.GetTunnel(data), data, false);
                    GameController.FlagGameState(false);
                }
                else
                    GameController.MoveBlocked(MoveType.Player);

                return;
            }

            if (!devNoClip && !CanMoveNextBlock(direction))
                return;
        }

        wormHead.data.SetMoving(true, direction);
        if (!wormEye.IsCurrentAnimation(Animation.Roll) && !wormEye.IsCurrentAnimation(Animation.EyeBite) && !wormHead.IsCurrentAnimation(Animation.EatOpen))
            ResetAnimation();

        // Update which way worm blocks are facing and check which ones move straight or turn
        bool[] moveTypes = new bool[worm.Count];
        int oldFacing = wormHead.data.blockData.facing;
        wormHead.data.blockData.facing = direction.x == 0 ? (direction.y > 0 ? 1 : 3) 
                                                          : (direction.x > 0 ? 0 : 2);

        for (int i = worm.Count - 1; i > 0; i--) {
            BlockData bd = worm[i].data.blockData;
            BlockData nextData = worm[i - 1].data.blockData;
            
            if (bd.facing != nextData.facing) {
                moveTypes[i] = true;
                int upper = bd.facing + 1;
                int lower = bd.facing - 1;
                if (upper > 3) upper = 0;
                if (lower < 0) lower = 3;
                worm[i].spriteRenderer.flipY = worm[i].skeleton.flipY = nextData.facing == lower;
            }

            bd.facing = nextData.facing;
        }
        
        int rollCheck = 0;
        bool doRollCheck = true;
        Data disableDigData = null;
        foreach (Worm w in worm) {
            Coordinates currCoord = w.data.blockData.coordinates;
            GameController.Grid.SetData(nextCoord, w.data, false);
            nextCoord = currCoord;

            if (!tunnelMove) {
                // If player is switching direction on the x axis and half or more of the player is in line,
                // roll the head around so it is upright
                if (doRollCheck && currCoord.y == wormHead.data.blockData.coordinates.y) rollCheck++;
                else                                                                     doRollCheck = false;
            }

            // If player is exiting a dig group, prepare to disable its outline
            if (w == worm[worm.Count - 1]) {
                if (GameController.Grid.GetData(w.data.blockData.coordinates, Layer.Dig) == null)
                    disableDigData = GameController.Grid.GetData(currCoord, Layer.Dig);
            }
        }

        bool rolled = false;
        bool fall = FallCheck();
        if (!tunnelMove) {
            if (!playerData.ignoreGravity && fall) {
                if (shaking) {
                    shaking = false;
                    GameController.ShowLengthMeter(false);
                }
                PlayAnimation(Animation.Fall);
            }
            else {
                if (rollCheck >= playerData.length / 2) {
                    // Make sure player hasn't already rolled
                    if ((worm[1].data.blockData.coordinates.x < wormHead.data.blockData.coordinates.x &&  playerData.headFlipped)
                     || (worm[1].data.blockData.coordinates.x > wormHead.data.blockData.coordinates.x && !playerData.headFlipped)) 
                    {
                        rolled = true;
                    }
                }
            }
        }

        UpdateConnections();
        InsideBlockCheck();

        if (!tunnelMove) {
            if (fall && !playerData.ignoreGravity)
                PlayAnimation(Animation.Fall);
            if (!holdingMove)
                holdingMove = true;
            GameController.PlayRandomSound(AudioController.playerMove);
            undoTree.UpdatePattern(this);
        }
        else
            holdingMove = false;

        if (rolled)
            Roll();

        GameController.CheckTemporaryOpenDoors();
        GameController.CheckForegrounds();
        PlayGroundMovingParticles();
        StartCoroutine(MoveWormPieces(moveTypes, oldFacing != wormHead.data.blockData.facing, disableDigData));
    }
    // Move worm pieces to follow each other
    IEnumerator MoveWormPieces(bool[] moveTypes, bool headCorner, Data disableDigData) {
        yield return new WaitWhile(() => headBump);

        LookForward(true);
        GameController.ApplyFacing(wormHead.data.blockData.facing, wormHead.data.blockObject);

        Vector2[] positions = new Vector2[worm.Count];
        for (int i = 0; i < worm.Count; i++) {
            positions[i] = worm[i].data.blockObject.transform.position;
            if (i > 0)
                SetAnimation(worm[i], Animation.Corner);

            // Play particles when a body piece enters or exits a dig group
            if (i < worm.Count - 1) {
                BlockData wormDataA = worm[i    ].data.blockData;
                BlockData wormDataB = worm[i + 1].data.blockData;
                if (GameController.Grid.GetData(wormDataA.coordinates, Layer.Tunnel) != null 
                 || GameController.Grid.GetData(wormDataB.coordinates, Layer.Tunnel) != null)
                    continue;

                Data digDataA = GameController.Grid.GetData(wormDataA.coordinates, Layer.Dig);
                Data digDataB = GameController.Grid.GetData(wormDataB.coordinates, Layer.Dig);
                if (digDataA == null && digDataB != null) PlayDigParticles(wormDataB, wormDataA);
                if (digDataA != null && digDataB == null) PlayDigParticles(wormDataA, wormDataB);

                void PlayDigParticles(BlockData a, BlockData b) {
                    ParticlePooler.Particle p = GameController.Particles.GetParticle("DigPuffParticles");
                    if (p != null) {
                        GameController.ApplyFacing(Coordinates.GetFacing(a.coordinates - b.coordinates), p.particleObject);
                        GameController.ApplyCoordinates(a.coordinates, p.particleObject);
                        GameController.Particles.PlayParticle(p);
                    }
                }
            }
        }
        // If the head is changing direction, advance the position one step so the sprite doesn't extend past the next body piece
        if (headCorner)
            wormHead.data.blockObject.transform.position = positions[0] + GameController.GetVector(Coordinates.FacingDirection[wormHead.data.blockData.facing]);
        
        float time = 0;
        int moveIndex = 0;
        while (time < 1) {
            int distance = GameController.GetCurveIndex(0, GameController.BLOCK_SIZE, time);
            if (distance >= moveIndex + 1) {
                moveIndex = distance;
                for (int move = 0; move < moveTypes.Length; move++) {
                    Coordinates direction = Coordinates.FacingDirection[worm[move].data.blockData.facing];
                    worm[move].data.moveDirection = direction;

                    // If piece is moving straight, then just move it, else play its cornering animation
                    if (moveTypes[move]) worm[move].SetFrame(moveIndex);
                    else                 worm[move].data.blockObject.transform.position = positions[move] + GameController.GetVector(direction, moveIndex);
                }
            }

            time += Time.deltaTime * GameController.BLOCK_MOVE_SPEED;
            yield return null;
        }

        for (int i = 0; i < worm.Count; i++) {
            worm[i].data.ApplyData();
            // Reset sprites to straight pieces
            if (i > 0)
                SetAnimation(worm[i], Animation.Grow, -1);
        }

        if (disableDigData != null)
            SetDigging(false, GameController.digOutlines[disableDigData.levelBlock]);

        wormHead.data.SetMoving(false);
        LookCheck();
        GameController.Audio.UpdatePositionalAudio(wormHead.data.blockData.coordinates);
        GameController.UpdatePistonsIfBlocked();
        GameController.ApplyGravity();
        GameController.FlagGameState(false);
    }
    
    public void GrowPlayer(Coordinates direction) {
        GameController.FlagGameState(true);
        holdingMove = false;
        StartCoroutine(ieGrowPlayer(direction));
    }
    IEnumerator ieGrowPlayer(Coordinates direction) {
        moveCooldown = MOVE_COOLDOWN_MAX;
        ResetAnimation();
        GameController.StartAction();

        // Shrink if going backwards
        if (playerData.length > 3 && direction == -Coordinates.FacingDirection[wormHead.data.blockData.facing]) {
            if (GameController.Grid.GetData(worm[1].data.blockData.coordinates, Layer.Tunnel) != null) {
                GameController.MoveBlocked(MoveType.Player);
                yield break;
            }
            
            GameController.PlayRandomSound(AudioController.playerShrink);

            playerData.length--;
            GameController.IncrementLengthMeter(-1);
            wormHead.data.blockData.facing = worm[2].data.blockData.facing;
            SetAnimation(worm[1], Animation.Grow);

            Vector2 position = wormHead.data.blockObject.transform.position;
            float time = 0;
            int moveIndex = 0;
            while (time < 1) {
                int distance = Mathf.RoundToInt(Mathf.Lerp(0, GameController.BLOCK_SIZE, time));
                if (distance >= moveIndex + 1) {
                    moveIndex = distance;
                    wormHead.data.blockObject.transform.position = position + GameController.GetVector(direction, moveIndex);
                    worm[1].SetFrame(GameController.BLOCK_SIZE - moveIndex);

                    if (moveIndex > GameController.BLOCK_SIZE - 1)
                        GameController.ApplyFacing(wormHead.data.blockData.facing, wormHead.data.blockObject);
                }

                time += Time.deltaTime * GameController.BLOCK_MOVE_SPEED;
                yield return null;
            }

            // Remove worm block behind head
            worm[1].SetFrame(-1);
            GameController.Grid.RemoveData(worm[1].data);
            SetWormPieceActive(false);
            GameController.Grid.MoveData(direction, wormHead.data, false);
            wormHead.data.ApplyData();
            
            UpdateHeadFlip();
            UpdateGradients(false);
            UpdateConnections();
            undoTree.UpdatePattern(this);
            InsideBlockCheck();
            GameController.CheckForegrounds();
            
            // Do fall check for shrinking off ledge
            if (!playerData.ignoreGravity && FallCheck()) {
                if (shaking) {
                    GameController.ShowLengthMeter(false);
                    shaking = false;
                }
                PlayAnimation(Animation.Fall);
            }
            GameController.ApplyGravity();

            GameController.FlagGameState(false);
        }
        else {
            // Grow in direction
            if (playerData.length < playerData.GetMaxLength() && playerData.length < WORM_BLOCK_AMOUNT && direction != -Coordinates.FacingDirection[wormHead.data.blockData.facing]) {
                if (!CanMoveNextBlock(direction))
                    yield break;

                headBump = false;
                GameController.PlayRandomSound(AudioController.playerGrow);
                if (!(wormHead.IsCurrentAnimation(Animation.EatOpen) || wormHead.IsCurrentAnimation(Animation.HeadBite)))
                    ResetAnimation();

                // Add worm block behind head
                playerData.length++;
                GameController.IncrementLengthMeter(1);
                int oldFacing = wormHead.data.blockData.facing;
                wormHead.data.blockData.facing = direction.x == 0 ? (direction.y > 0 ? 1 : 3) : (direction.x > 0 ? 0 : 2);
                GameController.ApplyFacing(wormHead.data.blockData.facing, wormHead.data.blockObject);
                GameController.Grid.MoveData(direction, wormHead.data, false);
                SetWormPieceActive(true);
                worm[1].data.blockData.coordinates = wormHead.data.blockData.coordinates - direction;
                worm[1].data.blockData.facing = wormHead.data.blockData.facing;
                GameController.Grid.AddData(worm[1].data);
                worm[1].data.ApplyData();
                
                SetAnimation(worm[1], Animation.Grow);
                UpdateHeadFlip();
                UpdateGradients(false);
                UpdateConnections();
                undoTree.UpdatePattern(this);

                Vector2 position = wormHead.data.blockObject.transform.position;
                if (oldFacing != wormHead.data.blockData.facing)
                    wormHead.data.blockObject.transform.position = position + GameController.GetVector(Coordinates.FacingDirection[wormHead.data.blockData.facing]);

                float time = 0;
                int moveIndex = 0;
                while (time < 1) {
                    int distance = Mathf.RoundToInt(Mathf.Lerp(0, GameController.BLOCK_SIZE, time));
                    if (distance >= moveIndex + 1) {
                        moveIndex = distance;
                        wormHead.data.blockObject.transform.position = position + GameController.GetVector(direction, moveIndex);
                        worm[1].SetFrame(moveIndex);
                    }
                    time += Time.deltaTime * GameController.BLOCK_MOVE_SPEED;
                    yield return null;
                }

                worm[1].SetFrame(-1);
                wormHead.data.ApplyData();
                GameController.ApplyGravity();
                GameController.FlagGameState(false);
            }
            else
                GameController.MoveBlocked(MoveType.Player);
        }
    }

    public void SetWormPieceActive(bool active) {
        if (active) {
            worm.Insert(1, wormQueue.Dequeue());
            worm[1].data.blockObject.transform.SetParent(GameController.Game.playerHolder.transform);
            worm[1].data.ApplyData();

            for (int i = 1; i < worm.Count; i++) {
                worm[i].spriteRenderer.sortingOrder = -i;
                worm[i].skeleton.sortingOrder = -(i + 1);
            }
            worm[1].spriteRenderer.color = gradientColors[1];
        }
        else {
            worm[1].data.blockObject.transform.SetParent(wormHolder.transform);
            worm[1].data.blockObject.transform.localPosition = Vector3.zero;
            wormQueue.Enqueue(worm[1]);
            worm.RemoveAt(1);
        }
    }

    bool CanMoveNextBlock(Coordinates direction) {
        Data[] datas = GameController.Grid.GetData(wormHead.data.blockData.coordinates + direction, Layer.Tunnel , 
                                                                                                    Layer.Player ,
                                                                                                    Layer.Dig    ,
                                                                                                    Layer.Block  , 
                                                                                                    Layer.Piston , 
                                                                                                    Layer.Collect, 
                                                                                                    Layer.Misc   );
        foreach (Data d in datas) {
            if (d == null)
                continue;

            switch (d.layer) {
                case Layer.Tunnel:
                case Layer.Player:
                    GameController.MoveBlocked(MoveType.Player);
                    return false;

                case Layer.Dig:
                    if (!playerData.abilities[(int)AbilityIndex.Dig] || d.blockData.facing == -1)
                        break;

                    SetDigging(true, GameController.digOutlines[d.levelBlock]);
                    GameController.DestroyDig(d);
                    return true;

                case Layer.Block:
                    if (!GameController.MoveBlock(d, direction, MoveType.Player)) {
                        GameController.FlagGameState(false);
                        return false;
                    }
                    break;

                case Layer.Piston:
                    if (d.blockData.state == (int)Activation.On) {
                        GameController.MoveBlocked(MoveType.Player);
                        return false;
                    }
                    break;

                case Layer.Collect:
                    if (!d.blockData.destroyed)
                        GameController.Eat(d);
                    return true;

                case Layer.Misc:
                    if (d.blockData.blockName == "GateSlot") {
                        Activation state = (Activation)d.blockData.state;
                        switch (state) {
                            case Activation.Off:
                                if (datas[3] != null)
                                    return true;

                                Coordinates headCoord = wormHead.data.blockData.coordinates;
                                if (playerData.fragments > 0 && direction == Coordinates.FacingDirection[wormHead.data.blockData.facing]) {
                                    GameController.PlaceFragment(d);
                                    GameController.FlagGameState(false);
                                    return false;
                                }
                                return true;

                            case Activation.On:
                                GameController.Eat(d);
                                return true;
                        }
                    }
                    return true;
            }
        }
        return true;
    }

    public void SetDigging(bool active, SpriteRenderer digOutline, Activation instant = Activation.Off) {
        if (GameController.digMask == null)
            return;

        if (digOutline != null) {
            if (digOutline.transform.localPosition.z != (active ? 1 : 0) || instant == Activation.Alt)
                StartCoroutine(ShowDigOutline(digOutline, active, instant));

            active = false;
            foreach (SpriteRenderer sr in GameController.digOutlines.Values) {
                if (sr.transform.localPosition.z == 1) {
                    active = true;
                    break;
                }
            }
        }
        else {
            foreach (SpriteRenderer sr in GameController.digOutlines.Values) {
                if (sr.transform.localPosition.z == 1)
                    StartCoroutine(ShowDigOutline(sr, false, instant));
            }
            if (active)
                InsideBlockCheck(instant);
        }

        GameController.digMask.enabled = active;
        playerData.digging = playerData.ignoreGravity = active;
    }
    IEnumerator ShowDigOutline(SpriteRenderer sr, bool active, Activation instant) {
        sr.transform.localPosition = active ? Vector3.forward : Vector3.zero;

        float fromAlpha = 0.05f;
        float toAlpha   = 0.75f;
        if (!active) {
            fromAlpha = 0.75f;
            toAlpha   = 0.05f;
        }
        Color c = sr.color;

        if (instant == Activation.Off) {
            float time = 0;
            while (time < 1) {
                c.a = Mathf.Lerp(fromAlpha, toAlpha, time);
                sr.color = c;
                time += Time.deltaTime * 5;
                yield return null;
            }
        }
        c.a = toAlpha;
        sr.color = c;
    }

    public void ResetHeadTrigger() {
        headTrigger.transform.position = wormHead.data.blockObject.transform.position;
        headTrigger.SetActive(true);
    }
    public void BuildWorm(MapController.RoomData.Tunnel tunnel) {
        GameController.Level.playerIcon.SetActive(false);
        UpdateLength();

        List<BlockData> positionData = null;
        if (tunnel == null) {
            // Build player starting from player icon in editor
            positionData = new List<BlockData>();
            Coordinates iconCoord = GameController.GetCoordinates(GameController.Level.playerIcon.transform.position);
            positionData.Add(new BlockData("", iconCoord));
        }
        else {
            // Build player inside of tunnel
            playerData.ignoreGravity = true;
            positionData = GetTunnelPositions(tunnel);

            if (GameController.initialStart) {
                Data tunnelData = GameController.Grid.GetData(positionData[0].coordinates, Layer.Tunnel);
                if (tunnelData != null) {
                    // Check if tunnel door needs to start open
                    LevelBlock.BlockItem panelItem = GameController.Grid.GetData(positionData[0].coordinates, Layer.Misc).levelBlock.GetBlockItem("Panel");
                    if (panelItem != null) {
                        Data panelData = GameController.Grid.GetData(positionData[1].coordinates, Layer.Misc);
                        if (panelData.blockData.state == (int)Activation.Off) {
                            TunnelPanel tp = (TunnelPanel)panelItem.script;
                            if (tp.lightColors != null && tp.lightColors.Length > 0)
                                tp.SetPanel(Activation.Alt);
                        }
                    }
                }

                // If there is space in front of the tunnel, build the player so its head is sticking out
                Coordinates forwardCoord = positionData[0].coordinates + Coordinates.FacingDirection[positionData[0].facing];
                if (GameController.Grid.GetData(forwardCoord, Layer.Block) == null)
                    positionData.Insert(0, new BlockData("", forwardCoord, positionData[0].facing));
            }
        }

        // Build player according to position data until no more positions left, then keep building onto last piece
        for (int i = 0; i < worm.Count; i++) {
            if (i < positionData.Count) {
                worm[i].data.blockData.facing      = positionData[i].facing;
                worm[i].data.blockData.coordinates = positionData[i].coordinates;
            }
            else {
                worm[i].data.blockData.facing = worm[i - 1].data.blockData.facing;
                Coordinates backwardCoord     = worm[i - 1].data.blockData.coordinates - Coordinates.FacingDirection[worm[i].data.blockData.facing];
                worm[i].data.blockData.coordinates = backwardCoord;
            }

            worm[i].data.ApplyData();
            GameController.Grid.AddData(worm[i].data);
            worm[i].data.blockObject.transform.SetParent(GameController.Game.playerHolder.transform);
            worm[i].data.blockObject.SetActive(true);
        }

        wormHead.data.SetMoving(false);
        playerData.headFlipped = wormHead.data.blockData.coordinates.x < worm[1].data.blockData.coordinates.x;
        UpdateHeadFlip();
        UpdateGradients(true);
        UpdateConnections();
        InsideBlockCheck();

        GameController.fallDatas[0] = new GameController.FallData(wormHead.data);
        GameController.Audio.UpdatePositionalAudio(wormHead.data.blockData.coordinates);

        if (GameController.initialStart) {
            sleeping  = false;
            sleepTime = 0;
            ResetHeadTrigger();
            GameController.initialStart = false;

            if (GameController.devMode)
                return;

            if (!playerData.promptTriggers[5]) {
                // Player hasn't activated Move prompt and is starting a new game
                Sleep(false, true);
                StartCoroutine(Intro());
                IEnumerator Intro() {
                    // Check if player switched or deleted save before playing
                    int save    = GameController.currentSave;
                    string room = GameController.currentRoom;
                    yield return new WaitWhile(() => GameController.paused);
                    while (GameController.gameStateFlags > 1) {
                        if (save != GameController.currentSave || room != GameController.currentRoom)
                            yield break;
                        yield return null;
                    }
                    if (save != GameController.currentSave || room != GameController.currentRoom)
                        yield break;
                    
                    GameController.canPause = false;
                    GameController.FlagGameState(true);

                    // Make sure enough time passes to see intro dream after pause menu is closed
                    float dreamTime = 0;
                    while (GameController.gameStateFlags > 1) {
                        dreamTime += Time.deltaTime;
                        yield return null;
                    }
                    yield return new WaitForSeconds(Mathf.Max(0, 10 - dreamTime));

                    GameController.ActivateDream(false);

                    yield return new WaitForSeconds(2);

                    GameController.ActivatePromptCloud(PromptIndex.Move, true);
                    GameController.FlagGameState(false);
                    GameController.canPause = true;
                }
            }
            else {
                // Check if player has an unfinished think prompt from last play session
                ThinkIndex thinkIndex = GameController.CheckThinkPromptActivation();
                if (thinkIndex != (ThinkIndex)(-1)) {
                    Sleep(true, true);
                    StartCoroutine(CheckThinkDelay());
                }
                else
                    Sleep(false, true);

                IEnumerator CheckThinkDelay() {
                    // Check if player switched or deleted save before playing
                    int save    = GameController.currentSave;
                    string room = GameController.currentRoom;
                    yield return new WaitWhile(() => GameController.paused);
                    while (!GameController.resolvedGameState) {
                        if (save != GameController.currentSave || room != GameController.currentRoom)
                            yield break;
                        yield return null;
                    }
                    if (save != GameController.currentSave || room != GameController.currentRoom)
                        yield break;

                    GameController.canPause = false;
                    GameController.FlagGameState(true);

                    yield return new WaitForSeconds(2);

                    ActivateThinkOption(thinkIndex, thinkIndex == ThinkIndex.RoomInfo, true);
                    GameController.FlagGameState(false);
                    GameController.canPause = true;
                }
            }
        }
        else {
            if (tunnel != null)
                GameController.ExitTunnel(tunnel, true);
        }
    }
    public void SetToTunnel(MapController.RoomData.Tunnel tunnel) {
        playerData.ignoreGravity = true;

        foreach (Worm w in worm)
            GameController.Grid.RemoveData(w.data);

        // Build player according to position data until no more positions left, then keep building onto last piece
        List<BlockData> positionData = GetTunnelPositions(tunnel);
        for (int i = 0; i < worm.Count; i++) {
            if (i < positionData.Count) {
                worm[i].data.blockData.facing = positionData[i].facing;
                GameController.Grid.SetData(positionData[i].coordinates, worm[i].data, true);
            }
            else {
                worm[i].data.blockData.facing = worm[i - 1].data.blockData.facing;
                Coordinates backwardCoord     = worm[i - 1].data.blockData.coordinates - Coordinates.FacingDirection[worm[i].data.blockData.facing];
                GameController.Grid.SetData(backwardCoord, worm[i].data, true);
            }
        }

        wormHead.data.SetMoving(false);
        playerData.headFlipped = wormHead.data.blockData.coordinates.x < worm[1].data.blockData.coordinates.x;
        UpdateHeadFlip();
        UpdateGradients(true);
        UpdateConnections();
        
        GameController.ExitTunnel(tunnel, false);
    }
    public List<BlockData> GetTunnelPositions(MapController.RoomData.Tunnel tunnel) {
        List<BlockData> positionData = new List<BlockData>();
        for (int i = 0; i < tunnel.connectedBlocks.Length; i++) {
            Coordinates c = tunnel.connectedBlocks[i];
            BlockData tunnelData = GameController.Grid.GetData(c, Layer.Tunnel).blockData;
            positionData.Add(new BlockData("", c, positionData.Count == 0 ? tunnelData.facing : Coordinates.GetFacing(positionData[positionData.Count - 1].coordinates - c)));

            // Check for local tunnel and attach it
            if (i == tunnel.connectedBlocks.Length - 1) {
                Coordinates backwardCoord = c - Coordinates.FacingDirection[tunnelData.facing];
                if (GameController.Grid.WithinBounds(backwardCoord)) {
                    Data localTunnelData = GameController.Grid.GetData(backwardCoord, Layer.Tunnel);
                    if (localTunnelData != null) {
                        for (int j = localTunnelData.blockData.connectedBlocks.Length - 1; j >= 0; j--) {
                            Coordinates cb = localTunnelData.blockData.connectedBlocks[j];
                            positionData.Add(new BlockData("", cb, Coordinates.GetFacing(positionData[positionData.Count - 1].coordinates - cb)));
                        }
                    }
                }
            }
        }

        return positionData;
    }
    
    void SetForceDirection(Coordinates direction) {
        if (direction == GameController.forceDirection) {
            if (direction == Coordinates.Zero)
                GameController.SetForceDirection(direction);
            return;
        }

        GameController.StartAction();
        GameController.SetForceDirection(direction);
        if (direction != Coordinates.Zero) {
            Look(true, Coordinates.GetFacing(direction));
            SetEyeColor(GameController.GetGameColor(ColorIndex.Force));
            StartCoroutine(ResolveForceMove());
        }
    }
    IEnumerator ResolveForceMove() {
        yield return new WaitWhile(() => GameController.BlocksForceMoving());

        ResetEyeColor();
        Look(false);
    }

    public void CollectEyes(Color32 color) {
        StartCoroutine(ieCollectEyes(color));
    }
    IEnumerator ieCollectEyes(Color32 color) {
        SetEyeColor(color);
        GameController.PlayRandomSound(AudioController.playerEyeGlow);

        float fromIntensity = 0;
        float toIntensity   = 0.2f;

        float time  = 0;
        float speed = 7.5f;
        while (time < 1) {
            wormEye.light.intensity = Mathf.Lerp(fromIntensity, toIntensity, GameController.GetCurve(time));

            time += Time.deltaTime * speed;
            yield return null;
        }
        wormEye.light.intensity = toIntensity;

        yield return null;

        time = 0;
        while (time < 1) {
            wormEye.light.intensity = Mathf.Lerp(toIntensity, fromIntensity, GameController.GetCurve(time));

            time += Time.deltaTime * speed;
            yield return null;
        }
        wormEye.light.intensity = fromIntensity;

        ResetEyeColor();
    }
    
    public void UndoEyes() {
        Look(false, 0);
        StartCoroutine(ieUndoEyes());
    }
    IEnumerator ieUndoEyes() {
        SetEyeColor(GameController.GetGameColor(ColorIndex.Time));

        yield return new WaitForSeconds(0.1f);

        ResetEyeColor();
    }
    public void ResetEyes(bool enabled) {
        Look(false, 0);

        if (!enabled) {
            wormEye.light.intensity = 0.2f;
            ResetEyeColor();
        }
        else
            wormEye.light.enabled = true;
    }
    
    public void ColorEyes() {
        StartCoroutine(ieColorEyes());
    }
    IEnumerator ieColorEyes() {
        canShoot = false;
        GameController.CycleCurrentCrystalColor();
        while (!playerData.colors[GameController.colorCycle])
            GameController.CycleCurrentCrystalColor();

        int cycle = GameController.colorCycle;
        SetEyeColor(GameController.GetGameColor(cycle));
        while (GameController.shooting) {
            WakeUp();

            if (playerData.colors[cycle]) {
                Color32 cycleColor = GameController.GetGameColor(cycle);
                SetEyeColor(cycleColor);
                GameController.SetBulletColor(cycleColor);
                yield return new WaitForSeconds(0.1f);
            }
            if (++cycle >= GameController.CRYSTAL_COLORS) cycle = 0;
        }
        ResetEyeColor();

        yield return new WaitForSeconds(0.25f);

        canShoot = true;
    }

    void SetEyeColor(Color32 color) {
        wormEye.light.enabled = true;
        wormEye.light.color = wormEye.spriteRenderer.color = color;
    }
    void ResetEyeColor() {
        wormEye.light.enabled = false;
        wormEye.spriteRenderer.color = GameController.Assets.eyeColor;
    }

    // Roll head to be right side up
    void Roll() {
        Look(false, 0);
        LookForward(false);
        playerData.headFlipped = !playerData.headFlipped;
        UpdateHeadFlip();
        if (!wormEye.IsCurrentAnimation(Animation.EyeBite))
            PlayAnimation(Animation.Roll, Animation.HeadDefault);
        Invoke("LookCheck", 0.2f);
    }

    // Check if player fell far enough to have big eyes
    bool FallCheck() {
        if (devFlying)
            return false;

        foreach (Worm w in worm) {
            Coordinates checkCoord = w.data.blockData.coordinates;
            for (int i = 0; i < 5; i++) {
                checkCoord += GameController.gravityDirection;
                if (!GameController.Grid.WithinBounds(checkCoord))
                    return false;

                Data[] datas = GameController.Grid.GetData(checkCoord, Layer.Block, Layer.Tunnel, Layer.Piston);
                foreach (Data d in datas) {
                    if (d != null) {
                        if (d.layer == Layer.Piston && d.blockData.state == (int)Activation.Off)
                            continue;
                        return false;
                    }
                }
            }
        }
        return true;
    }

    // If player is pushing the same block its being supported by, simulate bumping it forward with the head
    public void HeadBumpCheck(Data pushData) {
        if (wormHead.data.blockData.facing % 2 != 0)
            return;
        
        foreach (Worm w in worm) {
            Data[] datas = GameController.Grid.GetData(w.data.blockData.coordinates + Coordinates.Down, Layer.Block, Layer.Piston);
            foreach (Data d in datas) {
                if (d == null)
                    continue;

                switch (d.layer) {
                    case Layer.Block:
                        if (!d.HasTag(Tag.Push) || pushData.blockData.connectedBlocks != d.blockData.connectedBlocks)
                            return;
                        continue;

                    case Layer.Piston:
                        if (d.blockData.state == (int)Activation.On)
                            return;
                        continue;
                }
            }
        }

        headBump = true;
        StartCoroutine(HeadBump());
    }
    IEnumerator HeadBump() {
        Vector3 direction = wormHead.data.blockData.facing == 0 ? Vector3.left : Vector3.right;

        wormHead.data.blockObject.transform.position += direction;
        yield return new WaitForSeconds(0.075f);
        wormHead.data.blockObject.transform.position -= direction;

        headBump = false;
    }

    public void InsideBlockCheck(Activation digInstant = Activation.Off) {
        bool tunnel = false;
        bool dig    = false;
        foreach (Worm w in worm) {
            bool wTunnel = false;
            bool wDig    = false;

            Data[] datas = GameController.Grid.GetData(w.data.blockData.coordinates, Layer.Tunnel, Layer.Dig, Layer.Fg);
            if (datas[0] != null)
                tunnel = wTunnel = true;
            else {
                if (datas[2] != null)
                    GameController.SetForeground(datas[2], Activation.Off);
            }
            if (datas[1] != null && datas[1].blockData.facing != -1) {
                dig = wDig = true;
                SetDigging(true, GameController.digOutlines[datas[1].levelBlock], digInstant);
            }
            
            w.light.enabled = !(wTunnel || wDig);
        }
        playerData.ignoreGravity = tunnel || dig;
    }

    // Check for adjacent collectables to look at
    public void LookCheck() {
        for (int i = 0; i < Coordinates.FacingDirection.Length; i++) {
            if (Coordinates.FacingDirection[i] == -Coordinates.FacingDirection[wormHead.data.blockData.facing])
                continue;

            Coordinates checkCoord = wormHead.data.blockData.coordinates + Coordinates.FacingDirection[i];
            Data collectData = GameController.Grid.GetData(checkCoord, Layer.Collect);
            if (collectData != null && !collectData.blockData.destroyed) {
                Look(true, i);
                return;
            }

            Data slotData = GameController.Grid.GetData(checkCoord, Layer.Misc);
            if (slotData != null && slotData.blockData.blockName == "GateSlot" && slotData.blockData.state == (int)Activation.On) {
                Look(true, i);
                return;
            }
        }
    }

    // Look in direction
    public void Look(bool activate, int facing = 0) {
        Transform eye = wormEye.data.blockObject.transform;
        if (activate) {
            ResetEyeColor();
            PlayAnimation(Animation.EyeDefault, Animation.Look);
            eye.localPosition = Vector3.zero;
            eye.position += (Vector3)GameController.GetVector(Coordinates.FacingDirection[facing]);
        }
        else {
            if (eye.localPosition == Vector3.zero) return;
                eye.localPosition =  Vector3.zero;

            if (!wormEye.IsCurrentAnimation(Animation.EyeBite))
                PlayAnimation(wormEye.IsCurrentAnimation(Animation.Sing) ? Animation.EyeDefault : Animation.None, Animation.HeadDefault, Animation.MouthDefault);
        }
    }
    
    void LookForward(bool activate) {
        if (activate) {
            if (wormEye.IsCurrentAnimation(Animation.Roll) && wormEye.playingAnimation != null)
                return;

            ResetEyeColor();
            wormEye.data.blockObject.transform.localPosition = Vector3.right;
        }
        else
            wormEye.data.blockObject.transform.localPosition = Vector3.zero;
    }

    void Sing(bool active) {
        if (active) {
            if (!singing) {
                CheckCurrentSongIndex(false);
                PlayAnimation(Animation.Sing);
                PlayNote();
            }

            // Change song when holding down sing long enough
            singHoldTime += Time.deltaTime;
            if (singHoldTime >= SONG_CHANGE_TIME) {
                singHoldTime = 0;
                CheckCurrentSongIndex(true);
            }
        }
        else {
            if (singing) {
                singHoldTime = 0;
                singing = false;
                StartCoroutine(EndSingDelay());

                IEnumerator EndSingDelay() {
                    yield return new WaitUntil(() => wormHead.IsCurrentAnimation(Animation.EatClose));
                    yield return new WaitForSeconds(0.2f);
                    if (!singing && wormHead.playingAnimation == null && wormHead.IsCurrentAnimation(Animation.EatClose))
                        PlayAnimation(Animation.EyeDefault);
                }
            }
        }

        void PlayNote() {
            AudioClip audioClip = null;
            if (++currentNoteIndex >= currentSong.notes.Length) currentNoteIndex = 0;
            int type = currentSong.notes[currentNoteIndex].type;
            switch (type) {
                case 0: audioClip = AudioController.playerSingShort;  break;
                case 1: audioClip = AudioController.playerSingMedium; break;
                case 2: audioClip = AudioController.playerSingLong;   break;
            }
            GameController.PlayPitchedSound(audioClip, currentSong.notes[currentNoteIndex].pitch);

            if (!wormHead.IsCurrentAnimation(Animation.EatOpen)) {
                PlayAnimation(Animation.None, Animation.HeadDefault, Animation.MouthDefault);
                PlayMouthAnimation(0.2f + type * 0.1f);
            }

            var main = musicNote.main;
            if (++currentNoteColorIndex >= GameController.Assets.gameColors.Length) currentNoteColorIndex = 0;
            main.startColor = new ParticleSystem.MinMaxGradient(GameController.Assets.gameColors[currentNoteColorIndex]);
            main.startLifetimeMultiplier = 0.5f + type * 0.2f;
            var psr = musicNote.GetComponent<ParticleSystemRenderer>();
            psr.material = musicNoteMaterials[Random.Range(0, musicNoteMaterials.Length)];
            musicNote.Emit(1);

            singHoldTime = 0;
            singing = true;
        }

        // Cycle song, skipping ones that haven't been unlocked
        void CheckCurrentSongIndex(bool next) {
            if (!next && playerData.songs[playerData.currentSongIndex] && playerData.currentSongIndex < songs.Length)
                return;

            do {
                if (++playerData.currentSongIndex >= playerData.songs.Length)
                    playerData.currentSongIndex = 0;
            }
            while (!playerData.songs[playerData.currentSongIndex] || playerData.currentSongIndex >= songs.Length);

            SetSong(playerData.currentSongIndex);
        }
    }
    void SetSong(int index) {
        currentSong = songs[playerData.currentSongIndex];
        GameController.ShowSongIndex(playerData.currentSongIndex);
    }
    public void PlaySong() {
        SetSong(playerData.currentSongIndex);
        currentNoteIndex = -1;
        Song song = currentSong;
        StartCoroutine(iePlaySong());

        IEnumerator iePlaySong() {
            while (currentSong == song) {
                Sing(true);

                if (currentNoteIndex == song.notes.Length - 1)
                    break;

                yield return null;
            }

            Sing(false);
            currentSong = song;
        }
    }
    
    public void WakeUp() {
        sleepTime = Random.Range(25f, 35f);
        if (sleeping) {
            sleeping = false;
            if (!wormMouth.IsCurrentAnimation(Animation.MouthSleep))
                return;

            GameController.PlayRandomSound(AudioController.bubblePop);
            ResetAnimation();
            blinkTime = 0;
            foreach (ParticleSystem ps in bubblePops)
                ps.Play();

            if (GameController.Game.dreamCloud.activeSelf)
                GameController.ActivateDream(false);
        }
    }
    void Sleep(bool skipDream, bool initialStart = false) {
        if (sleeping) return;
            sleeping = true;

        GameController.Player.ResetAnimation();
        SetAnimation(wormEye, Animation.Blink);
        StartCoroutine(SleepDelay());

        IEnumerator SleepDelay() {
            // Check if player switched or deleted save before playing
            int save    = GameController.currentSave;
            string room = GameController.currentRoom;
            yield return new WaitForSeconds(0.75f);
            if (save != GameController.currentSave || room != GameController.currentRoom)
                yield break;

            // Check if player already woke up
            if (!sleeping || !wormEye.IsCurrentAnimation(Animation.Blink))
                yield break;

            PlayAnimation(Animation.EyeSleep, Animation.HeadDefault, Animation.MouthSleep, true);
            if (!skipDream && GameController.currentPromptIndex == (PromptIndex)(-1)) {
                if (!initialStart && Random.Range(0, 100) >= 5)
                    yield break;

                GameController.FlagGameState(true);
                GameController.ActivateDream(true);
            }
        }
    }

    public void ActivateThinkOption(ThinkIndex thinkIndex, bool ignoreCheck = false, bool skipSequence = false) {
        int index = (int)thinkIndex;
        if (index == -1)
            return;

        if (playerData.thinkOptions[index] && !ignoreCheck) return;
            playerData.thinkOptions[index] = true;

        playerData.abilities[(int)AbilityIndex.Think] = true;
        GameController.Menu .UnlockControl(ControlIndex.Think, true);
        GameController.Input.UnlockToggleButton(Action .Think, true);

        if (skipSequence) GameController.ActivatePromptCloud   ((PromptIndex)(thinkIndex + 5), true            );
        else              GameController.ActivatePromptSequence((PromptIndex)(thinkIndex + 5), (ColorIndex)(-1));
    }
        
    void ShowRoomInfo(bool enable) {
        showRoomInfo = showingRoomInfo = enable;

        if (enable) {
            List<LevelBlock.BlockItem>[] roomInfo = GameController.Level.roomInfo;
            if (roomInfo[0] != null) StartCoroutine(ShowOtherInfo(roomInfo[0]));
            if (roomInfo[1] != null) StartCoroutine(ShowPanelInfo(roomInfo[1]));

            StartCoroutine(FadeInfoOutlines());
        }
    }
    IEnumerator ShowOtherInfo(List<LevelBlock.BlockItem> otherInfo) {
        foreach (LevelBlock.BlockItem bi in otherInfo)
            bi.blockObject.SetActive(true);

        yield return new WaitWhile(() => showRoomInfo);

        foreach (LevelBlock.BlockItem bi in otherInfo)
            bi.blockObject.SetActive(false);

        showingRoomInfo = false;
    }
    IEnumerator ShowPanelInfo(List<LevelBlock.BlockItem> panelInfo) {
        foreach (LevelBlock.BlockItem bi in panelInfo)
            bi.blockObject.SetActive(true);

        Vector3 scaleMin = Vector3.one;
        Vector3 scaleMax = scaleMin * 2; scaleMax.z = 1;

        float time  = 0;
        float speed = 10;
        if (!GameController.instantUI) {
            while (time < 1) {
                foreach (LevelBlock.BlockItem bi in panelInfo)
                    bi.blockObject.transform.localScale = Vector3.Lerp(scaleMin, scaleMax, GameController.GetCurve(time));

                time += Time.deltaTime * speed;
                yield return null;
            }
        }
        foreach (LevelBlock.BlockItem bi in panelInfo)
            bi.blockObject.transform.localScale = scaleMax;

        yield return new WaitWhile(() => showRoomInfo);

        time = 0;
        if (!GameController.instantUI) {
            while (time < 1) {
                foreach (LevelBlock.BlockItem bi in panelInfo)
                    bi.blockObject.transform.localScale = Vector3.Lerp(scaleMax, scaleMin, GameController.GetCurve(time));

                time += Time.deltaTime * speed;
                yield return null;
            }
        }
        foreach (LevelBlock.BlockItem bi in panelInfo) {
            bi.blockObject.transform.localScale = scaleMin;
            bi.blockObject.SetActive(false);
        }

        showingRoomInfo = false;
    }
    IEnumerator FadeInfoOutlines() {
        Color colorMax = GameController.Assets.infoOutlineColor;
        Color colorMin = colorMax; colorMin.a = 0;

        float time = 0;
        while (showRoomInfo) {
            float alpha = Mathf.Abs(Mathf.Sin(time));
            foreach (SpriteRenderer sr in GameController.Level.roomInfoOutlines) {
                if      (sr.sortingOrder == -10) sr.color = Color.Lerp(colorMax, colorMin, alpha);
                else if (sr.sortingOrder == -11) sr.color = Color.Lerp(colorMin, colorMax, alpha);
            }
            time += Time.deltaTime * 2.5f;
            yield return null;
        }
    }

    void InitInventory() {
        for (int i = 0; i < inventoryItems.Length; i++) {
            inventoryItems[i].itemHolder.transform.localPosition = Vector3.left * INVENTORY_PIXEL_SPACING * INVENTORY_MOVE_STEPS;
            Vector2 pos = inventoryItems[i].itemHolder.transform.localPosition;

            int startCount = i * INVENTORY_DELAY + 1;
            for (int startPadding = 0; startPadding < startCount; startPadding++)
                inventoryItems[i].movePositions.Add(pos);
            inventoryItems[i].startIndex = inventoryItems[i].movePositions.Count - 1;

            for (int steps = 0; steps < INVENTORY_MOVE_STEPS; steps++) {
                pos += Vector2.right * INVENTORY_PIXEL_SPACING;
                inventoryItems[i].movePositions.Add(pos);
            }

            for (int startBounce = 0; startBounce < INVENTORY_BOUNCE_STEPS; startBounce++) {
                int bounceCount = startBounce - Mathf.RoundToInt((float)startBounce / 2);
                for (int bounce = 0; bounce < bounceCount; bounce++) {
                    pos += Vector2.right * INVENTORY_PIXEL_SPACING;
                    for (int repeat = 0; repeat < 3; repeat++)
                        inventoryItems[i].movePositions.Add(pos);
                }
            }
            for (int endBounce = 0; endBounce < INVENTORY_BOUNCE_STEPS; endBounce++) {
                int bounceCount = endBounce - Mathf.RoundToInt((float)endBounce / 2);
                for (int bounce = 0; bounce < bounceCount; bounce++) {
                    pos += Vector2.left * INVENTORY_PIXEL_SPACING;
                    for (int repeat = 0; repeat < 3; repeat++)
                        inventoryItems[i].movePositions.Add(pos);
                }
            }

            inventoryItems[i].endIndex = inventoryItems[i].movePositions.Count - 1;
            int endCount = (inventoryItems.Length - i - 1) * INVENTORY_DELAY;
            for (int endPadding = 0; endPadding < endCount; endPadding++)
                inventoryItems[i].movePositions.Add(Vector2.zero);
        }
        inventoryMoveTotal = inventoryItems[0].movePositions.Count;
        
        for (int i = 0; i < playerData.colors.Length; i++)
            GameController.AddColorObject(i, null, null, inventoryItems[i].itemObject.GetComponent<Image>(), false);
        GameController.AddColorObject(ColorIndex.Time    , null, null, inventoryItems[(int)InventoryIndex.Undo    ].itemObject.GetComponent<Image>(), false);
        GameController.AddColorObject(ColorIndex.Time    , null, null, inventoryItems[(int)InventoryIndex.Reset   ].itemObject.GetComponent<Image>(), false);
        GameController.AddColorObject(ColorIndex.Force   , null, null, inventoryItems[(int)InventoryIndex.Force   ].itemObject.GetComponent<Image>(), false);
        GameController.AddColorObject(ColorIndex.Fragment, null, null, inventoryItems[(int)InventoryIndex.Fragment].itemObject.GetComponent<Image>(), false);
    }
    public void UpdateInventory() {
        EnableInventoryItem(InventoryIndex.Undo , playerData.abilities[(int)AbilityIndex.Undo ]);
        EnableInventoryItem(InventoryIndex.Reset, playerData.abilities[(int)AbilityIndex.Reset]);
        EnableInventoryItem(InventoryIndex.Force, playerData.abilities[(int)AbilityIndex.Force]);
        for (int i = 0; i < playerData.colors.Length; i++)
            EnableInventoryItem((InventoryIndex)i, playerData.colors[i]);

        UpdateLengthInventory(false);
        UpdateInventoryCount(InventoryIndex.Fragment);
    }
    public void EnableInventoryItem(InventoryIndex inventoryIndex, bool enable) {
        GameObject itemObject = inventoryItems[(int)inventoryIndex].itemObject;
        if (itemObject != null)
            itemObject.SetActive(enable);
    }
    public void IncrementInventoryCount(InventoryIndex inventoryIndex, int amount, bool updateUI) {
        int count = -1;
        switch (inventoryIndex) {
            case InventoryIndex.Grow:
                playerData.lengthIncrements += amount;

                if (updateUI)
                    UpdateLengthInventory(false);
                break;

            case InventoryIndex.Fragment:
                count = playerData.fragments += amount;

                if (updateUI) {
                    UpdateInventoryCount(inventoryIndex);
                    EnableInventoryItem(inventoryIndex, count > 0);
                }
                break;
        }
    }
    public void UpdateInventoryCount(InventoryIndex inventoryIndex, bool completeLengthSequence = false) {
        int count = -1;
        switch (inventoryIndex) {
            case InventoryIndex.Grow:
                UpdateLengthInventory(completeLengthSequence);
                return;

            case InventoryIndex.Fragment:
                count = playerData.fragments;
                break;
        }

        EnableInventoryItem(inventoryIndex, count > 0);
        inventoryItems[(int)inventoryIndex].number.text = count + "";
    }
    public void UpdateLengthInventory(bool completeSequence) {
        InventoryItem.LengthItem[] lengthItems = inventoryItems[(int)InventoryIndex.Grow].lengthItems;
        foreach (InventoryItem.LengthItem li in lengthItems) {
            li.itemComplete.SetActive(false);
            li.itemOutline.sprite = GameController.Assets.lengthItemOutlines[0];
        }

        int lengthIndex = 0;
        for (int i = 0; i < playerData.lengthIncrements; i++) {
            lengthItems[lengthIndex].itemIncrements[i % LENGTH_INCREMENT_AMOUNT].SetActive(true);

            if ((i + 1) % LENGTH_INCREMENT_AMOUNT == 0) {
                if (i == playerData.lengthIncrements - 1 && completeSequence) StartCoroutine(CompleteSequence(lengthIndex));
                else                                                          Complete(lengthIndex);
                lengthIndex++;
            }
        }

        void Complete(int index) {
            lengthItems[index].itemComplete.SetActive(true);
            lengthItems[index].itemOutline.sprite = GameController.Assets.lengthItemOutlines[1];
        }
        IEnumerator CompleteSequence(int index) {
            yield return new WaitForSeconds(0.25f);

            GameController.PlayRandomSound(AudioController.collectColor);
            Complete(index);
        }
    }
    public void SetInventoryPositions(int moveIndex) {
        foreach (InventoryItem ii in inventoryItems)
            ii.itemHolder.transform.localPosition = ii.movePositions[moveIndex];
    }
    public void AddToInventory(InventoryIndex inventoryIndex, int amount = 0) {
        switch (inventoryIndex) {
            case InventoryIndex.Grow    :
            case InventoryIndex.Fragment:
                UpdateInventoryCount(inventoryIndex);
                break;
        }

        InventoryItem inventoryItem = inventoryItems[(int)inventoryIndex];
        switch (inventoryItem.activationState) {
            case Activation.Off:
                if (amount != 0)
                    IncrementInventoryCount(inventoryIndex, amount, false);
                StartCoroutine(ieAddToInventory(inventoryIndex));
                break;

            case Activation.On:
                if (amount != 0)
                    IncrementInventoryCount(inventoryIndex, amount, false);
                break;

            case Activation.Alt:
                EnableInventoryItem(inventoryIndex, true);
                if (amount != 0)
                    IncrementInventoryCount(inventoryIndex, amount, true);
                break;
        }
    }
    // Show inventory pop out and then item collected
    IEnumerator ieAddToInventory(InventoryIndex inventoryIndex) {
        disableManualInventoryDisplay = true;

        InventoryItem inventoryItem = inventoryItems[(int)inventoryIndex];
        DisplayInventoryItem(inventoryItem, true);

        // Inventory prompt is shown after player collects a fragment or length update for the first time
        bool triggerPrompt = (inventoryIndex == InventoryIndex.Fragment || inventoryIndex == InventoryIndex.Grow) && !playerData.promptTriggers[4];
        if (triggerPrompt)
            GameController.FlagGameState(true);

        yield return new WaitUntil(() => inventoryItem.activationState == Activation.Alt);
        yield return new WaitForSeconds(0.25f);
        
        switch (inventoryIndex) {
            case InventoryIndex.Grow:
                UpdateInventoryCount(inventoryIndex, true);
                if (playerData.lengthIncrements % LENGTH_INCREMENT_AMOUNT == 0)
                    yield return new WaitForSeconds(0.25f);
                break;
            case InventoryIndex.Fragment:
                UpdateInventoryCount(inventoryIndex);
                break;

            default:
                EnableInventoryItem(inventoryIndex, true);
                break;
        }

        yield return new WaitForSeconds(0.25f);

        DisplayInventoryItem(inventoryItem, false);

        yield return new WaitUntil(() => inventoryItem.activationState == Activation.Off);

        if (triggerPrompt) {
            playerData.promptTriggers[4] = true;
            ActivateThinkOption(ThinkIndex.Inventory);
            GameController.FlagGameState(false);
        }
        disableManualInventoryDisplay = false;
    }
    void DisplayInventoryItem(InventoryItem inventoryItem, bool active) {
        if (inventoryItem.displayCoroutine != null) StopCoroutine(inventoryItem.displayCoroutine);
            inventoryItem.displayCoroutine  = ieDisplayInventoryItem(inventoryItem, active);
        StartCoroutine(inventoryItem.displayCoroutine);
    }
    IEnumerator ieDisplayInventoryItem(InventoryItem inventoryItem, bool active) {
        int startIndex = inventoryItem.startIndex;
        int endIndex   = inventoryItem.endIndex;
        if (!active) {
            startIndex = inventoryItem.endIndex;
            endIndex   = inventoryItem.startIndex;
        }
        inventoryItem.activationState = Activation.On;

        float time = 0;
        while (time < 1) {
            inventoryItem.itemHolder.transform.localPosition = inventoryItem.movePositions[GameController.GetCurveIndex(startIndex, endIndex, time)];

            time += Time.deltaTime * INVENTORY_SPEED;
            yield return null;
        }
        inventoryItem.itemHolder.transform.localPosition = inventoryItem.movePositions[endIndex];

        inventoryItem.activationState = active ? Activation.Alt : Activation.Off;
        inventoryItem.displayCoroutine = null;
    }

    public void PlayAnimation(Animation eyeAnimation, Animation headAnimation = Animation.None, Animation mouthAnimation = Animation.None, bool sync = false) {
        if (!holdingMove && eyeAnimation != Animation.Blink && eyeAnimation != Animation.None)
            Look(false);
        if (wormEye.IsCurrentAnimation(Animation.EyeNod)) {
            if (eyeAnimation  == Animation.None) eyeAnimation  = Animation.EyeDefault;
            if (headAnimation == Animation.None) headAnimation = Animation.HeadDefault;
        }

        if (!sync) {
            Play(wormEye  , eyeAnimation  );
            Play(wormHead , headAnimation );
            Play(wormMouth, mouthAnimation);
        }
        else
            Sync();

        void Play(Worm worm, Animation animation) {
            if (animation == Animation.None)
                return;

            if (worm.playingAnimation != null) {
                StopCoroutine(worm.playingAnimation);
                worm.playingAnimation = null;
            }

            SetAnimation(worm, animation);
            worm.playingAnimation = iePlayAnimation(worm);
            StartCoroutine(worm.playingAnimation);
        }
        void Sync() {
            if (wormEye.playingAnimation != null) {
                StopCoroutine(wormEye.playingAnimation);
                wormEye.playingAnimation = null;
            }
            if (wormHead.playingAnimation != null) {
                StopCoroutine(wormHead.playingAnimation);
                wormHead.playingAnimation = null;
            }

            SetAnimation(wormEye , eyeAnimation );
            SetAnimation(wormHead, headAnimation);

            if (mouthAnimation != Animation.None) {
                if (wormMouth.playingAnimation != null) {
                    StopCoroutine(wormMouth.playingAnimation);
                    wormMouth.playingAnimation = null;
                }

                SetAnimation(wormMouth, mouthAnimation);
            }

            if (eyeAnimation == Animation.EyeDefault && headAnimation == Animation.HeadDefault)
                return;

            if (mouthAnimation == Animation.None || mouthAnimation == Animation.MouthDefault)
                wormEye.playingAnimation = wormHead.playingAnimation = iePlayAnimation(wormEye, wormHead);
            else
                wormEye.playingAnimation = wormHead.playingAnimation = wormMouth.playingAnimation = iePlayAnimation(wormEye, wormHead, wormMouth);

            StartCoroutine(wormEye.playingAnimation);
        }

        IEnumerator iePlayAnimation(params Worm[] worms) {
            float time = 0;
            AnimationData a = worms[0].currentAnimation;
            int length = a.frames[0].sprites.Length;
            if (worms.Length == 3 && worms[1].IsCurrentAnimation(Animation.HeadDefault))
                worms[1] = null;

            do {
                int frame = -1;
                while (++frame < length) {
                    foreach (Worm w in worms) {
                        if (w != null)
                            w.SetFrame(frame);
                    }

                    while (time < a.duration) {
                        time += Time.deltaTime;
                        yield return null;
                    }
                    time -= a.duration;
                }
            } while (a.loop);

            foreach (Worm w in worms)
                w.playingAnimation = null;
        }
    }
    void SetAnimation(Worm worm, Animation animation, int frame = 0) {
        worm.currentAnimation = animationData[(int)animation];
        worm.SetFrame(frame);
    }
    public void ResetAnimation() {
        PlayAnimation(Animation.EyeDefault, Animation.HeadDefault, Animation.MouthDefault, true);
    }

    // Open and close mouth
    public void PlayMouthAnimation(float closeDelay = 0.2f) {
        PlayAnimation(Animation.None, Animation.EatOpen);
        StartCoroutine(CloseDelay());

        IEnumerator CloseDelay() {
            yield return new WaitForSeconds(closeDelay);
            if (wormHead.IsCurrentAnimation(Animation.EatOpen))
                PlayAnimation(Animation.None, Animation.EatClose);
        }
    }
    // Teeth clacking for dig collectable
    public void PlayBiteMouthAnimation() {
        GameController.FlagGameState(true);
        StartCoroutine(iePlayBiteMouthAnimation());

        IEnumerator iePlayBiteMouthAnimation() {
            yield return new WaitForSeconds(0.5f);

            SetAnimation(wormEye  , Animation.EyeBite  );
            SetAnimation(wormHead , Animation.HeadBite );
            SetAnimation(wormMouth, Animation.MouthBite);

            int[] frames = new int[] { 0, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 1, 0 };
            int frame = -1;
            float time = 0;
            float duration = animationData[(int)Animation.MouthBite].duration;
            while (++frame < frames.Length) {
                int index = frames[frame];
                wormEye  .SetFrame(index);
                wormHead .SetFrame(index);
                wormMouth.SetFrame(index);

                if (frame == 5 || frame == 14)
                    GameController.PlayRandomSound(AudioController.teethClack);

                while (time < duration) {
                    time += Time.deltaTime;
                    yield return null;
                }
                time -= duration;
            }

            ResetAnimation();
            GameController.FlagGameState(false);
        }
    }

    public void UpdateLength() {
        int difference = worm.Count - playerData.length;
        if (difference == 0)
            return;

        int count = Mathf.Abs(difference);
        bool active = difference < 0;
        for (int i = 0; i < count; i++)
            SetWormPieceActive(active);

        GameController.UpdateLengthMeter(playerData.length, playerData.GetMaxLength());
    }

    // Set correct gradients on each worm piece to match length, worm gets darker as it gets longer
    public void UpdateGradients(bool instant) {
        foreach (IEnumerator ie in gradientCoroutines)
            StopCoroutine(ie);
        gradientCoroutines.Clear();

        if (!instant) {
            for (int i = 1; i < worm.Count; i++) {
                IEnumerator ie = LerpGradient(worm[i].spriteRenderer, gradientColors[i]);
                gradientCoroutines.Add(ie);
                StartCoroutine(ie);
            }
        }
        else {
            for (int i = 1; i < worm.Count; i++)
                worm[i].spriteRenderer.color = gradientColors[i];
        }
    }
    IEnumerator LerpGradient(SpriteRenderer sr, Color32 color) {
        Color32 startColor = sr.color;
        float time = 0;
        while (time < 1) {
            sr.color = Color32.Lerp(startColor, color, time);

            time += Time.deltaTime * COLOR_CHANGE_SPEED;
            yield return null;
        }
        sr.color = color;
    }
    
    public void UpdateHeadFlip() {
        wormHead.spriteRenderer.flipY = playerData.headFlipped;
        wormHead.skeleton         .transform.localScale
      = wormEye  .data.blockObject.transform.localScale 
      = wormMouth.data.blockObject.transform.localScale
      = new Vector3(1, playerData.headFlipped ? -1 : 1, 1);
    }

    // Updates stored connected blocks to match coordinates
    public void UpdateConnections() {
        Coordinates[] cb = new Coordinates[worm.Count];
        worm[0].data.connectedData = new List<Data>();
        for (int i = 0; i < worm.Count; i++) {
            cb[i] = worm[i].data.blockData.coordinates;
            worm[i].data.blockData.connectedBlocks = cb;
            if (i > 0) worm[0].data.connectedData.Add(worm[i].data);
        }
    }

    // Play particles when moving across the ground
    void PlayGroundMovingParticles() {
        if (!GameController.enableParticles)
            return;
        
        for (int i = 0; i < worm.Count; i++) {
            Data d = worm[i].data;
            if (i == 0 && d.blockData.facing % 2 != 0)
                continue;

            // Don't play particles if inside a block
            Data[] datas = GameController.Grid.GetData(d.blockData.coordinates, Layer.Block, Layer.Tunnel, Layer.Dig);
            foreach (Data data in datas) {
                if (data != null) {
                    datas = null;
                    break;
                }
            }
            if (datas == null)
                continue;

            // Don't play particles if certain blocks are overlapping the ground block
            Coordinates groundCoord = d.blockData.coordinates + Coordinates.Down;
            Data[] belowDatas = GameController.Grid.GetData(groundCoord, Layer.Block, Layer.Tunnel, Layer.Dig, Layer.Piston);
            for (int j = 1; j < belowDatas.Length; j++) {
                if (belowDatas[j] != null && belowDatas[j].blockData.facing != -1) {
                    belowDatas = null;
                    break;
                }
            }
            if (belowDatas == null || belowDatas[0] == null || belowDatas[0].blockData.blockName != "Ground")
                continue;
            
            ParticlePooler.Particle p = GameController.Particles.GetParticle("GroundMovingParticles");
            if (p != null) {
                GameController.ApplyCoordinates(d.blockData.coordinates, p.particleObject);
                int facing = i < worm.Count - 1 ? worm[i + 1].data.blockData.facing : d.blockData.facing;
                p.particleObject.transform.localRotation = Quaternion.Euler(new Vector3(-90, 0, facing == 0 ? 0 : 180));
                GameController.Particles.PlayParticle(p);
            }
        }
    }
    
    public void UpdateUndoOutline() {
        GameController.ApplyData(wormHead.data.blockData, undoOutlineHolder);
        undoTree.SetPattern(this);
        undoOutlineHolder.transform.localScale = undoTree.flipped ? new Vector3(1, -1, 1) : Vector3.one;
    }
    void BuildUndoOutline(int[] pattern, PatternTree.FacingPattern facingPattern) {
        // Get bounds of all blocks
        Coordinates[] c = new Coordinates[worm.Count];
        c[0] = Coordinates.Zero;
        int[] bounds = new int[] { 0, 0, 0, 0 }; // [Largest X, Smallest X, Largest Y, Smallest Y]
        Sprite[] sprites = new Sprite[worm.Count];

        for (int i = 1; i < pattern.Length; i++) {
            c[i] = c[i - 1] - Coordinates.FacingDirection[pattern[i]];
            if      (c[i].x > bounds[0]) bounds[0] = c[i].x;
            else if (c[i].x < bounds[1]) bounds[1] = c[i].x;
            if      (c[i].y > bounds[2]) bounds[2] = c[i].y;
            else if (c[i].y < bounds[3]) bounds[3] = c[i].y;

            // Use straight or corner outline
            sprites[i - 1] = pattern[i] == pattern[i - 1] ? undoSprites[1] : undoSprites[2];
        }
        sprites[0] = sprites[sprites.Length - 1] = undoSprites[0];
        
        Texture2D texture = new Texture2D((Mathf.Abs(bounds[0] - bounds[1]) + 1) * GameController.BLOCK_SIZE,
                                          (Mathf.Abs(bounds[2] - bounds[3]) + 1) * GameController.BLOCK_SIZE);
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < texture.height; y++) {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, Color.clear);
        }

        // Build new sprite from block pieces
        int b = GameController.BLOCK_SIZE - 1;
        for (int i = 0; i < worm.Count; i++) {
            Coordinates gridCoord = (c[i] - new Coordinates(bounds[1], bounds[3])) * GameController.BLOCK_SIZE;
            Texture2D spriteTexture = sprites[i].texture;

            int facing = pattern[i];
            if (i < worm.Count - 1) {
                // Correct corner pieces that should be flipped
                if (i > 0 && sprites[i] == undoSprites[2]) {
                    int nextFacing = facing + 1;
                    if (nextFacing > 3) nextFacing = 0;
                    if (nextFacing == pattern[i + 1]) {
                        if (--facing < 0)
                            facing = 3;
                    }
                }
            }
            else
                facing = facing + 2 > 3 ? facing - 2 : facing + 2;

            for (int y = 0; y < spriteTexture.height; y++) {
                for (int x = 0; x < spriteTexture.width; x++) {
                    Coordinates rotatedCoord = new Coordinates(x, y);
                    switch (facing) {
                        case 1: rotatedCoord = new Coordinates(-y + b,  x    ); break;
                        case 2: rotatedCoord = new Coordinates(-x + b, -y + b); break;
                        case 3: rotatedCoord = new Coordinates( y    , -x + b); break;
                    }

                    Coordinates textureCoord = gridCoord + rotatedCoord;
                    Color color = spriteTexture.GetPixel(x, y);
                    if (color.a > 0)
                        texture.SetPixel(textureCoord.x, textureCoord.y, color);
                }
            }
        }
        texture.Apply();
        
        facingPattern.offset = new Vector3(Mathf.Lerp(bounds[1], bounds[0], 0.5f), Mathf.Lerp(bounds[3], bounds[2], 0.5f)) * GameController.GRID_SIZE;
        facingPattern.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector3(0.5f, 0.5f), 1);
    }
}