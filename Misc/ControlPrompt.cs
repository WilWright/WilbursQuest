using System.Collections;
using UnityEngine;

public class ControlPrompt : MonoBehaviour {
    public class PromptObject {
        public SpriteRenderer[] objects = new SpriteRenderer[4];
        public float highlightTime;
        public IEnumerator coroutine;
    }
    
    public ControlPrompt nextPrompt;
    ControlPrompt parentPrompt;
    public bool holdRequired;
    public float holdBuffer;
    float currentHoldBuffer;
    bool triggerHoldBuffer;
    public bool directional;
    bool pressingDirection;

    [HideInInspector] public SpriteRenderer spriteRenderer;
    [HideInInspector] public PromptObject promptObject;
    [HideInInspector] public UIControl uiControl;
    [HideInInspector] public Action currentDirection;
    [HideInInspector] public bool pressed;
    [HideInInspector] public bool completed;
    [HideInInspector] public bool doneLoop;
    [HideInInspector] public bool locked;
    [HideInInspector] public bool standaloneFeedback;
    [HideInInspector] public int directionalCooldowns;

    static readonly Action[] directions = new Action[] { Action.Up, Action.Right, Action.Down, Action.Left };
    const float PRESS_TIME = 0.25f;
    const float HOLD_TIME = 1;
    const float LOOP_TIME = 1.25f;
    IEnumerator loopCoroutine;
    IEnumerator activateCoroutine;
    IEnumerator checkInputCoroutine;
    IEnumerator fadeCoroutine;

    const float PIXEL_SPACING = 1f;
    Color highlightColor;
    Color backgroundColor;
    Color inactiveColor;

    public void Init() {
        highlightColor  = GameController.Assets.highlightColor;
        backgroundColor = GameController.Assets.backgroundColor;
        inactiveColor   = GameController.Assets.inactiveColor;

        spriteRenderer = GetComponent<SpriteRenderer>();
        uiControl      = GetComponent<UIControl>();
        promptObject = new PromptObject();

        // Create sprite layers for press animation
        SpriteRenderer[] os = promptObject.objects;
        os[0] = GetComponent<SpriteRenderer>();
        os[0].color = backgroundColor;
        for (int i = 1; i < os.Length; i++) {
            os[i] = Instantiate(gameObject).GetComponent<SpriteRenderer>();
            Destroy(os[i].GetComponent<ControlPrompt>());
            if (!directional)
                InputController.uiControls.Add(os[i].GetComponent<UIControl>());
        }
        os[os.Length - 1].color = inactiveColor;

        for (int i = 1; i < os.Length; i++) {
            os[i].sortingOrder = os[i - 1].sortingOrder + 1;
            os[i].transform.SetParent(os[i - 1].transform);
            os[i].transform.localPosition = Vector3.left * PIXEL_SPACING;
            os[i].transform.localScale    = Vector3.one;
        }

        if (nextPrompt != null)
            nextPrompt.parentPrompt = this;
    }

    public void CheckInput() {
        if (checkInputCoroutine != null) {
            StopCoroutine(checkInputCoroutine);
            checkInputCoroutine = null;
        }
        checkInputCoroutine = ieCheckInput();
        StartCoroutine(checkInputCoroutine);
    }
    IEnumerator ieCheckInput() {
        while (true) {
            if (!GameController.paused)
                CheckInput();

            yield return null;
        }
        
        void CheckInput() {
            if (directional) {
                foreach (Action a in directions) {
                    if (InputController.Get(a)) {
                        if (!pressed)
                            Press(true);

                        pressingDirection = true;
                         currentDirection = a;
                        foreach (SpriteRenderer sr in promptObject.objects)
                            sr.sprite = InputController.actionInfo[(int)currentDirection].sprite;
                        break;
                    }
                }
                if (pressingDirection) {
                    if (InputController.Get(currentDirection, PressType.Up)) {
                        Press(false);
                        pressingDirection = false;
                        directionalCooldowns++;
                        Invoke("DirectionalCooldown", 1.5f);
                        foreach (SpriteRenderer sr in promptObject.objects)
                            sr.sprite = InputController.actionInfo[(int)currentDirection].sprite;
                    }
                }
            }
            else {
                if (!pressed && InputController.Get(uiControl.action))
                    Press(true);
                if (InputController.Get(uiControl.action, PressType.Up))
                    Press(false);
            }

            if (completed)
                return;

            // Don't complete if secondary prompt is activated before primary prompt
            if (parentPrompt != null)
                locked = parentPrompt.locked = pressed && !parentPrompt.pressed;

            if (locked)
                return;
            
            bool complete = nextPrompt == null ? pressed : pressed && nextPrompt.completed;

            if (holdBuffer > 0) {
                if (complete)
                    triggerHoldBuffer = true;

                if (!triggerHoldBuffer)
                    return;

                if (!holdRequired && !CompletedBuffer(parentPrompt.pressed)) return;
                if ( holdRequired && !CompletedBuffer(             pressed)) return;
                completed = true;

                bool CompletedBuffer(bool pressed) {
                    if (pressed) {
                        currentHoldBuffer += Time.deltaTime;
                        if (currentHoldBuffer >= holdBuffer)
                            return true;
                    }
                    else {
                        triggerHoldBuffer = false;
                        currentHoldBuffer = 0;
                    }

                    return false;
                }
            }
            else
                completed = complete;
        }
    }
    void DirectionalCooldown() { directionalCooldowns--; }

