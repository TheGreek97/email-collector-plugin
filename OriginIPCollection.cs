using System;
using System.Collections;
using System.Collections.Generic;

namespace PhishingDataCollector;

public class OriginIPCollection : ICollection<OriginIP>
{
    public IEnumerator<OriginIP> GetEnumerator()
    {
        return new OriginIPEnumerator(this);
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return new OriginIPEnumerator(this);
    }

    // The inner collection to store objects.
    private List<OriginIP> innerCol;

    public OriginIPCollection()
    {
        innerCol = new List<OriginIP>();
    }

    // Adds an index to the collection.
    public OriginIP this[int index]
    {
        get { return (OriginIP)innerCol[index]; }
        set { innerCol[index] = value; }
    }

    // Determines if an item is in the collection
    // by searching for the IP address.
    public bool Contains(OriginIP item)
    {
        foreach (OriginIP ip_obj in innerCol)
        {
            if (ip_obj.IP == item.IP)
            {
                return true;
            }
        }
        return false;
    }

    // Adds an item if it is not already in the collection
    // as determined by calling the Contains method.
    public void Add(OriginIP item)
    {

        if (!Contains(item))
        {
            innerCol.Add(item);
        }
        else
        {
            Console.WriteLine("The IP {0} was already added to the collection (origin = {1}).",
                item.IP, item.origin);
        }
    }

    public void Clear()
    {
        innerCol.Clear();
    }

    public void CopyTo(OriginIP[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException("The array cannot be null.");
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException("The starting array index cannot be negative.");
        if (Count > array.Length - arrayIndex)
            throw new ArgumentException("The destination array has fewer elements than the collection.");

        for (int i = 0; i < innerCol.Count; i++)
        {
            array[i + arrayIndex] = innerCol[i];
        }
    }

    public int Count
    {
        get
        {
            return innerCol.Count;
        }
    }

    public bool IsReadOnly
    {
        get { return false; }
    }

    public bool Remove(OriginIP item)
    {
        bool result = false;

        // Iterate the inner collection to
        // find the IP to be removed.
        for (int i = 0; i < innerCol.Count; i++)
        {

            OriginIP curIP = (OriginIP)innerCol[i];

            if (curIP.IP == item.IP))
            {
                innerCol.RemoveAt(i);
                result = true;
                break;
            }
        }
        return result;
    }
}

public class OriginIP
{
    public string IP { set; get; }
    public string origin { set; get; }
}

public class OriginIPEnumerator : IEnumerator<OriginIP>
{
    private OriginIPCollection _collection;
    private int curIndex;
    private OriginIP curIP;

    public OriginIPEnumerator(OriginIPCollection collection)
    {
        _collection = collection;
        curIndex = -1;
        curIP = default(OriginIP);
    }

    public bool MoveNext()
    {
        //Avoids going beyond the end of the collection.
        if (++curIndex >= _collection.Count)
        {
            return false;
        }
        else
        {
            // Set current box to next item in collection.
            curIP = _collection[curIndex];
        }
        return true;
    }

    public void Reset() { curIndex = -1; }

    void IDisposable.Dispose() { }

    public OriginIP Current
    {
        get { return curIP; }
    }

    object IEnumerator.Current
    {
        get { return Current; }
    }
}