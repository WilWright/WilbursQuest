using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum Action { Up, Down, Left, Right, Shoot, Grow, Undo, Reset, Force, Think, Menu }
public enum PressType { Current, Up, Down, None }
public class InputController : MonoBehaviour {
    public class ActionInfo {
        public InputAction inputAction;
        public Sprite sprite;
        public string description;
        public PressType pressType = PressType.None;
        public bool toggled;
        public bool togglable;

        public ActionInfo(InputAction inputAction, bool togglable) {
            this.inputAction = inputAction;
            this.togglable   = togglable;
        }

        public void Toggle() {
            if (togglable)
                toggled = !toggled;
        }
    }

    [System.Serializable]
    public class Prompt {
        [HideInInspector]
        public List<Action> excludedActions = new List<Action>();
        public GameObject[] promptHolders = new GameObject[2];
        public ControlPrompt[] gamePrompts = new ControlPrompt[1];
        public ControlPrompt[] playerPrompts = new ControlPrompt[1];
    }

    public Prompt[] prompts;
    public static Prompt currentPrompt;
    const int PROMPT_SPACING = 10;

    [HideInInspector]
    public static bool loopDirections;
    static readonly int[] loopDirectionsOrder = new int[] { 0, 3, 1, 2 }; // Up, Right, Down, Left
    static readonly int[] directions = new int[] { 0, 1, 2, 3 }; // Up, Down, Left, Right
    static readonly int[] altDirections = new int[] { 11, 12, 13, 14 }; // (Dpad) Up, Down, Left, Right
    static int currentDirection;
    static IEnumerator loopDirectionsCoroutine;

    enum Devices { Keyboard, XBox, PlayStation, Switch, Unknown };
    static Devices currentDevice;
    public static Controls currentControls;
    public static Controls currentMaster;
    public Controls[] ControlMasters;
    static Controls[] controlMasters;
    public Controls[] ControlTypes;
    static Controls[] controlTypes;
    static string[] settingsBinds;

    public ControlPrompt continueButton;
    public static List<UIControl> uiControls;
    public GameObject toggleButtonHolder;
    SpriteRenderer[][] toggleButtons;

    Input input;
    Action[] actions;
    public static ActionInfo[] actionInfo;
    public static bool usingGamepad;
    public static bool dpadPreferred;
    static InputActionRebindingExtensions.RebindingOperation rebindOp;

    public void Init() {
        if (!GameController.devMode)
            InputSystem.DisableDevice(Mouse.current);

        controlTypes   = ControlTypes;
        controlMasters = ControlMasters;

        input = new Input();
        input.Enable();

        settingsBinds = GameController.Menu.settingsData.keyboardBinds;
        actions = (Action[])System.Enum.GetValues(typeof(Action));
        uiControls = new List<UIControl>();

        actionInfo = new ActionInfo[] {
            new ActionInfo(input.Gameplay.Up     , false),
            new ActionInfo(input.Gameplay.Down   , false),
            new ActionInfo(input.Gameplay.Left   , false),
            new ActionInfo(input.Gameplay.Right  , false),
            new ActionInfo(input.Gameplay.Shoot  , false),
            new ActionInfo(input.Gameplay.Grow   , true ),
            new ActionInfo(input.Gameplay.Undo   , false),
            new ActionInfo(input.Gameplay.Reset  , false),
            new ActionInfo(input.Gameplay.Gravity, true ),
            new ActionInfo(input.Gameplay.Think  , true ),
            new ActionInfo(input.Gameplay.Menu   , false),
        };

        for (int i = 0; i < actionInfo.Length; i++) {
            controlTypes[0].controls[i].name = settingsBinds[i];
            foreach (Controls.Control c in controlMasters[0].controls) {
                if (c.name == settingsBinds[i]) {
                    actionInfo[i].sprite      = controlTypes[0].controls[i].sprite      = c.sprite;
                    actionInfo[i].description = controlTypes[0].controls[i].description = c.description;
                    break;
                }
            }

            InputAction ia = actionInfo[i].inputAction;
            InputBinding ib = ia.bindings[ia.GetBindingIndex(InputBinding.MaskByGroup("Keyboard"))];
            ib.overridePath = "<Keyboard>/#(" + settingsBinds[i] + ")";
            ia.ApplyBindingOverride(ib);
        }

        toggleButtons = new SpriteRenderer[actionInfo.Length][];
        SpriteRenderer[] toggleSpriteRenderers = toggleButtonHolder.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in toggleSpriteRenderers) {
            int index = -1;
            for (int i = 0; i < actionInfo.Length; i++) {
                string s = (Action)i + "";
                if (sr.name == s) {
                    index = i;
                    break;
                }
            }

            if (toggleButtons[index] == null) toggleButtons[index] = new SpriteRenderer[2];
            toggleButtons[index][sr.transform.parent.gameObject == toggleButtonHolder ? 0 : 1] = sr;
        }
        
