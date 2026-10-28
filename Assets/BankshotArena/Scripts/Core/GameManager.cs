using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Score, game-over state and restart. One per scene.</summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Score { get; private set; }
    public int Kills { get; private set; }
    public int RicochetKills { get; private set; }
    public int BestBounceKill { get; private set; }
    public bool IsGameOver { get; private set; }

    public event Action OnGameOver;
    /// <summary>(points awarded, bounces) — HUD uses this for "RICOCHET x2" popups.</summary>
    public event Action<int, int> OnKillScored;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>The twist's scoring rule: every bounce before the kill multiplies the score.</summary>
    public void RegisterKill(int baseScore, int bounces)
    {
        if (IsGameOver) return;

        int points = baseScore * (1 + bounces);
        Score += points;
        Kills++;
        if (bounces > 0) RicochetKills++;
        if (bounces > BestBounceKill) BestBounceKill = bounces;

        OnKillScored?.Invoke(points, bounces);
    }

    public void GameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        OnGameOver?.Invoke();
    }

    void Update()
    {
        if (IsGameOver && Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // Esc frees the mouse, clicking the game view grabs it again.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (!IsGameOver && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
