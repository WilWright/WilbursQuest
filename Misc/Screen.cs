using UnityEngine;

public class Screen : MonoBehaviour {
    [System.Serializable]
    public class ScreenData {
        public int[] bounds;
        public int size;
        public Coordinates coordinates;
        public bool visited;

        public bool CompareData(ScreenData screenData) {
            return screenData != null && coordinates == screenData.coordinates && size == screenData.size;
        }

        public bool WithinScreen(Coordinates c) {
            return visited && c.y <= bounds[0] 
                           && c.y >= bounds[1] 
                           && c.x >= bounds[2] 
                           && c.x <= bounds[3];
        }
    }
    
    public ScreenData screenData;
    public BoxCollider2D boxCollider;
    public GameObject[] borders;

    public void SetPosition(Coordinates coordinates) {
        screenData.coordinates = coordinates;
        GameController.ApplyCoordinates(coordinates, gameObject);
    }
    public void SetSize(int size) {
        screenData.bounds = new int[4];
        screenData.size   = size;
        float width  = size * GameController.GRID_SIZE;
        float height = GameController.SnapToGrid(width * 9.0f / 16.0f);

        boxCollider.size = new Vector2(width * 2 - GameController.GRID_SIZE, height * 2 - GameController.GRID_SIZE);
        for (int i = 0; i < 2; i++) {
            borders[i].transform.localPosition = borders[i].transform.up * height;
            screenData.bounds[i] = GameController.GetGridCoordinate(borders[i].transform.position.y);
            borders[i].transform.localScale = new Vector3((size * 2) - 1, 1, 1);
        }
        for (int i = 2; i < 4; i++) {
            borders[i].transform.localPosition = borders[i].transform.up * width;
            screenData.bounds[i] = GameController.GetGridCoordinate(borders[i].transform.position.x);
            borders[i].transform.localScale = new Vector3((height / GameController.GRID_SIZE * 2) + 1, 1, 1);
        }
    }
}