        InitUIControls();
        UpdateControls((Devices)GameController.Menu.settingsData.lastDevice);
        if (usingGamepad)
            SetDirectionSprites(GameController.Menu.settingsData.dpadPreffered ? altDirections : directions, false);

        InitPrompts();
        continueButton.Init();
        continueButton.gameObject.SetActive(false);
        GameController.Menu.InitMenu();
    }

    void Update() {
        UpdateInputs();
        CheckForDeviceChange();
        CheckForPreferredDirectionControls();

        if (currentPrompt != null && !currentPrompt.playerPrompts[0].standaloneFeedback) {
            if (currentPrompt.playerPrompts[0].completed)
                CompletePrompt(currentPrompt);
        }
    }

    static InputAction GetAction(Action action) {
        return actionInfo[(int)action].inputAction;
    }

    public static bool Get(Action action, PressType pressType = PressType.Current) {
        ActionInfo ai = actionInfo[(int)action];
        bool match = ai.pressType == pressType;
        if (GameController.toggleButtons && ai.togglable) {
            switch (pressType) {
                case PressType.Current: return match ||  ai.toggled || ai.pressType == PressType.Down;
                case PressType.Up     : return match && !ai.toggled;
                case PressType.Down   : return match &&  ai.toggled;
                case PressType.None   : return match && !ai.toggled;
            }
        }
        else {
            if (pressType == PressType.Current && ai.pressType == PressType.Down)
                match = true;
        }
        return match;
    }

    void UpdateInputs() {
        for (int i = 0; i < actionInfo.Length; i++) {
            ActionInfo ai = actionInfo[i];
            switch (ai.pressType) {
                case PressType.Current:
                    if (ai.inputAction.triggered && ai.inputAction.ReadValue<float>() == default)
                        ai.pressType = PressType.Up;
                    break;

                case PressType.Up:
                    if (!ai.inputAction.triggered && ai.inputAction.ReadValue<float>() == default)
                        ai.pressType = PressType.None;
                    break;

                case PressType.Down:
                    if (ai.inputAction.ReadValue<float>() > 0)
                        ai.pressType = PressType.Current;
                    break;

                case PressType.None:
                    if (ai.inputAction.triggered && ai.inputAction.ReadValue<float>() > 0) {
                        ai.pressType = PressType.Down;
                        SetToggleButton((Action)i, !ai.toggled);
                    }
                    break;
            }
        }
    }
    public void ResetInputs() {
        if (actionInfo == null)
            return;

        for (int i = 0; i < actionInfo.Length; i++) {
            ActionInfo ai = actionInfo[i];
            ai.pressType = PressType.None;
            SetToggleButton((Action)i, false);
        }
    }

    void CheckForDeviceChange() {
        if ((currentDevice != Devices.Keyboard && Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame) || currentDevice == Devices.Unknown) {
            UpdateInfo(Devices.Keyboard);
            return;
        }

        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame) {
            if (!ControlActuallyPressed())
                return;
            
            if      (Gamepad.current is UnityEngine.InputSystem.XInput.XInputController      ) UpdateInfo(Devices.XBox       );
            else if (Gamepad.current is UnityEngine.InputSystem.DualShock.DualShockGamepad   ) UpdateInfo(Devices.PlayStation);
            //else if (Gamepad.current is UnityEngine.InputSystem.Switch.SwitchProControllerHID) UpdateInfo(Devices.Switch     ); // Switch Pro Controller Unity bug causes constant random inputs
            else {
                currentDevice = Devices.Unknown;
                CheckForDeviceChange();
            }
        }

        // Prevents switching when controller analog control was barely pressed
        bool ControlActuallyPressed() {
            foreach (var control in Gamepad.current.allControls) {
                if (control.IsPressed())
                    return true;
            }
            return false;
        }

        void UpdateInfo(Devices device) {
            if (currentDevice == device)
                return;

            UpdateControls(device);
            GameController.Menu.UpdateControlInfo();
        }
    }
    void CheckForPreferredDirectionControls() {
        if (!usingGamepad)
            return;

        if (dpadPreferred) {
            if (Gamepad.current.leftStick.IsPressed()) {
                GameController.Menu.settingsData.dpadPreffered = dpadPreferred = false;
                SetDirectionSprites(directions, true);
            }
        }
        else {
            if (Gamepad.current.dpad.IsPressed()) {
                GameController.Menu.settingsData.dpadPreffered = dpadPreferred = true;
                SetDirectionSprites(altDirections, true);
            }
        }
    }
    void SetDirectionSprites(int[] indexes, bool updateToolTip) {
        for (int i = 0; i < Coordinates.FacingDirection.Length; i++) {
            actionInfo[i].sprite      = currentMaster.controls[indexes[i]].sprite;
            actionInfo[i].description = currentMaster.controls[indexes[i]].description;
        }

        UpdateUIControls();

        if (updateToolTip)
            GameController.Menu.UpdateToolTip();
    }

    public static void InitUIControls() {
        GameObject[] uics = GameObject.FindGameObjectsWithTag("UIControl");
        if (uics == null || uics.Length == 0)
            return;

        uiControls = new List<UIControl>();
        foreach (GameObject go in uics) {
            UIControl uic = go.GetComponent<UIControl>();
            uic.Init();
            uiControls.Add(uic);
        }
    }
    static void UpdateUIControls() {
        if (uiControls == null)
            return;

        foreach (UIControl uic in uiControls) {
            if (uic.image != null) uic.image         .sprite = actionInfo[(int)uic.action].sprite;
            else                   uic.spriteRenderer.sprite = actionInfo[(int)uic.action].sprite;
        }

        if (loopDirections) {
            foreach (Prompt p in GameController.Input.prompts) {
                if (p.promptHolders[0].activeSelf) {
                    foreach (ControlPrompt cp in p.gamePrompts) {
                        if (cp.directional && cp.directionalCooldowns == 0) {
                            foreach (SpriteRenderer sr in cp.promptObject.objects)
                                sr.sprite = actionInfo[currentDirection].sprite;
                        }
                    }
                }
                if (p.promptHolders[1].activeSelf) {
                    foreach (ControlPrompt cp in p.playerPrompts) {
                        if (cp.directional && !cp.pressed && cp.directionalCooldowns == 0) {
                            foreach (SpriteRenderer sr in cp.promptObject.objects)
                                sr.sprite = actionInfo[currentDirection].sprite;
                        }
                    }
                }
            }
        }
    }
    static void UpdateControls(Devices device) {
        currentControls = controlTypes  [(int)device];
        currentMaster   = controlMasters[(int)device];
        currentDevice = device;
        usingGamepad  = device != Devices.Keyboard;
        GameController.Menu.settingsData.lastDevice = (int)device;

        for (int i = 0; i < actionInfo.Length; i++) {
            actionInfo[i].sprite      = currentControls.controls[i].sprite;
            actionInfo[i].description = currentControls.controls[i].description;
        }

        UpdateUIControls();
        GameController.Menu.UpdateToolTip();
    }

    public void SetToggleButtonUI(bool active) {
        ResetInputs();
        toggleButtonHolder.SetActive(active);
    }
    public void SetToggleButton(Action action, bool active) {
        int index = (int)action;
        SpriteRenderer[] toggleButton = toggleButtons[index];
        if (toggleButton == null || !toggleButton[0].gameObject.activeSelf)
            return;

        actionInfo[index].toggled = active;
        if (active) {
            toggleButton[0].color = GameController.Assets.backDropColor;
            toggleButton[1].color = GameController.Assets.highlightColor;
        }
        else {
            toggleButton[0].color = GameController.Assets.backgroundColor;
            toggleButton[1].color = GameController.Assets.inactiveColor;
        }
    }
    public void UpdateToggleButtons(PlayerController.PlayerData playerData) {
        UnlockToggleButton(Action.Think, playerData.abilities[(int)AbilityIndex.Think]);
        UnlockToggleButton(Action.Grow , playerData.abilities[(int)AbilityIndex.Grow ]);
        UnlockToggleButton(Action.Force, playerData.abilities[(int)AbilityIndex.Force]);
    }
    public void UnlockToggleButton(Action action, bool active) {
        toggleButtons[(int)action][0].gameObject.SetActive(active);
    }

    public static void RebindAction(Action action, bool[] done) {
        foreach (ActionInfo ai in actionInfo)
            ai.inputAction.Disable();

        InputAction ia = GetAction(action);
        rebindOp = ia.PerformInteractiveRebinding(ia.GetBindingIndex(InputBinding.MaskByGroup("Keyboard")))
                     .WithCancelingThrough("<Keyboard>/escape")
                     .WithMatchingEventsBeingSuppressed(false)
                     .WithControlsHavingToMatchPath("<Keyboard>")
                     .Start()
                     .OnCancel(cancel => { Done(); })
                     .OnComplete(complete => {
                         int index = (int)action;
                         string currentKey         = settingsBinds[index];
                         Sprite currentSprite      = actionInfo[index].sprite;
                         string currentDescription = actionInfo[index].description;
                         bool validKey = false;
                         bool sameKey  = false;

                         // Check if key is in the master list
                         if (currentKey == complete.selectedControl.displayName)
                            sameKey = validKey = true;
                         else {
                             foreach (Controls.Control c in controlMasters[0].controls) {
                                 if (c.name == complete.selectedControl.displayName) {
                                     Controls.Control control = controlTypes[0].controls[index];
                                     control.name        = settingsBinds[index]          = c.name;
                                     control.sprite      = actionInfo[index].sprite      = c.sprite;
                                     control.description = actionInfo[index].description = c.description;
                                     validKey = true;
                                     break;
                                 }
                             }
                         }
                         if (!validKey) {
                             rebindOp.Dispose();
                             RebindAction(action, done);
                             return;
                         }

                         // Check for duplicate control and swap
                         for (int i = 0; i < settingsBinds.Length; i++) {
                             if (i == index)
                                 continue;

                             if (settingsBinds[i] == complete.selectedControl.displayName) {
                                 Controls.Control control = controlTypes[0].controls[i];
                                 control.name        = settingsBinds[i]          = currentKey;
                                 control.sprite      = actionInfo[i].sprite      = currentSprite;
                                 control.description = actionInfo[i].description = currentDescription;

                                 InputAction aia = actionInfo[i].inputAction;
                                 InputBinding ib = aia.bindings[aia.GetBindingIndex(InputBinding.MaskByGroup("Keyboard"))];
                                 ib.overridePath = "<Keyboard>/#(" + currentKey + ")";
                                 aia.ApplyBindingOverride(ib);
                                 break;
                             }
                         }

                         if (!sameKey) {
                             UpdateUIControls();
                             GameController.Menu.UpdateToolTip();
                         }

                         Done();
                     });
        
        void Done() {
            if (done != null)
                done[0] = true;

            rebindOp.Dispose();
            rebindOp = null;
            foreach (ActionInfo ai in actionInfo)
                ai.inputAction.Enable();
        }
    }

    void InitPrompts() {
        foreach (Prompt p in prompts) {
            List<Action> includedActions = new List<Action>();
            foreach (ControlPrompt cp in p.gamePrompts) {
                cp.Init();
                if (cp.directional) {
                    foreach (int d in loopDirectionsOrder) {
                        if (p.gamePrompts.Length == 1 && (Action)d == Action.Left)
                            continue;

                        includedActions.Add((Action)d);
                    }
                }
                else
                    includedActions.Add(cp.uiControl.action);
            }
            includedActions.Add(Action.Menu);

            p.playerPrompts[0].transform.localPosition = new Vector2(-PROMPT_SPACING, 0);
            if (p.gamePrompts.Length == 2) {
                p.gamePrompts  [0].transform.localPosition = new Vector2(-PROMPT_SPACING, 0);
                p.gamePrompts  [1].transform.localPosition = new Vector2( PROMPT_SPACING, 0);
                p.playerPrompts[1].transform.localPosition = new Vector2( PROMPT_SPACING, 0);
            }

            foreach (Action a in actions) {
                bool found = false;
                foreach (Action ia in includedActions) {
                    if (a == ia) {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    p.excludedActions.Add(a);
            }

            foreach (ControlPrompt cp in p.playerPrompts) cp.Init();
            foreach (GameObject    go in p.promptHolders) go.SetActive(false);
        }
    }
    public void ActivateContinueButton(bool active) {
        continueButton.gameObject.SetActive(active);
        if (active) continueButton.CheckInput();
        else        continueButton.ResetPrompt();
    }
    public void ActivatePrompt(PromptIndex promptIndex) {
        if (currentPrompt != null) {
            foreach (GameObject    go in currentPrompt.promptHolders) go.SetActive(false);
            foreach (ControlPrompt cp in currentPrompt.playerPrompts) cp.standaloneFeedback = false;
            ResetPrompt(currentPrompt);
        }

        Prompt prompt = prompts[(int)promptIndex];
        ResetPrompt(prompt);
        currentPrompt = prompt;

        if (!GameController.paused)
            EnablePromptActions(prompt, false);

        prompt.promptHolders[0].transform.position = GameController.Game.promptCloud.transform.position;
        foreach (GameObject go in prompt.promptHolders)
            go.SetActive(true);

        for (int i = 0; i < prompt.gamePrompts.Length; i++) {
            GameController.Game.promptCloudParticles[i * 2].transform.position = prompt.gamePrompts[i].transform.position;
            if (prompt.gamePrompts[i].directional)
                LoopDirections(true);
        }

        prompt.gamePrompts[0].LoopPrompt(true);
        if (GameController.currentPromptIndex != PromptIndex.Move)
            GameController.Player.promptMovementOverride = true;

        StartCoroutine(CheckInputDelay());
        IEnumerator CheckInputDelay() {
            GameController.FlagGameState(true);

            yield return new WaitUntil(() => GameController.gameStateFlags == 1);

            if (!prompt.promptHolders[1].activeSelf)
                yield break;

            foreach (ControlPrompt cp in prompt.playerPrompts)
                cp.CheckInput();

            yield return new WaitForSeconds(0.1f);

            GameController.FlagGameState(false);
        }
    }
    void CompletePrompt(Prompt prompt) {
        for (int i = 0; i < prompt.gamePrompts.Length; i++) {
            prompt.gamePrompts[i].ResetPrompt();
            prompt.gamePrompts[i].Press(true);

            if (prompt.gamePrompts[i].directional) {
                // Set game prompt to direction player completed feedback prompt with
                foreach (SpriteRenderer sr in prompt.gamePrompts[i].promptObject.objects)
                    sr.sprite = actionInfo[(int)prompt.playerPrompts[i].currentDirection].sprite;
            }

            bool pressed = prompt.playerPrompts[i].pressed;
            prompt.playerPrompts[i].standaloneFeedback = true;
            prompt.playerPrompts[i].ResetPrompt();
            if (pressed)
                prompt.playerPrompts[i].Press(true);
        }

        if (GameController.currentPromptIndex == PromptIndex.Move)
            GameController.Player.playerData.promptTriggers[5] = true;

        GameController.ActivatePromptCloud(GameController.currentPromptIndex, false);
        GameController.Player.promptMovementOverride = false;
        EnablePromptActions(prompt, true);
    }
    public void ResetPrompt(Prompt prompt) {
        foreach (ControlPrompt cp in prompt.gamePrompts  ) cp.ResetPrompt();
        foreach (ControlPrompt cp in prompt.playerPrompts) cp.ResetPrompt(true);
        foreach (GameObject    go in prompt.promptHolders) go.SetActive(false);
        
        EnablePromptActions(prompt, true);
        LoopDirections(false);
        GameController.Player.promptMovementOverride = false;
        currentPrompt = null;
    }

    public void EnablePromptActions(PromptIndex promptIndex, bool enable) {
        int index = (int)promptIndex;
        if (index == -1)
            return;

        EnablePromptActions(prompts[index], enable);
    }
    public void EnablePromptActions(Prompt prompt, bool enable) {
        if (prompt == null)
            return;

        ResetInputs();
        foreach (Action a in prompt.excludedActions) {
            if (enable) GetAction(a).Enable ();
            else        GetAction(a).Disable();
        }
    }

    public static void LoopDirections(bool enable) {
        loopDirections = enable;
        if (loopDirectionsCoroutine != null)
            GameController.Input.StopCoroutine(loopDirectionsCoroutine);

        if (enable) {
            loopDirectionsCoroutine = GameController.Input.ieLoopDirections();
            GameController.Input.StartCoroutine(loopDirectionsCoroutine);
        }
    }
    IEnumerator ieLoopDirections() {
        while (loopDirections) {
            foreach (int d in loopDirectionsOrder) {
                currentDirection = d;

                foreach (Prompt p in prompts) {
                    if (p.promptHolders[0].activeSelf) {
                        foreach (ControlPrompt cp in p.gamePrompts) {
                            if (cp.directional && cp.directionalCooldowns == 0) {
                                foreach (SpriteRenderer sr in cp.promptObject.objects)
                                    sr.sprite = actionInfo[d].sprite;
                            }
                        }
                    }
                    if (p.promptHolders[1].activeSelf) {
                        foreach (ControlPrompt cp in p.playerPrompts) {
                            if (cp.directional && !cp.pressed && cp.directionalCooldowns == 0) {
                                foreach (SpriteRenderer sr in cp.promptObject.objects)
                                    sr.sprite = actionInfo[d].sprite;
                            }
                        }
                    }
                }

                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
