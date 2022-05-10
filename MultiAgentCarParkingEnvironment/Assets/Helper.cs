using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

public static class Helper
{
    public static int Mod(int x, int m) {
        int r = x%m;
        return r<0 ? r+m : r;
    }

    public static List<int> GenRandom(int min, int max, int n)
    {
        var rand = new Random();
        return Enumerable.Range(min, max - min + 1)
            .OrderBy(i => rand.Next())
            .Take(n).ToList();
    }
    
    public static T RandomElement<T>(List<T> elements)
    {
        var rand = new Random();
        return elements[rand.Next(0, elements.Count)];
    }

    public static int ManhattanDistance(int x1, int y1, int x2, int y2)
    {
        return Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
    }

    public static float SquareDistance(int x1, int y1, int x2, int y2)
    {
        return Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));
    }
    
    private static Vector2 RandomiseVector(Vector2 vector, Vector2 randomness)
    {
        Vector2 deltas = new Vector2(
            UnityEngine.Random.Range(-randomness.x, randomness.x),
            UnityEngine.Random.Range(-randomness.y, randomness.y)
        );
        return vector + deltas;
    }
    
    public static void Shuffle<T> (this Random rng, T[] array)
    {
        int n = array.Length;
        while (n > 1) 
        {
            int k = rng.Next(n--);
            T temp = array[n];
            array[n] = array[k];
            array[k] = temp;
        }
    }
}