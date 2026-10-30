using UnityEngine;

/// <summary>
/// Tunables for the round (win/lose, truck-flip countdown, score). The asset lives at
/// Assets/Resources/RoundSettings.asset; if it is missing, these defaults are used.
/// </summary>
[CreateAssetMenu(fileName = "RoundSettings", menuName = "Game/Round Settings")]
public class RoundSettings : ScriptableObject
{
    [Header("Scenes")]
    public string gameplayScene = "Prototype_World";
    public string mainMenuScene = "MainMenu";

    [Header("Truck flipped = loss")]
    [Tooltip("Seconds the team has to put the truck back on its wheels.")]
    public float flipCountdownSeconds = 20f;
    [Tooltip("Truck counts as flipped when its up direction is more than this many degrees from world up (90 = on its side).")]
    [Range(30f, 170f)] public float flippedAngle = 70f;
    [Tooltip("Truck counts as upright again below this angle. Lower than Flipped Angle so it can't flicker at the edge.")]
    [Range(5f, 90f)] public float uprightAngle = 45f;
    [Tooltip("Only a (nearly) still truck counts as flipped, so a jump or a bounce doesn't start the countdown (m/s).")]
    public float maxStillSpeed = 1f;
    [Tooltip("Same, for turning (degrees per second).")]
    public float maxStillTurnRate = 30f;
    [Tooltip("Seconds the truck must stay flipped and still before the countdown starts.")]
    public float flipConfirmSeconds = 1.5f;
    [Tooltip("Seconds the truck must stay upright before a running countdown is cancelled.")]
    public float uprightConfirmSeconds = 1f;

    [Header("Score")]
    [Tooltip("Points for a full research bar (scaled by how far the team got).")]
    public int researchPoints = 300;
    public int tornadoDetectedPoints = 200;
    public int tornadoKilledPoints = 500;
    public int winBonus = 500;
    [Tooltip("Wins faster than this (seconds) get the speed bonus.")]
    public float targetTimeSeconds = 600f;
    [Tooltip("Speed bonus per second under the target time (wins only).")]
    public float pointsPerSecondUnderTarget = 1f;
    [Tooltip("Subtracted per item use, whole team (score never goes below 0).")]
    public int pointsPerItemUsed = -5;

    [Header("Debug keys (Editor and Development Builds; host or single player only)")]
    public KeyCode debugWinKey = KeyCode.F5;
    public KeyCode debugLoseKey = KeyCode.F6;
    [Tooltip("Pretends the truck is flipped (starts the countdown); press again to put it 'back upright'.")]
    public KeyCode debugFlipKey = KeyCode.F7;

    static RoundSettings _instance;
    public static RoundSettings Load()
    {
        if (_instance == null) _instance = Resources.Load<RoundSettings>("RoundSettings");
        if (_instance == null) _instance = CreateInstance<RoundSettings>();
        return _instance;
    }
}
