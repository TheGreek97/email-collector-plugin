using System;
using System.Collections;
using System.Collections.Generic;

public class BlacklistURL : URLObject
{
    public short NBlacklists{ set; get; }
    public BlacklistURL(string server,  short n_blacklist) : base (server)
    {
        NBlacklists = n_blacklist;
    }
}

public class BlacklistURLsCollection : URLsCollection
{
    public void CopyTo(BlacklistURL[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException("The array cannot be null.");
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
        if (Count > array.Length - arrayIndex)
            throw new ArgumentException("The destination array has fewer elements than the collection.");

        for (int i = 0; i < innerCol.Count; i++)
        {
            array[i + arrayIndex] = (BlacklistURL) innerCol[i];
        }
    }
}

