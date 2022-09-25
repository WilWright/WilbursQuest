#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public enum SpriteTag { Divot, NoOutline, Directional, Blur, Shading }
public class SpriteGeneration : MonoBehaviour {
    [System.Serializable]
    public class SpriteInfo {
        public string name;
        public Color32 color;
        public SpriteTag[] spriteTags;
        public Sprite[] tiles;
        public Sprite[] patterns;
        public Sprite[] stamps;
        
        public bool HasTag(params SpriteTag[] spriteTags) {
            if (this.spriteTags == null)
                return false;

            foreach (SpriteTag t in spriteTags) {
                foreach (SpriteTag tt in this.spriteTags) {
                    if (t == tt)
                        return true;
                }
            }
            return false;
        }
        public bool HasAllTags(params SpriteTag[] spriteTags) {
            if (this.spriteTags == null || this.spriteTags.Length < spriteTags.Length)
                return false;

            foreach (SpriteTag t in spriteTags) {
                bool found = false;
                foreach (SpriteTag tt in this.spriteTags) {
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
    }

    public Sprite digTiling;
    public Sprite[] directionalTiling;
    public Sprite[] shadingTiling;
    public SpriteInfo[] spriteInfos;

    const int SHADES = 5;
    const int SUB_SHADES = 3;
    const float SHADE_STEP = 0.07f;
    const float SHADE_DEVIATION = 0.02f;
    const byte CRYSTAL_ALPHA = 150;
    const byte CRYSTAL_OUTLINE_ALPHA = 200;

    static readonly Color32 FILL_COLOR = new Color32(255, 0, 0, 255);
    static readonly Color32 OUTLINE_COLOR = new Color32(0, 0, 255, 255);
    static readonly Color32 TIME_OUTLINE_COLOR = new Color32(255, 255, 255, 255);
    static readonly Color32 DIVOT_COLOR = new Color32(0, 255, 255, 255);
    static readonly Color32 SHADE_COLOR = new Color32(0, 0, 0, 255);
    static readonly Color32[] DIRECTION_COLORS = new Color32[] {
        new Color32(255, 255, 255, 255),
        new Color32(  0,   0,   0, 255),
        new Color32(255, 255,   0, 255),
        new Color32(  0, 255,   0, 255)
    };

    public SpriteInfo GetSpriteInfo(string name) {
        foreach (SpriteInfo si in spriteInfos) {
            if (si.name == name)
                return si;
        }
        return null;
    }

    public void GenerateSprite(List<Data> dataList, GridSystem gridSystem, params GameObject[] blocks) { GenerateSprite(dataList, gridSystem, null, null, blocks); }
    public void GenerateSprite(List<Data> dataList, GridSystem gridSystem, SpriteRenderer outline, SpriteRenderer timeOutline, params GameObject[] blocks) {
        // Get bounds of all blocks
        Coordinates c0 = dataList[0].blockData.coordinates;
        int[] bounds = new int[] { c0.x, c0.x, c0.y, c0.y }; // [Largest X, Smallest X, Largest Y, Smallest Y]
        for (int i = 1; i < dataList.Count; i++) {
            Coordinates c = dataList[i].blockData.coordinates;
            if      (c.x > bounds[0]) bounds[0] = c.x;
            else if (c.x < bounds[1]) bounds[1] = c.x;
            if      (c.y > bounds[2]) bounds[2] = c.y;
            else if (c.y < bounds[3]) bounds[3] = c.y;
        }

        SpriteInfo spriteInfo = GetSpriteInfo(dataList[0].blockData.blockName);
        int spriteSize = GameController.BLOCK_SIZE;
        switch (spriteInfo.name) {
            case "GroundBG1":
            case "GroundBG2":
            case "Support"  :
            case "Tunnel"   :
                spriteSize = 9;
                break;
        }
        
        Texture2D texture = new Texture2D((Mathf.Abs(bounds[0] - bounds[1]) + 1) * GameController.BLOCK_SIZE + spriteSize - GameController.BLOCK_SIZE,
                                          (Mathf.Abs(bounds[2] - bounds[3]) + 1) * GameController.BLOCK_SIZE + spriteSize - GameController.BLOCK_SIZE);
        Texture2D spriteTexture = new Texture2D(texture.width, texture.height);
        texture.filterMode = spriteTexture.filterMode = FilterMode.Point;

        Texture2D dirTiling  = null;
        Texture2D dirTexture = null;
        if (spriteInfo.HasTag(SpriteTag.Directional)) {
            dirTiling = directionalTiling[spriteInfo.name == "Support" ? 1 : 0].texture;
            dirTexture = new Texture2D(texture.width, texture.height);
            dirTexture.filterMode = FilterMode.Point;
        }

        Texture2D shadeTiling  = null;
        Texture2D shadeTexture = null;
        if (spriteInfo.HasTag(SpriteTag.Shading)) {
            switch (spriteInfo.name) {
                case "Tunnel": shadeTiling = shadingTiling[1].texture; break;
                default      : shadeTiling = shadingTiling[0].texture; break;
            }
            shadeTexture = new Texture2D(texture.width, texture.height);
            shadeTexture.filterMode = FilterMode.Point;
        }

        Texture2D outlineTexture     = null;
        Texture2D timeOutlineTexture = null;
        if (outline != null) {
            outlineTexture = new Texture2D(texture.width, texture.height);
            outlineTexture.filterMode = FilterMode.Point;
        }
        if (timeOutline != null) {
            timeOutlineTexture = new Texture2D(texture.width, texture.height);
            timeOutlineTexture.filterMode = FilterMode.Point;
        }
        
        Clear(texture           );
        Clear(spriteTexture     );
        Clear(dirTexture        );
        Clear(outlineTexture    );
        Clear(timeOutlineTexture);
        void Clear(Texture2D t) {
            if (t == null)
                return;

            for (int y = 0; y < texture.height; y++) {
                for (int x = 0; x < texture.width; x++)
                    t.SetPixel(x, y, Color.clear);
            }
        }

        // Build new sprite from block pieces
        int s = spriteSize - 1;
        bool checkDivot = spriteInfo.name == "Ground";
        List<Coordinates> stamps = (spriteInfo.stamps != null && spriteInfo.stamps.Length > 0) ? new List<Coordinates>() : null;
        Coordinates[] airDusts = GameObject.FindGameObjectWithTag("Level").GetComponent<LevelData>().airDusts;
        foreach (Data d in dataList) {
            if (gridSystem != null)
                GameController.ApplyTiling(d, gridSystem);

            int tileIndex = 0;
            if (spriteInfo.tiles.Length > 1 && (d.blockData.spriteState == 2 || d.blockData.spriteState == 3)) {
                if (Random.Range(0, 101) < 20)
                    tileIndex = Random.Range(1, spriteInfo.tiles.Length);
            }
            Texture2D tiling = spriteInfo.tiles[tileIndex].texture;

            bool changeDivot = false;
            if (checkDivot && gridSystem != null) {
                // Use dig tiles to indicate dig entrances
                if (gridSystem.GetData(d.blockData.coordinates, Layer.Dig) != null)
                    tiling = digTiling.texture;

                // Don't divot if overlapping/near certain blocks
                bool valid = false;
                foreach (Coordinates f in Coordinates.FacingDirection) {
                    Coordinates n = d.blockData.coordinates + f;
                    if (!gridSystem.WithinBounds(n)) {
                        changeDivot = true;
                        break;
                    }

                    Data data = gridSystem.GetData(n, Layer.Block);
                    if (data == null || data.HasTag(Tag.Push)) {
                        foreach (Coordinates c in airDusts) {
                            if (n == c) {
                                valid = true;
                                break;
                            }
                        }
                    }
                    if (valid)
                        break;
                }
                if (valid) {
                    List<Coordinates> checks = new List<Coordinates>() { d.blockData.coordinates };
                    foreach (Coordinates a in Coordinates.AllDirection) {
                        Coordinates n = d.blockData.coordinates + a;
                        if (gridSystem.WithinBounds(n))
                            checks.Add(n);
                    }

                    foreach (Coordinates c in checks) {
                        Data[] datas = gridSystem.GetData(c, Layer.Dig, Layer.Tunnel, Layer.Block);
                        if (datas[0] != null || datas[1] != null || (datas[2] != null && (datas[2].blockData.blockName == "Gate" || datas[2].blockData.blockName == "Basic"))) {
                            changeDivot = true;
                            break;
                        }
                    }
                }
                else
                    changeDivot = true;
            }

            Coordinates gridCoord = (d.blockData.coordinates - new Coordinates(bounds[1], bounds[3])) * GameController.BLOCK_SIZE;
            int offset = d.blockData.spriteState * (spriteSize + 1);
            for (int y = 0; y < spriteSize; y++) {
                for (int x = 0; x < spriteSize; x++) {
                    Coordinates rotatedCoord = new Coordinates(x, y);
                    switch (d.blockData.facing) {
                        case 1: rotatedCoord = new Coordinates(-y + s,  x    ); break;
                        case 2: rotatedCoord = new Coordinates(-x + s, -y + s); break;
                        case 3: rotatedCoord = new Coordinates( y    , -x + s); break;
                    }

                    Color color = tiling.GetPixel(x + offset, y);
                    if (color.a == 0)
                        continue;

                    if (color == DIVOT_COLOR) {
                        if (changeDivot || (d.blockData.spriteState == 5 && Random.Range(0, 4) < 3))
                            color = OUTLINE_COLOR;
                    }

                    Coordinates textureCoord = gridCoord + rotatedCoord;
                    texture.SetPixel(textureCoord.x, textureCoord.y, color);

                    if (dirTexture   != null) dirTexture  .SetPixel(textureCoord.x, textureCoord.y, dirTiling  .GetPixel(x + offset, y));
                    if (shadeTexture != null) shadeTexture.SetPixel(textureCoord.x, textureCoord.y, shadeTiling.GetPixel(x + offset, y));
                }
            }
        }

        bool blueCrystal = spriteInfo.name == "BlueCrystal";
        bool noOutline = spriteInfo.HasTag(SpriteTag.NoOutline);
        Color32 divotChangeColor = noOutline ? FILL_COLOR : OUTLINE_COLOR;
        int[,] pixels = new int[texture.width, texture.height];
        if (spriteInfo.HasTag(SpriteTag.Divot)) {
            for (int y = 0; y < texture.height; y++) {
                for (int x = 0; x < texture.width; x++) {
                    Color color = texture.GetPixel(x, y);
                    if (color != DIVOT_COLOR)
                        continue;

                    Coordinates c = new Coordinates(x, y);
                    Coordinates fillDirection  = Coordinates.Zero;
                    Coordinates divotDirection = Coordinates.Zero;
                    bool skip = false;
                    foreach (Coordinates f in Coordinates.FacingDirection) {
                        Coordinates n = c + f;

                        if (!WithinBounds(n)) {
                            if (checkDivot) {
                                skip = true;
                                break;
                            }
                            else
                                continue;
                        }

                        Color nColor = texture.GetPixel(n.x, n.y);
                        if (nColor == DIVOT_COLOR) {
                            divotDirection = f;
                            continue;
                        }
                        if (nColor == FILL_COLOR)
                            fillDirection = f;
                    }

                    if (fillDirection.x == divotDirection.x) divotDirection.x = GetRandomDirection();
                    if (fillDirection.y == divotDirection.y) divotDirection.y = GetRandomDirection();
                    int GetRandomDirection() { return Random.Range(0, 2) == 0 ? 1 : -1; }

                    // Check nearby pixels to prevent divots from being so close to each other
                    if (checkDivot) {
                        int range = 25;
                        for (int i = -range; i <= range; i++) {
                            Coordinates n = c + divotDirection * i;
                            if (WithinBounds(n) && pixels[n.x, n.y] == -3) {
                                skip = true;
                                break;
                            }
                        }
                    }

                    if (!skip) {
                        // Get contiguous divot pixels
                        List<Coordinates> divots = new List<Coordinates>() { c };
                        GetDivots( divotDirection);
                        GetDivots(-divotDirection);
                        void GetDivots(Coordinates dir) {
                            Coordinates n = c + dir;
                            Color nColor = texture.GetPixel(n.x, n.y);
                            while (nColor == DIVOT_COLOR) {
                                divots.Add(n);
                                n += dir;
                                nColor = texture.GetPixel(n.x, n.y);
                            }
                        }
                        List<Coordinates> doDivots = null;
                        if (blueCrystal) {
                            // Pick percentage of random pixels to divot
                            doDivots = new List<Coordinates>();
                            int count = Mathf.RoundToInt(divots.Count * 0.65f);
                            for (int i = 0; i < count; i++) {
                                Coordinates p = divots[Random.Range(0, divots.Count)];
                                doDivots.Add (p);
                                divots.Remove(p);
                            }
                        }
                        else {
                            // Random chance to actually divot
                            if ((checkDivot && Random.Range(0, 2) == 0) || (!checkDivot && Random.Range(0, 5) < 2)) {
                                // Pick random subset of 2 or 3 pixels from current group
                                doDivots = new List<Coordinates>() { divots[Random.Range(0, divots.Count)] };
                                divots.Remove(doDivots[0]);
                                Coordinates direction = divotDirection * (Random.Range(0, 2) == 0 ? 1 : -1);
                                for (int i = 0; i < Random.Range(1, 3); i++) {
                                    Coordinates a = doDivots[doDivots.Count - 1] + direction;
                                    if (divots.Contains(a)) {
                                        doDivots.Add (a);
                                        divots.Remove(a);
                                    }
                                    else {
                                        Coordinates b = doDivots[doDivots.Count - 1] - direction;
                                        if (divots.Contains(b)) {
                                            doDivots.Add (b);
                                            divots.Remove(b);
                                        }
                                    }
                                }
                            }
                        }
                        if (doDivots != null) {
                            foreach (Coordinates d in doDivots) {
                                Coordinates n = d + fillDirection;
                                texture.SetPixel(d.x, d.y, Color.clear     );
                                texture.SetPixel(n.x, n.y, divotChangeColor);
                                pixels[d.x, d.y] = -3;
                            }
                        }
                        foreach (Coordinates d in divots)
                            texture.SetPixel(d.x, d.y, divotChangeColor);
                    }
                }
            }
        }
        else {
            for (int y = 0; y < texture.height; y++) {
                for (int x = 0; x < texture.width; x++) {
                    Color color = texture.GetPixel(x, y);
                    if (color == OUTLINE_COLOR || color == DIVOT_COLOR)
                        texture.SetPixel(x, y, divotChangeColor);
                }
            }
        }

        // Generate shades and subshades from original color
        Color32[][] palette = new Color32[SHADES][];
        for (int i = 0; i < palette.Length; i++) {
            palette[i] = new Color32[SUB_SHADES];

            float shadeBase = i > 1 ? 0.15f : 0;
            for (int j = 0; j < palette[i].Length; j++)
                palette[i][j] = Color32.Lerp(spriteInfo.color, SHADE_COLOR, i * SHADE_STEP + shadeBase + (j * SHADE_DEVIATION));
        }

        if (stamps != null) {
            foreach (Coordinates c in stamps) {
                Texture2D stamp = spriteInfo.stamps[Random.Range(0, spriteInfo.stamps.Length)].texture;
                List<Coordinates> stampPixels = new List<Coordinates>();
                for (int x = 0; x < stamp.width; x++) {
                    for (int y = 0; y < stamp.height; y++) {
                        if (stamp.GetPixel(x, y).a > 0)
                            stampPixels.Add(new Coordinates(x, y));
                    }
                }

                bool place = true;
                foreach (Coordinates p in stampPixels) {
                    Coordinates n = c + p;
                    if (!WithinBounds(n) || texture.GetPixel(n.x, n.y) != FILL_COLOR || spriteTexture.GetPixel(n.x, n.y).a > 0) {
                        place = false;
                        break;
                    }
                }
                if (place) {
                    foreach (Coordinates p in stampPixels) {
                        Coordinates n = c + p;
                        pixels[n.x, n.y] = -2;
                        spriteTexture.SetPixel(n.x, n.y, stamp.GetPixel(p.x, p.y));
                    }
                }
            }
        }
        
        bool crystal     = spriteInfo.name.Contains("Crystal");
        bool shading     = spriteInfo.HasTag(SpriteTag.Shading    );
        bool directional = spriteInfo.HasTag(SpriteTag.Directional);
        int patternIndex = 0;
        for (int y = 0; y < texture.height; y++) {
            for (int x = 0; x < texture.width; x++) {
                Color color = texture.GetPixel(x, y);
                if (color.a == 0 || spriteTexture.GetPixel(x, y).a > 0)
                    continue;

                Coordinates c = new Coordinates(x, y);

                // Randomly pick shade that's different from adjacent pixels
                int shadeIndex = Random.Range(2, SHADES);
                Coordinates down = c + Coordinates.Down;
                Coordinates left = c + Coordinates.Left;
                if ((WithinBounds(down) && pixels[down.x, down.y] == shadeIndex) || (WithinBounds(left) && pixels[left.x, left.y] == shadeIndex)) {
                    if (++shadeIndex >= SHADES)
                        shadeIndex = 2;
                }

                Color32 patternColor        = palette[shadeIndex        ][Random.Range(0, SUB_SHADES)];
                Color32 patternOutlineColor = palette[Random.Range(0, 2)][Random.Range(0, SUB_SHADES)];
                if (crystal) {
                    patternOutlineColor   = palette[0][0];
                    patternOutlineColor.a = CRYSTAL_OUTLINE_ALPHA;
                    patternColor       .a = CRYSTAL_ALPHA;
                }
                if (noOutline)
                    patternOutlineColor = patternColor;

                // Cycle pattern used
                Texture2D pattern = spriteInfo.patterns[patternIndex].texture;
                if (++patternIndex >= spriteInfo.patterns.Length)
                    patternIndex = 0;

                int facing = 0;
                if (dirTexture != null) {
                    List<int> facings = new List<int>();
                    Color dirColor = dirTexture.GetPixel(x, y);
                    for (int i = 0; i < Coordinates.FacingDirection.Length; i++) {
                        Coordinates f = c + Coordinates.FacingDirection[i];
                        if (dirTexture.GetPixel(f.x, f.y) == dirColor)
                            facings.Add(i);
                    }
                    if (facings.Count > 0)
                        facing = facings[Random.Range(0, facings.Count)];
                }
                else
                    facing = Random.Range(0, Coordinates.FacingDirection.Length);

                for (int px = 0; px < pattern.width; px++) {
                    for (int py = 0; py < pattern.height; py++) {
                        if (pattern.GetPixel(px, py).a == 0)
                            continue;

                        Coordinates rotatedCoord = new Coordinates(px, py);
                        switch (facing) {
                            case 1: rotatedCoord = new Coordinates(-py,  px); break;
                            case 2: rotatedCoord = new Coordinates(-px, -py); break;
                            case 3: rotatedCoord = new Coordinates( py, -px); break;
                        }

                        Coordinates p = c + rotatedCoord;
                        if (!WithinBounds(p))
                            continue;

                        Color tColor = texture.GetPixel(p.x, p.y);
                        if (tColor.a == 0 || spriteTexture.GetPixel(p.x, p.y).a > 0)
                            continue;
                        
                        if (pixels[p.x, p.y] == -2) continue;
                            pixels[p.x, p.y] = shadeIndex;

                        if (tColor == FILL_COLOR) {
                            Color pColor = patternColor;
                            if (shading) {
                                Color sColor = shadeTexture.GetPixel(p.x, p.y);
                                if (sColor.a > 0)
                                    pColor = Color32.Lerp(patternColor, sColor, 0.15f);
                            }

                            spriteTexture.SetPixel(p.x, p.y, pColor);
                        }
                        else {
                            spriteTexture.SetPixel(p.x, p.y, patternOutlineColor);
                            if (outlineTexture     != null) outlineTexture    .SetPixel(p.x, p.y, patternOutlineColor);
                            if (timeOutlineTexture != null) timeOutlineTexture.SetPixel(p.x, p.y, TIME_OUTLINE_COLOR );
                        }
                    }
                }
            }
        }

        if (spriteInfo.HasTag(SpriteTag.Blur)) {
            List<Coordinates> coordinates = new List<Coordinates>();
            for (int y = -1; y <= texture.height; y++) {
                for (int x = -1; x <= texture.width; x++) {
                    Coordinates c = new Coordinates(x, y);
                    if (!WithinBounds(c)) {
                        coordinates.Add(c);
                        continue;
                    }
                    if (spriteTexture.GetPixel(x, y).a == 0) {
                        pixels[x, y] = -1;
                        coordinates.Add(c);
                    }
                }
            }

            int steps = 0;
            float blurStep = 0.45f;
            while (coordinates.Count > 0) {
                List<Coordinates> check = new List<Coordinates>();
                foreach (Coordinates c in coordinates) {
                    if (WithinBounds(c))
                        spriteTexture.SetPixel(c.x, c.y, Color.Lerp(Color.clear, spriteTexture.GetPixel(c.x, c.y), steps * blurStep));

                    foreach (Coordinates d in Coordinates.CompassDirection) {
                        Coordinates next = c + d;
                        if (!WithinBounds(next) || pixels[next.x, next.y] == -1)
                            continue;

                        check.Add(next);
                        pixels[next.x, next.y] = -1;
                    }
                }
                coordinates = check;
                steps++;
            }
        }

        bool WithinBounds(Coordinates c) {
            return c.x >= 0 && c.x < texture.width 
                && c.y >= 0 && c.y < texture.height;
        }

        spriteTexture.Apply();
        if (outlineTexture != null) {
            outlineTexture.Apply();
            outline.sprite = CreateSprite(outlineTexture);
        }
        if (timeOutlineTexture != null) {
            timeOutlineTexture.Apply();
            timeOutline.sprite = CreateSprite(timeOutlineTexture);
        }

        Sprite sprite = CreateSprite(spriteTexture);
        Vector3 center = new Vector3(Mathf.Lerp(bounds[1], bounds[0], 0.5f), Mathf.Lerp(bounds[3], bounds[2], 0.5f)) * GameController.GRID_SIZE;
        foreach (GameObject go in blocks) {
            go.GetComponent<SpriteRenderer>().sprite = sprite;
            go.transform.position = center;
        }

        Sprite CreateSprite(Texture2D t) {
            return Sprite.Create(t, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1);
        }
    }

    [ContextMenu("Create BG Sprite")]
    void CreateBGSprite() {
        int size = 10;
        List<Data> dataList = new List<Data>();
        for (int x = 0; x < size; x++) {
            for (int y = 0; y < size; y++) {
                Data d = new Data(new BlockData("BG", new Coordinates(x, y)));
                d.blockData.spriteState = 0;
                dataList.Add(d);
            }
        }

        GameObject tempBG = new GameObject();
        SpriteRenderer sr = tempBG.AddComponent<SpriteRenderer>();
        GenerateSprite(dataList, null, tempBG);
        Assets.SaveSprite(sr.sprite.texture, "Assets/Sprites/Blocks/Tiling/Generation/bg");
        DestroyImmediate(tempBG);

        Debug.LogError("Generated BG");
    }
}
#endif