    public void LoopPrompt(bool active) {
        if (loopCoroutine != null)
            StopCoroutine(loopCoroutine);

        if (active) {
            loopCoroutine = ieLoopPrompt();
            StartCoroutine(loopCoroutine);
        }
    }
    IEnumerator ieLoopPrompt() {
        yield return new WaitForSeconds(0.2f);

        Activate();

        yield return new WaitUntil(() => doneLoop);
        yield return new WaitForSeconds(LOOP_TIME);

        LoopPrompt(true);
    }

    public void Activate() {
        doneLoop = false;
        if (activateCoroutine != null) StopCoroutine(activateCoroutine);
            activateCoroutine  = ieActivate();
        StartCoroutine(activateCoroutine);
    }
    IEnumerator ieActivate() {
        Press(true);

        yield return new WaitForSeconds(PRESS_TIME);

        if (nextPrompt != null)
            nextPrompt.Activate();
        if (holdRequired) {
            yield return new WaitForSeconds(HOLD_TIME);

            if (nextPrompt != null)
                yield return new WaitUntil(() => nextPrompt.doneLoop);
        }

        Press(false);
        doneLoop = true;
    }

    public void Press(bool active) {
        pressed = active && !locked;
        if (!completed && !standaloneFeedback) {
            if (checkInputCoroutine == null) GameController.PlayRandomSound(pressed ? AudioController.buttonDown         : AudioController.buttonUp        );
            else                             GameController.PlayRandomSound(pressed ? AudioController.buttonFeedbackDown : AudioController.buttonFeedbackUp);
        }

        if (promptObject.coroutine != null) StopCoroutine(promptObject.coroutine);
            promptObject.coroutine  = iePress(active);
        StartCoroutine(promptObject.coroutine);
    }
    IEnumerator iePress(bool active) {
        int fromIndex = promptObject.objects.Length - 1;
        int   toIndex = 2;

        float speed = 10;
        if (active) {
            while (promptObject.highlightTime < 1) {
                int index = GameController.GetCurveIndex(fromIndex, toIndex, promptObject.highlightTime);
                promptObject.objects[index - 1].color = highlightColor;
                promptObject.objects[index].gameObject.SetActive(false);

                promptObject.highlightTime += Time.deltaTime * speed;
                yield return null;
            }
            promptObject.highlightTime = 1;
            promptObject.objects[toIndex - 1].color = highlightColor;
            promptObject.objects[toIndex].gameObject.SetActive(false);
        }
        else {
            while (promptObject.highlightTime > 0) {
                int index = GameController.GetCurveIndex(fromIndex, toIndex, promptObject.highlightTime);
                promptObject.objects[index - 1].color = backgroundColor;
                promptObject.objects[index    ].color = inactiveColor;
                promptObject.objects[index    ].gameObject.SetActive(true);

                promptObject.highlightTime -= Time.deltaTime * speed;
                yield return null;
            }
            promptObject.highlightTime = 0;
            promptObject.objects[fromIndex - 1].color = backgroundColor;
            promptObject.objects[fromIndex    ].color = inactiveColor;
            promptObject.objects[fromIndex    ].gameObject.SetActive(true);
        }
    }

    public void ResetPrompt(bool resetStandaloneFeedback = false) {
        LoopPrompt(false);
        if (activateCoroutine      != null) StopCoroutine(activateCoroutine);
        if (checkInputCoroutine    != null) StopCoroutine(checkInputCoroutine);
        if (promptObject.coroutine != null) StopCoroutine(promptObject.coroutine);

        locked = pressed = completed = pressingDirection = false;
        directionalCooldowns = 0;
        doneLoop = true;
        if (resetStandaloneFeedback)
            standaloneFeedback = false;

        if (standaloneFeedback)
            return;

        promptObject.highlightTime = 0;
        for (int i = 1; i < promptObject.objects.Length; i++) {
            promptObject.objects[i - 1].color = backgroundColor;
            promptObject.objects[i    ].color = inactiveColor;
            promptObject.objects[i    ].gameObject.SetActive(true);
        }
    }
}
