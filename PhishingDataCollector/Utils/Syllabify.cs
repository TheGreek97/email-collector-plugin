/***
 *  This file is part of Dataset-Collector.

    Dataset-Collector is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Dataset-Collector is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Dataset-Collector.  If not, see <http://www.gnu.org/licenses/>. 
 * 
 * 
 * Translated by Francesco Greco from Panos Ipeirotis' Java code:
 * https://github.com/ipeirotis/ReadabilityMetrics/blob/master/src/main/java/com/ipeirotis/readability/engine/Syllabify.java
 ***/

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