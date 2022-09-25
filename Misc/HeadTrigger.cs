using UnityEngine;

public class HeadTrigger : MonoBehaviour {
    public GameObject parallaxFollow;
    public Screen previousScreen;
    int screenFlags;
    const float PARALLAX_SPEED = 1.75f;
    const float FOLLOW_SPEED = 5;

    static readonly (float magnitude, float speedMultiplier)[] PARALLAX_VALUES = new (float magnitude, float speedMultiplier)[] {
        ( 0.35f, 1.5f), // Camera
        (-0.25f, 1.1f), // Support
        (-0.15f, 0.9f), // GroundBG2
        (-0.05f, 0.5f), // BG
        (-0.25f, 1.5f)  // FG
    };

    private void Update() {
        if (!GameController.levelInitialized)
            return;

        transform.position = GameController.Player.wormHead.data.blockObject.transform.position;
        parallaxFollow.transform.position = Vector3.Lerp(parallaxFollow.transform.position, transform.position, Time.deltaTime * FOLLOW_SPEED);

        if (GameController.enableCameraSnap || GameController.cameraMoving || GameController.currentScreen == null)
            return;

        Vector2 dir = (transform.position - GameController.currentScreen.transform.position).normalized;
        for (int i = 0; i < GameController.Level.parallaxLayers.Length; i++)
            MoveLayer(GameController.Level.parallaxLayers[i], dir, PARALLAX_VALUES[i].magnitude, PARALLAX_VALUES[i].speedMultiplier);
    }

    private void OnTriggerEnter2D(Collider2D collision) {
        switch (collision.tag) {
            case "Screen":
                screenFlags++;
                Screen s = GetScreen(collision);
                if (GameController.currentScreen != s) {
                    previousScreen = GameController.currentScreen;
                    GameController.GoToScreen(s, GameController.transitionState == Activation.On);
                }
                break;

            case "HeadTrigger":
                switch (collision.name) {
                    case "EndingCutsceneTrigger":
                        collision.enabled = false;
                        collision.GetComponent<EndingCutscene>().StartCutscene();
                        break;
                }
                break;

        }
    }
    private void OnTriggerExit2D(Collider2D collision) {
        if (previousScreen == null || (!GameController.Player.playerData.digging && GameController.Player.playerData.ignoreGravity))
            return;

        switch (collision.tag) {
            case "Screen":
                screenFlags--;
                if (screenFlags > 0 && GameController.currentScreen == GetScreen(collision))
                    GameController.GoToScreen(previousScreen, false);
                break;
        }
    }

    Screen GetScreen(Collider2D collision) {
        foreach (Screen s in GameController.Level.screens) {
            if (s.gameObject == collision.gameObject)
                return s;
        }
        return null;
    }
    void MoveLayer(GameObject layer, Vector2 direction, float magnitude, float speedMultiplier) {
        layer.transform.localPosition = Vector2.Lerp(layer.transform.localPosition, direction * magnitude, Time.deltaTime * PARALLAX_SPEED * speedMultiplier);
    }
}
