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
 * ***/

using System;
using System.Collections;
using System.Collections.Generic;

public abstract class URLObject
{
    public string Address { get; set; }

    public URLObject(string server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new ArgumentException(message: "The server address cannot be null or empty");
        }
        Address = server;
    }
}

public class URLsCollection : ICollection<URLObject>
{
    public IEnumerator<URLObject> GetEnumerator()
    {
        return new URLObjectEnumerator(this);
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return new URLObjectEnumerator(this);
    }

    // The inner collection to store objects.
    protected List<URLObject> innerCol;

    public URLsCollection()
    {
        innerCol = new List<URLObject>();
    }

    // Adds an index to the collection.
    public URLObject this[int index]
    {
        get { return (URLObject)innerCol[index]; }
        set { innerCol[index] = value; }
    }

    // Determines if an item is in the collection
    // by searching for the IP address.
    public bool Contains(URLObject item)
    {
        foreach (URLObject obj in innerCol)
        {
            if (obj.Address == item.Address)
            {
                return true;
            }
        }
        return false;
    }



    // Adds an item if it is not already in the collection
    // as determined by calling the Contains method.
    public void Add(URLObject item)
    {

        if (!Contains(item))
        {
            innerCol.Add(item);
        }
        else
        {
            Console.WriteLine("The server with address {0} was already added to the collection).", item.Address);
        }
    }

    // Tries to find and return the URL in the URLObject collection.
    // Can be used as a replacement for Contains() by checking if the result is != null 
    public virtual URLObject Find(string address)
    {

        foreach (URLObject obj in innerCol)
        {
            if (obj.Address == address)
            {
                return obj;
            }
        }
        return null;
    }

    public void Clear()
    {
        innerCol.Clear();
    }

    public void CopyTo(URLObject[] array, int arrayIndex)
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

    public bool Remove(URLObject item)
    {
        bool result = false;

        // Iterate the inner collection to
        // find the IP to be removed.
        for (int i = 0; i < innerCol.Count; i++)
        {

            URLObject curObj = (URLObject)innerCol[i];

            if (curObj.Address == item.Address)
            {
                innerCol.RemoveAt(i);
                result = true;
                break;
            }
        }
        return result;
    }
}

public class URLObjectEnumerator : IEnumerator<URLObject>
{
    protected URLsCollection _collection;
    protected int curIndex;
    protected URLObject curObj;

    public URLObjectEnumerator(URLsCollection collection)
    {
        _collection = collection;
        curIndex = -1;
        curObj = default(URLObject);
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
            curObj = _collection[curIndex];
        }
        return true;
    }

    public void Reset() { curIndex = -1; }

    void IDisposable.Dispose() { }

    public URLObject Current
    {
        get { return curObj; }
    }

    object IEnumerator.Current
    {
        get { return Current; }
    }
}