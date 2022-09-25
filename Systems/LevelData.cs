using System.Collections.Generic;
using UnityEngine;

public enum PuzzleIndex { Rock, Crystal, Button, Piston, Panel, Dig, Fg, Gate, GateSlot, Collectable, Player }
public class LevelData : MonoBehaviour {
    public string levelName;
    public GameObject playerIcon;

    public GameObject[] parallaxLayers; // [Camera, Support, GroundBG2, BG, FG]
    public GameObject groundHolder;
    public GameObject rockHolder;
    public GameObject crystalHolder;
    public GameObject tunnelHolder;
    public GameObject miscHolder;
    public GameObject collectHolder;
    public GameObject otherHolder;
    public GameObject resetHolder;
    
    public Coordinates initialForceDirection = Coordinates.Zero;
    public int[] screenBounds;
    public Screen[] screens;
    public Sprite roomMap;
    public Coordinates[] groundDusts;
    public Coordinates[] airDusts;

    public List<Data>[] puzzleData = new List<Data>[10];
    public Stack<List<BlockData.Diff>[]> puzzleHistory = new Stack<List<BlockData.Diff>[]>();
    public Stack<PlayerController.PlayerData.Diff> playerHistory = new Stack<PlayerController.PlayerData.Diff>();
    public List<BlockData.Diff>[] resetDataRecord;
    public PlayerController.PlayerData.Diff resetPlayerRecord;
    public List<LevelBlock.BlockItem>[] roomInfo = new List<LevelBlock.BlockItem>[2];

    [HideInInspector] public List<SpriteRenderer> roomInfoOutlines = new List<SpriteRenderer>();
    [HideInInspector] public int[] buttonColorActivations = new int[GameController.CRYSTAL_COLORS + 1];

    public bool WithinScreenBounds(Coordinates c) {
        return c.y <= screenBounds[0] 
            && c.y >= screenBounds[1] 
            && c.x >= screenBounds[2] 
            && c.x <= screenBounds[3];
    }
    public bool WithinAnyScreen(Coordinates c) {
        foreach (Screen s in screens) {
            if (s.screenData.WithinScreen(c))
                return true;
        }
        return false;
    }

    public void AddPuzzleData(Data data, PuzzleIndex puzzleIndex) {
        int index = (int)puzzleIndex;
        if (puzzleData[index] == null)
            puzzleData[index] = new List<Data>();

        puzzleData[index].Add(data);
    }
    public void AddRoomInfo(LevelBlock levelBlock, bool isPanel = false) {
        int index = isPanel ? 1 : 0;
        if (roomInfo[index] == null)
            roomInfo[index] = new List<LevelBlock.BlockItem>();
        
        roomInfo[index] .Add(levelBlock.GetBlockItem("InfoItem"));
        roomInfoOutlines.Add(levelBlock.GetBlockItem("InfoOutline").spriteRenderer);
    }
    
    public void RecordPuzzleData() {
        List<BlockData.Diff>[] puzzleDataRecord = new List<BlockData.Diff>[puzzleData.Length + 1];
        for (int i = 0; i < puzzleData.Length; i++) {
            if (puzzleData[i] == null)
                continue;

            puzzleDataRecord[i] = new List<BlockData.Diff>();
            for (int j = 0; j < puzzleData[i].Count; j++)
                puzzleDataRecord[i].Add(puzzleData[i][j].GetDiff());
        }
        puzzleDataRecord[puzzleDataRecord.Length - 1] = GetPlayerDataRecord();

        puzzleHistory.Push(puzzleDataRecord);
        playerHistory.Push(GameController.Player.playerData.GetDiff());
    }
    public void RecordResetData() {
        resetDataRecord = new List<BlockData.Diff>[puzzleData.Length + 1];
        int forceState = GameController.GetForceState(initialForceDirection);
        for (int i = 0; i < puzzleData.Length; i++) {
            if (puzzleData[i] == null)
                continue;

            resetDataRecord[i] = new List<BlockData.Diff>();
            switch ((PuzzleIndex)i) {
                case PuzzleIndex.Rock   :
                case PuzzleIndex.Crystal:
                    foreach (Data d in puzzleData[i]) {
                        BlockData.Diff bd = d.GetDiff();
                        bd.coordinates = d.blockData.origin;
                        bd.destroyed = false;

                        if (d.blockData.blockName[0] == 'F')
                            bd.state = forceState;

                        resetDataRecord[i].Add(bd);
                    }
                    break;

                case PuzzleIndex.Button:
                    foreach (Data d in puzzleData[i]) {
                        BlockData.Diff bd = d.GetDiff();
                        bd.state = (int)Activation.Off;
                        resetDataRecord[i].Add(bd);
                    }
                    break;

                case PuzzleIndex.Panel:
                    foreach (Data d in puzzleData[i]) {
                        BlockData.Diff bd = d.GetDiff();
                        Panel p = (Panel)d.levelBlock.GetBlockItem("Panel").script;
                        bd.state = p.inverted ? (int)Activation.On : (int)Activation.Off;
                        resetDataRecord[i].Add(bd);
                    }
                    break;

                case PuzzleIndex.Dig:
                    foreach (Data d in puzzleData[i]) {
                        BlockData.Diff bd = d.GetDiff();
                        bd.destroyed = false;
                        bd.state = (int)Activation.Off;
                        resetDataRecord[i].Add(bd);
                    }
                    break;

                case PuzzleIndex.Fg         :
                case PuzzleIndex.Gate       :
                case PuzzleIndex.GateSlot   :
                case PuzzleIndex.Collectable:
                    foreach (Data d in puzzleData[i])
                        resetDataRecord[i].Add(d.GetDiff());
                    break;

                default:
                    resetDataRecord[i] = null;
                    break;
            }
        }

        resetDataRecord[resetDataRecord.Length - 1] = GetPlayerDataRecord();
        resetPlayerRecord = GameController.Player.playerData.GetDiff();
    }
    public List<BlockData.Diff> GetPlayerDataRecord() {
        List<BlockData.Diff> playerDataRecord = new List<BlockData.Diff>();
        List<PlayerController.Worm> worm = GameController.Player.worm;
        for (int i = 0; i < worm.Count; i++)
            playerDataRecord.Add(worm[i].data.GetDiff());

        return playerDataRecord;
    }

    public void RecalculateButtonColorActivations() {
        buttonColorActivations = new int[GameController.CRYSTAL_COLORS + 1];
        foreach (Data d in puzzleData[(int)PuzzleIndex.Button]) {
            if (d.blockData.state == (int)Activation.On || d.blockData.state == (int)Activation.Alt)
                buttonColorActivations[GameController.ConvertColorNameToIndex(d.blockData.blockName)]++;
        }
    }
}
