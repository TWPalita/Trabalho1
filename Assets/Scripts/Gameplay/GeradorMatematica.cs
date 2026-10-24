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

    // Histórico de contas já sorteadas na partida atual para nunca repetir a mesma equação
    private readonly HashSet<string> usedQuestionsHistory = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    /// <summary>
    /// Limpa o histórico de contas já sorteadas (chamado ao iniciar/reiniciar a defesa).
    /// </summary>
    public void ResetHistory()
    {
        usedQuestionsHistory.Clear();
    }

    /// <summary>
    /// Sobrecarga compatível com chamadas sem parâmetro de rodada.
    /// </summary>
    public MathProblem GenerateNewProblem()
    {
        return GenerateNewProblem(1);
    }

    /// <summary>
    /// Gera um novo problema matemático inédito com resposta correta e alternativas incorretas válidas,
    /// aplicando progressão gradual de 10 rodadas (incluindo divisão exata e expressões com 3 números).
    /// </summary>
    public MathProblem GenerateNewProblem(int round)
    {
        int clampedRound = Mathf.Clamp(round, 1, 20);
        int correctAnswer = 0;
        string questionStr = "";

        // Tenta sortear uma conta que ainda não tenha aparecido na partida atual
        const int maxUniqueAttempts = 60;
        for (int attempt = 0; attempt < maxUniqueAttempts; attempt++)
        {
            BuildCandidateEquation(clampedRound, out questionStr, out correctAnswer);
            if (!string.IsNullOrEmpty(questionStr) && !usedQuestionsHistory.Contains(questionStr))
            {
                break;
            }
        }

        if (!string.IsNullOrEmpty(questionStr))
        {
            usedQuestionsHistory.Add(questionStr);
        }

        // Gera as alternativas incorretas com unicidade garantida e plausíveis para o nível da conta
        List<int> answers = new List<int> { correctAnswer };
        int attempts = 0;
        int spread = clampedRound <= 3 ? 5 : (clampedRound <= 7 ? 7 : 10);

        while (answers.Count < numberOfOptions && attempts < 100)
        {
            attempts++;
            int offset = Random.Range(-spread, spread + 1);
            if (offset == 0) offset = Random.Range(0, 2) == 0 ? -1 : 1;

            int candidate = correctAnswer + offset;
            if (candidate <= 0) candidate = correctAnswer + Random.Range(1, spread + 2);

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

    private void BuildCandidateEquation(int round, out string questionStr, out int correctAnswer)
    {
        questionStr = "";
        correctAnswer = 0;

        // Se o modo não for Mixed, respeita a operação específica configurada no Inspector
        if (operationType != MathOperationType.Mixed)
        {
            BuildTwoNumberOperation(operationType, round, out questionStr, out correctAnswer);
            return;
        }

        // =========================================================================
        // PROGRESSÃO GRADUAL DE 10 RODADAS (MODO MIXED)
        // Rodadas 1 a 3: Contas diretas de 2 números (+, -, ×, ÷ simples)
        // Rodadas 4 a 6: Contas intermediárias de 2 números (incluindo ÷ e × maiores) + início de 3 números (+ e -)
        // Rodadas 7 a 10: Contas avançadas de 3 números (combinando +, -, × e ÷ exata) e divisões/multiplicações maiores
        // =========================================================================

        if (round <= 3)
        {
            // Rodada 1: + ou - | Rodadas 2-3: +, -, × ou ÷ simples
            int opIndex = (round == 1) ? Random.Range(0, 2) : Random.Range(0, 4);
            MathOperationType op = opIndex switch
            {
                0 => MathOperationType.Addition,
                1 => MathOperationType.Subtraction,
                2 => MathOperationType.Multiplication,
                _ => MathOperationType.Division
            };
            BuildTwoNumberOperation(op, round, out questionStr, out correctAnswer);
            return;
        }

        if (round <= 6)
        {
            // 50% de chance de expressão com 3 números (+ e -) e 50% de chance de × ou ÷ intermediária
            bool useThreeNumbers = Random.value < 0.50f;
            if (useThreeNumbers)
            {
                BuildThreeNumberAddSub(round, out questionStr, out correctAnswer);
            }
            else
            {
                MathOperationType op = (Random.value < 0.55f)
                    ? MathOperationType.Division
                    : MathOperationType.Multiplication;
                BuildTwoNumberOperation(op, round, out questionStr, out correctAnswer);
            }
            return;
        }

        // Rodadas 7 a 10: Alta frequência (75%) de contas com 3 números (incluindo × e ÷) e 25% de ÷/× avançadas
        float roll = Random.value;
        if (roll < 0.45f)
        {
            BuildThreeNumberWithMultOrDiv(round, out questionStr, out correctAnswer);
        }
        else if (roll < 0.78f)
        {
            BuildThreeNumberAddSub(round, out questionStr, out correctAnswer);
        }
        else
        {
            MathOperationType op = (Random.value < 0.55f)
                ? MathOperationType.Division
                : MathOperationType.Multiplication;
            BuildTwoNumberOperation(op, round, out questionStr, out correctAnswer);
        }
    }

    private void BuildTwoNumberOperation(MathOperationType op, int round, out string questionStr, out int correctAnswer)
    {
        int curMin = round <= 3 ? 2 : (round <= 6 ? 4 : 6);
        int curMax = round <= 3 ? 9 : (round <= 6 ? 15 : 22);

        int numA;
        int numB;

        switch (op)
        {
            case MathOperationType.Addition:
            default:
                numA = Random.Range(curMin, curMax + 1);
                numB = Random.Range(curMin, curMax + 1);
                correctAnswer = numA + numB;
                questionStr = $"{numA} + {numB} = ?";
                break;

            case MathOperationType.Subtraction:
                numB = Random.Range(curMin, curMax);
                int diff = Random.Range(2, curMax + 1);
                numA = numB + diff;
                correctAnswer = diff;
                questionStr = $"{numA} - {numB} = ?";
                break;

            case MathOperationType.Multiplication:
                int multMin = round <= 3 ? 2 : 3;
                int multMax = round <= 3 ? 5 : (round <= 6 ? 9 : 12);
                numA = Random.Range(multMin, multMax + 1);
                numB = Random.Range(multMin, multMax + 1);
                correctAnswer = numA * numB;
                questionStr = $"{numA} × {numB} = ?";
                break;

            case MathOperationType.Division:
                // Divisão inteira exata: dividendo = divisor * quociente
                int divMax = round <= 3 ? 5 : (round <= 6 ? 9 : 12);
                int quotMax = round <= 3 ? 6 : (round <= 6 ? 9 : 12);
                numB = Random.Range(2, divMax + 1);
                int quotient = Random.Range(2, quotMax + 1);
                numA = numB * quotient;
                correctAnswer = quotient;
                questionStr = $"{numA} ÷ {numB} = ?";
                break;
        }
    }

    private void BuildThreeNumberAddSub(int round, out string questionStr, out int correctAnswer)
    {
        int maxVal = round <= 6 ? 11 : 16;
        int a = Random.Range(3, maxVal + 1);
        int b = Random.Range(2, maxVal + 1);
        int c = Random.Range(2, maxVal + 1);

        int pattern = Random.Range(0, 3);
        if (pattern == 0)
        {
            // A + B + C
            correctAnswer = a + b + c;
            questionStr = $"{a} + {b} + {c} = ?";
        }
        else if (pattern == 1)
        {
            // A + B - C (garantindo resultado >= 2)
            if (a + b <= c + 1)
            {
                a = c + Random.Range(2, 8);
            }
            correctAnswer = a + b - c;
            questionStr = $"{a} + {b} - {c} = ?";
        }
        else
        {
            // A - B + C (garantindo A > B para leitura intuitiva)
            if (a <= b)
            {
                int temp = a;
                a = b + Random.Range(2, 7);
                b = temp;
            }
            correctAnswer = a - b + c;
            questionStr = $"{a} - {b} + {c} = ?";
        }
    }

    private void BuildThreeNumberWithMultOrDiv(int round, out string questionStr, out int correctAnswer)
    {
        bool useDivision = Random.value < 0.50f;
        int c = Random.Range(2, round >= 9 ? 12 : 9);

        if (useDivision)
        {
            // (A ÷ B) + C ou (A ÷ B) - C com divisão inteira exata
            int b = Random.Range(2, round >= 9 ? 10 : 8);
            int quotient = Random.Range(2, round >= 9 ? 11 : 9);
            int a = b * quotient;

            bool addC = (Random.value < 0.55f) || (quotient <= c + 1);
            if (addC)
            {
                correctAnswer = quotient + c;
                questionStr = $"{a} ÷ {b} + {c} = ?";
            }
            else
            {
                correctAnswer = quotient - c;
                questionStr = $"{a} ÷ {b} - {c} = ?";
            }
        }
        else
        {
            // (A × B) + C ou (A × B) - C
            int a = Random.Range(2, round >= 9 ? 9 : 7);
            int b = Random.Range(2, round >= 9 ? 8 : 6);
            int prod = a * b;

            bool addC = (Random.value < 0.50f) || (prod <= c + 1);
            if (addC)
            {
                correctAnswer = prod + c;
                questionStr = $"{a} × {b} + {c} = ?";
            }
            else
            {
                correctAnswer = prod - c;
                questionStr = $"{a} × {b} - {c} = ?";
            }
        }
    }
}

public class MathManager : GeradorMatematica { }

