using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tipo de operação matemática gerada.
/// </summary>
public enum MathOperationType
{
    Addition,
    Subtraction,
    Multiplication,
    Mixed
}

/// <summary>
/// Estrutura contendo uma questão matemática e suas opções de resposta.
/// </summary>
public struct MathProblem
{
    public string QuestionText;
    public int CorrectAnswer;
    public int[] Options; // Todas as opções, incluindo a correta (embaralhadas)
}

/// <summary>
/// Responsável por gerar operações matemáticas, resposta correta e respostas incorretas.
/// </summary>
public class MathManager : MonoBehaviour
{
    public static MathManager Instance { get; private set; }

    [Header("Configurações da Matemática")]
    [SerializeField] private MathOperationType operationType = MathOperationType.Mixed;
    [SerializeField] private int minNumber = 1;
    [SerializeField] private int maxNumber = 12;
    [SerializeField] private int numberOfOptions = 5;

    public MathProblem CurrentProblem { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    /// <summary>
    /// Gera um novo problema matemático com resposta correta e alternativas incorretas válidas.
    /// </summary>
    public MathProblem GenerateNewProblem()
    {
        MathOperationType selectedOp = operationType;
        if (selectedOp == MathOperationType.Mixed)
        {
            int r = Random.Range(0, 3);
            selectedOp = r switch
            {
                0 => MathOperationType.Addition,
                1 => MathOperationType.Subtraction,
                _ => MathOperationType.Multiplication
            };
        }

        int numA = 0;
        int numB = 0;
        int correctAnswer = 0;
        string questionStr = "";

        switch (selectedOp)
        {
            case MathOperationType.Addition:
                numA = Random.Range(minNumber, maxNumber + 1);
                numB = Random.Range(minNumber, maxNumber + 1);
                correctAnswer = numA + numB;
                questionStr = $"{numA} + {numB} = ?";
                break;

            case MathOperationType.Subtraction:
                // Garante resultado positivo (numA >= numB)
                numA = Random.Range(minNumber, maxNumber + 1);
                numB = Random.Range(minNumber, maxNumber + 1);
                if (numA < numB)
                {
                    int temp = numA;
                    numA = numB;
                    numB = temp;
                }
                correctAnswer = numA - numB;
                questionStr = $"{numA} - {numB} = ?";
                break;

            case MathOperationType.Multiplication:
                // Usa valores menores para multiplicação inicial amigável (2 a 9)
                numA = Random.Range(2, Mathf.Min(maxNumber, 10));
                numB = Random.Range(2, Mathf.Min(maxNumber, 10));
                correctAnswer = numA * numB;
                questionStr = $"{numA} × {numB} = ?";
                break;
        }

        // Gera as alternativas incorretas
        List<int> answers = new List<int> { correctAnswer };
        int attempts = 0;

        while (answers.Count < numberOfOptions && attempts < 100)
        {
            attempts++;
            // Variação em torno da resposta correta para ser plausível
            int offset = Random.Range(-5, 6);
            if (offset == 0) offset = Random.Range(0, 2) == 0 ? -1 : 1;

            int candidate = correctAnswer + offset;

            // Se for negativo ou zero, ajusta
            if (candidate < 0) candidate = Mathf.Abs(candidate) + 1;

            if (!answers.Contains(candidate))
            {
                answers.Add(candidate);
            }
        }

        // Fallback se não preencheu
        while (answers.Count < numberOfOptions)
        {
            int fallback = correctAnswer + answers.Count + Random.Range(1, 10);
            if (!answers.Contains(fallback)) answers.Add(fallback);
        }

        // Embaralha as respostas (Fisher-Yates Shuffle)
        for (int i = answers.Count - 1; i > 0; i--)
        {
            int rnd = Random.Range(0, i + 1);
            int temp = answers[i];
            answers[i] = answers[rnd];
            answers[rnd] = temp;
        }

        CurrentProblem = new MathProblem
        {
            QuestionText = questionStr,
            CorrectAnswer = correctAnswer,
            Options = answers.ToArray()
        };

        return CurrentProblem;
    }
}
