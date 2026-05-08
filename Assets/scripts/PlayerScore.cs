using UnityEngine;

public class PlayerScore : MonoBehaviour
{
    public int currentScore;

    public void AddScore(int amount)
    {
        currentScore += amount;
        Debug.Log($"{gameObject.name} score: {currentScore}");
    }

    public void ResetScore()
    {
        currentScore = 0;
    }
}
