using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomCode
{
    public static string GenerateRandomCode(int length = 6)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        char[] code = new char[length];

        for (int i = 0; i < length; i++)
        {
            code[i] = chars[Random.Range(0, chars.Length)];
        }
        
        return new string(code);
    }
}
