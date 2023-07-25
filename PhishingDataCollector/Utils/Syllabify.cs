/*
 * Translated by Francesco Greco from Panos Ipeirotis' Java code:
 * https://github.com/ipeirotis/ReadabilityMetrics/blob/master/src/main/java/com/ipeirotis/readability/engine/Syllabify.java
 */

using System.Text.RegularExpressions;


public class Syllabify
{

    private static readonly Regex VOWELS = new Regex(@"[^aeiouy]+");

    private static readonly string[] _staticSubMatches = { "cial", "tia", "cius", "cious", "giu", "ion", "iou" };
    private static readonly Regex[] regexSubMatches = {
        new Regex(".*sia$"),
        new Regex(".*.ely$"),
        new Regex(".*[^td]ed$")
    };

    private static readonly string[] _staticAddMatches = { "ia", "riet", "dien", "iu", "io", "ii", "microor" };
    private static readonly Regex[] _regexAddMatches = {
        new Regex(".*[aeiouym]bl$"),
        new Regex(".*[aeiou]{3}.*"),
        new Regex("^mc.*"),
        new Regex(".*ism$"),
        new Regex(".*isms$"),
        new Regex(".*([^aeiouy])\\1l$"),
        new Regex(".*[^l]lien.*"),
        new Regex("^coa[dglx]..*"),
        new Regex(".*[^gq]ua[^auieo].*"),
        new Regex(".*dnt$")
      };

    public static int GetNSyllables(string word)
    {

        word = word.ToLower();
        if (word.Equals("w"))
        {
            return 2;
        }
        if (word.Length == 1)
        {
            return 1;
        }
        word = word.Replace('\'', ' ');

        if (word.EndsWith("e"))
        {
            word = word.Substring(0, word.Length - 1);
        }

        string[] phonems = VOWELS.Split(word);

        int syl = 0;
        foreach (string s in _staticSubMatches)
        {
            if (word.Contains(s))
            {
                syl--;
            }
        }

        foreach (string s in _staticAddMatches)
        {
            if (word.Contains(s))
            {
                syl++;
            }
        }

        foreach (Regex r in regexSubMatches)
        {
            if (r.IsMatch(word)) { syl--; }
        }

        foreach (Regex r in _regexAddMatches)
        {
            if (r.IsMatch(word)) { syl++; }
        }

        foreach (string ph in phonems)
        {
            if (ph.Length > 0)
            {
                syl++;
            }
        }

        if (syl <= 0)
        {
            syl = 1;
        }

        return syl;
    }

}