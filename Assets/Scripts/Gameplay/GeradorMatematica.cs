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
    Division,
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
/// Responsável por gerar operações matemáticas, resposta correta e respostas incorretas,
/// com suporte a adição, subtração, multiplicação e divisão inteira exata com progressão gradual.
/// </summary>
public class GeradorMatematica : MonoBehaviour
{
    public static GeradorMatematica Instance { get; private set; }

    [Header("Configurações da Matemática")]
    [SerializeField] private MathOperationType operationType = MathOperationType.Mixed;
    [SerializeField] private int minNumber = 1;
    [SerializeField] private int maxNumber = 12;
    [SerializeField] private int numberOfOptions = 5;

    public MathProblem CurrentProblem { get; private set; }
    public int CurrentCorrectAnswer => CurrentProblem.CorrectAnswer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    /// <summary>
    /// Sobrecarga compatível com chamadas sem parâmetro de rodada.
    /// </summary>
    public MathProblem GenerateNewProblem()
    {
        return GenerateNewProblem(1);
    }

    /// <summary>
    /// Gera um novo problema matemático com resposta correta e alternativas incorretas válidas,
    /// aplicando progressão de dificuldade baseada no número da rodada atual.
    /// </summary>
    public MathProblem GenerateNewProblem(int round)
    {
        MathOperationType selectedOp = operationType;

        // Progressão de dificuldade e seleção da operação em modo Mixed
        int curMin = minNumber;
        int curMax = maxNumber;

        if (operationType == MathOperationType.Mixed)
        {
            if (round <= 3)
            {
                // Rodadas 1-3: Adição, subtração e multiplicação simples (números menores)
                curMin = 1;
                curMax = 8;
                int r = Random.Range(0, 3);
                selectedOp = r switch
                {
                    0 => MathOperationType.Addition,
                    1 => MathOperationType.Subtraction,
                    _ => MathOperationType.Multiplication
                };
            }
            else if (round <= 6)
            {
                // Rodadas 4-6: Adição, subtração, multiplicação e divisão inteira
                curMin = 2;
                curMax = 12;
                int r = Random.Range(0, 4);
                selectedOp = r switch
                {
                    0 => MathOperationType.Addition,
                    1 => MathOperationType.Subtraction,
                    2 => MathOperationType.Multiplication,
                    _ => MathOperationType.Division
                };
            }
            else
            {
                // Rodadas 7+: Maior variedade com operandos maiores
                curMin = 2;
                curMax = 15;
                int r = Random.Range(0, 4);
                selectedOp = r switch
                {
                    0 => MathOperationType.Addition,
                    1 => MathOperationType.Subtraction,
                    2 => MathOperationType.Multiplication,
                    _ => MathOperationType.Division
                };
            }
        }

        int numA = 0;
        int numB = 0;
        int correctAnswer = 0;
        string questionStr = "";

        switch (selectedOp)
        {
            case MathOperationType.Addition:
                numA = Random.Range(curMin, curMax + 1);
                numB = Random.Range(curMin, curMax + 1);
                correctAnswer = numA + numB;
                questionStr = $"{numA} + {numB} = ?";
                break;

            case MathOperationType.Subtraction:
                // Garante resultado positivo (numA >= numB)
                numA = Random.Range(curMin, curMax + 1);
                numB = Random.Range(curMin, curMax + 1);
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
                int multLimit = round <= 3 ? 5 : (round <= 6 ? 9 : 12);
                numA = Random.Range(2, multLimit + 1);
                numB = Random.Range(2, multLimit + 1);
                correctAnswer = numA * numB;
                questionStr = $"{numA} × {numB} = ?";
                break;

            case MathOperationType.Division:
                // Garante sempre divisão inteira exata sem dízima e sem divisão por zero
                int maxDiv = round <= 6 ? 6 : 10;
                numB = Random.Range(2, maxDiv + 1); // Divisor entre 2 e maxDiv (nunca 0)
                int quotient = Random.Range(2, maxDiv + 1); // Quociente inteiro
                numA = numB * quotient; // Dividendo = divisor * quociente
                correctAnswer = quotient;
                questionStr = $"{numA} ÷ {numB} = ?";
                break;
        }

        // Gera as alternativas incorretas com unicidade garantida
        List<int> answers = new List<int> { correctAnswer };
        int attempts = 0;

        while (answers.Count < numberOfOptions && attempts < 100)
        {
            attempts++;
            int offset = Random.Range(-5, 6);
            if (offset == 0) offset = Random.Range(0, 2) == 0 ? -1 : 1;

            int candidate = correctAnswer + offset;
            if (candidate <= 0) candidate = Mathf.Abs(candidate) + 1;

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

public class MathManager : GeradorMatematica { }

